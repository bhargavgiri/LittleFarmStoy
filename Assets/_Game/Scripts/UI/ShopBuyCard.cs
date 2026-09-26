using LittleFarmStory.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// One purchasable line in the shop: a quantity stepper and a Buy button that always shows
    /// the price for the quantity currently selected.
    ///
    /// Works for whatever <see cref="ShopItemDefinition"/> the builder points it at - nothing
    /// here knows it is looking at wheat seeds, or that wheat seeds are the only purchasable
    /// item today. A second purchasable crop is one more card built from this same component,
    /// not a new branch anywhere.
    ///
    /// The authority for whether a purchase can happen is always <see cref="EconomyManager"/>;
    /// this only pre-emptively greys the button out so the player is not invited to tap
    /// something that would just be refused, and always calls <c>Purchase</c> for the real
    /// answer rather than trusting its own guess.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopBuyCard : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private EconomyManager economy;
        [SerializeField] private ShopItemDefinition item;

        [Header("Widgets")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private Button decrementButton;
        [SerializeField] private Button incrementButton;
        [SerializeField] private Button buyButton;

        private int quantity;

        private void Awake()
        {
            if (item != null)
            {
                quantity = item.MinQuantity;
            }

            if (nameLabel != null && item != null)
            {
                nameLabel.SetText(item.DisplayName);
            }
        }

        private void OnEnable()
        {
            if (decrementButton != null) { decrementButton.onClick.AddListener(Decrement); }
            if (incrementButton != null) { incrementButton.onClick.AddListener(Increment); }
            if (buyButton != null) { buyButton.onClick.AddListener(Buy); }

            if (economy != null && economy.Wallet != null)
            {
                economy.Wallet.BalanceChanged += OnEconomyChanged;
            }

            if (economy != null && economy.Inventory != null)
            {
                economy.Inventory.Changed += OnInventoryChanged;
            }

            if (economy == null || item == null)
            {
                Debug.LogError("ShopBuyCard '" + name + "' is missing its EconomyManager or " +
                               "ShopItemDefinition; buying will do nothing.", this);
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (decrementButton != null) { decrementButton.onClick.RemoveListener(Decrement); }
            if (incrementButton != null) { incrementButton.onClick.RemoveListener(Increment); }
            if (buyButton != null) { buyButton.onClick.RemoveListener(Buy); }

            if (economy != null && economy.Wallet != null)
            {
                economy.Wallet.BalanceChanged -= OnEconomyChanged;
            }

            if (economy != null && economy.Inventory != null)
            {
                economy.Inventory.Changed -= OnInventoryChanged;
            }
        }

        private void OnEconomyChanged(int balance)
        {
            Refresh();
        }

        private void OnInventoryChanged(string itemId, int newQuantity)
        {
            // Affordability alone does not decide the buy button - a stack cap (when one is
            // configured) also depends on how many the player already holds.
            if (item != null && itemId == item.ItemId)
            {
                Refresh();
            }
        }

        private void Decrement()
        {
            if (item == null)
            {
                return;
            }

            SetQuantity(quantity - item.QuantityStep);
        }

        private void Increment()
        {
            if (item == null)
            {
                return;
            }

            SetQuantity(quantity + item.QuantityStep);
        }

        private void SetQuantity(int value)
        {
            if (item == null)
            {
                return;
            }

            quantity = Mathf.Clamp(value, item.MinQuantity, item.MaxQuantity);
            Refresh();
        }

        private void Buy()
        {
            if (economy == null || item == null)
            {
                Debug.LogError("ShopBuyCard '" + name + "' cannot buy: missing EconomyManager " +
                               "or ShopItemDefinition.", this);
                return;
            }

            // The transaction is the only authority. Whatever it decides, Refresh afterwards
            // reflects the real resulting state rather than an assumption about what happened.
            economy.Purchase(item, quantity);
            Refresh();
        }

        private void Refresh()
        {
            if (item == null)
            {
                return;
            }

            if (quantityLabel != null)
            {
                quantityLabel.SetText("{0}", quantity);
            }

            if (priceLabel != null)
            {
                priceLabel.SetText("{0}", item.TotalBuyPrice(quantity));
            }

            bool canBuy = economy != null && economy.CanPurchase(item, quantity) == TransactionResult.Success;

            if (buyButton != null)
            {
                buyButton.interactable = canBuy;
            }

            if (decrementButton != null)
            {
                decrementButton.interactable = quantity > item.MinQuantity;
            }

            if (incrementButton != null)
            {
                incrementButton.interactable = quantity < item.MaxQuantity;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(
            EconomyManager economyManager, ShopItemDefinition shopItem,
            TMP_Text name, TMP_Text price, TMP_Text quantityText,
            Button decrement, Button increment, Button buy)
        {
            economy = economyManager;
            item = shopItem;
            nameLabel = name;
            priceLabel = price;
            quantityLabel = quantityText;
            decrementButton = decrement;
            incrementButton = increment;
            buyButton = buy;
        }
#endif
    }
}
