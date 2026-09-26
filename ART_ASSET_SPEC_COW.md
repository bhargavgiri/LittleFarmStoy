# Art Asset Specification — Cow

**Status:** procedural segmented placeholder ships today
(`Assets/_Game/Prefabs/Characters/Cow.prefab`).

**Recommendation: borderline.** The placeholder reads well standing and idling, which
is all the game currently asks of it. Commission a replacement when cows need to
*walk* or *graze* convincingly — rigid segments handle a standing animal fine but a
walk cycle on a large quadruped exposes the lack of deformation.

---

## 1. Style

Chunky, rounded, friendly. Wide barrel body on short legs, large readable head.
Must be unmistakably a different mass from the farmer and the chicken.
Original stylised pattern — irregular patches, not a copy of any existing game's cow.

## 2. Scale and orientation

| Property | Value |
|---|---|
| Withers height | **1.5 m** |
| Body length (nose to tail base) | ~2.4 m |
| Body width | 0.98 m |
| Forward axis | **+Z** (the muzzle points +Z) |
| Up axis | +Y |
| Origin | **Centred between the four hooves, on the ground plane** |
| Export scale | 1.0, metres |

Sanity check: the barn door opening is 3.2 m wide × 3.0 m tall — the cow must pass
through comfortably. Against a 1.72 m farmer the cow should read as chest-high.

## 3. Polygon budget

| Target | Triangles |
|---|---|
| Preferred | **1,200 – 2,000 tris** |
| Hard ceiling | 3,000 tris |

Expect 3–6 cows on screen.

## 4. Materials

**Maximum 4 slots.**

| Slot | Covers | Hex |
|---|---|---|
| 1 `Hide` | Body, legs, head base | `#FBFAF6` |
| 2 `Patch` | Patches, hooves, tail tuft, forelock | `#33302C` |
| 3 `Muzzle` | Muzzle, udder, inner ear | `#F0A0A5` |
| 4 `Horn` | Horns | `#E8DCC0` |

Patches should be **vertex-coloured or a separate submesh**, not a texture, so the
pattern can be varied per instance later. URP/Lit, metallic 0, smoothness ~0.03–0.12.

## 5. Rig

| Requirement | Detail |
|---|---|
| Bone count | **≤ 20** |
| Required bones | Root, Spine, Chest, Neck, Head, UpperLeg/LowerLeg/Hoof ×4, Tail ×2 |
| Skinning | ≤ 2 influences per vertex |
| Avatar | **Generic** |

## 6. Animation

| Clip | Length | Notes |
|---|---|---|
| `Idle` | 3–5 s | Slow breathing, tail swish, occasional ear flick |
| `Graze` | 3–4 s | Head lowers to the ground, holds, lifts |
| `Walk` | ~1.2 s | Slow four-beat gait, in place |

In-place only. No AI in this phase.

## 7. Prefab integration contract

If delivered unrigged, the transform names must be exactly:

```
Cow
├── Body          (barrel + shoulder + haunch + patches + udder)
│   ├── Head      (skull + muzzle + ears + horns + eyes)
│   └── Tail
├── Leg_FL
├── Leg_FR
├── Leg_BL
└── Leg_BR
```

`AnimalIdleAnimator` binds to `body`, `head`, `tail` and the four legs by serialized
reference. Keep those names and the existing component drives the new model unchanged.

If delivered rigged with clips: delete `AnimalIdleAnimator`, add an `Animator`.

## 8. Deliverables

- `.fbx` mesh (+ skeleton and clips if rigged), Y-up, metres, scale 1.0
- Source file
- Materials as described

## 9. Acceptance checks

- [ ] Origin centred between the hooves at y = 0, muzzle facing +Z
- [ ] 1.5 m at the withers; fits a 3.2 × 3.0 m barn door
- [ ] Chest-high against a 1.72 m farmer
- [ ] ≤ 4 slots, ≤ 3,000 tris, ≤ 20 bones
- [ ] Patches are geometry or vertex colour, not a baked texture
- [ ] Reads as a cow in silhouette at the gameplay camera
- [ ] No pink/missing materials in URP
