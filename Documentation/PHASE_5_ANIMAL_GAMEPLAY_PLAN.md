# Phase 5 Plan — Animal Gameplay (Chicken + Cow)

Audited against the sources on disk after Phase 4B. Baseline checkpoint: `d025a1d`; Phase 4A/4B are uncommitted on top.

---

## 1. Existing architecture (as audited, not assumed)

| System | File | What it actually does |
|---|---|---|
| Interaction contract | `Interaction/IInteractable.cs` | `Transform`, `InteractionLabel`, `CanInteract`, `Interact` |
| Interaction base | `Interaction/InteractableBase.cs` | `SetLabel` → `LabelChanged` event; `Interact` guards on `CanInteract` then calls `OnInteract` |
| Focus + input | `Interaction/InteractionController.cs` | `OverlapSphereNonAlloc` on a **0.12 s timer**, radius 2.6, picks **nearest valid**, routes `ConsumeInteractPressed()` |
| Inventory | `Inventory/PlayerInventory.cs` | id→int dictionary; `Add`, `Remove` (**all-or-nothing**), `Has`, `Changed` event |
| Item ids | `Inventory/ItemIds.cs` | `Seed(cropId)`, `Harvest(cropId)`; plain strings |
| Feedback | `Core/ActionFeedbackChannel.cs` | `Post(string)` → `MessagePosted` |
| Field ticking | `Farming/FarmGrid.cs` | Owns plots; **one `Update` per field**, ticks only `growingPlots`, accumulates rather than samples delta |
| Plot | `Farming/FarmPlot.cs` | Owns its own state; `Try*` methods return `FarmActionResult`; no `Update` |
| Data | `Farming/CropDefinition.cs` | `ScriptableObject` + `EditorConfigure*` editor-only authoring hooks |

**The pattern to copy is `FarmGrid → FarmPlot`:** a container owns a set of actors, runs a single `Update`, and pushes accumulated time into them. Actors own their own state and expose `Try*` methods that are safe to call twice.

## 2. Existing animal hierarchy (Phase 4B)

```
Chicken_A / Chicken_B (root, AnimalIdleAnimator)   Cow (root, AnimalIdleAnimator)
├── Body        renderer                           ├── Body       renderer
│   └── Head    renderer                           │   ├── Head   renderer
├── Leg_L       renderer                           │   └── Tail   renderer
└── Leg_R       renderer                           └── Leg_FL/FR/BL/BR
```

`AnimalIdleAnimator` writes **only child joint local rotations** plus `body` local position/scale. It never touches the root transform. It pauses entirely while off screen (`OnBecameInvisible`).

Currently the prefabs have **no collider, no interaction component and no gameplay component at all** — they are placed by `FarmPrototypeBuilder.BuildChickenArea` / `BuildCowArea` as pure scenery.

## 3. Current interaction flow

```
PlayerInputProvider.ConsumeInteractPressed()
   → InteractionController.TryInteract()
      → Current.CanInteract(player) → Current.Interact(player)
         → InteractableBase.Interact → OnInteract(interactor)
            → FarmPlot resolves PlayerInventory + ActionFeedbackChannel off the interactor, once, and caches them
```

Animals plug into this unchanged: an `InteractableBase` subclass on the animal root with a small trigger collider. **No new input path is introduced.**

## 4. New animal systems

| Type | Kind | Responsibility |
|---|---|---|
| `AnimalDefinition` | ScriptableObject | All per-species tuning. No chicken/cow logic in runtime code. |
| `AnimalController` | MonoBehaviour | Owns one animal's state; delegates to the three plain classes below. |
| `AnimalNeeds` | plain C# | Hunger + happiness. |
| `AnimalProduction` | plain C# | Production phase + timer. |
| `AnimalMovement` | plain C# | Bounded wandering; writes root position + root yaw only. |
| `AnimalInteraction` | MonoBehaviour : `InteractableBase` | Feed / collect / status, contextual label. |
| `AnimalHabitat` | MonoBehaviour | Registry, capacity, wander bounds, exclusion zones, **the single `Update`**. |
| `AnimalSnapshot` | struct | Save-system preparation, mirroring `FarmPlotSnapshot`. |

### Deviation from the suggested structure (deliberate)

