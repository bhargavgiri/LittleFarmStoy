using System.Collections;
using LittleFarmStory.Core;
using LittleFarmStory.Economy;
using LittleFarmStory.Interaction;
using LittleFarmStory.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// Binds gameplay state to the HUD widgets. It is the only place the two meet.
    ///
    /// The data flow is one-way:
    ///
    ///   InteractionController.FocusChanged   -> contextual action control
    ///   InteractableBase.LabelChanged        -> keeps that wording live as a plot ripens
    ///   PlayerInventory.Changed              -> resource chips
    ///   CurrencyWallet.BalanceChanged        -> coin display (top bar AND the shop header)
    ///   ActionFeedbackChannel.MessagePosted  -> toast queue
    ///
    /// This component owns no gameplay state, mutates no gameplay system, and has no Update.
    /// Every refresh is driven by an event, so a HUD with nothing happening costs nothing.
    ///
    /// Coins are no longer a placeholder: <see cref="SetCoins"/> is written to from exactly one
    /// place, <see cref="OnBalanceChanged"/>, which only ever fires from the wallet's own event.
    /// Nothing else may push a number into the coin display. Level and XP remain placeholders -
    /// there is no progression system yet - and follow the same one-way path so they will bind
    /// to a real one without touching this class's shape.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private InteractionController interaction;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private ActionFeedbackChannel feedback;
        [SerializeField] private CurrencyWallet wallet;

        [Header("Top bar")]
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text levelLabel;
        [Tooltip("Filled portion of the XP bar. Width is driven by fillAmount, not by layout.")]
        [SerializeField] private Image xpFill;
        [SerializeField] private RectTransform coinPunchTarget;
        [Tooltip("Optional second coin readout inside the shop panel's header. Kept in sync " +
                 "with coinLabel from the same event, so there is still only one writer.")]
        [SerializeField] private TMP_Text shopCoinLabel;

        [Header("Resources")]
        [Tooltip("Every chip on screen: the pinned ones and the inventory sheet rows alike. " +
                 "Each knows its own item id, so adding a resource needs no code change here.")]
        [SerializeField] private ResourceChip[] chips;

        [Header("Contextual action")]
        [SerializeField] private ActionPrompt actionPrompt;

        [Header("Feedback")]
        [SerializeField] private ToastPresenter toasts;

        [Header("Panels")]
        [SerializeField] private InventoryPanel inventoryPanel;
        [SerializeField] private Button menuButton;
        [SerializeField] private GameObject menuPlaceholder;

        [Header("Placeholder progression values")]
        [Tooltip("No progression system exists yet. These are the starting display values and " +
                 "are replaced by a real read once one does.")]
        [SerializeField] private int level = 1;
        [Range(0f, 1f)] [SerializeField] private float xpProgress = 0.35f;

        private InteractableBase trackedInteractable;
        private Coroutine coinPunch;

        /// <summary>-1 until the first sync, so that sync never punches the coin display.</summary>
        private int currentCoins = -1;

        // ============================================================ lifecycle

        private void Awake()
        {
            SetLevel(level, xpProgress);

            if (actionPrompt != null)
            {
                actionPrompt.SetAvailable(false);
            }

            if (menuPlaceholder != null)
            {
                menuPlaceholder.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (interaction != null)
            {
                interaction.FocusChanged += ShowPrompt;
                ShowPrompt(interaction.Current);
            }

            if (inventory != null)
            {
                inventory.Changed += OnInventoryChanged;
                RefreshAllChips();
            }

            if (wallet != null)
            {
                wallet.BalanceChanged += OnBalanceChanged;
                SetCoins(wallet.GetBalance(), false);
            }
            else
            {
                Debug.LogError("HudController on '" + name + "' has no CurrencyWallet; the coin " +
                               "display will never update.", this);
            }

            if (feedback != null)
            {
                feedback.MessagePosted += ShowMessage;
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(OnMenuClicked);
            }
        }

        private void OnDisable()
        {
            if (interaction != null)
            {
                interaction.FocusChanged -= ShowPrompt;
            }

            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }

            if (wallet != null)
            {
                wallet.BalanceChanged -= OnBalanceChanged;
            }

            if (feedback != null)
            {
                feedback.MessagePosted -= ShowMessage;
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
            }

            UntrackInteractable();
        }

        // ============================================================ top bar

        /// <summary>
        /// The only place a coin number is written to the screen. Called from exactly one
        /// site - <see cref="OnBalanceChanged"/> - so nothing outside CurrencyWallet can ever
        /// cause the display to disagree with the actual balance.
        /// </summary>
        private void OnBalanceChanged(int newBalance)
        {
            SetCoins(newBalance);
        }

        private void SetCoins(int value, bool animate = true)
        {
            bool first = currentCoins < 0;
            bool changed = currentCoins != value;
            currentCoins = value;

            if (coinLabel != null)
            {
                coinLabel.SetText("{0}", value);
            }

            if (shopCoinLabel != null)
            {
                shopCoinLabel.SetText("{0}", value);
            }

            if (animate && changed && !first && coinPunchTarget != null && isActiveAndEnabled)
            {
                if (coinPunch != null)
                {
                    StopCoroutine(coinPunch);
                }

                coinPunch = StartCoroutine(PunchCoins());
            }
        }

        private IEnumerator PunchCoins()
        {
            yield return UiTween.Punch(coinPunchTarget, 0.16f, 0.26f);
            coinPunch = null;
        }

        public void SetLevel(int value, float progress01)
        {
            level = value;
            xpProgress = Mathf.Clamp01(progress01);

            if (levelLabel != null)
            {
                levelLabel.SetText("Lv {0}", value);
            }

            if (xpFill != null)
            {
                xpFill.fillAmount = xpProgress;
            }
        }

        // ============================================================ resources

        private void OnInventoryChanged(string itemId, int quantity)
        {
            if (chips == null)
            {
                return;
            }

            // Several chips can share an id - a pinned chip and its row in the inventory sheet.
            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] != null && chips[i].ItemId == itemId)
                {
                    chips[i].SetValue(quantity);
                }
            }
        }

        private void RefreshAllChips()
        {
            if (chips == null || inventory == null)
            {
                return;
            }

            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] != null)
                {
                    chips[i].SetValue(inventory.GetQuantity(chips[i].ItemId), false);
                }
            }
        }

        // ============================================================ contextual action

        private void ShowPrompt(IInteractable target)
        {
            UntrackInteractable();

            bool hasTarget = target != null;

            if (actionPrompt != null)
            {
                actionPrompt.SetAvailable(hasTarget);
            }

            if (!hasTarget)
            {
                return;
            }

            WritePrompt(target);

            // Keep the wording live: a plot ripens, and a chicken finishes an egg, while the
            // player is standing still next to it.
            if (target is InteractableBase dynamicTarget)
            {
                trackedInteractable = dynamicTarget;
                trackedInteractable.LabelChanged += WritePrompt;
            }
        }

        private void WritePrompt(IInteractable target)
        {
            if (actionPrompt != null && target != null)
            {
                actionPrompt.SetAction(target.InteractionLabel);
            }
        }

        private void UntrackInteractable()
        {
            if (trackedInteractable == null)
            {
                return;
            }

            trackedInteractable.LabelChanged -= WritePrompt;
            trackedInteractable = null;
        }

        // ============================================================ feedback

        public void ShowMessage(string message)
        {
            if (toasts != null)
            {
                toasts.Show(message);
            }
        }

        // ============================================================ menu

        private void OnMenuClicked()
        {
            // Settings, audio and help arrive with their own phase. Until then the button is a
            // real, styled control that says so, rather than a dead hamburger.
            if (menuPlaceholder != null)
            {
                menuPlaceholder.SetActive(!menuPlaceholder.activeSelf);
            }
        }

        public void ToggleInventory()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.Toggle();
            }
        }
    }
}
