namespace LittleFarmStory.Economy
{
    /// <summary>
    /// Why a purchase or a sale did or did not happen.
    ///
    /// Deliberately granular. A shop that can only say "failed" forces the UI to guess at the
    /// reason, and forces whoever is debugging to guess twice - so every distinct validation
    /// gate gets its own value, and <see cref="TransactionOutcome"/> carries the numbers.
    ///
    /// Mirrors the <c>FarmActionResult</c> / <c>AnimalActionResult</c> pattern already used by
    /// farming and animals, so all three systems report failure the same way.
    /// </summary>
    public enum TransactionResult
    {
        Success = 0,

        // ---- configuration faults: the game is wired wrong, not the player's doing
        /// <summary>No <c>ShopDefinition</c> asset is assigned.</summary>
        NoShopDefinition = 1,

        /// <summary>No <c>CurrencyWallet</c> is available.</summary>
        NoWallet = 2,

        /// <summary>No <c>PlayerInventory</c> is available.</summary>
        NoInventory = 3,

        /// <summary>The shop item asset is null.</summary>
        NoItem = 4,

        /// <summary>The requested id is not in the catalogue.</summary>
        UnknownItem = 5,

        /// <summary>The item has a blank or malformed inventory id.</summary>
        InvalidItemId = 6,

        /// <summary>Price is zero or negative where a real price is required.</summary>
        InvalidPrice = 7,

        // ---- availability: correctly configured, but not tradeable right now
        /// <summary>The item exists but is switched off in the catalogue.</summary>
        ItemDisabled = 8,

        /// <summary>The item is not offered for purchase.</summary>
        NotPurchasable = 9,

        /// <summary>The item is not accepted for sale.</summary>
        NotSellable = 10,

        // ---- quantity
        InvalidQuantity = 11,
        QuantityBelowMinimum = 12,
        QuantityAboveMaximum = 13,

        /// <summary>Quantity does not land on the item's step, e.g. buying 3 of a 5-pack.</summary>
        QuantityOffStep = 14,

        // ---- the ordinary player-facing failures
        NotEnoughCoins = 15,
        NotEnoughItems = 16,

        /// <summary>The purchase would push the stack past the configured cap.</summary>
        NotEnoughRoom = 17,

        /// <summary>
        /// The same transaction was submitted again within the duplicate window - a double tap,
        /// a repeated event, or a stuck button.
        /// </summary>
        DuplicateTransaction = 18,

        /// <summary>
        /// The transaction began but could not be completed, and was rolled back. Nothing
        /// changed. This should be unreachable; it exists so that if it ever is reached, it is
        /// loud rather than a silently half-applied trade.
        /// </summary>
        RolledBack = 19,

        /// <summary>No <c>EconomySettings</c> asset is assigned.</summary>
        NoSettings = 20
    }

    /// <summary>
    /// The full story of one attempted transaction: what was asked for, what it would have
    /// cost, and what actually happened.
    ///
    /// Returned by value so a caller can report, log or test a transaction without re-deriving
    /// any of it, and so the UI never has to recompute a price to display it.
    /// </summary>
    public readonly struct TransactionOutcome
    {
        public readonly TransactionResult Result;

        /// <summary>Inventory id involved. Empty when the request could not be resolved to one.</summary>
        public readonly string ItemId;

        /// <summary>Units requested.</summary>
        public readonly int Quantity;

        /// <summary>Coins moved: spent on a purchase, earned on a sale. Zero on failure.</summary>
        public readonly int Coins;

        /// <summary>Wallet balance after the transaction, or the unchanged balance on failure.</summary>
        public readonly int Balance;

        public TransactionOutcome(
            TransactionResult result, string itemId, int quantity, int coins, int balance)
        {
            Result = result;
            ItemId = itemId ?? string.Empty;
            Quantity = quantity;
            Coins = coins;
            Balance = balance;
        }

        public bool Succeeded => Result == TransactionResult.Success;

        /// <summary>
        /// True when the failure is a wiring or authoring mistake rather than something the
        /// player did. Those deserve a console error; the rest deserve a toast.
        /// </summary>
        public bool IsConfigurationFault
        {
            get
            {
                // An explicit set, not a numeric range: a range would silently reclassify these
                // the first time somebody inserts a value in the middle of the enum.
                switch (Result)
                {
                    case TransactionResult.NoShopDefinition:
                    case TransactionResult.NoSettings:
                    case TransactionResult.NoWallet:
                    case TransactionResult.NoInventory:
                    case TransactionResult.NoItem:
                    case TransactionResult.InvalidItemId:
                    case TransactionResult.InvalidPrice:
                    case TransactionResult.RolledBack:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public override string ToString()
        {
            return Result + " (" + Quantity + " x " + ItemId + ", " + Coins + " coins, balance " + Balance + ")";
        }
    }
}
