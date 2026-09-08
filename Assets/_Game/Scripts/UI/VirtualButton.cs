using System;
using LittleFarmStory.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// Touch-friendly on-screen action button. Latches a press so the consumer can read it
    /// once per frame regardless of script execution order.
    /// </summary>
    [DisallowMultipleComponent]
    public class VirtualButton : MonoBehaviour, IActionInputSource,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private CanvasGroup visuals;
        [Range(0.2f, 1f)][SerializeField] private float pressedAlpha = 0.82f;

        [Header("Press feedback")]
        [Tooltip("Optional transform scaled down while held, for a physical button press.")]
        [SerializeField] private RectTransform scaleTarget;
        [Range(0.7f, 1f)][SerializeField] private float pressedScale = 0.92f;
        [Tooltip("Optional graphic shown only while the button is held.")]
        [SerializeField] private GameObject pressedOverlay;

        private bool pressedLatch;
        private Vector3 restScale = Vector3.one;
        private bool restScaleCaptured;

        /// <summary>Fired immediately on touch down, for UI feedback such as SFX.</summary>
        public event Action Pressed;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            CaptureRestScale();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            pressedLatch = true;
            ApplyPressedVisuals(true);
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
            ApplyPressedVisuals(false);
        }

        private void CaptureRestScale()
        {
            if (restScaleCaptured || scaleTarget == null)
            {
                return;
            }

            restScale = scaleTarget.localScale;
            restScaleCaptured = true;
        }

        /// <summary>Purely cosmetic. The input contract above never depends on any of this.</summary>
        private void ApplyPressedVisuals(bool held)
        {
            CaptureRestScale();

            if (visuals != null)
            {
                visuals.alpha = held ? pressedAlpha : 1f;
            }

            if (scaleTarget != null)
            {
                scaleTarget.localScale = held ? restScale * pressedScale : restScale;
            }

            if (pressedOverlay != null)
            {
                pressedOverlay.SetActive(held);
            }
        }

        public bool ConsumeInteractPressed()
        {
            if (!pressedLatch)
            {
                return false;
            }

            pressedLatch = false;
            return true;
        }

        private void OnDisable()
        {
            IsHeld = false;
            pressedLatch = false;
            ApplyPressedVisuals(false);
        }
    }
}
