using LittleFarmStory.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// Screen-space virtual joystick. Lives entirely in the UI layer and only exposes
    /// <see cref="IMoveInputSource"/>, so gameplay code never references it directly.
    /// Placed on a large invisible touch zone; the stick snaps to wherever the thumb lands.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class MobileJoystick : MonoBehaviour, IMoveInputSource,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private CanvasGroup visuals;

        [Header("Tuning")]
        [Tooltip("Stick snaps to the first touch position inside the touch zone.")]
        [SerializeField] private bool dynamicOrigin = true;
        [Tooltip("Fraction of the background radius the handle may travel.")]
        [Range(0.2f, 1f)][SerializeField] private float handleRange = 0.75f;
        [Tooltip("Input below this magnitude is treated as zero.")]
        [Range(0f, 0.5f)][SerializeField] private float deadZone = 0.12f;
        [Tooltip("Fade the stick out while it is not being touched.")]
        [SerializeField] private float idleAlpha = 0.45f;

        private RectTransform touchZone;
        private Canvas parentCanvas;
        private Camera uiCamera;
        private Vector2 restPosition;
        private Vector2 rawInput;
        private bool isPressed;
        private int activePointerId = -1;

        public Vector2 MoveInput => rawInput;

        public bool HasMoveInput => isPressed && rawInput.sqrMagnitude > 0.0001f;

        private void Awake()
        {
            touchZone = (RectTransform)transform;
            parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = parentCanvas.worldCamera;
            }

            if (background != null)
            {
                restPosition = background.anchoredPosition;
            }

            ResetStick();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isPressed)
            {
                return;
            }

            isPressed = true;
            activePointerId = eventData.pointerId;

            if (dynamicOrigin && background != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    touchZone, eventData.position, uiCamera, out Vector2 localPoint))
            {
                background.anchoredPosition = localPoint;
            }

            if (visuals != null)
            {
                visuals.alpha = 1f;
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isPressed || eventData.pointerId != activePointerId || background == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, uiCamera, out Vector2 localPoint))
            {
                return;
            }

            Vector2 radius = background.rect.size * 0.5f;
            if (radius.x <= 0f || radius.y <= 0f)
            {
                return;
            }

            Vector2 normalised = new Vector2(localPoint.x / radius.x, localPoint.y / radius.y);
            if (normalised.sqrMagnitude > 1f)
            {
                normalised = normalised.normalized;
            }

            float magnitude = normalised.magnitude;
            rawInput = magnitude < deadZone
                ? Vector2.zero
                : normalised.normalized * Mathf.InverseLerp(deadZone, 1f, magnitude);

            if (handle != null)
            {
                handle.anchoredPosition = normalised * radius * handleRange;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            ResetStick();
        }

        private void OnDisable()
        {
            ResetStick();
        }

        private void ResetStick()
        {
            isPressed = false;
            activePointerId = -1;
            rawInput = Vector2.zero;

            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            if (dynamicOrigin && background != null)
            {
                background.anchoredPosition = restPosition;
            }

            if (visuals != null)
            {
                visuals.alpha = idleAlpha;
            }
        }
    }
}
