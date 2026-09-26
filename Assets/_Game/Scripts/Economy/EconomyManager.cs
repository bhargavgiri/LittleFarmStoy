using System;
using LittleFarmStory.Core;
using LittleFarmStory.Inventory;
using UnityEngine;

namespace LittleFarmStory.Economy
{
    /// <summary>
    /// Runs purchases and sales against the existing <c>PlayerInventory</c> and the
    /// <see cref="CurrencyWallet"/>.
    ///
    /// There is exactly one generic transaction path. Nothing here asks what an item is - every
    /// rule comes from the <see cref="ShopItemDefinition"/> asset, so adding a crop to the shop
    /// is authoring an asset and changing no code.
    ///
    /// Two properties matter more than anything else in this class:
    ///
    ///   ATOMICITY. Every validation runs before any state is touched. A purchase spends coins
    ///   only after every gate has passed, then verifies the items actually landed and refunds
    ///   if they did not. A sale removes items first - through the inventory's own all-or-nothing
    ///   Remove - and credits coins only once that has succeeded. There is no ordering in which
    ///   coins move without items moving, or the reverse.
    ///
    ///   DUPLICATE PROTECTION AT THE API. An identical transaction inside the configured window
    ///   is refused here, not by whichever button happened to be disabled in time. A double tap,
    ///   a repeated event and a stuck button are all the same bug to this class.
    ///
    /// It owns no state beyond the duplicate guard: coins live in the wallet, items in the
    /// inventory, prices in the assets.
    /// </summary>
    [DisallowMultipleComponent]
    public class EconomyManager : MonoBehaviour
    {
        private enum TransactionKind
        {
            Purchase = 0,
            Sale = 1
        }

        [Header("Data")]
        [SerializeField] private ShopDefinition shop;
        [SerializeField] private EconomySettings settings;

        [Header("Runtime")]
        [Tooltip("Defaults to a CurrencyWallet on this object or a parent.")]
        [SerializeField] private CurrencyWallet wallet;
        [Tooltip("Defaults to a PlayerInventory on this object or a parent.")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("Optional. Player-facing messages are posted here.")]
        [SerializeField] private ActionFeedbackChannel feedback;

        // ---- duplicate guard
        private TransactionKind lastKind;
        private string lastItemId;
        private int lastQuantity;
        private float lastTransactionTime = float.NegativeInfinity;

        public ShopDefinition Shop => shop;

        public CurrencyWallet Wallet => wallet;

        public PlayerInventory Inventory => inventory;

        /// <summary>Raised after any transaction that actually changed state.</summary>
        public event Action<TransactionOutcome> TransactionCompleted;

        /// <summary>Raised when a transaction was refused, including the reason.</summary>
        public event Action<TransactionOutcome> TransactionRefused;

        private void Awake()
        {
            if (wallet == null)
            {
                wallet = GetComponentInParent<CurrencyWallet>();
            }

            if (inventory == null)
            {
                inventory = GetComponentInParent<PlayerInventory>();
            }

            if (feedback == null)
            {
                feedback = GetComponentInParent<ActionFeedbackChannel>();
            }

            // Configuration faults are reported once, at startup, rather than only when the
            // player first walks up to the shop and nothing happens.
            if (shop == null)
            {
                Debug.LogError("EconomyManager on '" + name + "' has no ShopDefinition; no trade is possible.", this);
            }

            if (settings == null)
            {
                Debug.LogError("EconomyManager on '" + name + "' has no EconomySettings; no trade is possible.", this);
            }

            if (wallet == null)
            {
                Debug.LogError("EconomyManager on '" + name + "' found no CurrencyWallet.", this);
            }

            if (inventory == null)
            {
                Debug.LogError("EconomyManager on '" + name + "' found no PlayerInventory.", this);
            }
        }

        // ============================================================ queries

