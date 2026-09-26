# Phase 4A Plan — Production Environment Art

Audited against the generated scene from 22:44 and the builder sources as they exist on disk.
Baseline checkpoint: `d025a1d`.

---

## 1. Visual audit — what is actually wrong

### 1.1 The world is geometrically flat

Every single object sits on `y = 0`. `BuildGround` is three stacked boxes (`Ground`, `OuterField`, `FarmLawn`) plus eight flat discs. There is **no height variation anywhere in the scene**. This is the single biggest reason it still reads as a prototype: real depth cues (overlap, occlusion, parallax, silhouettes against a horizon) are all absent, so the fog is doing all the work.

### 1.2 Silhouettes repeat

- **Trees**: 3 variants, but all are *trunk + 3–4 spheres*. From the gameplay camera they read as the same object at three sizes.
- **Buildings**: farmhouse, coop and barn all come from the same `Shell()` recipe — a chamfered box with a triangular prism roof. They differ only in **scale and roof colour**. A barn should be unmistakable at a glance; right now it is "the red one".

### 1.3 Paths are rectangles

`PathStrip` is a hard-edged box with a slightly larger box behind it. Only the spur ends get rounded caps. The two main avenues are 62 m × 5.4 m rectangles crossing at a circle — geometric, not designed.

### 1.4 No visual storytelling density

Props exist but are sparse and mostly decorative filler. Zones do not tell you what they are through their contents — the coop area has a feed trough and a water bowl, and that is it.

### 1.5 Nothing frames the composition

No foreground elements, no background mass. On a 9:16 portrait screen the top third of the frame is empty flat grass.

### 1.6 Material clutter

**58 materials on disk, ~48 in the palette.** Ten orphans survive from Phase 1/2 (`M_Grass`, `M_GrassDark`, `M_Leaf`, `M_LeafLight`, `M_TreeTrunk`, `M_Pink`, `M_RoofRed`, `M_RoofBlue`, `M_RoofGreen`, `M_CanopyStripe`, `M_Accent_*`). Harmless but they make the palette non-authoritative.

### 1.7 Crop visuals are broken (carried over, unfixed)

`stagePrefabs: []` on all three crops and `Prefabs/Crops/` does not exist. `M_Crop_wheat.mat` — created on the **first line** of `BuildStagePrefabs` — is absent, and the Editor log jumps straight from the crop `.asset` imports to `Prefabs/Props`. So `ConfigureCropGrowth` returned at its `definition == null` guard without ever calling through, silently.

**Planted crops therefore render nothing.** Fixing this is in scope: it is a visual-pipeline defect in editor tooling, not a gameplay change.

---

## 2. Art direction

**"A warm smallholding on a bright afternoon."** Same identity as Phase 3, executed with real form language instead of scaled primitives.

Three rules drive every decision:

1. **Every landmark gets a unique silhouette**, not a unique colour. You should recognise the barn from its roof shape alone, in shadow, at thumbnail size.
2. **Depth comes from mass, not fog.** Rolling ground beyond the fence, trees at three depth layers, buildings that occlude each other.
3. **Density is intentional.** Props cluster into *scenes* (a work-in-progress woodpile with an axe and chips) rather than scattering evenly.

---

## 3. Environment hierarchy

```
--- WORLD ---
  Environment
    Ground            flat walkable pad (collider unchanged)
    Terrain           NEW: rolling hills + berms, all outside the fence, non-walkable
    Paths             organic segmented avenues + plaza
    Boundary          fence + gateposts
  Fields              pads, frames, grids, signs
  Areas               production / chicken / cow / market / home
  Decoration
    Treeline          near (inside fence) + far (background mass)
    Groundcover       bushes, flowers, rocks
    Storytelling      per-zone prop clusters
    Pond
```

**Critical safety rule:** all new terrain relief is placed **outside the boundary fence** (|x| or |z| > 31) or is flat decoration inside it. The walkable ground stays a single flat box collider, so `PlayerController` and `CharacterController` behaviour is bit-identical.

---

## 4. New reusable assets

