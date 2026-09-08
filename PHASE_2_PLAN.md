# Phase 2 Plan — Real Farming System

Written after reading the Phase 1 implementation as it actually exists on disk, not from memory.

---

## 1. Current farming architecture (verified Phase 1 state)

| File | What it actually does today |
|---|---|
| `PlotState.cs` | `enum { Empty, Tilled, Planted, Growing, ReadyToHarvest }` — declared in Phase 1, only `Empty`/`Tilled` ever used |
| `FarmPlot.cs` | `InteractableBase` subclass. Holds `state`, `crop`, `soilRenderer`, `cropAnchor`. `OnInteract` just toggles Empty⇄Tilled. Tints soil via `MaterialPropertyBlock`. Has `StateChanged` event, `Coord`, `Grid`. **No growth, no timing, no visuals.** |
| `FarmGrid.cs` | Spawns `plotPrefab` in a rectangle on `Awake`, calls `plot.Initialise(this, coord, crop)` and `plot.SetLabel(...)`. Provides `TryGetPlot`, `CoordToWorld`, `WorldToCoord`, `ForEachPlot`. **No Update.** |
| `CropDefinition.cs` | SO with `cropId`, `displayName`, `growthStages(3)`, `secondsPerStage(20)`, `seedCost`, `sellValue`, `yieldAmount`, `cropColor`, `soilColor`. **No seed/harvest item ids, no stage prefabs.** |
| `GridCoord.cs` | `IEquatable` struct — fine as-is, no change needed |
| `InteractionController.cs` | Timed (0.12 s) `OverlapSphereNonAlloc` on the `Interactable` layer, picks nearest, raises `FocusChanged`, routes `ConsumeInteractPressed()` → `Current.Interact(gameObject)` |
| `InteractableBase.cs` | `interactionLabel` + `interactable` flag, `SetLabel`, `CanInteract`, `Interact` → `OnInteract` + `Interacted` event |
| `HudController.cs` | Subscribes to `FocusChanged`, writes `target.InteractionLabel` into `promptLabel` **once, on focus change only**. Coin/level placeholders. |
| `GameBootstrap.cs` | Frame rate / vSync / sleep / orientation only. Deliberately not a GameManager. |
| Scene `Farm_Prototype.unity` | 3 `FarmGrid` objects: `field_wheat` 5×4, `field_tomato` 4×3, `field_corn` 5×3 — **47 plots**, all spawning `FarmPlot.prefab`, `cellSize 2.2`, `buildOnAwake` on |
| Crop assets | `Crop_Wheat`, `Crop_Tomato`, `Crop_Corn` all `growthStages: 3`, `secondsPerStage: 20`, `yieldAmount: 1` |

### Gaps Phase 2 must close

1. `OnInteract` is a debug toggle — no real transitions, no validation.
2. No time source: nothing advances a plot.
3. No crop visuals; `CropAnchor` exists but is never used.
4. No inventory of any kind.
5. HUD prompt is static — it will not refresh if a plot ripens while the player stands next to it.
6. `CropDefinition` lacks item ids, a yield range and stage visuals.

---

## 2. State transitions

