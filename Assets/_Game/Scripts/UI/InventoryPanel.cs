using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// The resource sheet: a card that slides up from the bottom listing everything the player
    /// is carrying.
    ///
    /// This exists so the permanent HUD can stay small. Only the resources the player spends
    /// constantly earn a pinned chip; everything else lives here, one tap away.
    ///
    /// It is a container and an animation, nothing more. The rows are ordinary
    /// <see cref="ResourceChip"/> instances that <see cref="HudController"/> feeds from
    /// <c>PlayerInventory</c>, exactly like the pinned ones - this panel never reads inventory
    /// itself and holds no copy of the quantities.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryPanel : MonoBehaviour
    {
        [Header("Widgets")]
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Full-screen dimmer behind the card. Tapping it closes the sheet.")]
        [SerializeField] private Button scrim;
        [SerializeField] private Button closeButton;
        [Tooltip("Opens the sheet. Usually the bag button in the top bar.")]
        [SerializeField] private Button openButton;

        [Header("Feel")]
        [SerializeField] private float slideDistance = 420f;
        [SerializeField] private float openDuration = 0.22f;
        [SerializeField] private float closeDuration = 0.16f;

        private Coroutine routine;
        private Vector2 restPosition;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (card != null)
            {
                restPosition = card.anchoredPosition;
                card.anchoredPosition = restPosition - new Vector2(0f, slideDistance);
            }

            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            IsOpen = false;
        }

        private void OnEnable()
        {
            if (openButton != null) { openButton.onClick.AddListener(Open); }
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }
            if (scrim != null) { scrim.onClick.AddListener(Close); }
        }

        private void OnDisable()
        {
            if (openButton != null) { openButton.onClick.RemoveListener(Open); }
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); }
            if (scrim != null) { scrim.onClick.RemoveListener(Close); }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            Play(true);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Play(false);
        }

        private void Play(bool opening)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(opening ? OpenRoutine() : CloseRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            yield return UiTween.SlideFade(
                card, group,
                restPosition - new Vector2(0f, slideDistance), restPosition,
                0f, 1f, openDuration, UiTween.EaseOutCubic);

            routine = null;
        }

        private IEnumerator CloseRoutine()
        {
            yield return UiTween.SlideFade(
                card, group,
                restPosition, restPosition - new Vector2(0f, slideDistance * 0.7f),
                1f, 0f, closeDuration, UiTween.EaseInCubic);

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            if (card != null)
            {
                card.anchoredPosition = restPosition - new Vector2(0f, slideDistance);
            }

            routine = null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            RectTransform sheet, CanvasGroup canvasGroup, Button scrimButton,
            Button close, Button open)
        {
            card = sheet;
            group = canvasGroup;
            scrim = scrimButton;
            closeButton = close;
            openButton = open;
        }
#endif
    }
}
