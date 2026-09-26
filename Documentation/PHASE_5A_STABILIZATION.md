# Phase 5A — Gameplay Stabilization

Making the existing systems reliable and legible. No new gameplay, no UI redesign, no Save/Load.

---

## 1. What the previous fix actually achieved (verified from `Logs/Editor.log`)

The asset-lifetime fix worked. The log now shows, on two consecutive rebuilds:

```
Little Farm Story: wired 5 HUD inventory counters.
Little Farm Story: build verification PASSED - the farm is PLAYABLE. 3 fields, 2 habitats, 9 animals.
Little Farm Story: farm scene built ... and verified PLAYABLE. Press Play to test.
```

And on disk, for the first time in the project's history:

- `Assets/_Game/Prefabs/Crops/` exists with **12 stage prefabs** (4 per crop)
- `Crop_Wheat.asset` now reads `secondsPerStage: 6` (was a stale `20`) with four `stagePrefabs` entries
- `FarmingSettings.asset` reads `developmentGrowthMultiplier: 4`, `editorOnly: 1`

## 2. Remaining issues found in this audit, and what was done

### 2.1 Interaction priority ordering

The priority bands were `Animal 30 > Plot 20`. Reordered to the requested preference — `Plot 25 > Animal 20 > Production 10 > Area 5 > Decoration 0`. Both gameplay actors still outrank the coop/barn area triggers, which was the actual bug; plots and animals never share ground in this layout, so the relative order between them is documented intent rather than a live tie-break.

### 2.2 The player could act through walls

`InteractionController` had no occlusion test, so a solid barn wall did not block reach. Added an opt-in line-of-sight check: **one raycast per candidate, on the existing 0.12 s scan timer**, triggers ignored so interactables cannot shadow each other, and skipped entirely inside 1.2 m where nothing can meaningfully be in the way. It is a serialized toggle (`requireLineOfSight`) precisely because a false negative would be worse than the problem it solves — turn it off if a legitimate target is ever blocked.

### 2.3 Prompts read like debug tags

`TILL` / `PLANT WHEAT` / `WHEAT GROWING` / `HARVEST` became `Till` / `Plant Wheat` / `Growing` / `Harvest`. Animal prompts already read `Feed Chicken` / `Collect Egg` / `Feed Cow` / `Collect Milk`.

### 2.4 Feedback wording

Aligned to the requested strings: `Plot Tilled`, `Wheat Seed Planted`, `Wheat Is Growing 45%`, `Wheat Ready!`, `Harvested Wheat +1`, `Chicken Fed`, `Egg Ready!`, `Egg Collected +1`, `Cow Fed`, `Milk Ready!`, `Milk Collected +2`.

Every one of these is emitted **only** on a path that has already mutated state. The failure paths (`Need Wheat Seeds`, `Need 2 Corn to feed the Cow`) run after an all-or-nothing `PlayerInventory.Remove` returned false, so nothing changed anywhere.

### 2.5 No way to prove any of it

Addressed below — this is the substantial piece of work in this phase.

## 3. The automated Play-mode test

`Assets/_Game/Editor/GameplayLoopTest.cs`, menu **`Little Farm Story/Run Gameplay Loop Test (Play Mode)`**.

It exists because "it compiles" and "the scene looks correctly wired" both proved worthless as evidence — the farm was silently unplayable for three phases while every static check passed.

The test teleports the player to a real plot and a real animal and presses the **real interact button through the real `InteractionController`**, so it exercises the entire chain rather than calling gameplay methods directly:

```
player position → proximity scan → priority → line of sight → focus
→ InteractableBase.Interact → FarmPlot / AnimalInteraction
→ state change → inventory change → visual change
```

20 steps, each with its own timeout, stopping at the first failure so a broken precondition cannot produce a cascade of misleading errors:

| Group | Steps |
|---|---|
| Scene | gameplay objects exist and are wired |
| Interaction | player focuses a plot; focuses the chicken **rather than the coop's area trigger**; focuses the cow |
| Farming | till → plant (asserts **exactly one** seed consumed and a crop visual spawned) → grow **with the player moved 14 m away** → harvest (asserts produce gained, plot Empty, visual removed) → second press pays nothing |
| Chicken | becomes hungry → feed (asserts feed consumed, hunger reset, phase `Producing`) → egg ready → collect (asserts inventory gained) → second press pays nothing |
| Cow | the same, with corn and milk |
| Animals | at least one animal moved in 12 s, and **no animal left its habitat bounds** |

Results go to the Console and to `Documentation/RUNTIME_TEST_RESULTS.md`.

The harness lives in the Editor assembly and is referenced by nothing in gameplay code, so none of it ships.

## 4. Development pacing (editor only — device builds unaffected)

| Loop | Authored | In Editor |
|---|---|---|
| Wheat sow → harvest | 18 s | **4.5 s** |
| Chicken fed → egg | 25 s | **5 s** |
| Cow fed → milk | 50 s | **10 s** |

`DevelopmentGrowthMultiplier` and `DevelopmentAnimalMultiplier` in `FarmPrototypeBuilder`; set both to `1` for shipping pace.

## 5. Deliberately not changed

- **Camera.** Pitch 46°, distance 28, FOV 40, range 14–44. At that distance the visible width is ~11.5 m, which is exactly the wheat field's 11 m footprint — the framing is defensible, it is not implicated in any reported failure, and adjusting it without being able to look at the result risks making it worse. Revisit with a screenshot in hand.
- **Movement.** `moveSpeed 5`, accel 34, turn 720°/s, gravity −22. Nothing in the audit suggests a problem.
- **Architecture.** `FarmGrid`/`FarmPlot` central ticking, `AnimalHabitat` central ticking, `PlayerInventory`, `PlayerController`, `PlayerInputProvider`, `MobileJoystick`, `VirtualButton`, `AnimalIdleAnimator` — untouched. No per-crop or per-animal `Update`, no NavMesh, no runtime scene searches, no per-frame allocation.

## 6. Verification status

**Compilation:** Unity's own Roslyn (`6000.5.9f1`) against Unity's reference assemblies. 0 errors, 0 warnings beyond `CS0649` on `[SerializeField]` privates, which Unity suppresses project-wide. Two deprecated Unity 6.5 APIs (`FindFirstObjectByType`, `FindObjectsSortMode`) were caught and replaced during this pass.

**Runtime: not performed by me.** The Unity Editor is open and holds `Temp/UnityLockfile`, so a batch-mode instance cannot open the project and Play mode cannot be driven from here. Auto-entering Play mode in the user's open Editor without being asked would be an intrusive thing to do to their session, so the test is a menu item rather than something that fires on its own.

**To get the runtime result, one click:**

```
Little Farm Story → Rebuild Farm Scene
Little Farm Story → Run Gameplay Loop Test (Play Mode)
```

It enters Play mode, runs for roughly 60–90 seconds, exits by itself, and writes `Documentation/RUNTIME_TEST_RESULTS.md`. That file and `Logs/Editor.log` are both readable from here, so the next report can quote measured results instead of claims.
