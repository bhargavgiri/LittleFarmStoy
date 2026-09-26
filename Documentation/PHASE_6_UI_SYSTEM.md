# Phase 6 — UI System

The prototype HUD is replaced. Gameplay is untouched: the UI consumes state and never owns it.

---

## 1. Hierarchy

```
HUD_Canvas                       ScreenSpaceOverlay, 1080x1920, match HEIGHT
└── SafeArea                     SafeAreaPanel + 24px inner padding
    ├── TopHUD
    │   ├── CoinPill             coin icon + count, punches when it changes
    │   ├── LevelPill            star icon + "Lv 1" + XP fill bar
    │   ├── BagButton            opens Storage
    │   └── MenuButton           opens the menu shell
    ├── ResourceRail             pinned chips only (wheat seeds, wheat)
    ├── GameplayOverlay
    │   └── Toast                ToastPresenter, queued feedback
    ├── TouchControls
    │   ├── Joystick             MobileJoystick (input logic untouched)
    │   └── ActionControl        VirtualButton + ActionPrompt
    ├── InventorySheet           scrim + card, slides up
    └── MenuShell                placeholder card, hidden
```

One canvas. No nested canvases, no LayoutGroups anywhere — every rect is anchored explicitly, so nothing triggers a layout rebuild at runtime.

## 2. Typography

TextMeshPro, imported automatically by `TextMeshProSetup` from the uGUI package's own
`TMP Essential Resources.unitypackage`.

**Font assignment is explicit, not inherited.** A `TMP_Text` added from editor code does not reliably pick up TMP's default font — the component resolves one in its own `Awake`, which has not run yet, and resolves nothing at all if the resources were absent when the project last loaded. `TextMeshProSetup.ResolveDefaultFont()` looks the asset up deterministically (asset path → any project font asset → `TMP_Settings`) and `HudBuilder` assigns it, with its material, to every label it creates.

Two gates make a blank HUD impossible to ship by accident: `BuildPrototypeScene` aborts **before** replacing the current scene if no font asset can be resolved, and `VerifyBuild` fails the build if any label ends up without one.

| Style | Size | Use |
|---|---|---|
| Title | 60 | Menu shell heading |
| SectionTitle | 46 | Storage heading |
| Currency | 46 | Coin count |
| Interaction | 42 | The action wording |
| Counter | 40 | Resource quantities |
| Feedback | 40 | Toast text |
| Button | 40 | Button labels |
| Body | 36 | Item names, level |
| Small | 30 | Secondary text |

All bold: at these sizes on a bright, busy 3D background, regular weight loses. Nothing smaller than 30 (≈11 dp) exists.

## 3. Colour

`UiPalette` — semantic names only, one definition each. No widget names a hex value.

| Token | Role |
|---|---|
| `Primary` / `PrimaryDeep` | Ripe wheat. The action button, the coin. |
| `Secondary` / `SecondaryDeep` | Young leaf. Growth, confirmation, the XP fill. |
| `Accent` | Sky. Information that is neither action nor reward. |
| `Success` / `Warning` / `Error` / `Disabled` | State only. |
| `Panel` / `PanelSunken` / `PanelEdge` | Warm parchment surfaces and their rims. |
| `Shadow` / `Scrim` | Depth and modality. |
| `TextPrimary` / `TextSecondary` / `TextOnDark` | Deep soil brown, never black. |
| `JoystickBase` / `JoystickRim` / `JoystickHandle` | Translucent, so the farm shows through. |

Hues are pulled toward the world palette — straw, soil, leaf, terracotta, sky — so the interface reads as part of the farm. Saturation is deliberately off the ceiling: colour carries hierarchy and state, not decoration.

## 4. Component system

| Component | Responsibility |
|---|---|
| `SafeAreaPanel` | Insets to `Screen.safeArea` in normalised anchors. Re-applies only on a real change. |
| `UiTween` | `Punch`, `ScaleIn`, `Fade`, `SlideFade` + three easings. ~150 lines, no tween library. |
| `ResourceChip` | Icon + quantity. Punches on change, dims at zero. Knows its item id; owns no state. |
| `ToastPresenter` | Queued feedback with slide+fade. Tone inferred from the text. |
| `ActionPrompt` | The contextual action surface: wording + glyph + show/hide. |
| `InventoryPanel` | The storage sheet's open/close animation. Holds no quantities. |
| `ReadyMarker` | World-space badge over a ready plot or animal. Self-wires. |
| `HudController` | The single binding point between gameplay events and widgets. |

`Pill()` and `CircleButton()` in `HudBuilder` are the two surface recipes — rim, face, soft shadow — so every panel belongs to the same system by construction rather than by discipline.

## 5. The contextual action

