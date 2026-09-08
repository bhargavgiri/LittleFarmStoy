namespace LittleFarmStory.Inventory
{
    /// <summary>
    /// Naming convention for inventory item ids.
    /// Ids are plain strings so save data stays readable and future items (eggs, milk,
    /// processed goods) need no code change - but the convention lives here rather than
    /// being retyped as string literals across the codebase.
    /// </summary>
    public static class ItemIds
    {
        public const string SeedPrefix = "seed_";

        /// <summary>Seed item for a crop, e.g. "wheat" -> "seed_wheat".</summary>
        public static string Seed(string cropId)
        {
            return string.IsNullOrEmpty(cropId) ? string.Empty : SeedPrefix + cropId;
        }

        /// <summary>Harvested produce for a crop. Currently the crop id itself, e.g. "wheat".</summary>
        public static string Harvest(string cropId)
        {
            return string.IsNullOrEmpty(cropId) ? string.Empty : cropId;
        }

        public static bool IsSeed(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && itemId.StartsWith(SeedPrefix);
        }
    }
}
