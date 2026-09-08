using LittleFarmStory.Inventory;
using UnityEngine;

namespace LittleFarmStory.Farming
{
    /// <summary>
    /// Designer-authored configuration for one crop type.
    /// FarmPlot runs a single generic lifecycle against this data, so adding a crop means
    /// authoring an asset - never editing plot code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Crop_",
        menuName = "Little Farm Story/Crop Definition",
        order = 0)]
    public class CropDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by save data. Never rename this once shipped.")]
        [SerializeField] private string cropId = "wheat";
        [SerializeField] private string displayName = "Wheat";
        [Tooltip("Inventory id consumed when planting. Leave blank to derive 'seed_<cropId>'.")]
        [SerializeField] private string seedItemId = "";
        [Tooltip("Inventory id granted when harvesting. Leave blank to use the crop id.")]
        [SerializeField] private string harvestItemId = "";

        [Header("Growth")]
        [Tooltip("Growth steps after sowing. Visual stages are this + 1 (stage 0 is the freshly sown seed).")]
        [Min(1)] [SerializeField] private int growthStages = 3;
        [Tooltip("Authored seconds per growth step, before any development multiplier.")]
        [Min(0.1f)] [SerializeField] private float secondsPerStage = 6f;
        [Tooltip("One prefab per visual stage, index 0 = freshly sown. Length should be Growth Stages + 1.")]
        [SerializeField] private GameObject[] stagePrefabs;

        [Header("Economy")]
        [Min(0)] [SerializeField] private int seedCost = 5;
        [Min(0)] [SerializeField] private int sellValue = 12;
        [Min(1)] [SerializeField] private int yieldMin = 1;
        [Min(1)] [SerializeField] private int yieldMax = 3;

        [Header("Presentation")]
        [SerializeField] private Color cropColor = new Color(0.93f, 0.78f, 0.28f);
        [SerializeField] private Color soilColor = new Color(0.42f, 0.29f, 0.19f);

        public string CropId => cropId;

        public string DisplayName => displayName;

        public string SeedItemId =>
            string.IsNullOrEmpty(seedItemId) ? ItemIds.Seed(cropId) : seedItemId;

        public string HarvestItemId =>
            string.IsNullOrEmpty(harvestItemId) ? ItemIds.Harvest(cropId) : harvestItemId;

        /// <summary>Number of growth steps after sowing. Stage index runs 0..GrowthStages.</summary>
        public int GrowthStages => Mathf.Max(1, growthStages);

        /// <summary>Total visual stages, including stage 0.</summary>
        public int VisualStageCount => GrowthStages + 1;

        public float SecondsPerStage => Mathf.Max(0.1f, secondsPerStage);

        public float TotalGrowthSeconds => GrowthStages * SecondsPerStage;

        public int SeedCost => seedCost;

        public int SellValue => sellValue;

        public int YieldMin => Mathf.Max(1, yieldMin);

        public int YieldMax => Mathf.Max(YieldMin, yieldMax);

        public Color CropColor => cropColor;

        public Color SoilColor => soilColor;

        /// <summary>Random yield within the authored range, inclusive of both bounds.</summary>
        public int RollYield()
        {
            return Random.Range(YieldMin, YieldMax + 1);
        }

        /// <summary>Visual prefab for a stage index, or null when none is authored.</summary>
        public GameObject GetStagePrefab(int stageIndex)
        {
            if (stagePrefabs == null || stagePrefabs.Length == 0)
            {
                return null;
            }

            int clamped = Mathf.Clamp(stageIndex, 0, stagePrefabs.Length - 1);
            return stagePrefabs[clamped];
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(
            string id, string display, Color crop, Color soil, int seed, int sell)
        {
            cropId = id;
            displayName = display;
            cropColor = crop;
            soilColor = soil;
            seedCost = seed;
            sellValue = sell;
        }

        /// <summary>Editor-only authoring helper for the Phase 2 growth and yield data.</summary>
        public void EditorConfigureGrowth(
            int stages, float secondsPerGrowthStage, int minYield, int maxYield, GameObject[] stageVisuals)
        {
            growthStages = Mathf.Max(1, stages);
            secondsPerStage = Mathf.Max(0.1f, secondsPerGrowthStage);
            yieldMin = Mathf.Max(1, minYield);
            yieldMax = Mathf.Max(yieldMin, maxYield);
            stagePrefabs = stageVisuals;
        }
#endif
    }
}
