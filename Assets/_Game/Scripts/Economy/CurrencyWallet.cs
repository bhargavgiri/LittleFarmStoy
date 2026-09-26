using System;
using UnityEngine;

namespace LittleFarmStory.Economy
{
    /// <summary>
    /// The player's coins.
    ///
    /// A component on the Player, beside <c>PlayerInventory</c> - coins are per-player runtime
    /// state, not a shared definition, so this is deliberately not a ScriptableObject.
    ///
    /// Coins are a typed currency rather than an inventory id. They behave differently from
    /// items (no stack, no icon in storage, their own change event, their own display), and
    /// folding them into <c>PlayerInventory</c> would mean every inventory listener had to
    /// learn to ignore one magic id.
    ///
    /// The balance can never go negative: <see cref="TrySpendCoins"/> is all-or-nothing, in the
    /// same spirit as <c>PlayerInventory.Remove</c>. Nothing outside this class may write the
    /// balance, and the UI reacts to <see cref="BalanceChanged"/> rather than being told what
    /// to display.
    /// </summary>
    [DisallowMultipleComponent]
    public class CurrencyWallet : MonoBehaviour
    {
        [Header("Starting balance")]
        [Tooltip("Coins granted on a new game.")]
        [Min(0)] [SerializeField] private int startingCoins = 100;

        private bool initialised;

        /// <summary>Current coins. Never negative.</summary>
        public int Balance { get; private set; }

        /// <summary>
        /// Raised with the new balance whenever it actually changes. The UI subscribes to this;
        /// no other system may push a value into the coin display.
        /// </summary>
        public event Action<int> BalanceChanged;

        /// <summary>Raised with the signed delta, for effects that care about direction.</summary>
        public event Action<int> BalanceDelta;

        private void Awake()
        {
            EnsureInitialised();
        }

        private void EnsureInitialised()
        {
            if (initialised)
            {
                return;
            }

            initialised = true;
            Balance = Mathf.Max(0, startingCoins);
        }

        // ============================================================ queries

        public int GetBalance()
        {
            EnsureInitialised();
            return Balance;
        }

        /// <summary>
        /// True when the balance covers the amount. A non-positive amount is affordable by
        /// definition - it costs nothing.
        /// </summary>
        public bool CanAfford(int amount)
        {
            EnsureInitialised();
            return amount <= 0 || Balance >= amount;
        }

        // ============================================================ mutations

        /// <summary>
        /// Grants coins. Non-positive amounts are ignored rather than quietly subtracting, so a
        /// sign error can never drain the wallet through the credit path.
        /// </summary>
        public void AddCoins(int amount)
        {
            EnsureInitialised();

            if (amount <= 0)
            {
                return;
            }

            Balance += amount;
            Raise(amount);
        }

        /// <summary>
        /// All-or-nothing spend. Returns false and changes nothing when the balance does not
        /// cover the amount, so a caller can never drive the balance below zero, and a failed
        /// purchase can never leave coins missing.
        /// </summary>
        public bool TrySpendCoins(int amount)
        {
            EnsureInitialised();

            if (amount < 0)
            {
                Debug.LogError("CurrencyWallet: refused a negative spend of " + amount +
                               "; use AddCoins to grant coins.", this);
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            if (Balance < amount)
            {
                return false;
            }

            Balance -= amount;
            Raise(-amount);
            return true;
        }

        private void Raise(int delta)
        {
            BalanceDelta?.Invoke(delta);
            BalanceChanged?.Invoke(Balance);
        }

        // ============================================================ save preparation

        /// <summary>
        /// Restores a saved balance, bypassing the spend guard. For the future save system
        /// only - gameplay must go through <see cref="AddCoins"/> and <see cref="TrySpendCoins"/>.
        /// </summary>
        public void RestoreBalance(int value)
        {
            EnsureInitialised();

            int clamped = Mathf.Max(0, value);

            if (clamped == Balance)
            {
                return;
            }

            int delta = clamped - Balance;
            Balance = clamped;
            Raise(delta);
        }

        public void ResetToStartingBalance()
        {
            initialised = false;
            int previous = Balance;
            EnsureInitialised();

            if (Balance != previous)
            {
                Raise(Balance - previous);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(int coins)
        {
            startingCoins = Mathf.Max(0, coins);
        }
#endif
    }
}
