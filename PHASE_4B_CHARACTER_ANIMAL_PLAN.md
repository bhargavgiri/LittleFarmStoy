# Phase 4B Plan — Production Character & Animal Visuals

Audited against the sources on disk after Phase 4A. Baseline checkpoint: `d025a1d` (Phase 4A is uncommitted on top).

---

## 1. Current actor problems

### 1.1 The animals physically cannot be animated

`CharacterBuilder.BuildPrefab` ends with `StylizedMeshLibrary.CombineIntoSingleRenderer(temp, ...)`. Both the chicken and the cow are collapsed into **one mesh with one renderer**. There is no hierarchy left — no head, no legs, no tail as separate transforms. Nothing can move relative to anything else, ever.

This is the single most important structural finding: it is not a polish problem, it is an architecture problem.

### 1.2 The farmer has no joints

The farmer is a **flat list of 17 `MeshObject` calls** parented directly to `BobRoot`. There is no `Hips`, no `Shoulder`, no `Head` transform. `PlayerVisualBob` can therefore only translate/scale/rotate the *entire* body as one rigid lump — which is exactly what it does. Arms and legs can never swing.

### 1.3 Shape vocabulary is exhausted

Counting the actual calls: farmer 17 parts, chicken 10, cow 13 — built from four shapes (`LowPolySphere`, `ChamferBox`, `Tapered`, `Cone`). A head is a sphere. A chicken body is a sphere. A cow body is a chamfered box. The parts are well-placed but the *vocabulary* has no organic forms in it, so everything reads as assembled primitives.

### 1.4 No face

The farmer has eyes, a nose and cheeks — no mouth, no eyebrows, no hair. At the gameplay camera the head reads as a blank ball with two dots.

### 1.5 No farmer prefab

The farmer is built inline inside `FarmPrototypeBuilder.BuildPlayer`. There is no reusable `Farmer.prefab`, so the character cannot be dropped anywhere else (NPCs later will need exactly this).

### 1.6 No animation infrastructure at all

`com.unity.modules.animation` is present; **nothing in the project references `Animator`, `SkinnedMeshRenderer`, `AnimationClip` or `Avatar`.** There is no rig, no controller, no clips.

---

## 2. Desired proportions

Stylised, chunky, readable at a distance. Head deliberately oversized.

| Farmer | Value |
|---|---|
| Total height (incl. hat) | ~1.88 m |
| Body height (to crown) | 1.72 m |
| Head height | ~0.44 m → roughly **1 : 4** head-to-body |
| Shoulder pivot | y 1.14 |
| Hip pivot | y 0.80 |
| Shoulder width | 0.62 |

`CharacterController` is height 1.7, centre y 0.87 — unchanged. The hat sitting slightly proud of the capsule is intentional and harmless.

| Chicken | Value | | Cow | Value |
|---|---|---|---|---|
| Body length | 0.62 | | Body length | 2.0 |
| Standing height | ~0.72 | | Withers height | ~1.5 |
| vs farmer | knee-high | | vs farmer | chest-high |

Scale sanity against the world: barn door opening is 3.2 m wide × 3.0 m tall (cow 1.5 m tall, 0.98 m wide → walks through comfortably); coop pop-hole is 0.64 × 0.76 (chicken 0.72 tall → fits); farmhouse door is 1.25 × 2.15 (farmer 1.72 → fits with headroom).

---

## 3. Actor visual hierarchy

The mandated separation is preserved and extended:

```
Player                      gameplay root - CharacterController, PlayerController
└── Visual                  rotated by PlayerController (facing)
    └── BobRoot             PlayerVisualBob - body bob/breathe  [UNCHANGED]
        └── Rig             NEW joint root
            ├── Hips        renderer
            │   ├── Leg_L   renderer (thigh + shin + boot)
            │   └── Leg_R   renderer
            └── Torso       renderer (shirt + overalls + straps)
                ├── Arm_L   renderer (sleeve + hand)
                ├── Arm_R   renderer
                └── Head    renderer (head + face + hair + hat)
```

**No transform conflicts:** `PlayerController` writes `Visual.rotation`; `PlayerVisualBob` writes `BobRoot` local position/scale/rotation; the new `FarmerLimbAnimator` writes **only** `Leg_*`, `Arm_*` and `Head` local rotations. Three writers, three disjoint sets of transforms.

Animals follow the same idea:

```
Chicken            Cow
├── Body           ├── Body
│   ├── Head       │   ├── Head
│   ├── Leg_L      │   ├── Leg_FL / FR / BL / BR
│   └── Leg_R      │   └── Tail
```

---

## 4. Required meshes