        /// <summary>Catalogue entry for an inventory id, or null.</summary>
        public ShopItemDefinition FindItem(string itemId)
        {
            return shop != null ? shop.Find(itemId) : null;
        }

        /// <summary>
        /// Whether a purchase would be allowed, without performing it. For a UI that wants to
        /// grey out a button - the authority is still the transaction itself.
        /// </summary>
        public TransactionResult CanPurchase(ShopItemDefinition item, int quantity)
        {
            return ValidatePurchase(item, quantity, out _, out _);
        }

        public TransactionResult CanSell(ShopItemDefinition item, int quantity)
        {
            return ValidateSale(item, quantity, out _);
        }

        // ============================================================ purchase

        public TransactionOutcome Purchase(string itemId, int quantity)
        {
            ShopItemDefinition item = FindItem(itemId);

            if (item == null)
            {
                return Refuse(
                    shop == null ? TransactionResult.NoShopDefinition : TransactionResult.UnknownItem,
                    itemId, quantity, 0);
            }

            return Purchase(item, quantity);
        }

        /// <summary>
        /// Buys <paramref name="quantity"/> units. All-or-nothing: on any failure the wallet and
        /// the inventory are exactly as they were.
        /// </summary>
        public TransactionOutcome Purchase(ShopItemDefinition item, int quantity)
        {
            TransactionResult validation = ValidatePurchase(item, quantity, out int totalCost, out int held);

            if (validation != TransactionResult.Success)
            {
                return Refuse(validation, item != null ? item.ItemId : string.Empty, quantity, 0);
            }

            if (IsDuplicate(TransactionKind.Purchase, item.ItemId, quantity))
            {
                return Refuse(TransactionResult.DuplicateTransaction, item.ItemId, quantity, 0);
            }

            // ---- past this line state changes. Nothing above it has touched anything.
            if (!wallet.TrySpendCoins(totalCost))
            {
                // Unreachable: affordability was checked above. Kept because a wallet that
                // refuses a spend we believed was valid is a bug worth surfacing, not ignoring.
                return Refuse(TransactionResult.NotEnoughCoins, item.ItemId, quantity, 0);
            }

            inventory.Add(item.ItemId, quantity);

            int nowHeld = inventory.GetQuantity(item.ItemId);

            if (nowHeld != held + quantity)
            {
                // The items did not land. Put the coins back rather than leaving the player
                // poorer with nothing to show for it.
                wallet.AddCoins(totalCost);

                Debug.LogError("EconomyManager: purchase of " + quantity + " x '" + item.ItemId +
                               "' did not reach the inventory (" + held + " -> " + nowHeld +
                               "); the coins were refunded and nothing was changed.", this);

                return Refuse(TransactionResult.RolledBack, item.ItemId, quantity, 0);
            }

            RecordTransaction(TransactionKind.Purchase, item.ItemId, quantity);

            TransactionOutcome outcome = new TransactionOutcome(
                TransactionResult.Success, item.ItemId, quantity, totalCost, wallet.Balance);

            Log("bought " + quantity + " x " + item.DisplayName + " for " + totalCost +
                " coins, balance " + wallet.Balance);

            Post("Bought " + quantity + " " + item.DisplayName + " (-" + totalCost + ")");
            TransactionCompleted?.Invoke(outcome);
            return outcome;
        }

        private TransactionResult ValidatePurchase(
            ShopItemDefinition item, int quantity, out int totalCost, out int held)
        {
            totalCost = 0;
            held = 0;

            TransactionResult common = ValidateCommon(item);

            if (common != TransactionResult.Success)
            {
                return common;
            }

            if (!item.Purchasable)
            {
                return item.Enabled ? TransactionResult.NotPurchasable : TransactionResult.ItemDisabled;
            }

            TransactionResult quantityCheck = item.ValidateQuantity(quantity);

            if (quantityCheck != TransactionResult.Success)
            {
                return quantityCheck;
            }

            if (item.BuyPrice <= 0)
            {
                return TransactionResult.InvalidPrice;
            }

            totalCost = item.TotalBuyPrice(quantity);
            held = inventory.GetQuantity(item.ItemId);

            if (!settings.FitsInStack(held + quantity))
            {
                return TransactionResult.NotEnoughRoom;
            }

            if (!wallet.CanAfford(totalCost))
            {
                return TransactionResult.NotEnoughCoins;
            }

            return TransactionResult.Success;
        }

