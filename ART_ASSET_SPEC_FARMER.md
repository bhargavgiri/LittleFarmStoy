# Art Asset Specification — Farmer (Player Character)

**Status:** a procedural segmented placeholder ships today (`Assets/_Game/Prefabs/Characters/Farmer.prefab`).
**Recommendation: commission an external replacement.** The farmer is the most scrutinised asset in the game — always on screen, always near the centre of a portrait frame. A sculpted, skinned model with real deformation will beat rigid segments, and this is the one actor where that difference is clearly visible.

Delivering against this spec lets the model drop in **without a single line of gameplay code changing.**

---

## 1. Style

Original stylised casual-farm character. Chunky, friendly, readable at a distance. Not realistic, not chibi-extreme.

- Roughly **1 : 4** head-to-body ratio (deliberately oversized head)
- Rounded, soft forms — no sharp anatomical detail
- Flat-shaded / low-frequency surfaces; the game uses **no textures anywhere in the 3D scene**
- Must read as "friendly farmer" in silhouette alone

**Do not** reference or reproduce any existing game's character.

## 2. Scale and orientation

| Property | Value |
|---|---|
| Total height (crown, no hat) | **1.72 m** |
| Total height incl. hat | **1.86 m** |
| Shoulder width | 0.62 m |
| Unity units | 1 unit = 1 metre, model exported at **scale 1.0** |
| Forward axis | **+Z** |
| Up axis | **+Y** |
| Origin | **Between the feet, on the ground plane** (y = 0) |

The origin convention is not negotiable — `PlayerController` positions the root and the visual must sit on the ground from that root.

## 3. Polygon budget

| Target | Triangles |
|---|---|
| Preferred | **2,500 – 4,000 tris** |
| Hard ceiling | 6,000 tris |

Mid-range Android is the target. There may eventually be several NPCs using this same mesh.

## 4. Materials

**Maximum 4 material slots.** The game's palette is authoritative — match these values:

| Slot | Covers | Hex |
|---|---|---|
| 1 `Skin` | Face, hands, forearms | `#F0C199` |
| 2 `Clothing` | Shirt + dungarees (two-tone via vertex colour, not two slots) | shirt `#4F9DD9`, denim `#3B5D8C` |
| 3 `Straw` | Hat | `#EBCB78`, band `#D9603F` |
| 4 `Leather` | Boots, belt | `#6B4326` |

- Shader target: **URP/Lit**, metallic 0, smoothness **0.03–0.12**
- **Vertex colours are preferred over textures.** If a texture is unavoidable: single 512×512 albedo atlas, no normal map, no roughness map.
- Enable GPU instancing on all materials.

## 5. Rig

| Requirement | Detail |
|---|---|
| Type | Humanoid-compatible skeleton |
| Bone count | **≤ 30** |
| Required bones | Hips, Spine, Chest, Neck, Head, Shoulder/UpperArm/LowerArm/Hand ×2, UpperLeg/LowerLeg/Foot ×2 |
| Skinning | **≤ 2 bone influences per vertex** (mobile) |
| Avatar | Configure as Unity **Humanoid** so retargeted clips can be used |

Facial bones are **not** required — the face can be static geometry.

## 6. Animation

Minimum set, root-motion **off**, all loops seamless:

| Clip | Length | Notes |
|---|---|---|
| `Idle` | 2–4 s | Gentle breathing, occasional weight shift |
| `Walk` | ~1 s | Matches ~5 m/s at `moveSpeed` default; loop must not slide |
| `Run` *(optional)* | ~0.7 s | For a future sprint |
| `Interact` *(optional)* | ~1 s | A downward reach — used later for till/plant/harvest |

Animation must be **in-place**. `PlayerController` owns all translation and facing.

## 7. Prefab integration contract

The replacement must satisfy exactly this, and nothing more:

```
Farmer.prefab                    <- model root, origin at feet, facing +Z
└── (model hierarchy / Animator)
```

Integration steps when the asset arrives:

1. Drop the model in `Assets/_Game/Art/Characters/`.
2. Save the prefab as `Assets/_Game/Prefabs/Characters/Farmer.prefab`.
3. Delete the `FarmerLimbAnimator` component from that prefab.
4. Add an `Animator` with a controller exposing a float parameter `Speed` (0–1).
5. Feed it from `PlayerController.NormalisedSpeed` (already public) with a four-line component.

**Nothing else changes.** `FarmPrototypeBuilder.BuildPlayer` instantiates whatever prefab lives at that path under `BobRoot`; the `Player` root, `CharacterController` (height 1.7, radius 0.35, centre y 0.87), `PlayerController`, `InteractionController`, `PlayerInventory` and the whole input chain are untouched.

`PlayerVisualBob` can stay (it adds a subtle body bob on top) or be deleted — it is additive and independent.

## 8. Deliverables

- `.fbx` — mesh + skeleton + skinning, Y-up, metres, scale 1.0
- Separate `.fbx` or clips for each animation
- Source file (`.blend` / `.max`) for future edits
- Materials as described, or a material assignment note

## 9. Acceptance checks

- [ ] Origin between the feet at y = 0, facing +Z
- [ ] 1.72 m tall (1.86 m with hat)
- [ ] ≤ 4 material slots, ≤ 6,000 tris, ≤ 30 bones, ≤ 2 influences/vertex
- [ ] Humanoid avatar configures without errors
- [ ] Idle and Walk loop seamlessly with no foot sliding at 5 m/s
- [ ] Reads clearly at the gameplay camera (pitch 46°, distance 28, FOV 40, portrait 9:16)
- [ ] No pink/missing materials in URP