The brief sketched `AnimalController / AnimalData / AnimalState / AnimalNeeds / AnimalMovement / AnimalProduction / AnimalInteraction` as components, and a separate `AnimalManager`. Two changes, both because the existing project already has a cleaner equivalent:

1. **`AnimalHabitat` replaces `AnimalManager`.** The coop and the barn are already real scene objects with a footprint. Making the habitat the registry means capacity, bounds, registration and ticking all live in the one object that already defines them — exactly the `FarmGrid` relationship. No singleton, no scene-wide search, no lifetime question.
2. **Needs / production / movement are plain C# classes, not MonoBehaviours.** Composition is preserved (`AnimalController` is ~200 lines and delegates everything), but each animal costs **2 components** instead of 7. On a target of 10–20 animals that is 40 fewer Unity objects and 40 fewer serialization roots.

## 5. Data model

`AnimalDefinition` (ScriptableObject, `Little Farm Story/Animal Definition`):

| Field | Purpose |
|---|---|
| `animalId`, `displayName`, `animalType` | Identity; `animalId` is save-stable |
| `prefab` | Visual prefab reference (`Chicken_A`, `Cow`) |
| `purchaseCost` | Placeholder for the future shop — **unused in Phase 5** |
| `feedItemId`, `feedAmount` | What feeding consumes from `PlayerInventory` |
| `productItemId`, `productAmount`, `happyBonusAmount` | What collecting grants |
| `hungerDuration` | Seconds from fed to fully hungry |
| `hungerThreshold` | Fraction of hunger at which the animal reads as hungry |
| `productionSeconds` | Seconds from feeding to product ready |
| `feedHappiness`, `happinessDecayPerSecond`, `happyThreshold` | Happiness tuning |
| `eatDuration` | Seconds the eating animation/state holds |
| `moveSpeed`, `turnSpeed`, `idleDwell`, `wanderChance` | Movement tuning |
| `interactionRadius` | Trigger collider radius on the prefab |
| `habitatType`, `capacityWeight` | Which habitat accepts it, and how much room it takes |

Authored assets: `Assets/_Game/Data/Animal_Chicken.asset`, `Animal_Cow.asset`.

## 6. State machine

One enum per concern rather than one enum for everything, because the combined list in the brief contains contradictions — a hungry animal still wanders, and a happy animal is still idle or walking.

```
AnimalActivity   : Idle → Wandering → Idle,  interrupted by Eating
ProductionPhase  : Dormant → Producing → Ready → (collect) → Dormant
AnimalMood       : derived, not stored — Hungry / Content / Happy
```

Transitions:

```
Idle      --dwell elapsed & habitat has a free spot--> Wandering
Wandering --arrived or stuck timeout-->                Idle
any       --fed-->                                    Eating (eatDuration)
Eating    --timer elapsed-->                          Idle
```

Hunger raises the idle dwell (a hungry animal is lethargic) rather than adding a `Hungry` activity that would have to duplicate Idle and Wandering.

## 7. Feeding flow

```
Player interacts
  ├─ product Ready?      → collect instead (collection always wins)
  ├─ not hungry?         → status message, nothing consumed
  └─ hungry
       ├─ inventory.Remove(feedItemId, feedAmount) == false → "Need N Wheat", nothing changes
       └─ true → hunger = 0
                 happiness += feedHappiness
                 activity = Eating
                 if production phase == Dormant → begin Producing
                 feedback "Fed Bella"
```

`PlayerInventory.Remove` is all-or-nothing, so a failed feed can never partially spend.

## 8. Production flow

```
Dormant --fed--> Producing --productionSeconds elapsed--> Ready --collected--> Dormant
```

- Exactly **one** cycle per feeding: `TryFeed` only starts a cycle when the phase is `Dormant`.
- **Duplicate collection is impossible:** `TryCollect` returns `InvalidState` unless the phase is `Ready`, and it sets the phase to `Dormant` *before* granting the items — the same ordering `FarmPlot.TryHarvest` uses.
- **No GameObjects are spawned.** Production is a logical state; the ready-state is communicated through the interaction label and the feedback channel.
- Happiness ≥ `happyThreshold` grants `happyBonusAmount` extra product. That is the only gameplay effect of happiness, deliberately.

## 9. Wandering flow