        // ============================================================ sale

        public TransactionOutcome Sell(string itemId, int quantity)
        {
            ShopItemDefinition item = FindItem(itemId);

            if (item == null)
            {
                return Refuse(
                    shop == null ? TransactionResult.NoShopDefinition : TransactionResult.UnknownItem,
                    itemId, quantity, 0);
            }

            return Sell(item, quantity);
        }

        /// <summary>
        /// Sells <paramref name="quantity"/> units. Items are removed first, through the
        /// inventory's own all-or-nothing Remove, and coins are credited only once that has
        /// succeeded - so coins can never appear without the goods leaving.
        /// </summary>
        public TransactionOutcome Sell(ShopItemDefinition item, int quantity)
        {
            TransactionResult validation = ValidateSale(item, quantity, out int totalEarned);

            if (validation != TransactionResult.Success)
            {
                return Refuse(validation, item != null ? item.ItemId : string.Empty, quantity, 0);
            }

            if (IsDuplicate(TransactionKind.Sale, item.ItemId, quantity))
            {
                return Refuse(TransactionResult.DuplicateTransaction, item.ItemId, quantity, 0);
            }

            // ---- past this line state changes.
            if (!inventory.Remove(item.ItemId, quantity))
            {
                // Ownership was verified above, so reaching here means the stock moved between
                // the check and the removal. Nothing has changed; refuse cleanly.
                return Refuse(TransactionResult.NotEnoughItems, item.ItemId, quantity, 0);
            }

            wallet.AddCoins(totalEarned);

            RecordTransaction(TransactionKind.Sale, item.ItemId, quantity);

            TransactionOutcome outcome = new TransactionOutcome(
                TransactionResult.Success, item.ItemId, quantity, totalEarned, wallet.Balance);

            Log("sold " + quantity + " x " + item.DisplayName + " for " + totalEarned +
                " coins, balance " + wallet.Balance);

            Post("Sold " + quantity + " " + item.DisplayName + " (+" + totalEarned + ")");
            TransactionCompleted?.Invoke(outcome);
            return outcome;
        }

        private TransactionResult ValidateSale(ShopItemDefinition item, int quantity, out int totalEarned)
        {
            totalEarned = 0;

            TransactionResult common = ValidateCommon(item);

            if (common != TransactionResult.Success)
            {
                return common;
            }

            if (!item.Sellable)
            {
                return item.Enabled ? TransactionResult.NotSellable : TransactionResult.ItemDisabled;
            }

            TransactionResult quantityCheck = item.ValidateQuantity(quantity);

            if (quantityCheck != TransactionResult.Success)
            {
                return quantityCheck;
            }

            if (item.SellPrice <= 0)
            {
                return TransactionResult.InvalidPrice;
            }

            // Ownership, before anything is removed.
            if (!inventory.Has(item.ItemId, quantity))
            {
                return TransactionResult.NotEnoughItems;
            }

            totalEarned = item.TotalSellPrice(quantity);
            return TransactionResult.Success;
        }

        // ============================================================ shared validation