Added to `StylizedMeshLibrary` — all lathe profiles or extrusions, so they reuse existing, already-verified generators:

| Mesh | Purpose |
|---|---|
| `Egg` | Heads, chicken body — an ovoid, not a sphere |
| `Pear` | Cow muzzle, hands, wattles — fat-bottomed ovoid |
| `Teardrop` | Tail tufts, comb lobes, ear shapes |
| `Wedge` | Beaks, boot soles, hooves, eyebrows |
| `Crescent` | Horns and the curled hat brim |
| `Drum` | Chunky limb segments with rounded caps |

## 5. Required materials

**Target: no new materials.** The existing palette already carries `Skin`, `Shirt`, `Denim`, `Straw`, `Boots`, `White`, `Charcoal`, `Beak`, `Comb`, `Muzzle`, `Horn`, `WoodDark`. Additions only if a genuine gap appears — likely two: a hair colour and a second animal body tone for the chicken variant.

## 6. Prefab hierarchy

| Prefab | Contents |
|---|---|
| `Prefabs/Characters/Farmer.prefab` | **NEW** — the full rig, visual only, no gameplay components |
| `Prefabs/Characters/Chicken_A.prefab` | Cream plumage |
| `Prefabs/Characters/Chicken_B.prefab` | Warm brown plumage — same meshes, different materials |
| `Prefabs/Characters/Cow.prefab` | Segmented, with `AnimalIdleAnimator` |

`BuildPlayer` instantiates `Farmer.prefab` under `BobRoot` instead of building geometry inline. Gameplay components stay on the `Player` root exactly as they are.

## 7. Animation strategy

**Honest position: a skinned, rigged character cannot be produced through this tooling.** I will not fabricate a fake rig.

Instead: **segmented rigid-body animation**, which is a legitimate production technique for chunky stylised mobile games, not a workaround. Body parts are separate rigid meshes that rotate at joints, with geometry overlapping at each joint so no gap opens.

- `FarmerLimbAnimator` — reads `PlayerController.NormalisedSpeed` (already public), swings legs and arms in counterphase, adds a head lead-turn. Writes local rotations only.
- `AnimalIdleAnimator` — time-driven idle for animals: breathing, head sway, tail swish, occasional peck. Per-instance phase offset so a flock never moves in lockstep. Gated on renderer visibility so off-screen animals cost nothing.

No Animator, no clips, no state machine. The architecture stays ready for one: when a skinned model arrives, delete the animator component and the joints go with it.

## 8. Mobile performance strategy

| Actor | Renderers | Note |
|---|---|---|
| Farmer | 7 | Was 17 loose objects; each joint's geometry is combined into one mesh |
| Chicken | 4 | Was 1 (but unanimatable) |
| Cow | 7 | Was 1 (but unanimatable) |

Combining *per joint* is the key trade: it buys animation while actually **reducing** the farmer's renderer count. Animal animators self-disable when off screen. Shared meshes and materials throughout; the two chicken variants share every mesh.

## 9. Gameplay systems that must remain untouched

`PlayerController` · `PlayerInputProvider` · `KeyboardMoveInputSource` · `MobileJoystick` · `VirtualButton` · `InteractionController` · `IInteractable` · `InteractableBase` · `FarmPlot` · `FarmGrid` · `PlayerInventory` · `CropDefinition` · `FarmingSettings` · `HudController` · `GameBootstrap` · `FarmCameraController` · `PlayerVisualBob`.

The `Player` root, its `CharacterController` dimensions, and the `Visual` transform that `PlayerController.visualRoot` points at are all preserved exactly.

## 10. Procedural vs external assets — honest assessment

| Actor | Verdict |
|---|---|
| **Farmer** | **Recommend external.** A hero character under a portrait camera is the most scrutinised asset in the game. Segmented rigid limbs will look good and animate believably, but a sculpted, skinned model with real deformation will look better. Ship the placeholder, commission the replacement. |
| **Chicken** | **Procedural is adequate to ship.** Small, seen at distance, rigid-segment motion is exactly how a chicken moves anyway. |
| **Cow** | **Borderline.** The placeholder will read well standing and idling; a skinned model would be needed for convincing walking or grazing. |

Specifications written so the swap needs no gameplay change: `ART_ASSET_SPEC_FARMER.md`, `ART_ASSET_SPEC_CHICKEN.md`, `ART_ASSET_SPEC_COW.md`.

---

## 11. Verification limits

Compile status is verified for real with Unity's own Roslyn against Unity's reference assemblies. **Visual and runtime results cannot be verified from here** — no Play mode, no screenshots. Every visual claim in the report will be labelled unverified.
