# Little Farm Story — Project Audit (Phase 0)

Audit date: 2026-09-08
Audited by: lead engineer pass over the existing Unity project, before any code was added.

---

## 1. Current project state (as found)

| Area | Finding |
|---|---|
| Unity version | **6000.5.9f1** (Unity 6.5), revision `b57deb96f08d` |
| Render pipeline | **URP 17.5.0** (`com.unity.render-pipelines.universal`) |
| Project template | Unity 6 **URP 3D Mobile** template, untouched |
| Scenes | `Assets/Scenes/SampleScene.unity` only — Main Camera, Directional Light, Global Volume. Nothing else. |
| Scripts | **No gameplay code.** Only `Assets/TutorialInfo/Scripts/Readme.cs` + `ReadmeEditor.cs` (template welcome screen) |
| Input system | **Input System package 1.20.0**, `activeInputHandler: 1` = *Input System Package (New) only*. Legacy `UnityEngine.Input` is disabled. |
| Input actions | `Assets/InputSystem_Actions.inputactions` (template default: Player/UI maps) registered as **project-wide actions** |
| TextMeshPro | Ships inside `com.unity.ugui 2.5.0`, but **TMP Essential Resources are NOT imported** (no `Assets/TextMesh Pro/` folder) |
| Assets | 14 non-meta files total. URP settings assets + tutorial readme. No art, audio, prefabs or materials. |
| Compile state | **Clean.** No `error CS` entries in `Logs/Editor.log`. |
| Broken references | None found. |
| Version control | Not a git repository. |

### URP configuration found

- Global default pipeline asset: **`PC_RPAsset`**
- Quality levels: `Mobile` (index 0) → `Mobile_RPAsset`, `PC` (index 1) → `PC_RPAsset`
- `m_PerPlatformDefaultQuality: Android → 0` — Android correctly resolves to the **Mobile** tier at runtime
- `m_CurrentQuality: 1` — the **Editor is previewing the PC tier**, which is *not* what an Android build gets
- `Mobile_RPAsset` highlights: render scale `0.8`, MSAA off, HDR **on**, 1 shadow cascade, shadow distance 50, soft shadows off, SRP Batcher on, depth/opaque textures off, adaptive performance on

### Android player settings found

| Setting | Value found |
|---|---|
| Orientation | `defaultScreenOrientation: 4` (**Auto Rotation**), all 4 orientations allowed |
| Scripting backend | IL2CPP |
| Architectures | ARM64 only |
| Min SDK | 26 |
| Target SDK | 0 (auto — highest installed) |
| Graphics APIs | Vulkan → GLES3 (manual, not auto) |
| Application id | **empty** (falls back to `com.DefaultCompany.LittleFarmStory`) |
| Company name | `DefaultCompany` |
| Colour space | Linear |

---

## 2. Detected problems

1. **Orientation is Auto Rotation, not Portrait.** The design brief is portrait-first; a portrait HUD built against an auto-rotating player will look wrong on device.
2. **Application id / company name are template defaults.** Cannot be published as-is.
3. **Editor previews the PC quality tier** while Android builds use the Mobile tier — easy source of "looks fine in Editor, runs badly on phone".
4. **No TextMeshPro Essential Resources.** Any TMP component added right now throws at runtime until a manual import is done. (Worked around — see §5.)
5. **Template default input actions** are a first-person/3rd-person sample (Jump/Crouch/Sprint/Attack). Not harmful, but not our game's input model.
6. **No project structure.** Everything sits at `Assets/` root level.
7. **Not under version control.** Strongly recommended before Phase 2.
8. **HDR is enabled in `Mobile_RPAsset`.** Minor: costs bandwidth on mid-range GPUs for a game with no bloom/tonemapping needs. Flagged, not changed.

---

## 3. Recommended architecture

```
Assets/_Game/
    Art/  Audio/  Materials/  Prefabs/  Scenes/  ScriptableObjects/  Data/  UI/  Settings/
    Editor/                      <- editor-only tooling (own assembly)
    Scripts/                     <- LittleFarmStory.Runtime assembly
        Core/        Player/     Camera/      Input/
        Interaction/ Farming/    World/       UI/
        Animals/     Production/ Economy/     Inventory/
        Save/        Vehicles/   NPC/
```

Principles applied:

- **Two assembly definitions** (`LittleFarmStory.Runtime`, `LittleFarmStory.Editor`) — fast incremental compiles, editor code cannot leak into builds.
- **No god-object GameManager.** `GameBootstrap` does exactly one job (frame rate / sleep / orientation) and nothing depends on it.
- **No singletons, no `FindObjectOfType` for core systems.** Everything is wired through serialized references set by the scene builder.
- **Input is an abstraction, not a joystick.** `IMoveInputSource` / `IActionInputSource` → `PlayerInputProvider` → `PlayerController`. The joystick, the keyboard fallback and the on-screen button are interchangeable sources; gameplay never references the UI.
- **Polling on demand, not per-frame caching**, so there is no script execution order dependency between input, movement and interaction.
- **Event-driven where it matters.** `InteractionController.FocusChanged` drives the HUD prompt — the HUD has no `Update()`.
- **Cheap scanning.** Interaction uses `OverlapSphereNonAlloc` on a dedicated `Interactable` layer, on a 0.12 s timer, with a pre-allocated buffer.
- **ScriptableObjects for data.** `CropDefinition` holds crop identity/growth/economy/colour so Phase 2 systems consume data instead of hard-coded values.
- **Grid foundation, not a farming system.** `FarmGrid` + `FarmPlot` + `GridCoord` + `PlotState` exist and spawn plots at runtime; growth logic is deliberately absent.
- **Camera is independent of the player.** `FarmCameraController` takes a serialized target and already exposes zoom, yaw, bounds and target-swap APIs for vehicles / building focus / area transitions.

---

## 4. What was changed

### Added (new files only — nothing existing was modified or deleted)

- `Assets/_Game/**` — the whole folder structure above
- 15 runtime C# scripts + 2 assembly definitions
- 5 editor tooling scripts (scene/asset generator, Android settings applier)
- Generated assets: `Assets/_Game/Materials/*.mat`, `Assets/_Game/Data/Crop_*.asset`, `Assets/_Game/UI/*.png`, `Assets/_Game/Prefabs/FarmPlot.prefab`, `Assets/_Game/Scenes/Farm_Prototype.unity`

### Changed via the setup tool (reversible from Project Settings)

- Player Settings → orientation forced to **Portrait**
- Player Settings → Android application id set to `com.littlefarmstory.game` **only if it was still the template default**
- Graphics APIs for Android pinned to Vulkan → GLES3
- A user layer named **`Interactable`** added to the Tag Manager (first free slot)
- Build Settings scene list: `Farm_Prototype` inserted at **index 0**

---

## 5. What was NOT changed (deliberately)

| Left alone | Why |
|---|---|
| `Assets/Scenes/SampleScene.unity` | Your file. Untouched, and still in the build list after `Farm_Prototype`. |
| `Assets/TutorialInfo/**` (Readme) | Your file. Safe to delete yourself when you want. |
| `Assets/Settings/*.asset` (URP assets) | Already sensible for mobile. The prototype uses them as-is. |
| `Assets/InputSystem_Actions.inputactions` | Still the project-wide actions asset; the UI input module uses it. Phase 1 movement deliberately does not depend on it. |
| `Mobile_RPAsset` HDR flag | Flagged as a future optimisation; changing render settings mid-audit would hide a real perf comparison. |
| Company name (`DefaultCompany`) | Yours to set — it appears in the app id and store listing. |
| TextMeshPro | Essential Resources import is a manual Editor step. Phase 1 HUD uses legacy uGUI `Text` so nothing is broken; migrate in the UI phase. |
| Scripting backend, min SDK, ARM64 | Already correct for a modern Android release. |

---

## 6. Mobile performance decisions baked into Phase 1

- **One realtime light** (the sun) with hard/soft shadows at 1 cascade. No point lights, no post-processing volume.
- **Camera**: HDR off, MSAA off, occlusion culling on, far plane 120 m.
- **Geometry**: primitives only, all environment objects marked `BatchingStatic` + `OccludeeStatic`; light probes and reflection probes disabled per-renderer; motion vectors forced off.
- **Shadows**: cast-shadow flag switched off on every small prop (paths, pads, fences, UI-scale details) — only large silhouettes cast.
- **Colliders**: the farm boundary is 4 invisible box colliders instead of ~70 fence-post colliders. Ground is a single box.
- **Materials**: ~30 shared URP/Lit materials with GPU instancing enabled; no textures at all in the prototype.
- **Runtime object count**: ~300 static scene objects + 47 plots spawned on Awake.
- **Frame rate**: `GameBootstrap` sets `vSyncCount = 0` and `targetFrameRate = 60`; the design target is a stable 30 FPS floor on mid-range hardware.

---

## 7. Known limitations of Phase 1

- Placeholder art only — primitives, flat colours, no textures or animation.
- No crop growth, harvesting, inventory, economy, animal AI, machines, NPCs, vehicles, quests or save system. These are explicitly out of scope for this phase.
- HUD uses legacy uGUI `Text`; migrate to TextMeshPro once Essential Resources are imported.
- No NavMesh baked yet (`com.unity.ai.navigation` is installed and ready for the NPC phase).
- Not under version control.