```
Idle dwell expires
  → habitat.TrySampleDestination(asker, out point)     up to 8 random samples
      reject if outside the habitat rect
      reject if inside any exclusion rect (the coop / the barn footprint)
      reject if within minSeparation of another registered animal
      reject if within playerClearance of the player
  → AnimalMovement.SetDestination(point)
  → per frame: move at moveSpeed, rotate root yaw toward travel, arrive within arriveDistance
  → Idle for idleDwell * (1 + hunger)
```

If all 8 samples fail the animal simply stays idle and retries next dwell — no retry storm, no pathfinding, no NavMesh.

## 10. Capacity system

`AnimalHabitat` holds `capacity` and the registered list. `Register` returns false when `UsedCapacity + capacityWeight > capacity` or the animal's type is not accepted, and logs which. `UsedCapacity`, `Capacity`, `HasRoomFor(definition)` are public so a future shop can validate a purchase before spawning anything. Phase 5 spawns from the scene builder only; no purchase UI.

## 11. Inventory integration

The existing `PlayerInventory` is used unchanged — **no second inventory, no API rewrite**. Two ids are added to `ItemIds` (`Egg`, `Milk`) so the strings are not retyped across the codebase. Starting stock gains `wheat 6` and `corn 4` purely so both loops are testable from a cold start; this is dev-facing starting stock, same as the existing `seed_wheat 10`.

Guards against the failure modes named in the brief: negative quantities are impossible (`Remove` is all-or-nothing and `Add` ignores non-positive amounts); feeding without stock changes nothing; double production is blocked by the `Dormant` phase guard; double collection by the `Ready` phase guard; repeated interaction is idempotent because every mutation is behind a state guard, exactly as in `FarmPlot`.

## 12. Future integration points

| Future system | Hook that already exists after Phase 5 |
|---|---|
| Production machines | `productItemId` lands in `PlayerInventory`; machines consume from there |
| Market / economy | `AnimalDefinition.purchaseCost`, and eggs/milk as ordinary inventory ids |
| Orders | `animalId` + product ids are stable strings |
| Shop | `AnimalHabitat.HasRoomFor(definition)` + `Register` |
| Save/load | `AnimalController.CaptureSnapshot()` / `RestoreSnapshot()` (see §23 of the brief) |
| UI | `AnimalController.StateChanged`, `InteractableBase.LabelChanged`, `ActionFeedbackChannel.MessagePosted` |

## 13. Performance strategy

- **One `Update` per habitat**, not per animal. Two habitats in the scene → two `Update` calls for the whole farm.
- Two tick rates: a per-frame pass for movement smoothing and animator drive, and a **0.5 s simulation pass** for hunger, happiness and production, with accumulated (not sampled) delta so timing stays exact.
- No physics on animals: no `Rigidbody`, no `CharacterController`, no raycasts, no NavMesh agents. Movement is a `MoveTowards` plus a `RotateTowards`.
- No coroutines, no `Instantiate`/`Destroy` at runtime, no per-frame allocation. Destination sampling uses the habitat's own registered list, never a scene search.
- `AnimalIdleAnimator` keeps its off-screen pause. **Simulation is not gated on visibility** — hunger and production must keep running off screen, per the brief.
- Separation and player-clearance checks run only at destination-sampling time (roughly once per idle dwell per animal), not per frame.

## 14. Testing checklist

Chicken: exists → approach → focus acquired → feed → `wheat` decreases → happiness rises → production starts → ready → collect → `egg` in inventory → second collect rejected → refeed starts a new cycle.

Cow: same with `corn` → `milk`.

Wandering: animals move; stay inside the pen rect; never enter the coop/barn footprint; never overlap each other; never crowd the player; never leave the farm.

Regression: Empty → Till → Plant Wheat → Grow → Ready → Harvest → inventory, with `FarmPlot`, `FarmGrid`, `PlayerController`, `InteractionController`, `FarmCameraController`, `MobileJoystick` and `VirtualButton` untouched.

## 15. Verification limits

Compile status is verified for real, offline, with Unity's own Roslyn against Unity's reference assemblies. **Play-mode behaviour cannot be driven from here** — there is no way to enter Play mode or capture a screenshot. Every runtime claim in the final report is labelled accordingly.
