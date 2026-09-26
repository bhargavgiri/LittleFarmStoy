# Runtime Integration Fix

Not a new phase. This corrects why the generated farm played dead despite a builder that reported success.

---

## 1. Root cause (confirmed from `Logs/Editor.log`, not inferred)

Every rebuild in the log — going back through **every run recorded** — printed this three times:

```
Little Farm Story: ConfigureCropGrowth was handed a null CropDefinition.
The crop asset failed to load or create, so no crop visuals will exist.
```

### Why the reference was null

`BuildPrototypeScene` authored the crop and settings assets **before** this line:

```csharp
Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
```

`NewScene` unloads assets that nothing in the new scene references yet. The C# references then became Unity's **"fake null"**: the managed wrapper compares `== null` while the asset is still perfectly present on disk.

That single fact explains everything that was observed, including the parts that looked contradictory:

| Consequence | Effect in play |
|---|---|
| `ConfigureCropGrowth` returned at its `definition == null` guard | `Assets/_Game/Prefabs/Crops/` **was never created**; `stagePrefabs: []` on all three crops; **planted crops were invisible forever** |
| `EditorConfigureGrowth` never ran | `secondsPerStage` stayed at a stale `20` instead of the intended `6` — wheat took **60 s**, not 18 s |
| `WireInventoryCounters` returned at its `crop == null` guard | `counters: []` on `HudController` — **the HUD never displayed a single item quantity**, so no inventory change was ever visible |
| `VerifyCropVisuals` hit `if (crop == null) continue;` | The check that existed specifically to catch this **skipped itself silently** |
| Yet `FarmGrid.assignedCrop` held a valid GUID | `SerializedObject.objectReferenceValue` resolves the underlying instance id, so serialization still wrote the correct reference. **The scene looked perfectly wired while the C# reference read null.** This is why a file-level scene audit alone could not find it. |

Net effect: till and plant *did* mutate state correctly, but nothing appeared in the soil and no counter moved, so the whole farm read as scenery.

### Second root cause: interaction priority

`InteractionController` picked the **nearest** interactable with no tie-break. The coop's `FarmLandmark` area trigger is 4.5 × 2.2 × 3 centred at local `(3.4, 0, -0.4)`; chickens stand inside it. `FarmLandmark.OnInteract` only writes a Debug.Log. So pressing USE next to a chicken frequently hit the landmark and **did nothing at all** — matching "chicken feeding is not visibly working" exactly.

### Third: nothing was observable even when it worked

Production timers of 25 s and 50 s, hunger of 45 s and 75 s, and no HUD counters for corn, eggs or milk. Even a fully working loop was invisible.

## 2. Fixes

| # | Fix | File |
|---|---|---|
| 1 | **All asset authoring moved after `NewScene`** (crops, farming settings, palette) | `FarmPrototypeBuilder.cs` |
| 2 | `LoadOrCreate<T>` re-resolves a dead reference by reimporting instead of creating a duplicate — belt and braces against the same hazard | `FarmPrototypeBuilder.cs` |
| 3 | `IInteractable.InteractionPriority` + priority bands (Animal 30 > Plot 20 > Production 10 > Area 5 > Decoration 0). `InteractionController` now sorts by **priority first, distance as the tie-break** | `IInteractable.cs`, `InteractableBase.cs`, `InteractionController.cs` |
| 4 | **`VerifyBuild`** asserts every gameplay precondition and the build ends with `PLAYABLE` or `NOT PLAYABLE`, never a bare "scene built" | `FarmPrototypeBuilder.cs` |
| 5 | `VerifyCropVisuals` no longer skips a null crop silently | `FarmPrototypeBuilder.cs` |
| 6 | HUD counters extended to 5 rows: wheat seeds, wheat, corn, eggs, milk — same pill component, data-driven ids taken from the assets | `FarmPrototypeBuilder.cs` |
| 7 | Development pacing: `FarmingSettings.developmentGrowthMultiplier = 4`, `AnimalHabitat.developmentSpeedMultiplier = 5`, **both editor-only** | `FarmPrototypeBuilder.cs`, `AnimalHabitat.cs` |
| 8 | Animals seed hunger directly instead of fast-forwarding time (which also burned off the happiness), so ~half start feedable | `AnimalController.cs` |
| 9 | Feedback wording aligned to the requested strings; added `<Crop> Ready!` and `<Product> Ready!` announcements | `FarmPlot.cs`, `AnimalInteraction.cs` |
| 10 | Optional `logInteractions` diagnostic on `InteractionController`, **default off** | `InteractionController.cs` |

Nothing in `PlayerController`, `PlayerInputProvider`, `MobileJoystick`, `VirtualButton`, `FarmGrid`, `PlayerInventory`, `AnimalDefinition`, `AnimalMovement`, `AnimalProduction`, `AnimalNeeds` or `AnimalIdleAnimator` was restructured.

## 3. Development pacing (editor only — device builds are unaffected)

| Loop | Authored | In the Editor |
|---|---|---|
| Wheat, sow → harvest | 18 s | **4.5 s** |
| Chicken, fed → egg ready | 25 s | **5 s** |
| Chicken hunger, full → hungry | 22.5 s | **4.5 s** |
| Cow, fed → milk ready | 50 s | **10 s** |
| Cow hunger, full → hungry | 37.5 s | **7.5 s** |

To feel the shipping pace, set `DevelopmentGrowthMultiplier` and `DevelopmentAnimalMultiplier` to `1` in `FarmPrototypeBuilder`, or edit the two values in the inspector.

## 4. Seed vs harvest ids — verified distinct

`ItemIds.Seed("wheat")` → `seed_wheat` is consumed by `FarmPlot.TryPlant`.
`ItemIds.Harvest("wheat")` → `wheat` is granted by `FarmPlot.TryHarvest`, and is also the chicken's feed.
`VerifyBuild` now fails the build if any crop's seed id equals its harvest id.

Starting stock: `seed_wheat 10`, `wheat 6`, `corn 4`, `egg 0`, `milk 0` — and `VerifyBuild` fails if the player cannot afford to plant, feed a chicken, or feed a cow.

## 5. Verification status

**Compilation:** verified offline with Unity's own Roslyn (`6000.5.9f1`) against Unity's reference assemblies. 0 errors; 0 warnings other than `CS0649` on `[SerializeField]` privates, which Unity suppresses project-wide.

**Runtime: not performed.** The Unity Editor is open and holds `Temp/UnityLockfile`, so a batch-mode instance cannot open the project and Play mode cannot be driven from here. No runtime results are claimed.

The build now proves itself instead: after `Little Farm Story → Rebuild Farm Scene`, the Console ends with either

```
Little Farm Story: farm scene built at ... and verified PLAYABLE. Press Play to test.
```

or a list of `VERIFY -` errors followed by `NOT PLAYABLE`. That line, plus `Logs/Editor.log`, is checkable evidence rather than a claim.