`PlotState` is kept as-is (`ReadyToHarvest` is the existing name for the spec's `Ready`). The stage index drives which state a growing plot reports:

```
                 TryTill()              TryPlant()
   Empty ──────────────────▶ Tilled ──────────────────▶ Planted   (stage 0)
     ▲                                                     │
     │                                          growth tick │
     │                                                     ▼
     │                                                  Growing   (0 < stage < GrowthStages)
     │                                                     │
     │                                          growth tick │
     │                                                     ▼
     └──────────────────────────────────────────────  ReadyToHarvest  (stage == GrowthStages)
                          TryHarvest()
```

`GrowthStages` = number of growth *steps after sowing*. With the shipped value `3`, visual stages are **0 seed → 1 sprout → 2 young → 3 mature**, i.e. `GrowthStages + 1` prefabs.

### Transition table (everything not listed is rejected)

| From | Action | To | Guard |
|---|---|---|---|
| `Empty` | Till | `Tilled` | — |
| `Tilled` | Plant | `Planted` | crop assigned **and** inventory has ≥1 seed |
| `Planted` | growth tick | `Growing` | stage reached 1 |
| `Growing` | growth tick | `ReadyToHarvest` | stage reached `GrowthStages` |
| `ReadyToHarvest` | Harvest | `Empty` | — |

Explicitly rejected, per the brief: `Empty→Plant`, `Empty→Harvest`, `Tilled→Harvest`, `Planted→Plant`, `Growing→Harvest`, double `Harvest`.

Enforcement: the only public mutators are `TryTill/TryPlant/TryHarvest`, each of which returns `bool` and re-checks `state` as its first statement. `SetState` becomes private. A separate `RestoreState` exists solely for the future save system and is documented as such.

---

## 3. Data flow

```
USE button / E key
      │
      ▼
PlayerInputProvider ──▶ InteractionController.Update()
                              │  (existing Phase 1 path, unchanged)
                              ▼
                        FarmPlot.Interact(playerGameObject)
                              │
                              ▼
                        FarmPlot.OnInteract  ── switch on state ──▶ TryTill / TryPlant / TryHarvest
                              │                                            │
                              │                                            ▼
                              │                                   PlayerInventory (from interactor)
                              │                                    Remove(seedId) / Add(harvestId)
                              ▼                                            │
                        ActionFeedbackChannel.Post("Tilled!")              │
                              │                                            │
                              ▼                                            ▼
                        HudController.ShowMessage()          HudController counter refresh
```

Growth ticking (no per-plot `Update`):

```
FarmGrid.Update()            ← 3 instances in the scene, that is all
  └ accumulates dt, fires every 0.2 s
     └ iterates ONLY its `growing` list (plots actually mid-growth)
        └ FarmPlot.AdvanceGrowth(dt) → returns false when Ready → grid drops it from the list
```

A plot registers itself with its grid on planting (`Grid.NotifyGrowthStarted(this)`) and is removed automatically when it ripens. An `Empty`/`Tilled`/`Ready` plot costs **zero** per-frame work.

### How systems find each other (no singletons, no `Find`)

- `FarmPlot` → `PlayerInventory` / `ActionFeedbackChannel`: via the `interactor` `GameObject` already passed into `IInteractable.Interact`. Looked up once per interactor and cached.
- `HudController` → inventory / feedback / interaction: serialized references, wired by the scene builder.
- `FarmGrid` → `FarmingSettings`: serialized SO reference.

---

## 4. Files to create

| File | Purpose |
|---|---|
| `Scripts/Inventory/ItemIds.cs` | Naming convention helpers (`seed_wheat`, `wheat`) so ids are not scattered string literals |
| `Scripts/Inventory/PlayerInventory.cs` | `Get/Add/Remove/Has` over a `Dictionary<string,int>`, clamped at 0, `Changed` event, serialized starting stock |
| `Scripts/Core/ActionFeedbackChannel.cs` | Tiny event relay so gameplay can post a message without knowing the HUD exists |
| `Scripts/Farming/FarmingSettings.cs` | SO holding the **Development Crop Growth Multiplier** (and future farming tuning) |
| `Scripts/Farming/FarmPlotSnapshot.cs` | `[Serializable]` capture/restore struct — save-system preparation only, no persistence built |
| `Editor/CropVisualBuilder.cs` | Generates the per-crop growth-stage prefabs from primitives, tinted from `CropDefinition.CropColor` |

## 5. Files to modify

| File | Change |
|---|---|
| `Farming/CropDefinition.cs` | Add `seedItemId`, `harvestItemId`, `stagePrefabs[]`, `yieldMin`/`yieldMax` (replacing single `yieldAmount`), `RollYield()`, `GetStagePrefab(int)` |
| `Farming/FarmPlot.cs` | The real work: state machine, growth accumulation, stage visuals under `CropAnchor`, contextual label, inventory interaction, snapshot |
| `Farming/FarmGrid.cs` | Growing-plot list + interval ticking, `FarmingSettings` reference, stop overriding the plot label |
| `Interaction/InteractableBase.cs` | Add `LabelChanged` event so a focused interactable can refresh the HUD prompt |
| `UI/HudController.cs` | Inventory counters, live prompt refresh, transient message line |
| `Editor/FarmPrototypeBuilder.cs` | Generate crop stage prefabs + `FarmingSettings`; add `PlayerInventory`/`ActionFeedbackChannel` to the player; add seed/crop pills and the message label to the HUD; wire it all |

**Untouched:** `PlayerController`, `FarmCameraController`, `PlayerInputProvider`, `KeyboardMoveInputSource`, `MobileJoystick`, `VirtualButton`, `InteractionController`, `IInteractable`, `GameBootstrap`, `GridCoord`, `PlotState`, `FarmLandmark`, `FarmEnvironmentBuilder`, `ProtoPalette`, `ProtoUi`, `AndroidPlayerSetup`.

---

## 6. Testing strategy

Compile verification is real (Unity recompiles on file change; `Logs/Editor.log` is parsed for `error CS` / `warning CS`). Runtime behaviour is verified by the developer in Play mode using the checklist below, because Editor menu items cannot be driven from outside the running Editor.

| Test | Steps | Expected |
|---|---|---|
| A | Walk to any plot, press USE | Prompt `TILL` → `Tilled!`, soil darkens, prompt becomes `PLANT WHEAT` |
| B | Press USE on the tilled plot | Seeds 10 → 9, `Planted Wheat!`, seed mound appears, prompt `WHEAT GROWING` |
| C | Wait | Visual changes 4 times, prompt stays `WHEAT GROWING` until mature |
| D | At maturity | Prompt flips to `HARVEST` **without moving** (proves the live-refresh path) |
| E | Press USE | `+N Wheat`, wheat counter rises, visual removed, soil resets, prompt `TILL` |
| F | Spam USE on a Ready plot | Exactly one reward; second press shows `TILL` behaviour, never a second harvest |
| G | Empty seeds: plant 10 times, then till + USE an 11th plot | `No Wheat Seeds`, plot stays `Tilled`, seeds never go below 0 |
| H | Till/plant several plots at different times | Each ripens on its own clock; states are independent |
| I | Walk to a tomato plot, till, USE | `No Tomato Seeds` — proves generic data-driven path with zero tomato-specific code |

Growth speed for testing is controlled by `Assets/_Game/Data/FarmingSettings.asset` → **Development Growth Multiplier**, and is not surfaced in the player HUD.