        /// <summary>Everything both directions require: wiring, catalogue membership, a usable id.</summary>
        private TransactionResult ValidateCommon(ShopItemDefinition item)
        {
            if (shop == null) { return TransactionResult.NoShopDefinition; }
            if (settings == null) { return TransactionResult.NoSettings; }
            if (wallet == null) { return TransactionResult.NoWallet; }
            if (inventory == null) { return TransactionResult.NoInventory; }
            if (item == null) { return TransactionResult.NoItem; }

            if (string.IsNullOrEmpty(item.ItemId))
            {
                return TransactionResult.InvalidItemId;
            }

            // An item that is not in this shop's catalogue must not be tradeable here, even if
            // the caller happens to hold a reference to the asset.
            if (!shop.Contains(item))
            {
                return TransactionResult.UnknownItem;
            }

            if (!item.Enabled)
            {
                return TransactionResult.ItemDisabled;
            }

            return TransactionResult.Success;
        }

        // ============================================================ duplicate guard

        private bool IsDuplicate(TransactionKind kind, string itemId, int quantity)
        {
            float window = settings != null ? settings.DuplicateTransactionWindow : 0f;

            if (window <= 0f)
            {
                return false;
            }

            // Unscaled: a paused or slowed game must not make the guard longer or shorter.
            return kind == lastKind &&
                   quantity == lastQuantity &&
                   itemId == lastItemId &&
                   Time.unscaledTime - lastTransactionTime < window;
        }

        private void RecordTransaction(TransactionKind kind, string itemId, int quantity)
        {
            lastKind = kind;
            lastItemId = itemId;
            lastQuantity = quantity;
            lastTransactionTime = Time.unscaledTime;
        }

        // ============================================================ reporting

        private TransactionOutcome Refuse(
            TransactionResult result, string itemId, int quantity, int coins)
        {
            TransactionOutcome outcome = new TransactionOutcome(
                result, itemId, quantity, coins, wallet != null ? wallet.Balance : 0);

            // A wiring fault is a developer problem and gets an error; everything else is an
            // ordinary refusal the player caused and only needs a message.
            if (outcome.IsConfigurationFault)
            {
                Debug.LogError("EconomyManager: transaction refused - " + outcome + ". This is a " +
                               "configuration fault, not a player action.", this);
            }
            else
            {
                Log("refused - " + outcome);
            }

            Post(MessageFor(result, itemId, quantity));
            TransactionRefused?.Invoke(outcome);
            return outcome;
        }

        /// <summary>
        /// Player-facing wording for a refusal, or null when the player should not be told -
        /// a duplicate tap is not a failure they need to see, and a wiring fault is not their
        /// problem to read about.
        /// </summary>
        private string MessageFor(TransactionResult result, string itemId, int quantity)
        {
            ShopItemDefinition item = FindItem(itemId);
            string label = item != null ? item.DisplayName : itemId;

            switch (result)
            {
                case TransactionResult.NotEnoughCoins:
                    return "Not enough coins";

                case TransactionResult.NotEnoughItems:
                    return "Not enough " + label;

                case TransactionResult.NotEnoughRoom:
                    return "No room for more " + label;

                case TransactionResult.NotPurchasable:
                    return label + " is not for sale";

                case TransactionResult.NotSellable:
                    return "The shop does not buy " + label;

                case TransactionResult.QuantityBelowMinimum:
                case TransactionResult.QuantityAboveMaximum:
                case TransactionResult.QuantityOffStep:
                case TransactionResult.InvalidQuantity:
                    return "Cannot trade " + quantity + " " + label;

                default:
                    return null;
            }
        }

        private void Post(string message)
        {
            if (feedback != null && !string.IsNullOrEmpty(message))
            {
                feedback.Post(message);
            }
        }

        private void Log(string message)
        {
            if (settings != null && settings.LogTransactions)
            {
                Debug.Log("[ECONOMY] " + message, this);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only wiring used by the prototype builder tool.</summary>
        public void EditorConfigure(
            ShopDefinition shopDefinition, EconomySettings economySettings,
            CurrencyWallet currencyWallet, PlayerInventory playerInventory,
            ActionFeedbackChannel feedbackChannel)
        {
            shop = shopDefinition;
            settings = economySettings;
            wallet = currencyWallet;
            inventory = playerInventory;
            feedback = feedbackChannel;
        }
#endif
    }
}
