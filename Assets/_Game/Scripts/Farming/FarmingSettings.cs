using UnityEngine;

namespace LittleFarmStory.Farming
{
    /// <summary>
    /// Shared tuning for the farming systems. Exists mainly so growth speed can be changed
    /// in one place while developing, instead of editing every crop asset or every field.
    /// This is developer configuration - it is never surfaced in the player HUD.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FarmingSettings",
        menuName = "Little Farm Story/Farming Settings",
        order = 1)]
    public class FarmingSettings : ScriptableObject
    {
        [Header("Development")]
        [Tooltip("Multiplies crop growth speed. 1 = authored speed, 10 = ten times faster. " +
                 "Set to 1 before shipping, or leave 'Editor Only' ticked.")]
        [Min(0.01f)]
        [SerializeField] private float developmentGrowthMultiplier = 1f;

        [Tooltip("When ticked the multiplier applies in the Editor only, so a device build " +
                 "always runs at the authored speed.")]
        [SerializeField] private bool editorOnly = true;

        [Header("Ticking")]
        [Tooltip("Seconds between growth evaluations. Growth stays accurate regardless: " +
                 "elapsed time is accumulated, not sampled.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float growthTickInterval = 0.2f;

        public float GrowthTickInterval => growthTickInterval;

        /// <summary>Effective growth speed multiplier for the current runtime.</summary>
        public float GrowthSpeedMultiplier
        {
            get
            {
                if (editorOnly && !Application.isEditor)
                {
                    return 1f;
                }

                return Mathf.Max(0.01f, developmentGrowthMultiplier);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(float multiplier, bool onlyInEditor, float tickInterval)
        {
            developmentGrowthMultiplier = multiplier;
            editorOnly = onlyInEditor;
            growthTickInterval = tickInterval;
        }
#endif
    }
}
