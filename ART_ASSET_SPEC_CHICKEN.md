# Art Asset Specification — Chicken

**Status:** procedural segmented placeholder ships today
(`Assets/_Game/Prefabs/Characters/Chicken_A.prefab`, `Chicken_B.prefab`).

**Recommendation: the placeholder is adequate to ship.** A chicken is small, seen at
distance, and moves in sharp rigid jerks anyway — which is exactly what segmented
animation produces well. Replace it only when the rest of the art bar rises.

This spec exists so the swap is drop-in if and when you do.

---

## 1. Style

Rounded, compact, cheerful. Must share the farm's design language: soft forms,
flat shading, no textures. Body clearly larger than the head; distinct tail fan.

## 2. Scale and orientation

| Property | Value |
|---|---|
| Standing height | **0.72 m** |
| Body length | 0.62 m |
| Forward axis | **+Z** (the beak points +Z) |
| Up axis | +Y |
| Origin | **Between the feet, on the ground plane** |
| Export scale | 1.0, metres |

Sanity check: the coop pop-hole is 0.64 × 0.76 m, so the bird must fit through it.

## 3. Polygon budget

| Target | Triangles |
|---|---|
| Preferred | **400 – 800 tris** |
| Hard ceiling | 1,200 tris |

There may be a dozen or more birds on screen.

## 4. Materials

**Maximum 3 slots.** Two colourways must be possible from the same mesh.

| Slot | Covers | Variant A | Variant B |
|---|---|---|---|
| 1 `Plumage` | Body, wings, tail, head | `#FBFAF6` cream | `#C98B52` warm brown |
| 2 `Beak` | Beak, legs, feet | `#F2A33C` | same |
| 3 `Comb` | Comb, wattle | `#E05252` | same |

URP/Lit, metallic 0, smoothness ~0.03. Vertex colours preferred over textures.
If textured: one shared 256×256 albedo, albedo only.

## 5. Rig

| Requirement | Detail |
|---|---|
| Bone count | **≤ 10** |
| Required bones | Root, Body, Neck, Head, UpperLeg ×2, Foot ×2, Tail |
| Skinning | ≤ 2 influences per vertex |
| Avatar | **Generic** (not Humanoid) |

A rig is optional. Rigid segmented parts are perfectly acceptable for this animal —
if delivered that way, provide the parts as named child transforms matching §7.

## 6. Animation

| Clip | Length | Notes |
|---|---|---|
| `Idle` | 2–3 s | Small head bob, weight shift |
| `Peck` | ~0.5 s | Sharp head dip and return |
| `Walk` *(optional)* | ~0.6 s | Head thrusts forward on each step |

In-place only. No AI exists and none is planned in this phase.

## 7. Prefab integration contract

If delivered unrigged, the transform names must be exactly:

```
Chicken_A
├── Body      (body + wings + tail)
│   └── Head  (head + comb + beak + wattle + eyes)
├── Leg_L
└── Leg_R
```

`AnimalIdleAnimator` binds to `body`, `head` and the two legs by serialized reference.
Keep those names and the existing component drives the new model with zero code change.

If delivered rigged with clips: delete `AnimalIdleAnimator`, add an `Animator`, done.

## 8. Deliverables

- `.fbx` mesh (+ skeleton and clips if rigged), Y-up, metres, scale 1.0
- Source file
- Two material colourways

## 9. Acceptance checks

- [ ] Origin between the feet at y = 0, beak facing +Z
- [ ] 0.72 m tall; passes through a 0.64 × 0.76 m opening
- [ ] ≤ 3 slots, ≤ 1,200 tris
- [ ] Two colourways from one mesh
- [ ] Recognisable as a chicken in silhouette at the gameplay camera
- [ ] No pink/missing materials in URP
