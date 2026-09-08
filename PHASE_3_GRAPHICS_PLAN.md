# Phase 3 Plan — Graphics / Visual Foundation

Audit performed against the files as they exist on disk, not from memory.

---

## 0. Blocking finding, read this first

**The Phase 2 scene rebuild was never run.**

| Evidence | Result |
|---|---|
| `Farm_Prototype.unity` last written | **00:47** — the Phase 1 build |
| `PlayerInventory` / `ActionFeedbackChannel` in scene | **0 occurrences** |
| `SeedPill` / `ProducePill` / `MessagePanel` in scene | **0 occurrences** |
| `Assets/_Game/Data/FarmingSettings.asset` | **missing** |
| `Assets/_Game/Prefabs/Crops/` | **missing** |
| `Crop_Wheat.asset` fields | still Phase 1 (`yieldAmount: 1`, `secondsPerStage: 20`, no `stagePrefabs`) |

Phase 2 **code** is present and compiles cleanly. It has simply never been applied to the scene, so tilling/planting/growing/harvesting cannot be working in the current scene — the plots have no inventory to talk to.

This does not block Phase 3. Both phases land together the first time `Little Farm Story ▸ Rebuild Farm Scene` is run.

---

## 1. Current visual problems

Catalogued from `FarmEnvironmentBuilder.cs`, `FarmPrototypeBuilder.cs`, `ProtoPalette.cs`, `ProtoUi.cs` and the 33 generated materials.

### Geometry
1. **Everything is a stock Unity primitive.** Cube/Sphere/Cylinder/Capsule only. Hard 90° edges everywhere; nothing reads as "rounded and chunky".
2. **Stock spheres are 768 triangles each.** Tree canopies use 3 apiece — 18 trees ≈ 41k triangles of foliage alone, for shapes that should cost ~100 tris each.
3. **No shape language.** A tree is a cylinder plus balls; a cow is a stack of boxes. Silhouettes do not read at the gameplay camera distance.
4. **Buildings are a box with a two-slab roof.** No fascia, no trim, no window frames, no depth in the door, no chimney.
5. **Renderer count per prop is high** because every part is its own GameObject — nothing is combined.

### Framing (the biggest single problem)
6. **The camera is far too tight for portrait.** At `distance 17` / `FOV 45`, the vertical coverage is ≈14 m, which on a 9:16 screen is only **≈7.9 m horizontally**. A field pad is 13 m wide — *you cannot see a whole field on screen*.
7. **The ground runs out inside the view frustum.** At pitch 52° the visible ground reaches ≈19 m beyond the focus point; with focus clamped to ±26 that needs ground out to ±45, but the ground is only ±36. The player can see the world edge.
8. **No sky is ever visible** (top ray is 29° below horizontal), so the solid sky-blue background is dead code — all screen area is ground, which makes the ground quality decisive.

### Lighting and materials
9. **Hard shadows.** `Mobile_RPAsset` has `m_SoftShadowsSupported: 0`, so the requested soft shadows silently render hard.
10. **Flat ambient.** Trilight is set but the sky/equator/ground colours are near-neutral, so shadowed faces go muddy grey instead of picking up a cool sky bounce.
11. **No depth cue.** Fog is off and everything is the same contrast at all distances, so the farm reads flat.
12. **Uniform smoothness 0.08 on all 33 materials**, including water — nothing separates wet, painted, matte and foliage surfaces.
13. **Colours were picked per-object, not as a palette.** Several near-duplicate greens and browns that do not belong to one scheme.

### UI
14. **Default-Unity look**: flat cream rounded rects, plain white circles, no shadow, no depth, no border.
15. **USE button** has no pressed state beyond a CanvasGroup alpha change.
16. **Joystick** is a plain ring and a plain disc, low contrast against bright grass.
17. **Legacy `Text`** everywhere — TMP Essential Resources are still **not imported** (verified: no `Assets/TextMesh Pro/`).

### Content gaps
18. **No crop visuals exist at all** (Phase 2 builder never ran).
19. No flowers, barrels, crates, hay variety, or signage — the farm has no small-scale detail.
20. Pond is two flat cylinders with a hard edge.

---

## 2. Proposed art direction — original identity

**"Sunny mid-morning, hand-painted toy farm."**

- **Form language:** chamfered, faceted, chunky. Every solid gets a visible bevel so edges catch the key light. Flat-shaded (per-face normals) rather than smooth — the facets *are* the style, and they are cheaper.
- **Light:** warm key from the upper left, cool sky fill. Soft, low-contrast shadows. Nothing harsh or photoreal.
- **Colour:** saturated but not neon. Warm chocolate soil against bright grass; pale wheat-sand paths as the connective tissue; cream buildings so the saturated roofs carry the accent.
- **Readability first:** each farm zone gets a distinct roof colour and silhouette so it is identifiable from the camera without reading a label.

### Palette (original, 5 families)

