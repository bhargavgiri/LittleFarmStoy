using LittleFarmStory.Economy;
using LittleFarmStory.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// One sellable line in the shop: how many the player owns, what the shop pays, and three
    /// fixed-amount sell buttons.
    ///
    /// The amounts (1, 5, everything owned) are the only thing fixed here - the ITEM is not.
    /// Any <see cref="ShopItemDefinition"/> marked sellable gets one of these rows, built by the
    /// same component, with no per-item branching. If a future item's own minimum or step rules
    /// out selling exactly 5 at a time, that button simply shows disabled -
    /// <see cref="EconomyManager.CanSell"/> is the only authority ever consulted.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopSellRow : MonoBehaviour
    {
        private const int QuickSellAmount = 5;

        [Header("Data")]
        [SerializeField] private EconomyManager economy;
        [SerializeField] private ShopItemDefinition item;

        [Header("Widgets")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text ownedLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private Button sellOneButton;
        [SerializeField] private Button sellFiveButton;
        [SerializeField] private Button sellAllButton;

        private void Awake()
        {
            if (nameLabel != null && item != null)
            {
                nameLabel.SetText(item.DisplayName);
            }

            if (priceLabel != null && item != null)
            {
                priceLabel.SetText("{0}", item.SellPrice);
            }
        }

        private void OnEnable()
        {
            if (sellOneButton != null) { sellOneButton.onClick.AddListener(SellOne); }
            if (sellFiveButton != null) { sellFiveButton.onClick.AddListener(SellFive); }
            if (sellAllButton != null) { sellAllButton.onClick.AddListener(SellAll); }

            PlayerInventory inventory = economy != null ? economy.Inventory : null;

            if (inventory != null)
            {
                inventory.Changed += OnInventoryChanged;
            }

            if (economy == null || item == null)
            {
                Debug.LogError("ShopSellRow '" + name + "' is missing its EconomyManager or " +
                               "ShopItemDefinition; selling will do nothing.", this);
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (sellOneButton != null) { sellOneButton.onClick.RemoveListener(SellOne); }
            if (sellFiveButton != null) { sellFiveButton.onClick.RemoveListener(SellFive); }
            if (sellAllButton != null) { sellAllButton.onClick.RemoveListener(SellAll); }

            PlayerInventory inventory = economy != null ? economy.Inventory : null;

            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }
        }

        private void OnInventoryChanged(string itemId, int newQuantity)
        {
            if (item != null && itemId == item.ItemId)
            {
                Refresh();
            }
        }

        private void SellOne()
        {
            Sell(1);
        }

        private void SellFive()
        {
            Sell(QuickSellAmount);
        }

        private void SellAll()
        {
            if (economy == null || item == null || economy.Inventory == null)
            {
                return;
            }

            int owned = economy.Inventory.GetQuantity(item.ItemId);

            if (owned <= 0)
            {
                return;
            }

            // The largest quantity that is both <= what is actually owned and a legal trade for
            // this item - never more than the player has, whatever the item's own step is.
            Sell(item.ClampQuantity(owned));
        }

        private void Sell(int amount)
        {
            if (economy == null || item == null)
            {
                Debug.LogError("ShopSellRow '" + name + "' cannot sell: missing EconomyManager " +
                               "or ShopItemDefinition.", this);
                return;
            }

            economy.Sell(item, amount);
            Refresh();
        }

        private void Refresh()
        {
            if (item == null)
            {
                return;
            }

            int owned = economy != null && economy.Inventory != null
                ? economy.Inventory.GetQuantity(item.ItemId)
                : 0;

            if (ownedLabel != null)
            {
                ownedLabel.SetText("{0}", owned);
            }

            bool ownsAny = owned > 0;
            bool hasEconomy = economy != null;

            if (sellOneButton != null)
            {
                sellOneButton.interactable = ownsAny && hasEconomy &&
                    economy.CanSell(item, 1) == TransactionResult.Success;
            }

            if (sellFiveButton != null)
            {
                sellFiveButton.interactable = ownsAny && owned >= QuickSellAmount && hasEconomy &&
                    economy.CanSell(item, QuickSellAmount) == TransactionResult.Success;
            }

            if (sellAllButton != null)
            {
                int allAmount = ownsAny ? item.ClampQuantity(owned) : 0;
                sellAllButton.interactable = ownsAny && allAmount > 0 && hasEconomy &&
                    economy.CanSell(item, allAmount) == TransactionResult.Success;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(
            EconomyManager economyManager, ShopItemDefinition shopItem,
            TMP_Text name, TMP_Text owned, TMP_Text price,
            Button sellOne, Button sellFive, Button sellAll)
        {
            economy = economyManager;
            item = shopItem;
            nameLabel = name;
            ownedLabel = owned;
            priceLabel = price;
            sellOneButton = sellOne;
            sellFiveButton = sellFive;
            sellAllButton = sellAll;
        }
#endif
    }
}
