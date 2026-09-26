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

        // ---- crop ids. Constants, so the shop, the crop assets and the HUD all name a crop
        // the same way; the seed and produce ids are still DERIVED from these rather than
        // written out, which is what keeps "seed_wheat" and "wheat" from ever drifting apart.
        public const string Wheat = "wheat";
        public const string Corn = "corn";
        public const string Tomato = "tomato";

        /// <summary>Produce collected from a chicken.</summary>
        public const string Egg = "egg";

        /// <summary>Produce collected from a cow.</summary>
        public const string Milk = "milk";

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

        /// <summary>
        /// The crop a seed id belongs to: "seed_wheat" -> "wheat". Empty for anything that is
        /// not a seed. Lets a system pair a seed with its produce without a lookup table.
        /// </summary>
        public static string CropFromSeed(string seedItemId)
        {
            return IsSeed(seedItemId) ? seedItemId.Substring(SeedPrefix.Length) : string.Empty;
        }

        /// <summary>Human-readable fallback for an id, used where no display name is authored.</summary>
        public static string Titlecase(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return string.Empty;
            }

            string body = IsSeed(itemId) ? CropFromSeed(itemId) + " Seeds" : itemId;
            return char.ToUpperInvariant(body[0]) + body.Substring(1);
        }
    }
}
