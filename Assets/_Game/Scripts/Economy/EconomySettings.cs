using UnityEngine;

namespace LittleFarmStory.Economy
{
    /// <summary>
    /// Shared tuning for the economy, in one place rather than scattered across components.
    ///
    /// Mirrors <c>FarmingSettings</c>, which does the same job for crop growth: developer-facing
    /// configuration, never surfaced to the player.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EconomySettings",
        menuName = "Little Farm Story/Economy Settings",
        order = 22)]
    public class EconomySettings : ScriptableObject
    {
        [Header("New game")]
        [Tooltip("Coins a new save starts with. The wallet's own starting value is authored " +
                 "from this, so there is one number rather than two that can disagree.")]
        [Min(0)] [SerializeField] private int startingCoins = 100;

        [Header("Transaction safety")]
        [Tooltip("An identical transaction submitted again inside this window is rejected as a " +
                 "duplicate. Guards against double taps, repeated events and stuck buttons at " +
                 "the API, so correctness does not depend on the UI disabling a button in time. " +
                 "Set to 0 to allow repeats.")]
        [Range(0f, 2f)] [SerializeField] private float duplicateTransactionWindow = 0.35f;

        [Header("Inventory limits")]
        [Tooltip("Largest quantity of any single item the player may hold. 0 means unlimited. " +
                 "Checked before a purchase so coins are never spent on items that will not fit. " +
                 "PlayerInventory itself stays uncapped - this is an economy rule, not an " +
                 "inventory rewrite.")]
        [Min(0)] [SerializeField] private int maxStackPerItem;

        [Header("Diagnostics")]
        [Tooltip("Development only. Logs every transaction and the reason for every refusal.")]
        [SerializeField] private bool logTransactions;

        public int StartingCoins => Mathf.Max(0, startingCoins);

        public float DuplicateTransactionWindow => Mathf.Max(0f, duplicateTransactionWindow);

        /// <summary>0 means no cap.</summary>
        public int MaxStackPerItem => Mathf.Max(0, maxStackPerItem);

        public bool LogTransactions => logTransactions;

        /// <summary>True when holding <paramref name="resulting"/> of one item is allowed.</summary>
        public bool FitsInStack(int resulting)
        {
            return MaxStackPerItem == 0 || resulting <= MaxStackPerItem;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(int coins, float duplicateWindow, int stackCap, bool diagnostics)
        {
            startingCoins = coins;
            duplicateTransactionWindow = duplicateWindow;
            maxStackPerItem = stackCap;
            logTransactions = diagnostics;
        }
#endif
    }
}