| Family | Colours |
|---|---|
| Grass | `#7CC15A` light · `#63AC48` mid · `#4E8F39` deep |
| Earth | `#6B4A2F` rich soil · `#4E3320` tilled · `#E3C98F` sand path · `#CBAE74` path edge |
| Wood & stone | `#A9713F` warm · `#7A4E2A` dark · `#C99863` light · `#B8B2A6` stone |
| Buildings | `#FAF3E0` cream wall · `#D9603F` terracotta · `#3FA9A0` teal · `#E8B23C` mustard |
| Life & accent | `#8FD46A`/`#6FBF52`/`#4E9B3C` leaves · `#E8C55A` wheat · `#E04B3C` tomato · `#F5D046` corn · `#F5A623` amber accent · `#4FB3E8` water |

Three smoothness tiers only: **Matte 0.03** (soil, foliage, fabric), **Soft 0.12** (wood, walls, painted), **Sheen 0.42** (water, glass).

---

## 3. Files that will change

### New

| File | Purpose |
|---|---|
| `Editor/StylizedMeshLibrary.cs` | Procedural mesh generation: chamfered box, lathe/revolve, low-poly sphere, cone, triangular prism, disc. Saves shared `.asset` meshes. Includes **automatic winding correction** via signed-volume test, and a **mesh-combining** utility. |
| `Editor/PropLibrary.cs` | Reusable prefabs: trees ×3, bushes, rocks, flowers, fence section, hay bale, barrel, crate, signpost. |
| `Editor/CharacterBuilder.cs` | Farmer, chicken, cow. |
| `Editor/BuildingBuilder.cs` | Farmhouse, coop, barn, market stall, silo, production shelter. |
| `Scripts/Player/PlayerVisualBob.cs` | Lightweight idle-breathe / walk-bob driven by `PlayerController.NormalisedSpeed`. Runtime, but **read-only** with respect to movement. |

### Modified

| File | Change |
|---|---|
| `Editor/ProtoPalette.cs` | Replaced with the structured palette above |
| `Editor/ProtoAssets.cs` | Smoothness tiers, shared-material helpers, mesh-asset folder |
| `Editor/CropVisualBuilder.cs` | Rewritten: real wheat / tomato / corn built from the mesh library, combined into 1–2 renderers per stage |
| `Editor/FarmEnvironmentBuilder.cs` | Ground extended to ±55, rounded paths, new fences, new pond, decoration via `PropLibrary` |
| `Editor/FarmPrototypeBuilder.cs` | Orchestration, camera defaults, lighting, fog, UI rebuild |
| `Editor/ProtoUi.cs` | Panel/shadow/ring helpers, better sprite generation |
| `Scripts/Camera/FarmCameraController.cs` | **Default values only.** `SetTarget`/`SetZoom`/`SetYaw`/`SetBounds` untouched |
| `Scripts/UI/VirtualButton.cs` | **Additive only:** optional pressed-scale target. Existing `IActionInputSource` contract unchanged |

---

## 4. Systems that must remain untouched

`PlayerController` · `PlayerInputProvider` · `KeyboardMoveInputSource` · `MobileJoystick` (logic) · `InteractionController` · `IInteractable` · `InteractableBase` · `FarmPlot` state machine · `FarmGrid` · `PlayerInventory` · `CropDefinition` · `FarmingSettings` · `GameBootstrap` · `ItemIds` · `HudController` logic.

`FarmPlot` must keep driving visuals purely through `CropDefinition.GetStagePrefab(i)` and `CropAnchor` — **no crop-specific code enters the plot.**

---

## 5. Performance considerations

| Decision | Effect |
|---|---|
| Replace 768-tri stock spheres with ~110-tri lathe spheres | ≈85% fewer triangles on all foliage, animals, rocks |
| Chamfered box = 44 tris | Comparable to a stock cube (12), far better looking |
| **Combine each prop/crop stage into one renderer with per-material submeshes** | Mature wheat drops from ~9 renderers to 2; trees from 4 to 1 |
| Shared mesh + shared material assets | SRP Batcher and GPU instancing both stay effective |
| Shadow casting off on all small props and all crops | Only buildings, trees and the player cast |
| Ground enlarged but still **one box, one material** | No terrain system, no splat maps, zero textures |
| Linear fog 70→115 m | Hides the world edge, adds depth, negligible cost in URP Lit |
| No post-processing, no reflections, no realtime GI, no particles | Unchanged from Phase 1 |
| Zero texture assets in the 3D scene | Entire look is vertex colour + flat materials |

Camera reframing (distance 26 / FOV 42) widens the view, which raises what is on screen; the renderer-combining above is what pays for it.

---

## 6. Visual implementation order

1. `StylizedMeshLibrary` — everything else depends on it. Compile-check.
2. Palette + material tiers. Compile-check.
3. `PropLibrary` — trees, bushes, rocks, fences, containers, flowers.
4. `CharacterBuilder` — farmer, chicken, cow.
5. `BuildingBuilder` — all six structures.
6. `CropVisualBuilder` — wheat, then tomato, then corn.
7. `FarmEnvironmentBuilder` — ground, paths, pond, boundary, decoration placement.
8. UI polish + `VirtualButton` pressed state.
9. Camera defaults, lighting, fog.
10. `PlayerVisualBob`.
11. Full compile verification.

---

## 7. Verification limits

Compile status is verified for real by watching Unity rebuild the assemblies and parsing `Logs/Editor.log`. **Runtime and visual results cannot be verified from here** — Editor menu items and Play mode cannot be driven externally, and no screenshot can be taken. Every runtime claim in the final report will be labelled as unverified.
