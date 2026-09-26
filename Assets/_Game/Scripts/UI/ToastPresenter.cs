using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// Shows the one-line messages coming out of <c>ActionFeedbackChannel</c>, one at a time,
    /// with a short slide-and-fade.
    ///
    /// The old HUD overwrote a label and restarted a coroutine, so harvesting three plots in
    /// quick succession showed only the last message. This keeps a small queue instead, and
    /// caps it: a burst of twenty events must not turn into twenty seconds of toasts.
    ///
    /// It is presentation only. It never inspects gameplay state - it renders the strings the
    /// gameplay layer chose to post.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToastPresenter : MonoBehaviour
    {
        [Header("Widgets")]
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional leading glyph, tinted per message tone.")]
        [SerializeField] private Image accent;

        [Header("Timing")]
        [SerializeField] private float riseDistance = 46f;
        [SerializeField] private float inDuration = 0.16f;
        [SerializeField] private float holdDuration = 1.25f;
        [SerializeField] private float outDuration = 0.22f;

        [Header("Queue")]
        [Tooltip("Messages waiting behind the current one. Older ones are dropped past this.")]
        [SerializeField] private int maxQueued = 3;
        [Tooltip("Hold time is shortened by this factor for each message still waiting, so a " +
                 "burst drains quickly instead of queueing up seconds of backlog.")]
        [Range(0.2f, 1f)] [SerializeField] private float backlogHoldFactor = 0.55f;

        [Header("Tone")]
        [SerializeField] private Color positiveColor = new Color(0.42f, 0.72f, 0.34f);
        [SerializeField] private Color neutralColor = new Color(0.29f, 0.23f, 0.17f);
        [SerializeField] private Color warningColor = new Color(0.85f, 0.53f, 0.16f);

        private readonly Queue<string> pending = new Queue<string>();
        private Coroutine routine;
        private Vector2 restPosition;

        private void Awake()
        {
            if (root != null)
            {
                restPosition = root.anchoredPosition;
            }

            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        /// <summary>Entry point. Safe to call from a gameplay event at any rate.</summary>
        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message) || label == null)
            {
                return;
            }

            if (routine == null)
            {
                routine = StartCoroutine(Run(message));
                return;
            }

            // Drop the oldest waiting message rather than the newest: what just happened is
            // more relevant to the player than what happened three actions ago.
            while (pending.Count >= Mathf.Max(1, maxQueued))
            {
                pending.Dequeue();
            }

            pending.Enqueue(message);
        }

        private IEnumerator Run(string message)
        {
            while (true)
            {
                label.SetText(message);

                if (accent != null)
                {
                    accent.color = ToneFor(message);
                }

                yield return UiTween.SlideFade(
                    root, group,
                    restPosition - new Vector2(0f, riseDistance), restPosition,
                    0f, 1f, inDuration, UiTween.EaseOutCubic);

                float hold = holdDuration * Mathf.Pow(backlogHoldFactor, pending.Count);
                yield return new WaitForSecondsRealtime(hold);

                yield return UiTween.SlideFade(
                    root, group,
                    restPosition, restPosition + new Vector2(0f, riseDistance * 0.5f),
                    1f, 0f, outDuration, UiTween.EaseInCubic);

                if (pending.Count == 0)
                {
                    break;
                }

                message = pending.Dequeue();
            }

            if (root != null)
            {
                root.anchoredPosition = restPosition;
            }

            routine = null;
        }

        /// <summary>
        /// Tone is inferred from the message text, which keeps the gameplay layer free of any
        /// notion of UI severity - it posts plain sentences and this decides how to colour them.
        /// </summary>
        private Color ToneFor(string message)
        {
            if (message.IndexOf('+') >= 0 || message.EndsWith("!"))
            {
                return positiveColor;
            }

            if (message.StartsWith("Need", System.StringComparison.OrdinalIgnoreCase))
            {
                return warningColor;
            }

            return neutralColor;
        }

#if UNITY_EDITOR
        public void EditorConfigure(RectTransform toastRoot, CanvasGroup canvasGroup, TMP_Text text, Image dot)
        {
            root = toastRoot;
            group = canvasGroup;
            label = text;
            accent = dot;
        }
#endif
    }
}
