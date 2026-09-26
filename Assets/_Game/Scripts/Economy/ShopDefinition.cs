using System.Collections.Generic;
using UnityEngine;

namespace LittleFarmStory.Economy
{
    /// <summary>
    /// The shop's catalogue: which items it trades, in listing order.
    ///
    /// A separate asset from the items themselves so a second shop - a market stall, a travelling
    /// trader - is a new catalogue over the same item definitions rather than a duplicated
    /// price list.
    ///
    /// The id lookup is built once and cached, so a transaction never walks the array.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShopDefinition",
        menuName = "Little Farm Story/Shop Definition",
        order = 21)]
    public class ShopDefinition : ScriptableObject
    {
        [Tooltip("Everything this shop trades, in the order it should be listed.")]
        [SerializeField] private ShopItemDefinition[] items;

        private Dictionary<string, ShopItemDefinition> lookup;

        public IReadOnlyList<ShopItemDefinition> Items =>
            (IReadOnlyList<ShopItemDefinition>)items ?? System.Array.Empty<ShopItemDefinition>();

        public int Count => items != null ? items.Length : 0;

        /// <summary>Finds a catalogue entry by inventory id, or null.</summary>
        public ShopItemDefinition Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            EnsureLookup();
            return lookup.TryGetValue(itemId, out ShopItemDefinition item) ? item : null;
        }

        public bool Contains(ShopItemDefinition item)
        {
            if (item == null || items == null)
            {
                return false;
            }

            for (int i = 0; i < items.Length; i++)
            {
                if (ReferenceEquals(items[i], item))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, ShopItemDefinition>();

            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                ShopItemDefinition item = items[i];

                if (item == null)
                {
                    Debug.LogError("ShopDefinition '" + name + "' has an empty slot at index " + i + ".", this);
                    continue;
                }

                if (string.IsNullOrEmpty(item.ItemId))
                {
                    Debug.LogError("ShopDefinition '" + name + "': item '" + item.name +
                                   "' has no inventory id and can never be traded.", this);
                    continue;
                }

                if (lookup.ContainsKey(item.ItemId))
                {
                    // Two prices for one id is a data bug that would otherwise surface as the
                    // shop silently charging whichever entry happened to be found first.
                    Debug.LogError("ShopDefinition '" + name + "': duplicate entry for item id '" +
                                   item.ItemId + "'. Only the first will ever be used.", this);
                    continue;
                }

                lookup.Add(item.ItemId, item);
            }
        }

        /// <summary>Drops the cache. Called after the catalogue is re-authored in the editor.</summary>
        public void InvalidateLookup()
        {
            lookup = null;
        }

        private void OnDisable()
        {
            // Domain reloads and asset reimports must not leave a stale map behind.
            lookup = null;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(ShopItemDefinition[] catalogue)
        {
            items = catalogue;
            lookup = null;
        }
#endif
    }
}
