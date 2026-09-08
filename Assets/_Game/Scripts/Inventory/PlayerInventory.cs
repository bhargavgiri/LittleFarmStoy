using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleFarmStory.Inventory
{
    /// <summary>
    /// Deliberately small id-keyed item store. Enough for Phase 2 (seeds and produce) and
    /// for every later item type, without pretending to be a full inventory framework -
    /// no stacks, no slots, no weight, no UI model. Quantities can never go negative.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour
    {
        [Serializable]
        public struct StartingItem
        {
            public string ItemId;
            [Min(0)] public int Amount;
        }

        [Tooltip("Stock granted on a new game. Add rows here to give the player other seeds.")]
        [SerializeField]
        private StartingItem[] startingItems =
        {
            new StartingItem { ItemId = "seed_wheat", Amount = 10 },
            new StartingItem { ItemId = "wheat", Amount = 0 }
        };

        private readonly Dictionary<string, int> quantities = new Dictionary<string, int>();

        /// <summary>Raised with (itemId, newQuantity) whenever a quantity actually changes.</summary>
        public event Action<string, int> Changed;

        private void Awake()
        {
            ResetToStartingItems();
        }

        public void ResetToStartingItems()
        {
            quantities.Clear();

            if (startingItems == null)
            {
                return;
            }

            for (int i = 0; i < startingItems.Length; i++)
            {
                StartingItem item = startingItems[i];
                if (string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                quantities[item.ItemId] = Mathf.Max(0, item.Amount);
                Changed?.Invoke(item.ItemId, quantities[item.ItemId]);
            }
        }

        public int GetQuantity(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            return quantities.TryGetValue(itemId, out int amount) ? amount : 0;
        }

        public bool Has(string itemId, int amount = 1)
        {
            return amount <= 0 || GetQuantity(itemId) >= amount;
        }

        /// <summary>Adds stock. Non-positive amounts are ignored rather than silently subtracting.</summary>
        public void Add(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            int updated = GetQuantity(itemId) + amount;
            quantities[itemId] = updated;
            Changed?.Invoke(itemId, updated);
        }

        /// <summary>
        /// All-or-nothing removal. Returns false and changes nothing when the player
        /// does not have enough, so callers can never drive a quantity below zero.
        /// </summary>
        public bool Remove(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return false;
            }

            int current = GetQuantity(itemId);
            if (current < amount)
            {
                return false;
            }

            int updated = current - amount;
            quantities[itemId] = updated;
            Changed?.Invoke(itemId, updated);
            return true;
        }

        /// <summary>Snapshot for the future save system. Order is not guaranteed.</summary>
        public IReadOnlyDictionary<string, int> All => quantities;
    }
}
