using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// The one contextual control: a round action button with a label pill above it that says
    /// what pressing it will actually do.
    ///
    /// The old HUD split this in two - a generic orange "USE" circle in one corner and a
    /// separate prompt strip somewhere else - so the player had to read two widgets to
    /// understand one action.
    ///
    /// The wording is never invented here. It is <c>IInteractable.InteractionLabel</c> verbatim,
    /// pushed in by <see cref="HudController"/>. The only thing this class decides is which
    /// glyph to draw beside it, chosen by matching the label against an authored keyword table.
    ///
    /// Input is still owned by <c>VirtualButton</c> on the same object; this component never
    /// touches the interaction system.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActionPrompt : MonoBehaviour
    {
        /// <summary>Maps a word in the interaction label to the glyph shown on the button.</summary>
        [Serializable]
        public struct IconRule
        {
            [Tooltip("Matched case-insensitively against the interaction label.")]
            public string Keyword;
            public Sprite Icon;
        }

        [Header("Widgets")]
        [Tooltip("Everything that shows and hides with the prompt.")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform scaleRoot;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image icon;
        [Tooltip("The label pill behind the text. Hidden when there is nothing to do.")]
        [SerializeField] private GameObject labelPill;

        [Header("Icons")]
        [SerializeField] private Sprite defaultIcon;
        [SerializeField] private IconRule[] iconRules;

        [Header("Feel")]
        [SerializeField] private float showDuration = 0.16f;
        [SerializeField] private float hideDuration = 0.12f;
        [SerializeField] private float appearFromScale = 0.86f;

        private Coroutine visibilityRoutine;
        private Vector3 baseScale = Vector3.one;
        private bool visible;

        private void Awake()
        {
            if (scaleRoot != null)
            {
                baseScale = scaleRoot.localScale;
            }

            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            visible = false;
        }

        /// <summary>Shows or hides the whole control. Idempotent.</summary>
        public void SetAvailable(bool available)
        {
            if (visible == available)
            {
                return;
            }

            visible = available;

            if (!isActiveAndEnabled)
            {
                ApplyImmediate(available);
                return;
            }

            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
            }

            visibilityRoutine = StartCoroutine(available ? Appear() : Disappear());
        }

        /// <summary>Writes the action wording. Pass the interactable's label verbatim.</summary>
        public void SetAction(string actionLabel)
        {
            if (label != null)
            {
                label.SetText(actionLabel ?? string.Empty);
            }

            if (labelPill != null)
            {
                labelPill.SetActive(!string.IsNullOrEmpty(actionLabel));
            }

            if (icon != null)
            {
                icon.sprite = ResolveIcon(actionLabel);
            }
        }

        private Sprite ResolveIcon(string actionLabel)
        {
            if (!string.IsNullOrEmpty(actionLabel) && iconRules != null)
            {
                for (int i = 0; i < iconRules.Length; i++)
                {
                    string keyword = iconRules[i].Keyword;

                    if (!string.IsNullOrEmpty(keyword) && iconRules[i].Icon != null &&
                        actionLabel.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return iconRules[i].Icon;
                    }
                }
            }

            return defaultIcon;
        }

        private IEnumerator Appear()
        {
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            if (scaleRoot != null)
            {
                StartCoroutine(UiTween.ScaleIn(scaleRoot, appearFromScale, showDuration, baseScale));
            }

            yield return UiTween.Fade(group, 1f, showDuration);
            visibilityRoutine = null;
        }

        private IEnumerator Disappear()
        {
            yield return UiTween.Fade(group, 0f, hideDuration);

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            visibilityRoutine = null;
        }

        private void ApplyImmediate(bool available)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = available ? 1f : 0f;
            group.blocksRaycasts = available;
            group.interactable = available;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CanvasGroup canvasGroup, RectTransform scale, TMP_Text text, Image glyph,
            GameObject pill, Sprite fallbackIcon, IconRule[] rules)
        {
            group = canvasGroup;
            scaleRoot = scale;
            label = text;
            icon = glyph;
            labelPill = pill;
            defaultIcon = fallbackIcon;
            iconRules = rules;
        }
#endif
    }
}
