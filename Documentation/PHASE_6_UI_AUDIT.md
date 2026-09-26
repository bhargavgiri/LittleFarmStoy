# Phase 6 — UI Audit

Audited against the sources and the generated `Farm_Prototype.unity` on disk, not against previous reports.

---

## 1. What exists today

| Piece | File | Verdict |
|---|---|---|
| `HudController` | `Scripts/UI/HudController.cs` (274 lines) | Event-driven, no `Update` — **architecture is sound**, presentation is not |
| `MobileJoystick` | `Scripts/UI/MobileJoystick.cs` (160) | Implements `IMoveInputSource`, dynamic origin, dead zone, idle alpha — **working, keep the logic** |
| `VirtualButton` | `Scripts/UI/VirtualButton.cs` (104) | Implements `IActionInputSource`, press scale + alpha + overlay — **working, keep the logic** |
| `ActionFeedbackChannel` | `Scripts/Core/ActionFeedbackChannel.cs` | One `Post(string)` → one event. No queue. |
| UI construction | `FarmPrototypeBuilder.BuildHud` + `ProtoUi` | All code-generated; no UI prefabs exist at all |
| Sprites | `ProtoAssets.CircleSprite` / `RoundedBoxSprite` / `OutlinedBoxSprite` | Generated PNGs. Only 3 shapes, **zero icons** |
| Canvas | scene | `ScaleWithScreenSize`, reference **1080×1920**, match 0.5 — correct for portrait |
| Font | legacy `Text` + `LegacyRuntime.ttf` | **TextMeshPro Essential Resources are NOT imported** (`Assets/TextMesh Pro` does not exist) |

TMP itself *is* available — `Library/ScriptAssemblies/Unity.TextMeshPro.dll` is compiled, and `Package Resources/TMP Essential Resources.unitypackage` ships inside `com.unity.ugui`. Only the resource import is missing.

## 2. Concrete problems

1. **No safe area handling whatsoever.** Every element is anchored to the raw screen corners. On any notch, punch-hole or gesture bar the top pills and the menu button sit under system chrome.
2. **Legacy `Text` everywhere.** No font asset, no outline/shadow control, poor scaling, and no typography system — every label sets its own size inline.
3. **Right-hand pill stack.** Seven 372×108 pills stacked down the right edge occupy ~900 px of a 1920 px screen and read as a debug readout. They also grew ad-hoc: two in Phase 1, five after Phase 5.
4. **No icons.** Every pill uses a flat coloured disc as a stand-in. Nothing tells the player *what* a number counts except the caption text.
5. **The `USE` button is a plain orange circle** with the word USE. It never says what pressing it will do — the contextual label lives in a separate prompt strip elsewhere on screen, so the player reads two widgets to understand one action.
6. **Feedback has no queue.** `HudController.ShowMessage` overwrites the label and restarts one coroutine. Harvesting three plots quickly shows only the last message, and there is no animation in or out.
7. **No button state system.** Disabled, unavailable and normal all look identical. Only `VirtualButton` animates, and only via alpha + scale.
8. **No colour system.** Colours are scattered between `ProtoUi` (9 constants) and inline `ProtoPalette.Hex(...)` calls at call sites. Nothing is semantic — there is no "success", "warning", "disabled".
9. **No panel system.** `AddShadowBehind` is the only depth treatment, applied ad hoc.
10. **No inventory surface.** Resources can only ever be seen as permanent pills; there is nowhere for a resource that does not earn a permanent slot.
11. **No animation.** Nothing tweens except the joystick handle and the action button press.
12. **Only one aspect ratio considered.** 1080×1920 is assumed; 20:9 and 19.5:9 phones have never been checked.

## 3. What must not change

`MobileJoystick` and `VirtualButton` **input behaviour** — they are `IMoveInputSource` / `IActionInputSource` implementations registered into `PlayerInputProvider.sourceBehaviours`. Only their visuals get replaced.

`HudController`'s **data flow** is already correct and stays correct:

```
InteractionController.FocusChanged  →  prompt
InteractableBase.LabelChanged       →  prompt stays live as a plot ripens
PlayerInventory.Changed             →  counters
ActionFeedbackChannel.MessagePosted →  toast
```

No gameplay system is touched. The UI continues to consume state and never to own it.

## 4. Approach

Replace presentation, keep contracts:

- import TMP Essential Resources from the package, add `Unity.TextMeshPro` to both assembly definitions, and move every label to `TMP_Text` behind a typography system;
- add a `SafeAreaPanel` between the canvas and the HUD;
- introduce a semantic colour palette and a procedural icon set so numbers stop being naked;
- fold the prompt and the action button into **one** context control that says what it will do;
- give feedback a real queue with animation;
- keep the permanent readout to the few resources that earn it, and put the rest behind an inventory sheet.
