using System.Collections;
using LittleFarmStory.Interaction;
using LittleFarmStory.Player;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// The shop sheet: a modal card opened from the market, listing what can be bought and sold.
    ///
    /// Built and animated exactly like <see cref="InventoryPanel"/> - a card that slides up
    /// behind a scrim - because a second, differently-behaving modal would read as a different
    /// game. The rows inside it (<see cref="ShopBuyCard"/>, <see cref="ShopSellRow"/>) are the
    /// only part that is new; the container is the same recipe.
    ///
    /// While open, the farmer's movement and world interaction are genuinely disabled - their
    /// owning components, not merely their visuals. The scrim already blocks touches on the
    /// joystick and the action button underneath it, but a keyboard source or a touch outside
    /// the scrim's bounds does not go through uGUI raycasting at all, so the freeze has to
    /// happen at the gameplay components themselves to be real rather than cosmetic.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopPanel : MonoBehaviour
    {
        [Header("Widgets")]
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Full-screen dimmer behind the card. Tapping it closes the shop.")]
        [SerializeField] private Button scrim;
        [SerializeField] private Button closeButton;

        [Header("Feel")]
        [SerializeField] private float slideDistance = 460f;
        [SerializeField] private float openDuration = 0.22f;
        [SerializeField] private float closeDuration = 0.16f;

        [Header("World freeze")]
        [Tooltip("Disabled for as long as the shop is open, and re-enabled the moment it closes.")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private InteractionController interactionController;

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
            if (closeButton != null) { closeButton.onClick.AddListener(Close); }
            if (scrim != null) { scrim.onClick.AddListener(Close); }
        }

        private void OnDisable()
        {
            if (closeButton != null) { closeButton.onClick.RemoveListener(Close); }
            if (scrim != null) { scrim.onClick.RemoveListener(Close); }

            // Defensive: if this panel is ever disabled while open (the HUD being torn down,
            // for instance), the world must not stay frozen forever with nothing left to
            // un-freeze it.
            if (IsOpen)
            {
                IsOpen = false;
                SetWorldFrozen(false);
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            SetWorldFrozen(true);
            Play(true);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            SetWorldFrozen(false);
            Play(false);
        }

        private void SetWorldFrozen(bool frozen)
        {
            if (playerController != null)
            {
                playerController.enabled = !frozen;
            }
            else
            {
                Debug.LogError("ShopPanel '" + name + "' has no PlayerController; movement will " +
                               "not be blocked while the shop is open.", this);
            }

            if (interactionController != null)
            {
                interactionController.enabled = !frozen;
            }
            else
            {
                Debug.LogError("ShopPanel '" + name + "' has no InteractionController; world " +
                               "interaction will not be blocked while the shop is open.", this);
            }
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
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(
            RectTransform sheet, CanvasGroup canvasGroup, Button scrimButton, Button close,
            PlayerController player, InteractionController interaction)
        {
            card = sheet;
            group = canvasGroup;
            scrim = scrimButton;
            closeButton = close;
            playerController = player;
            interactionController = interaction;
        }
#endif
    }
}
