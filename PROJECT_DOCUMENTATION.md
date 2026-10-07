# Little Farm Story — Complete Project Documentation

**Generated:** 2026-09-26, from the files on disk, not from memory. Every number below (versions, counts, prices, timings) was read from the project. Anything built but not yet verified says so explicitly.

| | |
|---|---|
| **Game** | Little Farm Story. An original 3D casual farming simulator. |
| **Platform** | Android, portrait (9:16), mobile-first |
| **Engine** | Unity **6000.5.9f1** (Unity 6), Universal Render Pipeline **17.5.0** |
| **Language** | C# (.NET Standard 2.1 API level) |
| **Repository** | `github.com/bhargavgiri/LittleFarmStoy`, branch `main` |
| **Local folder** | `D:\UnityProjects\LittleFarmStory` |
| **Current phase** | Phase 8 (Save/Load): **code complete, not yet verified in Unity** |
| **Last verified phase** | Phase 7 (Economy): 23/23 EditMode tests, 20/20 Play Mode steps passed |

---

## Table of contents

1. [What the game is today](#1-what-the-game-is-today)
2. [Technology stack and tools](#2-technology-stack-and-tools)
3. [Platform and build settings](#3-platform-and-build-settings)
4. [Project structure](#4-project-structure)
5. [Architecture](#5-architecture)
6. [Systems in detail](#6-systems-in-detail)
7. [Game data and tuning values](#7-game-data-and-tuning-values)
8. [Art and content](#8-art-and-content)
9. [Editor tooling and the scene builder](#9-editor-tooling-and-the-scene-builder)
10. [Testing and verification](#10-testing-and-verification)
11. [Development history, phase by phase](#11-development-history-phase-by-phase)
12. [Code statistics](#12-code-statistics)
13. [Known issues and rough edges](#13-known-issues-and-rough-edges)
14. [Not implemented (out of scope so far)](#14-not-implemented-out-of-scope-so-far)
15. [Pending work and next steps](#15-pending-work-and-next-steps)
16. [How to run and use the project](#16-how-to-run-and-use-the-project)
17. [Documentation index](#17-documentation-index)

---

## 1. What the game is today

A farmer walks around one farm, seen from an elevated near-isometric camera, and interacts with the world through a single contextual **USE** button.

### What a player can do right now

| Activity | How it works |
|---|---|
| **Walk** | On-screen joystick (keyboard WASD/arrows in the Editor). The camera follows. |
| **Farm** | Walk to a plot: **Till → Plant** (uses 1 seed) **→ Grow** (3 stages) **→ Harvest** (1–3 wheat). |
| **Raise chickens** | Feed 1 wheat → an egg is ready after 25 s → collect it. |
| **Raise cows** | Feed 2 corn → 2 milk ready after 50 s → collect it. |
| **Trade** | Walk to the Market → "Shop" opens. Buy Wheat Seeds (2 coins each), sell Wheat (4 coins each). |
| **See progress** | HUD shows coins, seeds, wheat, corn, eggs, milk, all updated live. There is also a storage sheet listing everything held. |
| **Save** *(Phase 8, unverified)* | Progress autosaves on pause, on quit, and every 60 s, and reloads on the next start. |

### The world

- **3 fields, 47 plots:** Wheat (5×4 = 20), Tomato (4×3 = 12), Corn (5×3 = 15)
- **2 animal pens:** Coop (6 chickens in two colours, capacity 8), Barn (3 cows, capacity 4)
- **Market plaza:** the shop entrance
- **Other landmarks** (signposted, not yet interactive): Farmhouse/Home, Production area, field signs
- **Decoration:** paths, fences, pond, treeline, hills, props

---

## 2. Technology stack and tools

### 2.1 Engine and rendering

| Item | Value |
|---|---|
| Unity Editor | 6000.5.9f1 (revision b57deb96f08d) |
| Render pipeline | Universal Render Pipeline (URP) 17.5.0 |
| Colour space | Linear |
| Graphics APIs (Android) | Vulkan, then OpenGL ES 3 as fallback |
| Scripting backend (Android) | IL2CPP |
| API compatibility | .NET Standard 2.1 |
| Input handling | **New Input System only** (legacy `Input` disabled) |

### 2.2 Unity packages (from `Packages/manifest.json`)

**Actually used by the game's code:**

| Package | Version | Used for |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.5.0 | All rendering, lighting, shadows |
| `com.unity.inputsystem` | 1.20.0 | Keyboard input; on-screen UI input module |
| `com.unity.ugui` | 2.5.0 | The whole HUD (Canvas, Button, Image). **TextMeshPro ships inside this package** in Unity 6. |
| `com.unity.test-framework` | 1.7.0 | EditMode tests (NUnit) and the test-runner menus |
| `com.unity.modules.jsonserialize` | built-in | `JsonUtility`, used by the save system |
| `com.unity.modules.physics` | built-in | `CharacterController`, trigger overlap for interaction |

**Installed but not used by any game code** (template leftovers, verified by searching the code):

`com.unity.ai.navigation` (2.0.14), `com.unity.timeline` (1.8.12), `com.unity.visualscripting` (1.9.12), `com.unity.collab-proxy` (2.13.6), `com.unity.multiplayer.center` (1.0.1). The only NavMesh mention in the code is a comment explaining that animals deliberately do *not* use it.

IDE integrations installed: `com.unity.ide.rider` 3.0.38, `com.unity.ide.visualstudio` 2.0.26.

### 2.3 What is deliberately NOT used

| Not used | Instead |
|---|---|
| Legacy `UnityEngine.Input` | New Input System |
| NavMesh / NavMeshAgent | A simple MoveTowards walker inside a fenced rectangle (cheap on phones) |
| Animator / Mecanim | Code-driven limb animation (`FarmerLimbAnimator`, `AnimalIdleAnimator`) |
| Rigidbody physics for actors | `CharacterController` for the farmer; plain transforms for animals |
| A "GameManager" singleton | Each system owns its own state (see §5) |
| `FindObjectOfType` every frame | Serialized references wired by the scene builder |
| External art or Asset Store assets | Everything is procedurally generated (see §8) |
| Third-party packages (DOTween, Newtonsoft, etc.) | Small in-house helpers (`UiTween`) and built-in `JsonUtility` |

### 2.4 Development tools and workflow

| Tool | Role |
|---|---|
| **Unity Editor** | Scene generation (menu commands), Play Mode, running tests |
| **Git + GitHub** | Version control. 2 commits so far (see §11). |
| **Claude Code** (AI coding assistant) | Wrote and reviewed the code, audits and documentation |
| **Offline compile check** | Unity's own Roslyn compiler (`csc.dll` from the Unity install's .NET SDK 8.0.318), run against Unity's reference assemblies. It checks all three assemblies compile **without opening a second Unity**, which the project lock would block. |
| **Scripted test harnesses** | Menu commands that run tests and write results to Markdown files in `Documentation/` (see §10) |

---

## 3. Platform and build settings

These are applied by the menu command **Little Farm Story → Apply Android Player Settings** (`Editor/AndroidPlayerSetup.cs`):

| Setting | Value |
|---|---|
| Orientation | Portrait only (no upside-down, no landscape) |
| Minimum Android | API level 26 (Android 8.0) |
| Architecture | ARM64 |
| Scripting backend | IL2CPP |
| Graphics | Vulkan + OpenGL ES 3 |
| Colour space | Linear |
| Frame pacing | Optimized frame pacing on |
| VSync | Off |
| Application id | `com.littlefarmstory.game`. Set only if the current id is empty or contains "DefaultCompany". |

**Runtime settings** (applied by `GameBootstrap` at scene start):
- Target 60 fps with VSync off
- Screen never sleeps
- Portrait locked on mobile

**Render quality** (applied by **Apply URP Quality Settings**, `Editor/UrpQualitySetup.cs`):
- Shadow distance 78
- Soft shadows on (quality 1)
- Main-light shadow map 2048
- HDR off (nothing uses bloom or tonemapping, so it would only cost bandwidth)
- Render scale 0.9 on mobile

> **Note:** `ProjectSettings.asset` currently still shows the URP template's Android id `com.UnityTechnologies.com.unity.template.urpblank`, and company name `DefaultCompany`. That id doesn't contain "DefaultCompany", so the setup command would **not** replace it. Set a real id manually before publishing. See §13.

Version: `bundleVersion 0.1.0`, Android bundle version code `1`.

---

## 4. Project structure

```
LittleFarmStory/
├── Assets/
│   ├── _Game/                      ← everything that belongs to this game
│   │   ├── Art/Meshes/             310 procedurally generated meshes (.asset)
│   │   ├── Audio/                  (empty - no audio yet)
│   │   ├── Data/                   10 ScriptableObject data assets
│   │   ├── Editor/                 20 editor scripts: builders, setup, test harness
│   │   ├── Materials/              62 URP materials
│   │   ├── Prefabs/                53 prefabs (characters, crops, props, plot)
│   │   │   ├── Characters/         Farmer, Chicken_A, Chicken_B, Cow (+2 prop variants)
│   │   │   ├── Crops/              12 crop stage prefabs (3 crops × 4 stages)
│   │   │   ├── Props/              34 decoration props
│   │   │   └── FarmPlot.prefab
│   │   ├── Scenes/                 Farm_Prototype.unity  (GENERATED - do not hand-edit)
│   │   ├── ScriptableObjects/      (empty)
│   │   ├── Scripts/                56 runtime scripts in 13 folders
│   │   ├── Settings/               (empty)
│   │   ├── Tests/EditMode/         3 test files + test assembly
│   │   └── UI/Icons/               16 procedural icon PNGs
│   ├── Scenes/SampleScene.unity    (URP template leftover, unused)
│   └── TextMesh Pro/               TMP essential resources (fonts, shaders)
├── Documentation/                  phase docs + test result files
├── Packages/manifest.json
├── ProjectSettings/
├── *.md                            plans, audits, specs, status reports (root)
└── .gitignore                      excludes Library/, Temp/, Logs/, Build/, *.csproj, *.sln …
```

### Runtime scripts by folder (`Assets/_Game/Scripts/`)

| Folder | Files | What lives there |
|---|---|---|
| `Animals/` | 11 | Animal controller, needs, production, movement, habitat, interaction, animation, data |
| `Camera/` | 1 | Follow camera |
| `Core/` | 2 | Boot settings, feedback message channel |
| `Economy/` | 7 | Wallet, transaction manager, shop data, market entrance |
| `Farming/` | 8 | Field grid, plot state machine, crop data, snapshots |
| `Input/` | 4 | Input abstraction + keyboard source |
| `Interaction/` | 3 | Proximity focus + the interactable contract |
| `Inventory/` | 2 | Item store + item id conventions |
| `Persistence/` | 3 | **Phase 8:** save data, file I/O, save manager |
| `Player/` | 3 | Movement, limb animation, walk bob |
| `UI/` | 14 | HUD, joystick, buttons, toasts, panels, shop UI, tweens |
| `World/` | 1 | Generic landmark (placeholder) |

---

## 5. Architecture

### 5.1 Guiding principles

These were set in Phase 0/1 (`PROJECT_AUDIT.md`) and followed since:

1. **No god-object GameManager.** Each system owns its own state. A small `GameBootstrap` only applies platform settings.
2. **Data-driven content.** Crops, animals, shop items and settings are ScriptableObject assets. Runtime code never asks "is this wheat?". Adding content means authoring an asset, not writing code.
3. **One Update per container, not per object.** `FarmGrid` ticks only its *growing* plots. `AnimalHabitat` ticks its animals. A farm with nothing growing costs nothing per frame.
4. **Event-driven UI.** The HUD never polls. It subscribes to change events and redraws only when something changed.
5. **One-way data flow.** Gameplay → events → UI. The UI never writes gameplay state except through the gameplay system's own public API (e.g. the shop calls `EconomyManager.Purchase`).
6. **Atomic, guarded actions.** Every action validates first and mutates second. It returns a typed result instead of throwing or silently failing.
7. **Mobile first.** Non-allocating physics queries, timed (not per-frame) scans, no per-frame scene searches.
8. **The scene is a build product.** Generated by code and self-verified (§9), never hand-edited.

### 5.2 Assemblies

The code is split into three assembly definitions, so editor and test code can never ship in the game build:

| Assembly | Folder | Platforms | References |
|---|---|---|---|
| `LittleFarmStory.Runtime` | `Scripts/` | All | InputSystem, UnityEngine.UI, TextMeshPro |
| `LittleFarmStory.Editor` | `Editor/` | Editor only | Runtime, InputSystem, UI, TextMeshPro |
| `LittleFarmStory.Tests.EditMode` | `Tests/EditMode/` | Editor only, `UNITY_INCLUDE_TESTS` | Runtime, TestRunner, `nunit.framework.dll` |

Dependencies point one way: **Tests → Runtime ← Editor**. Runtime depends on neither.

### 5.3 Namespaces

`LittleFarmStory.Core`, `.Input`, `.Player`, `.CameraSystem`, `.Interaction`, `.Farming`, `.Animals`, `.Inventory`, `.Economy`, `.Persistence`, `.UI`, `.World`, plus `.EditorTools` (editor) and `.Tests`.

### 5.4 Scene hierarchy (generated)

```
Farm_Prototype
├── --- SYSTEMS ---
│   ├── GameBootstrap              frame rate, sleep, orientation   [runs first: order -1000]
│   ├── EventSystem                (Input System UI module)
│   └── SaveSystem                 SaveManager                      [Phase 8, runs late: order 1000]
├── --- WORLD ---
│   ├── Environment                terrain, paths, boundary, hills
│   ├── Fields                     Field_Wheat / Field_Tomato / Field_Corn  (each: FarmGrid)
│   ├── Areas                      Coop (AnimalHabitat + 6 chickens), Barn (AnimalHabitat + 3 cows),
│   │                              Market (MarketInteractable), Home, Production (FarmLandmark)
│   └── Decoration                 treeline, groundcover, plaza dressing, pond
├── --- ACTORS ---
│   ├── Player                     (components listed below)
│   └── Camera                     FarmCameraController
└── --- UI ---
    └── HUD Canvas                 SafeArea → top bar, resource chips, joystick, action button,
                                   toasts, storage sheet, shop panel
```

**Components on the Player object:**
- `CharacterController`
- `PlayerInputProvider`, `KeyboardMoveInputSource`
- `PlayerController`
- `InteractionController`
- `PlayerInventory`
- `CurrencyWallet`, `EconomyManager`
- `ActionFeedbackChannel`

The visual child carries `FarmerLimbAnimator` and `PlayerVisualBob`.

### 5.5 Recurring design patterns

| Pattern | Where | Why |
|---|---|---|
| **ScriptableObject definitions + generic runtime** | `CropDefinition`, `AnimalDefinition`, `ShopItemDefinition`, `ShopDefinition`, `FarmingSettings`, `EconomySettings` | Content is data; logic never branches on an id |
| **Container-driven ticking** | `FarmGrid` → `FarmPlot`, `AnimalHabitat` → `AnimalController` | One `Update` per container; idle objects cost nothing |
| **Explicit state machines** | `PlotState` (Empty → Tilled → Planted → Growing → ReadyToHarvest), `ProductionPhase` (Dormant → Producing → Ready) | Guards make double-harvest and double-collect impossible |
| **Result enums** | `FarmActionResult`, `AnimalActionResult`, `TransactionResult` + `TransactionOutcome` | Every refusal has a specific, loggable reason |
| **All-or-nothing mutations** | `PlayerInventory.Remove`, `CurrencyWallet.TrySpendCoins`, `EconomyManager.Purchase/Sell` | State can never go negative or half-apply |
| **Events for change** | `BalanceChanged`, `Inventory.Changed`, `StateChanged`, `FocusChanged`, `LabelChanged`, `MessagePosted` | HUD and save system follow along without polling |
| **Plain C# composition** | `AnimalNeeds`, `AnimalProduction`, `AnimalMovement` are plain classes owned by `AnimalController` | Fewer components per animal |
| **Snapshot / Restore** | `FarmPlotSnapshot`, `AnimalSnapshot` + `CaptureSnapshot()`/`RestoreSnapshot()` | Save system is a serializer, not a refactor |
| **Editor-only configuration** | `EditorConfigure(...)` methods inside `#if UNITY_EDITOR` | The scene builder wires things without exposing public setters at runtime |
| **Build-time self-verification** | `FarmPrototypeBuilder.VerifyBuild` | Wiring mistakes fail the build loudly; checks use reference equality |

### 5.6 Execution order (boot)

1. `GameBootstrap.Awake` — `[DefaultExecutionOrder(-1000)]`, first
2. All other `Awake`/`OnEnable`, in Unity's default order. Each system sets itself to its **new-game defaults**:
   - The wallet takes the starting coins.
   - The inventory takes its starting items.
   - Each `FarmGrid` builds its plots, all Empty.
   - Animals get randomised starting hunger and register with their habitat.
   - The HUD subscribes to events and pulls initial values.
3. All `Start`
4. `SaveManager.Start` — `[DefaultExecutionOrder(1000)]`, last. If a save file exists it **overwrites** those defaults. The restore paths raise the same events gameplay does, so the HUD updates by itself.

---

## 6. Systems in detail

### 6.1 Core
| Class | Responsibility |
|---|---|
| `GameBootstrap` | Frame rate 60, VSync off, no screen sleep, portrait lock. Nothing gameplay-related. |
| `ActionFeedbackChannel` | One-line relay: gameplay posts "Plot Tilled", and the HUD shows it as a toast. Lives on the Player. |

### 6.2 Input
| Class | Responsibility |
|---|---|
| `IMoveInputSource`, `IActionInputSource` | Interfaces, so movement and interaction don't care where input comes from |
| `PlayerInputProvider` | Merges input sources, exposes `Move` and `ConsumeInteractPressed()` |
| `KeyboardMoveInputSource` | Editor/desktop: **WASD / arrows** to move, **E / Space** to interact |
| `MobileJoystick` (UI) | On-screen joystick |
| `VirtualButton` (UI) | On-screen buttons (the USE button) |

### 6.3 Player
| Class | Responsibility |
|---|---|
| `PlayerController` | Camera-relative, acceleration-based movement (speed 5, accel 34, decel 42, turn 720°/s, gravity −22). Turns the **visual child**, not the root. `Teleport()`, plus Phase 8 `VisualYaw`/`RestoreTransform()`. |
| `FarmerLimbAnimator` | Code-driven arm/leg/head swing, scaled by walk speed |
| `PlayerVisualBob` | Subtle up/down walk bob |

### 6.4 Camera
`FarmCameraController`: elevated, angled follow camera tuned for portrait.
- Pitch 46°, distance 28 (range 14–44), follow smoothing 0.2 s
- Focus clamped to the farm bounds
- Also offers `SetTarget`, `SetZoom`, `SetYaw`, `SnapToTarget`

### 6.5 Interaction
| Class | Responsibility |
|---|---|
| `IInteractable` | Contract: label, priority, `CanInteract`, `Interact` |
| `InteractableBase` | Base class with a live label (`LabelChanged` event) and priority |
| `InteractionController` | Every 0.12 s, a non-allocating sphere overlap (radius 2.6 m) around the player. Picks the best target by **priority first, then distance**, raises `FocusChanged`, and routes USE to it. There is an optional line-of-sight check, **off** by default. |

**Priority bands:**

| Band | Priority |
|---|---|
| Decoration | 0 |
| Area | 5 |
| Production | 10 |
| Plot | 25 |
| Animal | 30 |

Animals rank highest so a chicken standing inside the coop's trigger still wins the button.

### 6.6 Farming
| Class | Responsibility |
|---|---|
| `CropDefinition` (SO) | Crop id, display name, seed/harvest item ids (derived), growth stages, seconds per stage, 4 stage prefabs, yield range, colours |
| `FarmingSettings` (SO) | Dev growth multiplier (**×4, Editor only**), growth tick interval 0.2 s |
| `FarmGrid` | One field. Spawns its plots at runtime from `FarmPlot.prefab`. Ticks only growing plots (accumulated time, not sampled). Stable `fieldId`. Phase 8: `CaptureSnapshots`/`RestoreSnapshots`. |
| `FarmPlot` | One tile's whole lifecycle. `TryTill`, `TryPlant` (consumes 1 seed), `TryHarvest` (grants yield), growth advance, soil tint, stage visuals, contextual label ("Till", "Plant Wheat", "Growing", "Harvest"). |
| `PlotState` | Empty, Tilled, Planted, Growing, ReadyToHarvest |
| `FarmActionResult` | Success, InvalidState, NoCrop, NoInventory, NotEnoughSeeds … |
| `GridCoord` | Integer X/Z cell coordinate |
| `FarmPlotSnapshot` | Serializable plot state for saving |

### 6.7 Animals
| Class | Responsibility |
|---|---|
| `AnimalDefinition` (SO) | Species data: feed item and amount, hunger duration, product item and amount, production time, happiness rules, movement, capacity weight, `purchaseCost`, `produceSellValue` (both present, unused) |
| `AnimalController` | One animal's state and actions. `TryFeed` (all-or-nothing), `TryCollect` (only when Ready). Stable `instanceId`. Capture/restore. No `Update` of its own. |
| `AnimalNeeds` | Hunger 0–1 and happiness 0–100. Happiness decays only while hungry. |
| `AnimalProduction` | Dormant → Producing → Ready → Dormant. One cycle per feeding. |
| `AnimalMovement` | Walks to a point with MoveTowards/RotateTowards. No NavMesh, no physics. |
| `AnimalHabitat` | The pen: registry, capacity, wander rectangle with exclusion zones, spacing from other animals and the player, and the **single Update** for all its animals. Dev speed **×5, Editor only**. |
| `AnimalInteraction` | Bridges the interaction system to feed/collect, with feedback messages |
| `AnimalIdleAnimator` | Code-driven walk cycle, head/tail motion, eating dip |
| `AnimalDebug` | Optional `[ANIMAL DEBUG]` console tracing |
| `AnimalSnapshot`, `AnimalEnums` | Save shape; AnimalType, ProductionPhase, AnimalActivity, AnimalMood, AnimalActionResult |

### 6.8 Inventory
| Class | Responsibility |
|---|---|
| `PlayerInventory` | Id-keyed store (`Dictionary<string,int>`). `Add`, all-or-nothing `Remove`, `GetQuantity`, `Has`, and `Changed(itemId, newQuantity)`. Quantities can never go negative. Phase 8: `RestoreAll`. **Exactly one instance** in the game, shared by farming, animals, economy and HUD (verified by scene-file inspection). |
| `ItemIds` | Naming convention: `seed_<crop>` for seeds, `<crop>` for produce, plus `egg`, `milk`. Helpers: `Seed()`, `Harvest()`, `IsSeed()`, `CropFromSeed()`, `Titlecase()`. |

### 6.9 Economy (Phase 7)
| Class | Responsibility |
|---|---|
| `CurrencyWallet` | Coins as a **typed currency**, separate from inventory. Starts at 100 and can never go negative. `AddCoins`, `TrySpendCoins` (all-or-nothing), `CanAfford`, `RestoreBalance`. Events: `BalanceChanged`, `BalanceDelta`. |
| `EconomyManager` | The only transaction authority. `Purchase`/`Sell` by item or id, plus `CanPurchase`/`CanSell` for the UI. **Atomic**: a purchase spends coins only after every check, then verifies the items arrived and refunds if not. A sale removes items first and credits coins only on success. **Duplicate guard**: an identical transaction within 0.35 s is refused, at the API level, not just by the UI. |
| `ShopItemDefinition` (SO) | Item id, name, icon, enabled, purchasable + buy price, sellable + sell price, min/max quantity, quantity step |
| `ShopDefinition` (SO) | The catalogue (id → item lookup) |
| `EconomySettings` (SO) | Starting coins 100, duplicate window 0.35 s, max stack 0 (unlimited), transaction logging |
| `TransactionResult` / `TransactionOutcome` | 21 result codes (Success, NotEnoughCoins, NotEnoughItems, InvalidQuantity, DuplicateTransaction, RolledBack …) |
| `MarketInteractable` | The Market's entrance. USE opens the shop. |

### 6.10 UI / HUD (Phase 6, extended in Phase 7)
| Class | Responsibility |
|---|---|
| `HudController` | **The only place gameplay and UI meet.** Subscribes to focus, labels, inventory, wallet and feedback events. It has no `Update` and owns no gameplay state. |
| `SafeAreaPanel` | Keeps UI inside the phone's safe area (notches, rounded corners) |
| `ResourceChip` | One resource counter (icon + number) |
| `ActionPrompt` | The contextual USE button, showing the focused object's own wording |
| `ToastPresenter` | Queue of short feedback messages |
| `InventoryPanel` | Slide-up storage sheet listing all items |
| `ShopPanel` | Slide-up shop modal. **Genuinely freezes the world while open**: it disables the real `PlayerController` and `InteractionController` components, and re-enables them on close. |
| `ShopBuyCard` | Buy row: name, price, −/+ quantity stepper, Buy |
| `ShopSellRow` | Sell row: owned count, price, Sell 1 / Sell 5 / Sell All, each enabled only when valid |
| `ReadyMarker` | World-space badge over plots and animals that are ready |
| `MobileJoystick`, `VirtualButton` | Touch controls |
| `UiTween` | Tiny coroutine tweens: slide, fade, punch |

Typography is **TextMeshPro** throughout, with a font asset resolved and verified at build time. Colours come from a semantic palette (`UiPalette`). Icons come from a procedural set of 16 (§8).

### 6.11 Persistence (Phase 8 — built, NOT yet verified)
| Class | Responsibility |
|---|---|
| `SaveData` | The save file's shape: version, timestamp, coins, inventory rows, player snapshot, fields (each with its plot snapshots), animal snapshots |
| `SaveSystem` | Static JSON file I/O via `JsonUtility`. **Atomic write:** it writes a `.tmp` file first, then replaces the real one, so a crash mid-write cannot corrupt the last good save. It rejects empty files and files from newer builds. |
| `SaveManager` | Captures and restores through each system's own API. Autosaves on **app pause** (the callback Android actually delivers), **quit**, and **every 60 s**. Loads automatically at start if a save exists. |

- **Save file:** `Application.persistentDataPath/littlefarmstory.save.json`
- **Format version:** 1

**Decisions you made for Phase 8:**
- Autosave on pause/quit + periodic
- Plain JsonUtility
- **No offline progression** (crops and animals don't advance while the app is closed)
- Scope limited to core save/load (level/XP stays a placeholder)

**What is saved:**
- Coins
- All inventory
- Farmer position and facing
- All 47 plots (state, crop, stage, growth time)
- All 9 animals (hunger, happiness, production phase and progress, position, facing)

### 6.12 World
`FarmLandmark` is a generic landmark (Home, Production, field signs). **Placeholder:** it logs and does nothing.

---

## 7. Game data and tuning values

All values are read from `Assets/_Game/Data/*.asset`.

### 7.1 Crops

| Crop | Growth | Real time | Yield | `seedCost` / `sellValue` on the crop asset | Can the player grow it? |
|---|---|---|---|---|---|
| Wheat | 3 stages × 6 s | 18 s (4.5 s in Editor) | 1–3 | 2 / 4 | **Yes** |
| Tomato | 3 stages × 9 s | 27 s | 1–2 | 9 / 22 | **No** — no tomato seeds are given or sold |
| Corn | 3 stages × 12 s | 36 s | 1–2 | 14 / 34 | **No** — no corn seeds are given or sold |

In the Editor, growth runs ×4 faster (`FarmingSettings`, Editor-only). Device builds run at authored speed.

### 7.2 Animals

| | Chicken | Cow |
|---|---|---|
| Eats | 1 wheat | 2 corn |
| Gets hungry after | 45 s | 75 s |
| Produces | 1 egg (+1 if happy) | 2 milk (+1 if happy) |
| Production time | 25 s | 50 s |
| Happy threshold | 70 | 70 |
| Move speed | 0.62 | 0.34 |
| `purchaseCost` (unused) | 120 | 450 |
| `produceSellValue` (unused) | 0 | 0 |
| In the farm | 6 (two colours) in the Coop (cap 8) | 3 in the Barn (cap 4) |

In the Editor, animal simulation runs ×5 faster (Editor-only).

### 7.3 Economy

| | |
|---|---|
| Starting coins | 100 |
| Wheat Seeds | buy **2** coins (1–99 per trade) |
| Wheat | sell **4** coins (1–99 per trade) |
| Duplicate-transaction window | 0.35 s |
| Max stack per item | unlimited |

The shop has only these two entries today.

### 7.4 Starting inventory (new game)

| Item | Amount | Why |
|---|---|---|
| `seed_wheat` | 10 | Start the farming loop |
| `wheat` | 6 | Chickens can be fed from a cold start |
| `corn` | 4 | Cows can be fed from a cold start (2 feeds) |
| `egg`, `milk` | 0 | Shown on the HUD from the start |

---

## 8. Art and content

**All art is procedural placeholder**, generated by editor code, with no external files and no Asset Store content. Final art is described in `ART_ASSET_SPEC_FARMER.md`, `ART_ASSET_SPEC_CHICKEN.md` and `ART_ASSET_SPEC_COW.md`. The code is structured so real art can be swapped in without logic changes.

| Content | Count | Generated by |
|---|---|---|
| Meshes (`Art/Meshes/*.asset`) | 310 | `StylizedMeshLibrary`: irregular discs, mounds, chamfered boxes, tapered shapes, cones, cylinders, fences, character parts, crop parts. Winding order is checked to be correct. |
| Materials | 62 | `ProtoPalette`: grass, soil, crops, roofs, walls, wood, stone, water, character colours |
| Prefabs | 53 | 4 characters + 2 prop variants, 12 crop stages, 34 props, 1 farm plot |
| UI icons | 16 | `UiIconLibrary` (SDF-painted PNGs): Bag, Close, Coin, Collect, Corn, Egg, Feed, Hand, Harvest, Level, Menu, Milk, Plant, Seed, Till, Wheat |
| Buildings | — | `BuildingBuilder`: farmhouse, coop, barn, market stalls |
| Characters | 3 rigs | `CharacterBuilder`: segmented farmer (7 renderers), chicken (4 renderers, 2 plumage variants), cow (7 renderers) |
| Audio | 0 | None yet |

---

## 9. Editor tooling and the scene builder

### 9.1 Menu commands (Unity menu bar → **Little Farm Story**)

| Menu item | Script | What it does |
|---|---|---|
| Run Full Setup (Player Settings + Scene) | `FarmPrototypeBuilder` | Android settings + full scene build |
| **Rebuild Farm Scene** | `FarmPrototypeBuilder` | Regenerates `Farm_Prototype.unity` from scratch and verifies it |
| Apply Android Player Settings | `AndroidPlayerSetup` | Settings listed in §3 |
| Apply URP Quality Settings | `UrpQualitySetup` | Shadow/HDR/render-scale settings listed in §3 |
| Import TextMeshPro Essentials | `TextMeshProSetup` | Imports TMP fonts/shaders from the uGUI package |
| Run Gameplay Loop Test (Play Mode) | `GameplayLoopTest` | Full farming + animal + HUD suite |
| Run Economy Tests | `TestRunnerMenus` | 23 EditMode economy tests |
| Run Phase 7 Runtime Verification | `GameplayLoopTest` | 20-step shop suite |
| Run Persistence Tests | `TestRunnerMenus` | 17 EditMode save tests (Phase 8) |
| Run Phase 8 Save Verification | `GameplayLoopTest` | 18-step save/load suite (Phase 8) |

### 9.2 Scene build pipeline (`FarmPrototypeBuilder.BuildPrototypeScene`)

1. Ask to save any open scene. Apply the URP quality settings. Make sure TextMeshPro is imported; **abort if there is no font** (so the HUD can never be blank).
2. **Create a new empty scene first.** Assets authored before this line were being silently unloaded into Unity's "fake null" state. That was a real bug found and fixed in Phase 5A.
3. Author the data assets: 3 crops, farming settings, crop stage visuals, 16 icons, props, characters, 2 animal definitions, economy settings, shop catalogue.
4. Build the plot prefab and lighting.
5. Create the scene roots: SYSTEMS, WORLD, ACTORS, UI.
6. Build the world: environment, 3 fields, coop and barn with animals, market, landmarks, decoration.
7. Build the player (all components, starting inventory, wallet, economy manager) and the camera.
8. Build the HUD, including the shop panel. Point the Market at the shop panel.
9. Wire everything. Build the **SaveManager** (Phase 8).
10. **`VerifyBuild`** — dozens of checks that fail loudly. Checks include:
    - Crop visuals exist and resolve.
    - HUD labels have fonts.
    - Habitats and animals are wired.
    - Shop prices are non-zero.
    - The economy, HUD and SaveManager all point at the **exact same** wallet and inventory as the Player (reference equality, not just "some wallet").
    - The save system knows every field and pen.
11. Mark scenery static (animals are left dynamic, because they move). Save the scene and register it in Build Settings.

### 9.3 Editor script inventory

| Script | Lines | Purpose |
|---|---|---|
| `FarmPrototypeBuilder` | 1,759 | Scene generation, wiring, verification |
| `GameplayLoopTest` | 2,235 | Play Mode test driver: 3 suites, survives the domain reload |
| `StylizedMeshLibrary` | 1,394 | Procedural meshes |
| `HudBuilder` | 1,143 | Builds the whole HUD and shop UI |
| `CharacterBuilder` | 709 | Farmer, chicken, cow rigs |
| `BuildingBuilder` | 567 | Buildings |
| `UiIconLibrary` | 547 | Procedural icons |
| `ProtoAssets` | 540 | Asset/folder/layer helpers |
| `FarmEnvironmentBuilder` | 521 | Fields, paths, landmarks, pond |
| `PropLibrary` / `PropLibraryExtra` | 461 / 298 | Props |
| `ZoneDressing` | 345 | Per-zone decoration |
| `CropVisualBuilder` | 330 | Crop stage prefabs |
| `TerrainDressing` | 210 | Background hills |
| `TextMeshProSetup` | 169 | TMP import and font resolution |
| `ProtoPalette` / `UiPalette` / `ProtoUi` | 163 / 79 / 155 | Colours and UI primitives |
| `UrpQualitySetup` | 132 | Render settings |
| `AndroidPlayerSetup` | 55 | Player settings |

---

## 10. Testing and verification

**Rule followed throughout:** a result is only reported as passing after reading it from its results file, never assumed.

### 10.1 EditMode tests (NUnit, no Play Mode needed)

| Suite | File | Tests | Last result |
|---|---|---|---|
| Economy | `EconomyTests.cs` | 23 | ✅ **23/23 passed** (`Documentation/ECONOMY_TEST_RESULTS.md`, 2026-09-13) |
| Persistence | `PersistenceTests.cs` | 17 | ⏳ **Not run yet** |

**Economy coverage:**
- A — starting coins are 100
- B — buying 5 seeds costs 10
- C — insufficient funds are rejected with no change
- D — selling 1 wheat pays 4
- E — overselling is rejected with no partial sale
- F — negative and zero quantities are rejected
- G — rapid and repeated calls each process exactly once and cannot corrupt state

**Persistence coverage:**
- JSON round-trips of every snapshot type, including enums and 64-bit ticks
- File write/read/replace/delete, and that no temp file is left behind
- A null write is refused
- Empty files and newer-version files are ignored
- `RestoreAll` replaces the whole store, reports items that dropped to 0, clamps negatives, and skips blank ids

The persistence tests **stash any real save file before running and restore it afterwards**, so running them can't wipe a player's farm.

### 10.2 Play Mode suites (drive the real game)

Each suite enters Play Mode and drives the **real** interaction chain: move the player, proximity scan, focus, `Interact()`, state change, inventory change, visual change. It writes a results file, then exits Play Mode.

| Suite | Steps | Last result |
|---|---|---|
| Gameplay Loop (full farming, chicken, cow, HUD, wandering) | 29 | Last run before Phase 7; its results file has since been overwritten by the Phase 7 suite (both write `RUNTIME_TEST_RESULTS.md`), so no current evidence on disk |
| Phase 7 Runtime Verification (shop) | 20 | ✅ **20/20 passed** (`Documentation/RUNTIME_TEST_RESULTS.md`, 2026-09-13) |
| Phase 8 Save Verification | 18 | ⏳ **Not run yet** → will write `Documentation/SAVE_TEST_RESULTS.md` |

**How the Phase 8 suite proves save/load really works:**
1. Build a distinctive state: plant a crop, add 23 coins (100 → 123), move the farmer to an unusual spot.
2. Save, and confirm the file exists and contains those values.
3. **Deliberately change every value to something different:** +500 coins, +77 seeds, clear the plot, move the farmer far away, change the chicken's hunger.
4. Load, and confirm every value came back to the *saved* one.

A test that saved and immediately loaded would pass even if loading did nothing. This one can't.

Every suite start **clears any existing save**, because the suites assert a brand-new farm.

### 10.3 Build verification
- `VerifyBuild` runs on every scene rebuild (§9.2).
- Offline compile check: **0 errors** on Runtime, Editor and Tests as of Phase 8. The only warnings are `CS0649` "field never assigned", which is expected for `[SerializeField]` fields that Unity fills in.

---

## 11. Development history, phase by phase

| Phase | Delivered | Committed |
|---|---|---|
| **0 / 1** | Project audit and architecture rules; basic movement, camera, a tillable plot | `d025a1d` |
| **2** | Real farming system: `FarmGrid`/`FarmPlot` state machine, `CropDefinition`, growth ticking | `d025a1d` |
| **3** | Visual foundation: procedural mesh library, material palette, mobile render settings | `d025a1d` |
| **4A** | Full environment: farmhouse, fields, coop, barn, market, paths, terrain dressing | `5666218` |
| **4B** | Segmented, animated character rigs: farmer, 2 chicken variants, cow | `5666218` |
| **5** | Animal gameplay: hunger, happiness, production, habitats, wandering, feed/collect | `5666218` |
| **5A** | Stabilisation: fixed the "fake null" builder-ordering bug; added `VerifyBuild` and the Play Mode harness | `5666218` |
| **6** | Full UI/HUD replacement: safe area, TextMeshPro, procedural icons, contextual action button, toasts, storage sheet | `5666218` |
| **7** | Economy: wallet, atomic transactions, shop at the Market, buy seeds / sell wheat; 23 + 20 tests passing | `5666218` |
| **8** | Save/Load: persistence audit (17 architecture questions), 4 key decisions taken, full implementation | **Not committed yet** |

**Git history:**
```
5666218  Add Phase 4-7: environment, characters, animals, UI and economy   (885 files)
d025a1d  Checkpoint: Phase 1-3 gameplay and visual foundation
```

**Uncommitted right now (Phase 8):**
- New: `Scripts/Persistence/`
- New: `Tests/EditMode/PersistenceTests.cs`
- New: `Tests/EditMode/TestRunnerMenus.cs`
- Modified: `FarmGrid`, `PlayerInventory`, `PlayerController`, `FarmPrototypeBuilder`, `GameplayLoopTest`
- Deleted: `EconomyTestRunnerMenu.cs` (replaced by `TestRunnerMenus.cs`)

---

## 12. Code statistics

| Area | Files | Lines |
|---|---|---|
| Runtime scripts | 56 | 8,805 |
| Editor scripts | 20 | 11,812 |
| Tests | 3 | 1,008 |
| **Total C#** | **79** | **21,625** |

**Largest runtime classes:**

| Class | Lines |
|---|---|
| `FarmPlot` | 521 |
| `EconomyManager` | 497 |
| `SaveManager` | 476 |
| `AnimalController` | 428 |
| `AnimalHabitat` | 416 |
| `HudController` | 336 |
| `FarmGrid` | 335 |

---

## 13. Known issues and rough edges

| # | Issue | Impact |
|---|---|---|
| 1 | **Tomato and Corn fields can't actually be farmed.** No tomato or corn seeds are in the starting inventory or the shop. Corn exists only as 4 units of cow feed. | 27 of 47 plots are unusable, and cows run out of feed after 2 feedings with no way to get more corn |
| 2 | **Level / XP bar is cosmetic.** `HudController` shows fixed placeholder values (Lv 1, 35%); nothing earns XP. | Misleading HUD element |
| 3 | **Egg and milk can't be sold.** `produceSellValue` is 0 by design, and `VerifyBuild` asserts it stays 0. | Animal produce accumulates with no use |
| 4 | **Crop `seedCost`/`sellValue` fields are unused by the shop**, which uses `ShopItemDefinition` prices instead. Wheat's are kept in sync by the builder. Tomato/corn values exist only on the crop assets. | Two places that look like prices |
| 5 | **Animal `purchaseCost` is unused.** There is no way to buy animals. | Dormant data |
| 6 | **`FarmLandmark` placeholders** (Home, Production, field signs) log and do nothing | Interactable-looking objects that don't respond |
| 7 | **Menu button** shows a placeholder panel only | No settings, audio or help screen |
| 8 | **Android application id** in `ProjectSettings.asset` is the URP template default (`com.UnityTechnologies.com.unity.template.urpblank`), company `DefaultCompany`. The setup command won't replace it because it doesn't contain "DefaultCompany". | Must be set manually before any store build |
| 9 | **Diagnostics flags are ON** in the builder: animal, interaction, economy and save logging | Console noise; switch off before shipping |
| 10 | **Unused template leftovers:** `Assets/Scenes/SampleScene.unity`, empty `Audio/`, `ScriptableObjects/`, `Settings/` folders, and 5 unused packages (§2.2) | Clutter / extra import time |
| 11 | **Save keys are fragile** (Phase 8). Animal ids are index-based (`chicken_0` … `cow_2`), and plot saves are keyed by grid coordinate. Changing animal counts, order or grid sizes in the builder can mismatch old saves. Mismatches are skipped with a warning, not crashed. | Only matters once saves exist on devices |
| 12 | **No offline progression** (your decision). `plantedUtcTicks` is saved but not used for catch-up. | Growth pauses while the app is closed |
| 13 | **Interaction line-of-sight check is off** by default, so the farmer can reach through walls | Minor |
| 14 | **No audio at all** | — |

---

## 14. Not implemented (out of scope so far)

Each of these was explicitly kept out of scope by phase instructions:
- Cloud save and multiple save slots
- Progression / XP system
- Production machines (processing raw goods into higher-value goods)
- Orders, quests, NPCs, NPC economy
- Vehicles
- Buying animals, expanding the farm, unlocking fields
- Selling egg/milk; tomato/corn in the shop
- Audio, music, settings screen, tutorial / onboarding
- Monetization (ads, IAP), analytics, backend, multiplayer
- Final hand-authored art

---

## 15. Pending work and next steps

### Immediate (to close Phase 8)
1. In Unity, click in order:
   1. **Rebuild Farm Scene**
   2. **Run Persistence Tests**
   3. **Run Economy Tests**
   4. **Run Phase 8 Save Verification**
2. Read the three results files. Fix anything that fails and re-run.
3. **Commit and push Phase 8.**

### Remaining Phase 8 audit questions (not yet decided)
- Save slots
- Schema migration strategy
- Enforcing id stability in `VerifyBuild`
- Whether a save taken while the shop is open should restore the shop

### Candidate next phases (your choice)
| Option | Effort | Notes |
|---|---|---|
| Fix issue #1: add corn/tomato seeds to the shop and inventory | Very small | Data only. It makes 27 idle plots and the cow loop actually work. |
| Egg/milk selling | Small | Set `produceSellValue` + add shop items + relax one `VerifyBuild` rule |
| Progression (XP from harvests/sales/animal care → levels) | Medium | The HUD display already exists |
| Buy animals / expand farm | Medium | `purchaseCost` and habitat capacity already exist |
| Audio + settings screen | Medium | Nothing exists yet |
| Offline progression | Medium | Timestamps are partly in place |

---

## 16. How to run and use the project

### Open
1. Unity Hub → open `D:\UnityProjects\LittleFarmStory` with **Unity 6000.5.9f1**.
2. Open `Assets/_Game/Scenes/Farm_Prototype.unity`.
3. Press **Play**.

### Controls
| Action | Editor | Phone |
|---|---|---|
| Move | WASD / arrow keys | On-screen joystick |
| Use / interact | E or Space | Action button |
| Storage | Bag button | Bag button |

### After changing builder code
Run **Little Farm Story → Rebuild Farm Scene**. Never hand-edit the generated scene; changes are lost on the next rebuild.

### Save file location
`Application.persistentDataPath/littlefarmstory.save.json`
- **Windows Editor:** typically `%USERPROFILE%\AppData\LocalLow\DefaultCompany\LittleFarmStory\`
- **Android:** the app's private data folder

To reset progress, delete this file. Running any Play Mode test suite also deletes it.

### Build for Android
1. Run **Apply Android Player Settings**.
2. Set a real application id in Player Settings (see issue #8).
3. File → Build Settings → Android → Build.

---

## 17. Documentation index

| File | Contents |
|---|---|
| `PROJECT_DOCUMENTATION.md` | **This file:** everything |
| `PROJECT_STATUS.md` | Phase 0–7 status report |
| `PROJECT_OVERVIEW_AND_PENDING.md` | Short Hindi/English summary of done vs pending |
| `PROJECT_AUDIT.md` | Phase 0/1 audit and architecture rules |
| `PHASE_2_PLAN.md` | Farming system plan |
| `PHASE_3_GRAPHICS_PLAN.md` | Visual foundation plan |
| `PHASE_4A_ENVIRONMENT_PLAN.md` | Environment plan |
| `PHASE_4B_CHARACTER_ANIMAL_PLAN.md` | Character and animal rig plan |
| `ART_ASSET_SPEC_FARMER.md` / `_CHICKEN.md` / `_COW.md` | Specs for future hand-made art |
| `Documentation/PHASE_5_ANIMAL_GAMEPLAY_PLAN.md`, `PHASE_5_ANIMAL_GAMEPLAY.md` | Animal system plan and write-up |
| `Documentation/PHASE_5A_STABILIZATION.md` | Runtime bug fixes, `VerifyBuild`, test harness |
| `Documentation/RUNTIME_INTEGRATION_FIX.md` | Runtime integration fix notes |
| `Documentation/PHASE_6_UI_AUDIT.md`, `PHASE_6_UI_SYSTEM.md` | UI audit and system design |
| `Documentation/ECONOMY_TEST_RESULTS.md` | Latest economy test results (23/23) |
| `Documentation/RUNTIME_TEST_RESULTS.md` | Latest Phase 7 Play Mode results (20/20) |
| `Documentation/PERSISTENCE_TEST_RESULTS.md` | *(created when Persistence Tests are run)* |
| `Documentation/SAVE_TEST_RESULTS.md` | *(created when Phase 8 Save Verification is run)* |
| Phase 8 persistence audit | Sent earlier as `PHASE_8_PERSISTENCE_AUDIT.md` (session scratch folder, not in the repo) |
