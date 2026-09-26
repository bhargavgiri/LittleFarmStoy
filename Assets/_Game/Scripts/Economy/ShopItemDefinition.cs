using UnityEngine;

namespace LittleFarmStory.Economy
{
    /// <summary>
    /// One tradeable line in the shop: what it is, what it costs, and in what quantities.
    ///
    /// Buying and selling live on the same asset because they are two sides of one item's
    /// price, and splitting them across two assets is how a game ends up buying wheat seeds at
    /// one id and selling them at another.
    ///
    /// <see cref="EconomyManager"/> runs one generic transaction against this data, so there is
    /// no per-item branching anywhere in the runtime code. Adding a crop to the shop is
    /// authoring an asset - there is nothing to change in code, and nothing anywhere asks
    /// "is this wheat?".
    /// </summary>
    [CreateAssetMenu(
        fileName = "Shop_",
        menuName = "Little Farm Story/Shop Item Definition",
        order = 20)]
    public class ShopItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Inventory id traded. Must match the id PlayerInventory uses - author it from " +
                 "ItemIds (e.g. ItemIds.Seed(\"wheat\")), never as a typed literal.")]
        [SerializeField] private string itemId = "";

        [SerializeField] private string displayName = "";

        [Tooltip("Shown by the shop UI. Optional here; the UI phase supplies the sprite set.")]
        [SerializeField] private Sprite icon;

        [Header("Availability")]
        [Tooltip("Master switch. An item that is off is not tradeable in either direction, and " +
                 "the shop behaves as though it were not in the catalogue.")]
        [SerializeField] private bool enabled = true;

        [Header("Buying")]
        [SerializeField] private bool purchasable = true;
        [Tooltip("Coins per single unit.")]
        [Min(0)] [SerializeField] private int buyPrice = 1;

        [Header("Selling")]
        [Tooltip("Off for anything the shop does not buy back.")]
        [SerializeField] private bool sellable;
        [Tooltip("Coins paid per single unit. Only meaningful while Sellable is on.")]
        [Min(0)] [SerializeField] private int sellPrice;

        [Header("Quantity")]
        [Tooltip("Smallest tradeable amount in one transaction.")]
        [Min(1)] [SerializeField] private int minQuantity = 1;

        [Tooltip("Largest tradeable amount in one transaction. A cap on a single trade, not on " +
                 "how much the player may own.")]
        [Min(1)] [SerializeField] private int maxQuantity = 99;

        [Tooltip("Quantity granularity. 1 allows any amount; 5 would sell only in fives.")]
        [Min(1)] [SerializeField] private int quantityStep = 1;

        public string ItemId => itemId;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? itemId : displayName;

        public Sprite Icon => icon;

        public bool Enabled => enabled;

        public bool Purchasable => enabled && purchasable;

        public bool Sellable => enabled && sellable;

        public int BuyPrice => buyPrice;

        /// <summary>Coins per unit when selling back, or 0 when this item is not bought back.</summary>
        public int SellPrice => sellable ? sellPrice : 0;

        public int MinQuantity => Mathf.Max(1, minQuantity);

        public int MaxQuantity => Mathf.Max(MinQuantity, maxQuantity);

        public int QuantityStep => Mathf.Max(1, quantityStep);

        /// <summary>Total coins for a purchase of this many units.</summary>
        public int TotalBuyPrice(int quantity)
        {
            return buyPrice * Mathf.Max(0, quantity);
        }

        /// <summary>Total coins earned selling this many units.</summary>
        public int TotalSellPrice(int quantity)
        {
            return SellPrice * Mathf.Max(0, quantity);
        }

        /// <summary>
        /// Checks a quantity against this item's own rules. Returns
        /// <see cref="TransactionResult.Success"/> when it is tradeable.
        /// </summary>
        public TransactionResult ValidateQuantity(int quantity)
        {
            if (quantity <= 0)
            {
                return TransactionResult.InvalidQuantity;
            }

            if (quantity < MinQuantity)
            {
                return TransactionResult.QuantityBelowMinimum;
            }

            if (quantity > MaxQuantity)
            {
                return TransactionResult.QuantityAboveMaximum;
            }

            // Steps are measured from the minimum, so a "3 minimum, step 5" item allows 3, 8, 13.
            if ((quantity - MinQuantity) % QuantityStep != 0)
            {
                return TransactionResult.QuantityOffStep;
            }

            return TransactionResult.Success;
        }

        /// <summary>The nearest legal quantity at or below a requested one. For the UI's steppers.</summary>
        public int ClampQuantity(int quantity)
        {
            int clamped = Mathf.Clamp(quantity, MinQuantity, MaxQuantity);
            int offset = clamped - MinQuantity;
            return MinQuantity + offset - offset % QuantityStep;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(
            string id, string display, Sprite iconSprite,
            bool canBuy, int buy, bool canSell, int sell,
            int minimum, int maximum, int step, bool isEnabled)
        {
            itemId = id;
            displayName = display;
            icon = iconSprite;
            purchasable = canBuy;
            buyPrice = buy;
            sellable = canSell;
            sellPrice = sell;
            minQuantity = minimum;
            maxQuantity = maximum;
            quantityStep = step;
            enabled = isEnabled;
        }
#endif
    }
}
