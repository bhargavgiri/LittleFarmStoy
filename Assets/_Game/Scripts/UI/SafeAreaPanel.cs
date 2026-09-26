using UnityEngine;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// Insets a RectTransform to the device's safe area, so nothing lands under a notch, a
    /// punch-hole camera, a rounded corner or a gesture bar.
    ///
    /// It works in normalised anchors rather than pixel offsets, which means it is resolution
    /// independent and costs nothing once applied. It re-applies only when the safe area or the
    /// orientation actually changes, so there is no per-frame layout rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        [Tooltip("Apply the horizontal inset. Turn off for a full-bleed background layer.")]
        [SerializeField] private bool applyHorizontal = true;

        [Tooltip("Apply the vertical inset. This is the one that matters on a notched phone.")]
        [SerializeField] private bool applyVertical = true;

        [Tooltip("Extra padding inside the safe area, in reference-resolution pixels. Keeps the " +
                 "HUD off the very edge even on a device that reports no cut-out at all.")]
        [SerializeField] private Vector2 additionalPadding = new Vector2(24f, 24f);

        private RectTransform rectTransform;

        private Rect appliedSafeArea;
        private Vector2Int appliedResolution;
        private ScreenOrientation appliedOrientation;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            // Cheap guard: three comparisons, and the actual work runs only on a real change
            // (rotation, a foldable opening, the editor Game view being resized).
            if (Screen.safeArea == appliedSafeArea &&
                Screen.width == appliedResolution.x &&
                Screen.height == appliedResolution.y &&
                Screen.orientation == appliedOrientation)
            {
                return;
            }

            Apply();
        }

        private void Apply()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            Rect safeArea = Screen.safeArea;
            int width = Screen.width;
            int height = Screen.height;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            appliedSafeArea = safeArea;
            appliedResolution = new Vector2Int(width, height);
            appliedOrientation = Screen.orientation;

            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;

            min.x /= width;
            min.y /= height;
            max.x /= width;
            max.y /= height;

            if (!applyHorizontal)
            {
                min.x = 0f;
                max.x = 1f;
            }

            if (!applyVertical)
            {
                min.y = 0f;
                max.y = 1f;
            }

            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;

            // Offsets are in canvas units, which the CanvasScaler already keeps at the
            // reference resolution, so the padding reads the same size on every device.
            rectTransform.offsetMin = additionalPadding;
            rectTransform.offsetMax = -additionalPadding;
        }

#if UNITY_EDITOR
        public void EditorConfigure(bool horizontal, bool vertical, Vector2 padding)
        {
            applyHorizontal = horizontal;
            applyVertical = vertical;
            additionalPadding = padding;
        }
#endif
    }
}