The prototype split this in two: a generic orange **USE** circle in one corner and a prompt strip elsewhere, so the player read two widgets to understand one action. They are now one control — a round button carrying the glyph for what it does, with a pill above it spelling out the same thing.

**The UI never invents the action.** The wording is `IInteractable.InteractionLabel` verbatim, pushed in by `HudController` when `InteractionController.FocusChanged` fires, and kept live by `InteractableBase.LabelChanged` as a plot ripens or an egg finishes. The only decision `ActionPrompt` makes is which glyph to draw, from an authored keyword table:

```
Till → Till    Plant → Plant    Harvest → Harvest    Feed → Feed
Collect Egg → Egg              Collect Milk → Milk   Collect → Collect
```

## 6. Feedback

`ActionFeedbackChannel.MessagePosted` → `ToastPresenter`. A real queue, capped at 3 waiting; the **oldest** waiting message is dropped, because what just happened matters more than what happened three actions ago. Hold time shortens with backlog so a burst drains instead of stacking seconds of toasts. Tone (green / neutral / amber) is inferred from the text, which keeps the gameplay layer free of any notion of UI severity.

## 7. Icons

16 icons, painted from signed distance fields by `UiIconLibrary` into 128px PNG sprites: coin, level, seed, wheat, corn, egg, milk, till, plant, harvest, feed, collect, menu, bag, close, hand.

SDFs give exact antialiasing at any size, and describing every glyph from the same primitives with the same stroke weight and the same deep-shade-behind-light-shade treatment is what makes a set look designed rather than collected.

**These are honest placeholders for hand-authored artwork.** The architecture is the deliverable: each icon is a named sprite asset, so replacing one is dropping a PNG at the same path — no code change anywhere.

## 8. Safe area and responsiveness

`CanvasScaler` matches **height**, not the average. On a taller phone (20:9, 19.5:9) the HUD then keeps its physical size and gains vertical breathing room, instead of everything growing and crowding the world.

Touch targets, in reference pixels against 1080 wide (≈2.75 px per dp on a 1080p phone):

| Control | Size | ≈dp |
|---|---|---|
| Action button | 220 | 80 |
| Menu / Bag | 128 | 46 |
| Storage close | 88 | 32 visual, inside a larger rect |
| Joystick | 320 visual, +140 invisible hit area | 116 |

The joystick's hit area is deliberately larger than its graphic: the thumb should not have to find the control.

## 9. Performance

- **One canvas**, no nested canvases, no `LayoutGroup` anywhere — nothing rebuilds layout at runtime.
- **No `Update` in `HudController`.** Every refresh is driven by an event. A HUD with nothing happening costs nothing.
- `SafeAreaPanel.Update` is three comparisons; the work runs only on rotation or resize.
- `ReadyMarker.Update` returns immediately unless something is actually ready — the common case across a whole farm.
- Tweens are short coroutines that end; no update manager, no per-frame allocation.
- `TMP_Text.SetText("{0}", value)` is used for counters — it formats without allocating a string.
- 16 icon PNGs at 128px; no blur, no post-processing, no UI shaders beyond the default.

## 10. Screen composition

The farm is the subject; the interface is the frame. Controls sit in the two bottom thumb zones, information in the top band, and the middle two thirds — where the crops, the animals and the farmer are — stays empty apart from a transient toast. Only the two resources the player spends constantly are pinned; the rest are one tap away in Storage.

## 11. Known limitations

1. **Nothing here has been seen.** See §12 — no Play mode from this session. Every visual claim is a design intent, not an observation.
2. **Coins, level and XP are placeholders.** No economy or progression system exists yet. They are serialized display values bound through the same one-way path, so they will connect to the real systems without touching presentation.
3. **The menu shell is a card that says the menu arrives later.** Settings, audio and help are out of scope by instruction.
4. **The icons are procedural placeholders** — coherent and legible, but not hand-authored art.
5. **`ReadyMarker` uses `SpriteRenderer`,** which renders correctly in URP but does not depth-sort against the 3D world the way a proper billboard shader would. A badge can be occluded by a building it stands behind.
6. **Only 9:16 has been reasoned about in detail.** 20:9 and 19.5:9 follow from the match-height scaler, but they have not been looked at.

## 12. Verification

**Compilation:** Unity's own Roslyn (`6000.5.9f1`) against Unity's reference assemblies. **0 errors, 0 warnings** other than `CS0649` on `[SerializeField]` privates, which Unity suppresses project-wide.

**Runtime: not performed.** The Unity Editor is open and holds `Temp/UnityLockfile`, so a batch-mode instance cannot open the project and Play mode cannot be driven from here. No screenshots or visual results are claimed.

The build proves what it can on its own: `VerifyBuild` now fails the build if the HUD has no resource chips, no `ActionPrompt`, no `ToastPresenter`, no `SafeAreaPanel`, if TMP resources are missing, or if any HUD label ends up without a font asset.