### Meshes (added to `StylizedMeshLibrary`)
| Mesh | Use |
|---|---|
| `GambrelPrism` | The barn's four-slope roof — the iconic barn silhouette |
| `HipRoof` | Farmhouse / coop, softer than a bare prism |
| `IrregularDisc` | Organic ground patches, pond, path blobs — seeded radius jitter |
| `Mound` | Background hills and berms |
| `Plank` | Fence rails, decking, crates — a box with bevelled long edges |
| `Trapezoid` | Building bodies with a slight batter, planters, troughs |

### Prefabs
- **Trees ×5**: `Broadleaf`, `Tall`, `Fruit`, `Conifer`, `Sapling` — genuinely different silhouettes
- **Bushes ×3**: `Round`, `Wide`, `Sprig`
- **Rocks ×3**: `Boulder`, `Rock`, `Pebbles`
- **Flowers ×3**: `White`, `Amber`, `Pink` clusters
- **Props ×10 new**: `Wheelbarrow`, `Bucket`, `WateringCan`, `Bench`, `LogPile`, `Sack`, `Basket`, `Trough`, `Lantern`, `ToolRack`

All combined into a single renderer with per-material submeshes, as in Phase 3.

---

## 5. Materials

The palette proved sufficient as-is: every new prop and building part was built from existing entries, so **no new materials were added** — a better outcome than growing the palette.

The ten orphaned Phase 1/2 materials (`M_Grass`, `M_TreeTrunk`, `M_RoofRed`, `M_Pink`, …) are **left in place**, not deleted: the brief for this phase forbids deleting assets. They are unreferenced and safe for you to remove manually whenever you want.

Still three finishes only: Matte 0.03 / Soft 0.12 / Sheen 0.42. Contrast comes from value and hue separation, not saturation.

---

## 6. Lighting

Keep the Phase 3 setup (it is sound) and refine:
- Key slightly warmer and angled to rake across the buildings rather than face-on, so roof planes separate.
- Ambient sky cooled a touch to push shadow separation without raising contrast.
- Fog start pushed out so the near farm is unaffected and only the new background hills soften.
- Shadow distance already 78 from `UrpQualitySetup`; unchanged.

No post-processing, no reflections, no realtime GI, no particles.

---

## 7. Performance strategy

| Decision | Effect |
|---|---|
| Background hills are ~8 large low-poly mounds | Massive depth for ~8 draw calls |
| Every prop and tree combined to 1 renderer | A 5-variant treeline of 40 trees is 40 draw calls, not 200 |
| Shadow casting **on** only for buildings, trees, player, large props | Small props, flowers, groundcover, path detail all off |
| Shared meshes + shared materials, instancing on | SRP Batcher stays effective |
| Zero textures in the 3D scene | Unchanged from Phase 3 |
| Palette unchanged, no new materials | No extra shader variants to warm |

Target unchanged: 30 FPS floor, 60 on stronger devices.

---

## 8. Files to create

| File | Purpose |
|---|---|
| `Editor/TerrainDressing.cs` | Background hills, berms, ground patches, path organics |
| `Editor/PropLibraryExtra.cs` | The 10 new storytelling props |
| `Editor/ZoneDressing.cs` | Per-zone intentional prop placement |

## 9. Files to modify

| File | Change |
|---|---|
| `Editor/StylizedMeshLibrary.cs` | 6 new meshes |
| `Editor/ProtoPalette.cs` | Unchanged — the existing palette covered every new asset |
| `Editor/PropLibrary.cs` | 5 trees, 3 bushes, 3 rocks, 3 flower variants |
| `Editor/BuildingBuilder.cs` | Gambrel barn, L-plan farmhouse, elevated coop, richer market/silo |
| `Editor/FarmEnvironmentBuilder.cs` | Organic paths, richer field pads, better fence, pond |
| `Editor/FarmPrototypeBuilder.cs` | Orchestration, camera tune, lighting tune, **crop-generation diagnostics** |

**Untouched:** every runtime gameplay script. `PlayerController`, `PlayerInputProvider`, `MobileJoystick`, `VirtualButton`, `InteractionController`, `FarmPlot`, `FarmGrid`, `PlayerInventory`, `CropDefinition`, `HudController`, `FarmCameraController` (values are set by the builder, not by editing the class).

---

## 10. Verification limits

Compile status is verified for real, with Unity's own Roslyn against Unity's reference assemblies. **Visual results cannot be verified from here** — no Play mode, no screenshots. The report will label every visual claim as unverified and hand over a specific checklist.
