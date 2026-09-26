using System.Collections.Generic;
using LittleFarmStory.Economy;
using LittleFarmStory.Interaction;
using LittleFarmStory.Player;
using LittleFarmStory.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Builds the production HUD.
    ///
    /// Composition rule for the whole screen: the farm is the subject and the interface is the
    /// frame. Controls live in the two bottom thumb zones and information lives in the top
    /// band; the middle two thirds - where the crops, the animals and the farmer actually are -
    /// stays empty apart from a transient toast.
    ///
    /// Everything is generated, so the HUD is reproducible and reviewable as code rather than
    /// as a scene nobody can diff.
    /// </summary>
    public static class HudBuilder
    {
        /// <summary>One resource the HUD knows how to display.</summary>
        public struct ResourceEntry
        {
            public string ItemId;
            public string DisplayName;
            public string IconName;

            /// <summary>Pinned resources get a permanent chip; the rest live in the sheet only.</summary>
            public bool Pinned;
        }

        /// <summary>What the scene builder needs in order to connect the HUD to gameplay.</summary>
        public class Result
        {
            public HudController Hud;
            public MobileJoystick Joystick;
            public VirtualButton ActionButton;
            public ShopPanel ShopPanel;
        }

        // ---------------------------------------------------------------- layout constants
        // Authored against the 1080x1920 reference the CanvasScaler uses. Touch targets are
        // sized in reference pixels: on a 1080-wide phone 1 dp is roughly 2.75 px, so the
        // 128 px buttons below land at ~46 dp and the action button at ~80 dp.

        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 1920f;

        private const float SafePadding = 24f;
        private const float ChipHeight = 92f;
        private const float RoundButton = 128f;
        private const float ActionButtonSize = 220f;
        private const float JoystickSize = 320f;

        // Typography, one scale for the whole interface.
        private const float FontTitle = 60f;
        private const float FontSectionTitle = 46f;
        private const float FontCurrency = 46f;
        private const float FontCounter = 40f;
        private const float FontBody = 36f;
        private const float FontSmall = 30f;
        private const float FontButton = 40f;
        private const float FontInteraction = 42f;
        private const float FontFeedback = 40f;

        private static Sprite panelSprite;
        private static Sprite pillSprite;
        private static Sprite circleSprite;
        private static Sprite ringSprite;
        private static Dictionary<string, Sprite> icons;
        private static TMP_FontAsset font;

        // ================================================================ entry point

        public static Result Build(
            Transform parent, ResourceEntry[] resources,
            ShopDefinition shop, EconomyManager economy,
            PlayerController playerController, InteractionController interactionController)
        {
            TextMeshProSetup.EnsureImported();

            // Resolved once, assigned to every label explicitly. Nothing here relies on TMP
            // handing a component its default font later - that is precisely what produced a
            // HUD of invisible labels.
            font = TextMeshProSetup.ResolveDefaultFont();

            if (font == null)
            {
                Debug.LogError(
                    "Little Farm Story: refusing to build the HUD without a TMP font asset. " +
                    "Every label would render blank, which looks like a broken build rather " +
                    "than a missing import.");
                return null;
            }

            panelSprite = ProtoAssets.RoundedBoxSprite("UI_PanelRound", 96, 30);
            pillSprite = ProtoAssets.RoundedBoxSprite("UI_Pill", 96, 46);
            circleSprite = ProtoAssets.CircleSprite("UI_Circle");
            ringSprite = ProtoAssets.CircleSprite("UI_Ring", 160, 0.78f);
            icons = UiIconLibrary.BuildAll();

            GameObject canvasGo = new GameObject("HUD_Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.transform.SetParent(parent, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

            // Match height, not the average: on a taller phone (20:9) the HUD then keeps its
            // physical size and simply gains vertical breathing room, instead of everything
            // growing and crowding the world.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            RectTransform safeArea = ProtoUi.Rect("SafeArea", canvasGo.transform);
            ProtoUi.Stretch(safeArea, Vector2.zero, Vector2.one);
            SafeAreaPanel safe = safeArea.gameObject.AddComponent<SafeAreaPanel>();
            safe.EditorConfigure(true, true, new Vector2(SafePadding, SafePadding));

            Result result = new Result();

            TopBarWidgets top = BuildTopBar(safeArea);
            List<ResourceChip> chips = new List<ResourceChip>();

            BuildResourceRail(safeArea, resources, chips);

            RectTransform overlay = ProtoUi.Rect("GameplayOverlay", safeArea);
            ProtoUi.Stretch(overlay, Vector2.zero, Vector2.one);
            ToastPresenter toasts = BuildToast(overlay);

            RectTransform touch = ProtoUi.Rect("TouchControls", safeArea);
            ProtoUi.Stretch(touch, Vector2.zero, Vector2.one);

            result.Joystick = BuildJoystick(touch);
            ActionWidgets action = BuildActionControl(touch);
            result.ActionButton = action.Button;

            InventoryWidgets inventoryUi = BuildInventorySheet(safeArea, resources, chips, top.BagButton);
            GameObject menuPlaceholder = BuildMenuPlaceholder(safeArea);

            ShopWidgets shopUi = BuildShopPanel(
                safeArea, shop, economy, playerController, interactionController,
                out TMP_Text shopCoinLabel);
            result.ShopPanel = shopUi.Panel;

            // ---- assemble the controller last, once every widget exists
            HudController hud = canvasGo.AddComponent<HudController>();
            SerializedObject so = new SerializedObject(hud);

            SetRef(so, "coinLabel", top.CoinLabel);
            SetRef(so, "shopCoinLabel", shopCoinLabel);
            SetRef(so, "levelLabel", top.LevelLabel);
            SetRef(so, "xpFill", top.XpFill);
            SetRef(so, "coinPunchTarget", top.CoinPunchTarget);
            SetRef(so, "actionPrompt", action.Prompt);
            SetRef(so, "toasts", toasts);
            SetRef(so, "inventoryPanel", inventoryUi.Panel);
            SetRef(so, "menuButton", top.MenuButton);
            SetRef(so, "menuPlaceholder", menuPlaceholder);

            // The wallet already exists on the player by the time this runs (BuildPlayer runs
            // before HudBuilder.Build), so it is read straight off the EconomyManager rather
            // than threaded through as a separate parameter.
            SetRef(so, "wallet", economy != null ? economy.Wallet : null);

            SerializedProperty chipArray = so.FindProperty("chips");
            chipArray.arraySize = chips.Count;
            for (int i = 0; i < chips.Count; i++)
            {
                chipArray.GetArrayElementAtIndex(i).objectReferenceValue = chips[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            result.Hud = hud;

            Debug.Log("Little Farm Story: HUD built with " + chips.Count + " resource chips and " +
                      icons.Count + " icons.");

            return result;
        }

        // ================================================================ top bar

        private class TopBarWidgets
        {
            public TMP_Text CoinLabel;
            public TMP_Text LevelLabel;
            public Image XpFill;
            public RectTransform CoinPunchTarget;
            public Button MenuButton;
            public Button BagButton;
        }

        private static TopBarWidgets BuildTopBar(RectTransform safeArea)
        {
            TopBarWidgets widgets = new TopBarWidgets();

            RectTransform bar = ProtoUi.Rect("TopHUD", safeArea);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -230f);
            bar.offsetMax = new Vector2(0f, 0f);

            // ---- coins
            Image coinPill = Pill("CoinPill", bar, new Vector2(268f, ChipHeight),
                new Vector2(0f, 1f), new Vector2(0f, 0f));
            widgets.CoinPunchTarget = coinPill.rectTransform;

            Image coinIcon = Icon("Coin", coinPill.rectTransform, UiIconLibrary.Names.Coin, 62f);
            Place(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(50f, 0f));

            widgets.CoinLabel = Label("CoinValue", coinPill.rectTransform, "0",
                FontCurrency, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            StretchInside(widgets.CoinLabel.rectTransform, new Vector4(92f, 0f, 20f, 0f));

            // ---- level and XP
            Image levelPill = Pill("LevelPill", bar, new Vector2(268f, ChipHeight),
                new Vector2(0f, 1f), new Vector2(0f, -(ChipHeight + 14f)));

            Image levelIcon = Icon("Level", levelPill.rectTransform, UiIconLibrary.Names.Level, 56f);
            Place(levelIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 0f));

            widgets.LevelLabel = Label("LevelValue", levelPill.rectTransform, "Lv 1",
                FontBody, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            RectTransform levelRect = widgets.LevelLabel.rectTransform;
            levelRect.anchorMin = new Vector2(0f, 0.5f);
            levelRect.anchorMax = new Vector2(1f, 1f);
            levelRect.offsetMin = new Vector2(86f, -6f);
            levelRect.offsetMax = new Vector2(-18f, -8f);

            // XP track sits under the level text inside the same pill, so progression reads as
            // one idea rather than two widgets.
            Image xpTrack = ProtoUi.Sprite("XpTrack", levelPill.rectTransform, pillSprite, UiPalette.PanelSunken);
            RectTransform trackRect = xpTrack.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.offsetMin = new Vector2(86f, 18f);
            trackRect.offsetMax = new Vector2(-18f, 34f);

            Image xpFill = ProtoUi.Sprite("XpFill", xpTrack.rectTransform, pillSprite, UiPalette.Secondary);
            ProtoUi.Stretch(xpFill.rectTransform, Vector2.zero, Vector2.one);
            xpFill.type = Image.Type.Filled;
            xpFill.fillMethod = Image.FillMethod.Horizontal;
            xpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            xpFill.fillAmount = 0.35f;
            widgets.XpFill = xpFill;

            // ---- right-hand round buttons
            widgets.MenuButton = CircleButton("MenuButton", bar, UiIconLibrary.Names.Menu,
                new Vector2(1f, 1f), new Vector2(0f, 0f));
            widgets.BagButton = CircleButton("BagButton", bar, UiIconLibrary.Names.Bag,
                new Vector2(1f, 1f), new Vector2(-(RoundButton + 18f), 0f));

            return widgets;
        }

        // ================================================================ pinned resources

        private static void BuildResourceRail(
            RectTransform safeArea, ResourceEntry[] resources, List<ResourceChip> chips)
        {
            RectTransform rail = ProtoUi.Rect("ResourceRail", safeArea);
            rail.anchorMin = new Vector2(0f, 1f);
            rail.anchorMax = new Vector2(0f, 1f);
            rail.pivot = new Vector2(0f, 1f);
            rail.anchoredPosition = new Vector2(0f, -(230f + 8f));
            rail.sizeDelta = new Vector2(600f, 84f);

            float x = 0f;

            for (int i = 0; i < resources.Length; i++)
            {
                if (!resources[i].Pinned)
                {
                    continue;
                }

                ResourceChip chip = CompactChip(rail, resources[i], new Vector2(x, 0f));
                chips.Add(chip);
                x += 186f;
            }
        }

        /// <summary>A small icon-plus-number chip. Used for the two or three pinned resources.</summary>
        private static ResourceChip CompactChip(RectTransform parent, ResourceEntry entry, Vector2 position)
        {
            Image pill = Pill("Chip_" + entry.ItemId, parent, new Vector2(172f, 80f),
                new Vector2(0f, 1f), position);

            Image icon = Icon("Icon", pill.rectTransform, entry.IconName, 56f);
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f));

            TMP_Text value = Label("Value", pill.rectTransform, "0",
                FontCounter, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            StretchInside(value.rectTransform, new Vector4(80f, 0f, 16f, 0f));

            CanvasGroup fade = pill.gameObject.AddComponent<CanvasGroup>();

            ResourceChip chip = pill.gameObject.AddComponent<ResourceChip>();
            chip.EditorConfigure(entry.ItemId, value, icon, pill.rectTransform, fade);
            return chip;
        }

        // ================================================================ toast

        private static ToastPresenter BuildToast(RectTransform overlay)
        {
            Image card = ProtoUi.Sprite("Toast", overlay, pillSprite, UiPalette.Panel);
            RectTransform rect = card.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // Above the thumb zone, below the top band: the one part of the screen where a
            // transient message cannot hide anything the player is currently acting on.
            rect.anchoredPosition = new Vector2(0f, 620f);
            rect.sizeDelta = new Vector2(640f, 96f);

            ProtoUi.AddShadowBehind(card, 6f, 6f);

            Image dot = ProtoUi.Sprite("Tone", rect, circleSprite, UiPalette.Success);
            Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f));
            dot.rectTransform.sizeDelta = new Vector2(22f, 22f);

            TMP_Text text = Label("Message", rect, string.Empty,
                FontFeedback, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            StretchInside(text.rectTransform, new Vector4(72f, 0f, 28f, 0f));

            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            ToastPresenter presenter = overlay.gameObject.AddComponent<ToastPresenter>();
            presenter.EditorConfigure(rect, group, text, dot);
            return presenter;
        }

        // ================================================================ touch controls

        private static MobileJoystick BuildJoystick(RectTransform touch)
        {
            RectTransform root = ProtoUi.Rect("Joystick", touch);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(JoystickSize * 0.5f + 40f, JoystickSize * 0.5f + 40f);
            root.sizeDelta = new Vector2(JoystickSize, JoystickSize);

            // A generous invisible hit area around the visible well: the thumb should not have
            // to find the graphic, the graphic should come to the thumb.
            Image touchArea = ProtoUi.Sprite("TouchArea", root, null, new Color(0f, 0f, 0f, 0f), true);
            ProtoUi.Stretch(touchArea.rectTransform, Vector2.zero, Vector2.one);
            touchArea.rectTransform.offsetMin = new Vector2(-70f, -70f);
            touchArea.rectTransform.offsetMax = new Vector2(70f, 70f);

            RectTransform visualsRect = ProtoUi.Rect("Visuals", root);
            ProtoUi.Stretch(visualsRect, Vector2.zero, Vector2.one);
            CanvasGroup visuals = visualsRect.gameObject.AddComponent<CanvasGroup>();
            visuals.blocksRaycasts = false;
            visuals.interactable = false;

            Image well = ProtoUi.Sprite("Well", visualsRect, circleSprite, UiPalette.JoystickBase);
            ProtoUi.Stretch(well.rectTransform, Vector2.zero, Vector2.one);

            Image rim = ProtoUi.Sprite("Rim", visualsRect, ringSprite, UiPalette.JoystickRim);
            ProtoUi.Stretch(rim.rectTransform, Vector2.zero, Vector2.one);

            Image handle = ProtoUi.Sprite("Handle", visualsRect, circleSprite, UiPalette.JoystickHandle);
            ProtoUi.Centre(handle.rectTransform, new Vector2(128f, 128f));

            Image handleRim = ProtoUi.Sprite("HandleRim", handle.rectTransform, ringSprite,
                new Color(1f, 1f, 1f, 0.5f));
            ProtoUi.Stretch(handleRim.rectTransform, Vector2.zero, Vector2.one);

            MobileJoystick joystick = root.gameObject.AddComponent<MobileJoystick>();
            SerializedObject so = new SerializedObject(joystick);
            SetRef(so, "background", root);
            SetRef(so, "handle", handle.rectTransform);
            SetRef(so, "visuals", visuals);
            so.ApplyModifiedPropertiesWithoutUndo();

            return joystick;
        }

        private class ActionWidgets
        {
            public VirtualButton Button;
            public ActionPrompt Prompt;
        }

        /// <summary>
        /// The contextual action: one round button with the glyph for what it does, and a pill
        /// above it spelling out the same thing in words. The wording is the interactable's own
        /// label, pushed in at runtime - this only builds the surface for it.
        /// </summary>
        private static ActionWidgets BuildActionControl(RectTransform touch)
        {
            ActionWidgets widgets = new ActionWidgets();

            RectTransform root = ProtoUi.Rect("ActionControl", touch);
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(-(ActionButtonSize * 0.5f + 50f), ActionButtonSize * 0.5f + 70f);
            root.sizeDelta = new Vector2(ActionButtonSize, ActionButtonSize);

            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();

            RectTransform scaleRoot = ProtoUi.Rect("Scale", root);
            ProtoUi.Stretch(scaleRoot, Vector2.zero, Vector2.one);

            Image shadow = ProtoUi.Sprite("Shadow", scaleRoot, circleSprite, UiPalette.Shadow);
            ProtoUi.Stretch(shadow.rectTransform, Vector2.zero, Vector2.one);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -9f);
            shadow.rectTransform.sizeDelta = new Vector2(10f, 10f);

            Image rim = ProtoUi.Sprite("Rim", scaleRoot, circleSprite, UiPalette.PrimaryDeep, true);
            ProtoUi.Stretch(rim.rectTransform, Vector2.zero, Vector2.one);

            Image face = ProtoUi.Sprite("Face", scaleRoot, circleSprite, UiPalette.Primary);
            ProtoUi.Stretch(face.rectTransform, Vector2.zero, Vector2.one);
            face.rectTransform.offsetMin = new Vector2(9f, 9f);
            face.rectTransform.offsetMax = new Vector2(-9f, -13f);

            Image glyph = Icon("Glyph", scaleRoot, UiIconLibrary.Names.Hand, 116f);
            ProtoUi.Centre(glyph.rectTransform, new Vector2(116f, 116f));

            GameObject pressedOverlay = ProtoUi.Sprite("Pressed", scaleRoot, circleSprite,
                new Color(0f, 0f, 0f, 0.16f)).gameObject;
            ProtoUi.Stretch(((RectTransform)pressedOverlay.transform), Vector2.zero, Vector2.one);
            pressedOverlay.SetActive(false);

            // ---- wording pill, floating above the button
            Image labelPill = Pill("ActionLabel", root, new Vector2(340f, 84f),
                new Vector2(0.5f, 1f), new Vector2(0f, 74f));
            labelPill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            labelPill.rectTransform.anchoredPosition = new Vector2(-40f, ActionButtonSize * 0.5f + 58f);

            TMP_Text labelText = Label("Text", labelPill.rectTransform, "Interact",
                FontInteraction, UiPalette.TextPrimary, TextAlignmentOptions.Center);
            StretchInside(labelText.rectTransform, new Vector4(20f, 0f, 20f, 0f));

            VirtualButton button = root.gameObject.AddComponent<VirtualButton>();
            SerializedObject buttonSo = new SerializedObject(button);
            SetRef(buttonSo, "visuals", group);
            SetRef(buttonSo, "scaleTarget", scaleRoot);
            SetRef(buttonSo, "pressedOverlay", pressedOverlay);
            buttonSo.ApplyModifiedPropertiesWithoutUndo();

            ActionPrompt prompt = root.gameObject.AddComponent<ActionPrompt>();
            prompt.EditorConfigure(group, scaleRoot, labelText, glyph, labelPill.gameObject,
                icons[UiIconLibrary.Names.Hand], BuildIconRules());

            widgets.Button = button;
            widgets.Prompt = prompt;
            return widgets;
        }

        /// <summary>
        /// Maps a word in the interaction label to a glyph. First match wins, so the more
        /// specific verbs come first.
        /// </summary>
        private static ActionPrompt.IconRule[] BuildIconRules()
        {
            return new[]
            {
                Rule("Till", UiIconLibrary.Names.Till),
                Rule("Plant", UiIconLibrary.Names.Plant),
                Rule("Harvest", UiIconLibrary.Names.Harvest),
                Rule("Feed", UiIconLibrary.Names.Feed),
                Rule("Collect Egg", UiIconLibrary.Names.Egg),
                Rule("Collect Milk", UiIconLibrary.Names.Milk),
                Rule("Collect", UiIconLibrary.Names.Collect),
                Rule("Growing", UiIconLibrary.Names.Seed),
                Rule("Chicken", UiIconLibrary.Names.Egg),
                Rule("Cow", UiIconLibrary.Names.Milk),
                Rule("Shop", UiIconLibrary.Names.Bag)
            };
        }

        private static ActionPrompt.IconRule Rule(string keyword, string iconName)
        {
            return new ActionPrompt.IconRule { Keyword = keyword, Icon = icons[iconName] };
        }

        // ================================================================ inventory sheet

        private class InventoryWidgets
        {
            public InventoryPanel Panel;
        }

        private static InventoryWidgets BuildInventorySheet(
            RectTransform safeArea, ResourceEntry[] resources, List<ResourceChip> chips, Button openButton)
        {
            RectTransform root = ProtoUi.Rect("InventorySheet", safeArea);
            ProtoUi.Stretch(root, Vector2.zero, Vector2.one);

            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image scrimImage = ProtoUi.Sprite("Scrim", root, null, UiPalette.Scrim, true);
            ProtoUi.Stretch(scrimImage.rectTransform, Vector2.zero, Vector2.one);
            // Cover the full screen, not just the safe area, so no strip of world is tappable.
            scrimImage.rectTransform.offsetMin = new Vector2(-120f, -200f);
            scrimImage.rectTransform.offsetMax = new Vector2(120f, 200f);
            Button scrimButton = scrimImage.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            int rowCount = resources.Length;
            float rowHeight = 104f;
            float cardHeight = 150f + rowCount * rowHeight + 40f;

            Image card = ProtoUi.Sprite("Card", root, panelSprite, UiPalette.Panel);
            RectTransform cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0f);
            cardRect.anchorMax = new Vector2(0.5f, 0f);
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.sizeDelta = new Vector2(ReferenceWidth - 2f * SafePadding - 40f, cardHeight);
            cardRect.anchoredPosition = new Vector2(0f, 40f);

            ProtoUi.AddShadowBehind(card, 10f, 10f);

            TMP_Text title = Label("Title", cardRect, "Storage",
                FontSectionTitle, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(44f, -96f);
            titleRect.offsetMax = new Vector2(-140f, -28f);

            Button closeButton = CircleButton("Close", cardRect, UiIconLibrary.Names.Close,
                new Vector2(1f, 1f), new Vector2(-24f, -18f), 88f);

            for (int i = 0; i < rowCount; i++)
            {
                chips.Add(InventoryRow(cardRect, resources[i], i, rowHeight));
            }

            InventoryPanel panel = root.gameObject.AddComponent<InventoryPanel>();
            panel.EditorConfigure(cardRect, group, scrimButton, closeButton, openButton);

            return new InventoryWidgets { Panel = panel };
        }

        private static ResourceChip InventoryRow(
            RectTransform card, ResourceEntry entry, int index, float rowHeight)
        {
            Image row = ProtoUi.Sprite("Row_" + entry.ItemId, card, panelSprite, UiPalette.PanelSunken);
            RectTransform rect = row.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(28f, -(110f + (index + 1) * rowHeight) + 12f);
            rect.offsetMax = new Vector2(-28f, -(110f + index * rowHeight));

            Image icon = Icon("Icon", rect, entry.IconName, 68f);
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f, 0f));

            TMP_Text name = Label("Name", rect, entry.DisplayName,
                FontBody, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            StretchInside(name.rectTransform, new Vector4(112f, 0f, 160f, 0f));

            TMP_Text value = Label("Value", rect, "0",
                FontCounter, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            StretchInside(value.rectTransform, new Vector4(0f, 0f, 36f, 0f));

            CanvasGroup fade = row.gameObject.AddComponent<CanvasGroup>();

            ResourceChip chip = row.gameObject.AddComponent<ResourceChip>();
            chip.EditorConfigure(entry.ItemId, value, icon, rect, fade);
            return chip;
        }

        // ================================================================ shop panel

        private class ShopWidgets
        {
            public ShopPanel Panel;
        }

        /// <summary>
        /// The shop sheet, opened from the market. Same card-behind-a-scrim recipe as
        /// <see cref="BuildInventorySheet"/> - a second, differently-built modal would read as a
        /// different game - just centred rather than docked to the bottom, since its height
        /// varies with the size of the catalogue.
        ///
        /// Built directly from the shop's own catalogue: one <see cref="ShopBuyCard"/> per
        /// purchasable item, one <see cref="ShopSellRow"/> per sellable item. Nothing here
        /// names "wheat" - today that is one card and one row because that is what the
        /// catalogue currently contains, and it would be exactly as many cards and rows as the
        /// catalogue defines if a second crop were added to it.
        /// </summary>
        private static ShopWidgets BuildShopPanel(
            RectTransform safeArea, ShopDefinition shop, EconomyManager economy,
            PlayerController playerController, InteractionController interactionController,
            out TMP_Text coinLabel)
        {
            coinLabel = null;

            RectTransform root = ProtoUi.Rect("ShopPanel", safeArea);
            ProtoUi.Stretch(root, Vector2.zero, Vector2.one);

            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image scrimImage = ProtoUi.Sprite("Scrim", root, null, UiPalette.Scrim, true);
            ProtoUi.Stretch(scrimImage.rectTransform, Vector2.zero, Vector2.one);
            scrimImage.rectTransform.offsetMin = new Vector2(-120f, -200f);
            scrimImage.rectTransform.offsetMax = new Vector2(120f, 200f);
            Button scrimButton = scrimImage.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;

            List<ShopItemDefinition> purchasable = new List<ShopItemDefinition>();
            List<ShopItemDefinition> sellable = new List<ShopItemDefinition>();

            if (shop == null)
            {
                Debug.LogError("Little Farm Story: HudBuilder was given no ShopDefinition; the " +
                               "shop panel will open with nothing to buy or sell.");
            }
            else
            {
                for (int i = 0; i < shop.Items.Count; i++)
                {
                    ShopItemDefinition item = shop.Items[i];

                    if (item == null)
                    {
                        continue;
                    }

                    if (item.Purchasable) { purchasable.Add(item); }
                    if (item.Sellable) { sellable.Add(item); }
                }
            }

            const float headerHeight = 160f;
            const float sectionTitleHeight = 56f;
            const float buyRowHeight = 176f;
            const float sellRowHeight = 128f;
            const float footerPadding = 36f;

            float cardHeight = headerHeight
                + (purchasable.Count > 0 ? sectionTitleHeight + purchasable.Count * buyRowHeight : 0f)
                + (sellable.Count > 0 ? sectionTitleHeight + sellable.Count * sellRowHeight : 0f)
                + footerPadding;

            Image card = ProtoUi.Sprite("Card", root, panelSprite, UiPalette.Panel);
            RectTransform cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(ReferenceWidth - 2f * SafePadding - 40f, cardHeight);
            cardRect.anchoredPosition = Vector2.zero;

            ProtoUi.AddShadowBehind(card, 10f, 10f);

            // ---- header: title, coin balance, close
            TMP_Text title = Label("Title", cardRect, "Farmers Market",
                FontSectionTitle, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(44f, -96f);
            titleRect.offsetMax = new Vector2(-140f, -28f);

            Button closeButton = CircleButton("Close", cardRect, UiIconLibrary.Names.Close,
                new Vector2(1f, 1f), new Vector2(-24f, -18f), 88f);

            Image coinPill = Pill("CoinBalance", cardRect, new Vector2(224f, 68f),
                new Vector2(0f, 1f), new Vector2(44f, -(96f + 16f)));
            Image coinIcon = Icon("Coin", coinPill.rectTransform, UiIconLibrary.Names.Coin, 44f);
            Place(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(38f, 0f));
            coinLabel = Label("Value", coinPill.rectTransform, "0",
                FontCounter, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            StretchInside(coinLabel.rectTransform, new Vector4(70f, 0f, 16f, 0f));

            float topOffset = headerHeight;

            if (purchasable.Count > 0)
            {
                SectionLabel(cardRect, "Buy", topOffset, sectionTitleHeight);
                topOffset += sectionTitleHeight;

                for (int i = 0; i < purchasable.Count; i++)
                {
                    BuildBuyCard(cardRect, economy, purchasable[i], topOffset, buyRowHeight);
                    topOffset += buyRowHeight;
                }
            }

            if (sellable.Count > 0)
            {
                SectionLabel(cardRect, "Sell", topOffset, sectionTitleHeight);
                topOffset += sectionTitleHeight;

                for (int i = 0; i < sellable.Count; i++)
                {
                    BuildSellRow(cardRect, economy, sellable[i], topOffset, sellRowHeight);
                    topOffset += sellRowHeight;
                }
            }

            ShopPanel panel = root.gameObject.AddComponent<ShopPanel>();
            panel.EditorConfigure(cardRect, group, scrimButton, closeButton, playerController, interactionController);

            return new ShopWidgets { Panel = panel };
        }

        /// <summary>A small left-aligned heading inside the shop card ("Buy", "Sell").</summary>
        private static void SectionLabel(RectTransform card, string text, float topOffset, float height)
        {
            TMP_Text label = Label("Section_" + text, card, text,
                FontBody, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(44f, -(topOffset + height));
            rect.offsetMax = new Vector2(-44f, -(topOffset + height * 0.3f));
        }

        /// <summary>
        /// One purchasable row: icon and name up top, a quantity stepper, the running total and
        /// a Buy button along the bottom. Works for any purchasable ShopItemDefinition.
        /// </summary>
        private static void BuildBuyCard(
            RectTransform card, EconomyManager economy, ShopItemDefinition item,
            float topOffset, float rowHeight)
        {
            Image row = ProtoUi.Sprite("Buy_" + item.ItemId, card, panelSprite, UiPalette.PanelSunken);
            RectTransform rect = row.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(28f, -(topOffset + rowHeight) + 12f);
            rect.offsetMax = new Vector2(-28f, -topOffset);

            // ---- icon + name, top strip
            Image icon = ProtoUi.Sprite("Icon", rect, item.Icon, Color.white);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.sizeDelta = new Vector2(72f, 72f);
            iconRect.anchoredPosition = new Vector2(20f, -18f);

            TMP_Text name = Label("Name", rect, item.DisplayName,
                FontBody, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            RectTransform nameRect = name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.offsetMin = new Vector2(108f, -70f);
            nameRect.offsetMax = new Vector2(-20f, -18f);

            // ---- bottom strip: [-] qty [+]   total   [ Buy ]
            const float controlSize = 64f;
            const float bottomMargin = 20f;

            Button decrement = CircleTextButton("Minus", rect, "-",
                new Vector2(0f, 0f), new Vector2(20f, bottomMargin), controlSize);

            TMP_Text quantity = Label("Quantity", rect, "1",
                FontBody, UiPalette.TextPrimary, TextAlignmentOptions.Center);
            RectTransform quantityRect = quantity.rectTransform;
            quantityRect.anchorMin = new Vector2(0f, 0f);
            quantityRect.anchorMax = new Vector2(0f, 0f);
            quantityRect.pivot = new Vector2(0f, 0f);
            quantityRect.sizeDelta = new Vector2(64f, controlSize);
            quantityRect.anchoredPosition = new Vector2(20f + controlSize + 8f, bottomMargin);

            Button increment = CircleTextButton("Plus", rect, "+",
                new Vector2(0f, 0f), new Vector2(20f + (controlSize + 8f) * 2f, bottomMargin), controlSize);

            Button buy = ActionButtonSmall("Buy", rect, new Vector2(150f, controlSize),
                new Vector2(1f, 0f), new Vector2(-20f, bottomMargin), "Buy",
                UiPalette.Primary, UiPalette.PrimaryDeep);

            Image priceIcon = Icon("PriceIcon", rect, UiIconLibrary.Names.Coin, 32f);
            RectTransform priceIconRect = priceIcon.rectTransform;
            priceIconRect.anchorMin = new Vector2(1f, 0f);
            priceIconRect.anchorMax = new Vector2(1f, 0f);
            priceIconRect.pivot = new Vector2(1f, 0.5f);
            priceIconRect.anchoredPosition = new Vector2(-(20f + 150f + 14f), bottomMargin + controlSize * 0.5f);

            TMP_Text price = Label("Price", rect, "0",
                FontBody, UiPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            RectTransform priceRect = price.rectTransform;
            priceRect.anchorMin = new Vector2(1f, 0f);
            priceRect.anchorMax = new Vector2(1f, 0f);
            priceRect.pivot = new Vector2(1f, 0.5f);
            priceRect.sizeDelta = new Vector2(74f, controlSize);
            priceRect.anchoredPosition = new Vector2(-(20f + 150f + 40f), bottomMargin + controlSize * 0.5f);

            ShopBuyCard buyCard = row.gameObject.AddComponent<ShopBuyCard>();
            buyCard.EditorConfigure(economy, item, name, price, quantity, decrement, increment, buy);
        }

        /// <summary>
        /// One sellable row: icon, name and owned/price info on the left, three fixed-amount
        /// sell buttons on the right. Works for any sellable ShopItemDefinition.
        /// </summary>
        private static void BuildSellRow(
            RectTransform card, EconomyManager economy, ShopItemDefinition item,
            float topOffset, float rowHeight)
        {
            Image row = ProtoUi.Sprite("Sell_" + item.ItemId, card, panelSprite, UiPalette.PanelSunken);
            RectTransform rect = row.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(28f, -(topOffset + rowHeight) + 12f);
            rect.offsetMax = new Vector2(-28f, -topOffset);

            Image icon = ProtoUi.Sprite("Icon", rect, item.Icon, Color.white);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 0f));
            icon.rectTransform.sizeDelta = new Vector2(64f, 64f);

            const float infoRight = 260f;

            TMP_Text name = Label("Name", rect, item.DisplayName,
                FontBody, UiPalette.TextPrimary, TextAlignmentOptions.MidlineLeft);
            RectTransform nameRect = name.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.offsetMin = new Vector2(108f, -58f);
            nameRect.offsetMax = new Vector2(-infoRight, -14f);

            TMP_Text owned = Label("Owned", rect, "0",
                FontSmall, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft);
            RectTransform ownedRect = owned.rectTransform;
            ownedRect.anchorMin = new Vector2(0f, 0f);
            ownedRect.anchorMax = new Vector2(0.5f, 0f);
            ownedRect.pivot = new Vector2(0.5f, 0f);
            ownedRect.offsetMin = new Vector2(108f, 14f);
            ownedRect.offsetMax = new Vector2(0f, 58f);

            TMP_Text price = Label("Price", rect, "0",
                FontSmall, UiPalette.TextSecondary, TextAlignmentOptions.MidlineLeft);
            RectTransform priceRect = price.rectTransform;
            priceRect.anchorMin = new Vector2(0.5f, 0f);
            priceRect.anchorMax = new Vector2(1f, 0f);
            priceRect.pivot = new Vector2(0.5f, 0f);
            priceRect.offsetMin = new Vector2(0f, 14f);
            priceRect.offsetMax = new Vector2(-infoRight, 58f);

            const float buttonSize = 84f;
            const float gap = 10f;

            Button sellAll = ActionButtonSmall("SellAll", rect, new Vector2(buttonSize, 88f),
                new Vector2(1f, 0.5f), new Vector2(-20f, 0f), "All",
                UiPalette.Secondary, UiPalette.SecondaryDeep);

            Button sellFive = ActionButtonSmall("SellFive", rect, new Vector2(buttonSize, 88f),
                new Vector2(1f, 0.5f), new Vector2(-(20f + buttonSize + gap), 0f), "5",
                UiPalette.Secondary, UiPalette.SecondaryDeep);

            Button sellOne = ActionButtonSmall("SellOne", rect, new Vector2(buttonSize, 88f),
                new Vector2(1f, 0.5f), new Vector2(-(20f + (buttonSize + gap) * 2f), 0f), "1",
                UiPalette.Secondary, UiPalette.SecondaryDeep);

            ShopSellRow sellRow = row.gameObject.AddComponent<ShopSellRow>();
            sellRow.EditorConfigure(economy, item, name, owned, price, sellOne, sellFive, sellAll);
        }

        // ================================================================ menu shell

        private static GameObject BuildMenuPlaceholder(RectTransform safeArea)
        {
            Image card = ProtoUi.Sprite("MenuShell", safeArea, panelSprite, UiPalette.Panel);
            ProtoUi.Centre(card.rectTransform, new Vector2(680f, 300f));
            ProtoUi.AddShadowBehind(card, 10f, 10f);

            TMP_Text title = Label("Title", card.rectTransform, "Menu",
                FontTitle, UiPalette.TextPrimary, TextAlignmentOptions.Center);
            RectTransform titleRect = title.rectTransform;
            ProtoUi.Stretch(titleRect, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = new Vector2(-24f, -40f);

            TMP_Text body = Label("Body", card.rectTransform,
                "Settings, audio and help arrive in a later phase.",
                FontSmall, UiPalette.TextSecondary, TextAlignmentOptions.Center);
            RectTransform bodyRect = body.rectTransform;
            ProtoUi.Stretch(bodyRect, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
            bodyRect.offsetMin = new Vector2(40f, 40f);
            bodyRect.offsetMax = new Vector2(-40f, 0f);
            body.textWrappingMode = TextWrappingModes.Normal;

            card.gameObject.SetActive(false);
            return card.gameObject;
        }

        // ================================================================ widget helpers

        /// <summary>A rounded surface with a rim and a soft shadow. The one panel recipe.</summary>
        private static Image Pill(
            string objectName, Transform parent, Vector2 size, Vector2 anchor, Vector2 offset)
        {
            Image rim = ProtoUi.Sprite(objectName, parent, pillSprite, UiPalette.PanelEdge);
            RectTransform rect = rim.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(offset.x, offset.y);

            ProtoUi.AddShadowBehind(rim, 5f, 4f);

            Image face = ProtoUi.Sprite("Face", rect, pillSprite, UiPalette.Panel);
            ProtoUi.Stretch(face.rectTransform, Vector2.zero, Vector2.one);
            face.rectTransform.offsetMin = new Vector2(3f, 3f);
            face.rectTransform.offsetMax = new Vector2(-3f, -3f);

            return rim;
        }

        /// <summary>The rim+face+shadow shell shared by every round button. See <see cref="CircleButton"/>
        /// and <see cref="CircleTextButton"/>, which only differ in what they draw on top of it.</summary>
        private static (RectTransform Root, Image Rim) CircleButtonShell(
            string objectName, Transform parent, Vector2 anchor, Vector2 offset, float size)
        {
            RectTransform root = ProtoUi.Rect(objectName, parent);
            root.anchorMin = anchor;
            root.anchorMax = anchor;
            root.pivot = anchor;
            root.sizeDelta = new Vector2(size, size);
            root.anchoredPosition = offset;

            Image rim = ProtoUi.Sprite("Rim", root, circleSprite, UiPalette.PanelEdge, true);
            ProtoUi.Stretch(rim.rectTransform, Vector2.zero, Vector2.one);

            ProtoUi.AddShadowBehind(rim, 5f, 4f);

            Image face = ProtoUi.Sprite("Face", root, circleSprite, UiPalette.Panel);
            ProtoUi.Stretch(face.rectTransform, Vector2.zero, Vector2.one);
            face.rectTransform.offsetMin = new Vector2(4f, 4f);
            face.rectTransform.offsetMax = new Vector2(-4f, -6f);

            return (root, rim);
        }

        /// <summary>Applies the one button colour recipe used everywhere: white normal, a soft
        /// press-darken, and a muted grey when Button.interactable is false.</summary>
        private static Button FinishButton(RectTransform root, Image targetGraphic)
        {
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = targetGraphic;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.88f, 0.84f, 0.78f);
            colors.disabledColor = UiPalette.Disabled;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            return button;
        }

        private static Button CircleButton(
            string objectName, Transform parent, string iconName,
            Vector2 anchor, Vector2 offset, float size = RoundButton)
        {
            (RectTransform root, Image rim) = CircleButtonShell(objectName, parent, anchor, offset, size);

            Image glyph = Icon("Glyph", root, iconName, size * 0.5f);
            ProtoUi.Centre(glyph.rectTransform, new Vector2(size * 0.5f, size * 0.5f));

            return FinishButton(root, rim);
        }

        /// <summary>
        /// Same shell as <see cref="CircleButton"/>, with a text glyph instead of an icon -
        /// used for the quantity stepper, where "-" and "+" read instantly and painting two new
        /// SDF icons for them would be needless art.
        /// </summary>
        private static Button CircleTextButton(
            string objectName, Transform parent, string label,
            Vector2 anchor, Vector2 offset, float size)
        {
            (RectTransform root, Image rim) = CircleButtonShell(objectName, parent, anchor, offset, size);

            TMP_Text text = Label("Glyph", root, label, size * 0.5f,
                UiPalette.TextPrimary, TextAlignmentOptions.Center);
            ProtoUi.Stretch(text.rectTransform, Vector2.zero, Vector2.one);

            return FinishButton(root, rim);
        }

        /// <summary>
        /// A solid, filled action button: one coloured pill with a centred label. Unlike
        /// <see cref="CircleButton"/>'s rim-plus-separate-face, the coloured surface itself is
        /// the button's targetGraphic, so it visibly greys out when Button.interactable is
        /// false - the rim-tint trick would leave a vivid, seemingly-enabled face showing
        /// through a dimmed rim, which is wrong for a primary Buy/Sell action.
        /// </summary>
        private static Button ActionButtonSmall(
            string objectName, Transform parent, Vector2 size, Vector2 anchor, Vector2 offset,
            string label, Color color, Color pressedColor)
        {
            RectTransform root = ProtoUi.Rect(objectName, parent);
            root.anchorMin = anchor;
            root.anchorMax = anchor;
            root.pivot = anchor;
            root.sizeDelta = size;
            root.anchoredPosition = offset;

            Image face = ProtoUi.Sprite("Face", root, pillSprite, color, true);
            ProtoUi.Stretch(face.rectTransform, Vector2.zero, Vector2.one);

            TMP_Text text = Label("Label", root, label, FontButton,
                UiPalette.TextOnDark, TextAlignmentOptions.Center);
            ProtoUi.Stretch(text.rectTransform, Vector2.zero, Vector2.one);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color;
            colors.pressedColor = pressedColor;
            colors.disabledColor = UiPalette.Disabled;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            return button;
        }

        private static Image Icon(string objectName, Transform parent, string iconName, float size)
        {
            Sprite sprite = icons != null && icons.TryGetValue(iconName, out Sprite found)
                ? found
                : UiIconLibrary.Get(iconName);

            if (sprite == null)
            {
                Debug.LogError("Little Farm Story: UI icon '" + iconName + "' was not generated.");
            }

            Image image = ProtoUi.Sprite(objectName, parent, sprite, Color.white);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        private static TMP_Text Label(
            string objectName, Transform parent, string content, float size,
            Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = ProtoUi.Rect(objectName, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            // Explicit, every time. A TMP_Text created from editor code does not reliably pick
            // up a default font, and a TMP_Text without one draws absolutely nothing.
            if (font != null)
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            else
            {
                Debug.LogError("Little Farm Story: label '" + objectName +
                               "' was built with no TMP font asset and will render blank.");
            }

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontStyle = FontStyles.Bold;

            return text;
        }

        // ---------------------------------------------------------------- rect helpers

        /// <summary>Anchors a fixed-size rect against one edge, vertically centred by default.</summary>
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }

        /// <summary>Stretches a rect to its parent with per-edge padding (left, bottom, right, top).</summary>
        private static void StretchInside(RectTransform rect, Vector4 padding)
        {
            ProtoUi.Stretch(rect, Vector2.zero, Vector2.one);
            rect.offsetMin = new Vector2(padding.x, padding.y);
            rect.offsetMax = new Vector2(-padding.z, -padding.w);
        }

        private static void SetRef(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);

            if (p == null)
            {
                Debug.LogError("Little Farm Story: missing serialized property '" + property +
                               "' on " + so.targetObject.GetType().Name);
                return;
            }

            p.objectReferenceValue = value;
        }
    }
}
