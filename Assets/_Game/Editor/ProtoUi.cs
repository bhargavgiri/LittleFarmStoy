using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// uGUI construction helpers for the HUD.
    ///
    /// The style is: cream rounded panels with a dark outline and a soft drop shadow, amber
    /// accents, and generous touch targets. Everything is built from two generated sprites,
    /// so there is no UI atlas to ship.
    ///
    /// Legacy <see cref="Text"/> is used on purpose: TextMeshPro Essential Resources are not
    /// imported in this project, and importing them is a manual Editor step.
    /// </summary>
    public static class ProtoUi
    {
        public static readonly Color Ink = ProtoPalette.Hex("4A3A2A");
        public static readonly Color InkLight = ProtoPalette.Hex("FBF6EA");
        public static readonly Color Panel = ProtoPalette.Hex("FDF6E6");
        public static readonly Color PanelDim = ProtoPalette.Hex("EFE2C8");
        public static readonly Color Accent = ProtoPalette.Hex("F5A623");
        public static readonly Color AccentDeep = ProtoPalette.Hex("C97A0A");
        public static readonly Color Leaf = ProtoPalette.Hex("6FBF52");
        public static readonly Color Coin = ProtoPalette.Hex("F7C948");
        public static readonly Color Seed = ProtoPalette.Hex("A9713F");
        public static readonly Color Shadow = new Color(0.18f, 0.13f, 0.08f, 0.28f);

        public static RectTransform Rect(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Sprite(
            string objectName, Transform parent, Sprite sprite, Color color, bool raycastTarget = false)
        {
            RectTransform rt = Rect(objectName, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;

            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        /// <summary>
        /// Drops a soft shadow behind an already-positioned panel by cloning its rect into a
        /// sibling that sorts underneath. Call this after the panel has been sized.
        /// A sibling rather than a child, so it is never clipped by the panel's own layout.
        /// </summary>
        public static Image AddShadowBehind(Image panel, float offset = 7f, float grow = 4f)
        {
            if (panel == null)
            {
                return null;
            }

            Image shadow = Sprite(panel.name + "_Shadow", panel.transform.parent, panel.sprite, Shadow);
            shadow.type = panel.type;

            CopyRect(panel.rectTransform, shadow.rectTransform, new Vector2(0f, -offset));
            shadow.rectTransform.sizeDelta = panel.rectTransform.sizeDelta + new Vector2(grow, grow);

            // Sort underneath the panel it belongs to.
            shadow.rectTransform.SetSiblingIndex(panel.rectTransform.GetSiblingIndex());
            return shadow;
        }

        public static Text Label(
            string objectName, Transform parent, string content, int fontSize,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            RectTransform rt = Rect(objectName, parent);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = Ink;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            if (text.font == null)
            {
                Debug.LogWarning("Built-in font 'LegacyRuntime.ttf' was not found; HUD labels will be invisible until a font is assigned.");
            }

            return text;
        }

        /// <summary>Text with a cheap 1-pixel outline, so labels stay legible over bright grass.</summary>
        public static Text OutlinedLabel(
            string objectName, Transform parent, string content, int fontSize,
            TextAnchor anchor = TextAnchor.MiddleCenter, float outlineSize = 2f)
        {
            Text text = Label(objectName, parent, content, fontSize, anchor);
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.16f, 0.11f, 0.06f, 0.55f);
            outline.effectDistance = new Vector2(outlineSize, -outlineSize);
            return text;
        }

        /// <summary>Anchors a rect to a corner with a pixel size and margin, in reference resolution units.</summary>
        public static void AnchorCorner(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 margin)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;

            float x = Mathf.Approximately(anchor.x, 0f) ? margin.x : -margin.x;
            float y = Mathf.Approximately(anchor.y, 0f) ? margin.y : -margin.y;
            rt.anchoredPosition = new Vector2(x, y);
        }

        public static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Centre(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Copies one rect's anchoring and size onto another, with an offset.</summary>
        public static void CopyRect(RectTransform source, RectTransform target, Vector2 offset)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition + offset;
        }
    }
}
