# Phase 5 — Animal Gameplay (implemented)

Chickens and cows are gameplay entities: they get hungry, they are fed from the existing inventory, they produce eggs and milk on a timer, they are collected through the existing interaction button, and they wander inside their pens.

---

## 1. Implemented systems

| Type | File | Kind |
|---|---|---|
| `AnimalType`, `AnimalActivity`, `ProductionPhase`, `AnimalMood`, `AnimalActionResult` | `Scripts/Animals/AnimalEnums.cs` | enums |
| `AnimalDefinition` | `Scripts/Animals/AnimalDefinition.cs` | ScriptableObject |
| `AnimalNeeds` | `Scripts/Animals/AnimalNeeds.cs` | plain C# |
| `AnimalProduction` | `Scripts/Animals/AnimalProduction.cs` | plain C# |
| `AnimalMovement` | `Scripts/Animals/AnimalMovement.cs` | plain C# |
| `AnimalController` | `Scripts/Animals/AnimalController.cs` | MonoBehaviour |
| `AnimalInteraction` | `Scripts/Animals/AnimalInteraction.cs` | MonoBehaviour : `InteractableBase` |
| `AnimalHabitat` | `Scripts/Animals/AnimalHabitat.cs` | MonoBehaviour |
| `AnimalSnapshot` | `Scripts/Animals/AnimalSnapshot.cs` | serializable struct |

Two components per animal (`AnimalController`, `AnimalInteraction`) on top of the Phase 4B `AnimalIdleAnimator`. Needs, production and movement are plain classes owned by the controller — composition without seven MonoBehaviours per animal.

`AnimalHabitat` takes the role a scene-wide `AnimalManager` usually plays. The pen already defines membership, capacity and bounds, so putting the registry there means no singleton and no scene search, and it mirrors the `FarmGrid → FarmPlot` relationship the project already uses.

## 2. Animal states

Three independent concerns rather than one enum, because a hungry animal still walks and a producing animal still stands about:

```
AnimalActivity   Idle  ⇄  Wandering        interrupted by  Eating
ProductionPhase  Dormant → Producing → Ready → (collect) → Dormant
AnimalMood       Hungry / Content / Happy   — derived from hunger + happiness, never stored
```

Hunger does not get its own activity; it lengthens the idle dwell instead, so a hungry animal reads as lethargic without duplicating Idle and Wandering.

## 3. Feeding rules

1. Collection takes priority — a ready product is collected instead of feeding.
2. `AnimalNeeds.IsHungry` must be true (hunger ≥ `hungerThreshold`), else the press only reports status.
3. `PlayerInventory.Remove(feedItemId, feedAmount)` is all-or-nothing. It failing means nothing changed anywhere.
4. On success: hunger → 0, happiness += `feedHappiness`, activity → `Eating` for `eatDuration`, `AnimalIdleAnimator.TriggerDip()` plays the peck/graze, and a production cycle begins **only if the phase was `Dormant`**.

## 4. Production rules

- One cycle per feeding. `AnimalProduction.TryBegin` refuses while a cycle runs or a product waits.
- `Ready` after `productionSeconds` of accumulated simulation time.
- `Collect` sets the phase back to `Dormant` **before** granting anything, so a second press returns `InvalidState` and pays nothing. Same ordering as `FarmPlot.TryHarvest`.
- Happiness ≥ `happyThreshold` grants `happyBonusAmount` extra units. That is the only mechanical effect of happiness.
- **Nothing is instantiated.** No egg GameObjects, no spawned props, no `Instantiate`/`Destroy` at runtime.

## 5. Inventory integration

The existing `PlayerInventory` is used unchanged. `ItemIds` gained two constants:

```csharp
public const string Egg  = "egg";
public const string Milk = "milk";
```

Scene starting stock (authored in the builder, not in the runtime class): `seed_wheat 10`, `wheat 6`, `corn 4`, `egg 0`, `milk 0`. The wheat and corn exist so both animal loops are testable before the first harvest.

| Failure mode | What prevents it |
|---|---|
| Negative quantities | `Remove` is all-or-nothing; `Add` ignores non-positive amounts |
| Feeding without stock | `Remove` returns false; the animal is untouched |
| Duplicate production | `TryBegin` requires phase `Dormant` |
| Duplicate collection | `Collect` requires phase `Ready` and clears it first |
| Repeated button presses | Every mutation sits behind a state guard, so extra presses are no-ops |

## 6. Capacity system

`AnimalHabitat` holds `capacity` and a registered list. `Register` refuses — with a log naming the reason — when the species is wrong or the pen is full. `HasRoomFor(definition)`, `Capacity`, `UsedCapacity` and `AnimalCount` are public so a future shop can validate a purchase before spawning anything.

Scene values: coop capacity 8 with 6 chickens; barn capacity 4 with 3 cows.

## 7. Movement system

Destination legality is decided **once per walk** by `AnimalHabitat.TrySampleDestination` (up to 8 random samples), which rejects a point that is:

- outside the pen's walkable rectangle,
- inside an exclusion rectangle (the coop footprint, the barn footprint),
- within `minSeparation` (0.9 m) of another registered animal,
- within `playerClearance` (1.6 m) of the player.

