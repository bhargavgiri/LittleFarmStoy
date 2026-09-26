using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// One pinned readout: an icon and a quantity, in a rounded chip.
    ///
    /// It owns no state. <see cref="HudController"/> pushes a value in when
    /// <c>PlayerInventory.Changed</c> fires, and the chip punches so the eye is pulled to the
    /// number that actually moved. Nothing here polls, and nothing here reads gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public class ResourceChip : MonoBehaviour
    {
        [Header("Binding")]
        [Tooltip("Inventory id this chip displays. Authored from the crop and animal assets, " +
                 "never typed as a literal.")]
        [SerializeField] private string itemId;

        [Header("Widgets")]
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private Image icon;
        [Tooltip("Scaled on change. Usually the whole chip.")]
        [SerializeField] private RectTransform punchTarget;

        [Header("Feel")]
        [SerializeField] private float punchMagnitude = 0.14f;
        [SerializeField] private float punchDuration = 0.24f;
        [Tooltip("Dim the chip while the quantity is zero, so an empty resource reads as empty.")]
        [SerializeField] private CanvasGroup emptyFadeGroup;
        [Range(0.2f, 1f)] [SerializeField] private float emptyAlpha = 0.55f;

        private Coroutine punchRoutine;
        private Vector3 baseScale = Vector3.one;
        private int current = -1;

        public string ItemId => itemId;

        private void Awake()
        {
            if (punchTarget != null)
            {
                baseScale = punchTarget.localScale;
            }
        }

        /// <summary>
        /// Writes a quantity. Animates only on a real change, and never on the first write -
        /// the HUD should not punch five chips the moment the scene loads.
        /// </summary>
        public void SetValue(int value, bool animate = true)
        {
            bool first = current < 0;
            bool changed = current != value;
            current = value;

            if (valueLabel != null)
            {
                valueLabel.SetText("{0}", value);
            }

            if (emptyFadeGroup != null)
            {
                emptyFadeGroup.alpha = value > 0 ? 1f : emptyAlpha;
            }

            if (!animate || first || !changed || punchTarget == null || !isActiveAndEnabled)
            {
                return;
            }

            if (punchRoutine != null)
            {
                StopCoroutine(punchRoutine);
            }

            punchRoutine = StartCoroutine(PunchThenClear());
        }

        private IEnumerator PunchThenClear()
        {
            yield return UiTween.Punch(punchTarget, punchMagnitude, punchDuration, baseScale);
            punchRoutine = null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id, TMP_Text label, Image iconImage, RectTransform punch, CanvasGroup fadeGroup)
        {
            itemId = id;
            valueLabel = label;
            icon = iconImage;
            punchTarget = punch;
            emptyFadeGroup = fadeGroup;
        }
#endif
    }
}
