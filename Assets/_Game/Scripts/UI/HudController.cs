using System;
using System.Collections;
using LittleFarmStory.Core;
using LittleFarmStory.Interaction;
using LittleFarmStory.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// Minimal portrait HUD: currency placeholders, inventory counters, a contextual
    /// interaction prompt and a transient action message.
    /// Fully event driven - this component has no Update.
    /// Uses legacy uGUI Text because TextMeshPro Essential Resources are not imported yet.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudController : MonoBehaviour
    {
        /// <summary>Binds one inventory item id to one on-screen label.</summary>
        [Serializable]
        public struct InventoryCounter
        {
            public string ItemId;
            public Text Label;
            [Tooltip("Optional text placed before the quantity, e.g. \"x\".")]
            public string Prefix;
        }

        [Header("Sources")]
        [SerializeField] private InteractionController interaction;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private ActionFeedbackChannel feedback;

        [Header("Widgets")]
        [SerializeField] private Text coinLabel;
        [SerializeField] private Text xpLabel;
        [SerializeField] private Text promptLabel;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private GameObject actionButtonRoot;
        [SerializeField] private Button menuButton;

        [Header("Inventory readout")]
        [SerializeField] private InventoryCounter[] counters;

        [Header("Action messages")]
        [SerializeField] private Text messageLabel;
        [SerializeField] private GameObject messageRoot;
        [Min(0.2f)] [SerializeField] private float messageDuration = 1.6f;

        [Header("Placeholder values")]
        [SerializeField] private int coins = 250;
        [SerializeField] private int level = 1;

        private InteractableBase trackedInteractable;
        private Coroutine messageRoutine;

        private void Awake()
        {
            SetCoins(coins);
            SetLevel(level);
            ShowPrompt(null);
            HideMessage();
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
                RefreshAllCounters();
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

        // ============================================================ currency placeholders

        public void SetCoins(int value)
        {
            coins = value;

            if (coinLabel != null)
            {
                coinLabel.text = value.ToString();
            }
        }

        public void SetLevel(int value)
        {
            level = value;

            if (xpLabel != null)
            {
                xpLabel.text = "Lv " + value;
            }
        }

        // ============================================================ inventory

        private void OnInventoryChanged(string itemId, int quantity)
        {
            if (counters == null)
            {
                return;
            }

            for (int i = 0; i < counters.Length; i++)
            {
                if (counters[i].ItemId == itemId)
                {
                    WriteCounter(counters[i], quantity);
                }
            }
        }

        private void RefreshAllCounters()
        {
            if (counters == null || inventory == null)
            {
                return;
            }

            for (int i = 0; i < counters.Length; i++)
            {
                WriteCounter(counters[i], inventory.GetQuantity(counters[i].ItemId));
            }
        }

        private static void WriteCounter(InventoryCounter counter, int quantity)
        {
            if (counter.Label != null)
            {
                counter.Label.text = counter.Prefix + quantity;
            }
        }

        // ============================================================ interaction prompt

        private void ShowPrompt(IInteractable target)
        {
            UntrackInteractable();

            bool hasTarget = target != null;

            if (promptRoot != null)
            {
                promptRoot.SetActive(hasTarget);
            }

            if (actionButtonRoot != null)
            {
                actionButtonRoot.SetActive(hasTarget);
            }

            if (!hasTarget)
            {
                return;
            }

            WritePrompt(target);

            // Keep the prompt live: a plot can ripen while the player stands next to it.
            if (target is InteractableBase dynamicTarget)
            {
                trackedInteractable = dynamicTarget;
                trackedInteractable.LabelChanged += WritePrompt;
            }
        }

        private void WritePrompt(IInteractable target)
        {
            if (promptLabel != null && target != null)
            {
                promptLabel.text = target.InteractionLabel;
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

        // ============================================================ transient messages

        public void ShowMessage(string message)
        {
            if (messageLabel == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            messageLabel.text = message;

            if (messageRoot != null)
            {
                messageRoot.SetActive(true);
            }

            if (messageRoutine != null)
            {
                StopCoroutine(messageRoutine);
            }

            messageRoutine = StartCoroutine(HideMessageAfterDelay());
        }

        private IEnumerator HideMessageAfterDelay()
        {
            yield return new WaitForSeconds(messageDuration);
            HideMessage();
            messageRoutine = null;
        }

        private void HideMessage()
        {
            if (messageRoot != null)
            {
                messageRoot.SetActive(false);
            }
        }

        private void OnMenuClicked()
        {
            // Phase 1 placeholder. The pause / settings menu arrives with the UI phase.
            Debug.Log("Menu button pressed - menu system arrives in a later phase.", this);
        }
    }
}
