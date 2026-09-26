using System;
using System.Collections;
using UnityEngine;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// The smallest tweening helper that covers everything this HUD needs: a scale punch, a
    /// scale-in, and a fade. No tween library, no allocation per frame, no update manager.
    ///
    /// Every tween is a coroutine on the component that owns the transform, so it dies with
    /// that object and can be cancelled by starting another. Durations are deliberately short -
    /// mobile UI that lingers feels slow, not premium.
    /// </summary>
    public static class UiTween
    {
        /// <summary>Ease-out-back: overshoots slightly then settles. The "satisfying" curve.</summary>
        public static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float p = t - 1f;
            return p * p * ((overshoot + 1f) * p + overshoot) + 1f;
        }

        /// <summary>Ease-out-cubic: fast start, soft landing. The default for anything moving.</summary>
        public static float EaseOutCubic(float t)
        {
            float p = 1f - t;
            return 1f - p * p * p;
        }

        public static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        /// <summary>
        /// Quick attention punch: scale up past the target and settle back. Used when a counter
        /// changes, so the player's eye is pulled to the number that moved.
        /// </summary>
        public static IEnumerator Punch(
            Transform target, float magnitude = 0.18f, float duration = 0.26f, Vector3 baseScale = default)
        {
            if (target == null)
            {
                yield break;
            }

            if (baseScale == default)
            {
                baseScale = Vector3.one;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // One half-sine: out and back, with no discontinuity at either end.
                float pulse = Mathf.Sin(t * Mathf.PI) * magnitude;
                target.localScale = baseScale * (1f + pulse);

                yield return null;
            }

            target.localScale = baseScale;
        }

        /// <summary>Scale from a smaller size up to the target, easing out with a slight overshoot.</summary>
        public static IEnumerator ScaleIn(
            Transform target, float from = 0.9f, float duration = 0.18f, Vector3 baseScale = default)
        {
            if (target == null)
            {
                yield break;
            }

            if (baseScale == default)
            {
                baseScale = Vector3.one;
            }

            float elapsed = 0f;
            target.localScale = baseScale * from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localScale = baseScale * Mathf.LerpUnclamped(from, 1f, EaseOutBack(t));
                yield return null;
            }

            target.localScale = baseScale;
        }

        /// <summary>Fades a CanvasGroup, optionally driving its interactivity with it.</summary>
        public static IEnumerator Fade(
            CanvasGroup group, float to, float duration = 0.15f, bool setInteractable = false)
        {
            if (group == null)
            {
                yield break;
            }

            float from = group.alpha;
            float elapsed = 0f;

            if (setInteractable && to > 0.01f)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(t));
                yield return null;
            }

            group.alpha = to;

            if (setInteractable && to <= 0.01f)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        /// <summary>
        /// Slides an anchored position and fades at the same time. The toast animation.
        /// </summary>
        public static IEnumerator SlideFade(
            RectTransform target, CanvasGroup group,
            Vector2 fromOffset, Vector2 toOffset, float fromAlpha, float toAlpha,
            float duration, Func<float, float> ease = null)
        {
            if (target == null)
            {
                yield break;
            }

            ease ??= EaseOutCubic;
            float elapsed = 0f;

            target.anchoredPosition = fromOffset;

            if (group != null)
            {
                group.alpha = fromAlpha;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = ease(Mathf.Clamp01(elapsed / duration));

                target.anchoredPosition = Vector2.LerpUnclamped(fromOffset, toOffset, t);

                if (group != null)
                {
                    group.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, t);
                }

                yield return null;
            }

            target.anchoredPosition = toOffset;

            if (group != null)
            {
                group.alpha = toAlpha;
            }
        }
    }
}