All 8 rejected simply means the animal stays put and tries again after its next dwell.

`AnimalMovement` then turns toward the destination and walks along its own facing, so animals never strafe sideways. No `Rigidbody`, no `CharacterController`, no `NavMeshAgent`, no raycasts. A 12-second travel timeout means an animal can never be permanently stuck.

The pens sit inside the farm fence and away from the pond, so leaving the farm or entering water is geometrically impossible rather than merely discouraged.

## 8. Animation integration

`AnimalIdleAnimator` is preserved and extended, not replaced. No Animator Controller, no clips, no re-combining of the mesh, no destroyed transforms. Added:

- `SetLocomotion(float)` — blends a stride into the existing leg joints and adds a footfall bounce to the body. Legs 0 and 3 swing against 1 and 2, which is a diagonal gait on a cow and a simple alternation on a chicken.
- `TriggerDip()` — starts the existing peck/graze dip immediately, used the moment an animal is fed.

With locomotion at 0 the idle behaves exactly as it did in Phase 4B.

**Transform ownership** (three writers, three disjoint sets):

| Writer | Owns |
|---|---|
| `AnimalMovement` | animal **root** position and Y rotation |
| `AnimalIdleAnimator` | child joint local rotations, plus `Body` local position/scale |
| nothing else | — |

## 9. Performance decisions

- **One `Update` per habitat**, not per animal. Two pens → two `Update` calls for all nine animals.
- Two tick rates: per-frame for movement and the animator drive; **0.5 s** for hunger, happiness and production, with accumulated (not sampled) delta so timing stays exact.
- Zero physics on animals; the only collider is a trigger the existing interaction scan already looks for.
- No coroutines, no runtime `Instantiate`/`Destroy`, no per-frame allocation, no `FindObjectOfType`, no `GameObject.Find`.
- Separation and player-clearance checks run once per walk, not per frame.
- Starting hunger is staggered per animal so a flock never changes state on the same frame.
- Off screen: `AnimalIdleAnimator` still pauses its visuals. **Simulation is not gated on visibility** — hunger and production keep running, as required.

### Static-batching fix (real defect found and corrected)

`ProtoAssets.MarkStatic(worldRoot)` was flagging every world object `BatchingStatic`, animals included. A batching-static renderer is baked into a shared combined mesh at its authored transform, so moving it afterwards does not work. `ProtoAssets.ClearStatic` was added and the builder now carves every `AnimalController` hierarchy back out of the static set. This also corrects the Phase 4B animals, whose idle motion had the same conflict.

## 10. Future save-system requirements

`AnimalController.CaptureSnapshot()` / `RestoreSnapshot()` already round-trip everything a save needs: instance id, species id, habitat id, hunger, happiness, production phase, production elapsed, position and yaw. `RestoreSnapshot` bypasses the action guards deliberately — gameplay must go through the `Try*` methods. No `SaveManager` was built.

## 11. Future economy integration points

Eggs and milk are ordinary inventory ids, so the market will consume them with no animal-side change. `AnimalDefinition.purchaseCost` is authored but unused. `AnimalHabitat.HasRoomFor` + `Register` are the hooks a shop needs. No selling, no prices, no coins from produce.

## 12. Known limitations

1. **No runtime verification** — see §14. Every behavioural claim here is from code reading, not from Play mode.
2. **The scene has not been regenerated.** `Farm_Prototype.unity` is a build product; the animals become gameplay entities only after `Little Farm Story/Rebuild Farm Scene` is run. Until then the scene still holds the Phase 4B scenery animals.
3. **Animals ignore each other while walking.** Separation is enforced when a destination is chosen, not continuously, so two animals can pass close mid-walk. They separate again at the next dwell.
4. **Exclusion zones are axis-aligned rectangles in habitat space.** Adequate for the coop and the barn; a rotated or L-shaped building would need a better shape.
5. **No walk-cycle foot planting.** Legs swing about their joints; feet slide slightly. Acceptable at the gameplay camera distance, and it is a property of segmented rigid animation, not a bug.
6. **Happiness is thin by design** — it gates a bonus unit and nothing else.
7. **The HUD shows no animal state.** By instruction: no UI work in this phase. State is exposed through `StateChanged`, `LabelChanged` and `ActionFeedbackChannel` for the UI phase to consume.

## 13. Testing results

**Compile:** verified offline with Unity's own Roslyn (`6000.5.9f1`) against Unity's reference assemblies, both assemblies. **0 errors, 0 warnings** other than `CS0649` on `[SerializeField]` private fields, which Unity suppresses project-wide via `suppressCommonWarnings: 1`.

One real error was caught and fixed this way: `Object.GetInstanceID()` is obsolete in Unity 6.5 (`CS0619`, "use GetEntityId instead"); the instance-id fallback no longer uses it.

**Play mode:** not run — see below.

## 14. Runtime verification

**Runtime verification not performed.**

The Unity Editor is currently open and holds `Temp/UnityLockfile`, so a second batch-mode instance cannot open the project, and there is no way to drive Play mode or capture a screenshot from here. No screenshots or runtime results are fabricated anywhere in this document.

To verify, in the open Editor:

1. `Little Farm Story → Rebuild Farm Scene`
2. Press Play and walk to the coop.
