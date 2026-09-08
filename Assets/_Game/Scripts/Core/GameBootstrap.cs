using UnityEngine;

namespace LittleFarmStory.Core
{
    /// <summary>
    /// Applies runtime platform settings once at scene start: frame rate cap, screen sleep and
    /// orientation. Intentionally tiny - this is not a god-object GameManager, and gameplay
    /// systems never depend on it.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Frame rate")]
        [Tooltip("Requested frame rate on device. 60 on capable phones, 30 is the floor we design for.")]
        [SerializeField] private int targetFrameRate = 60;
        [Tooltip("Disable vSync so targetFrameRate is actually honoured.")]
        [SerializeField] private bool disableVSync = true;

        [Header("Screen")]
        [SerializeField] private bool lockPortrait = true;
        [SerializeField] private bool keepScreenAwake = true;

        private void Awake()
        {
            if (disableVSync)
            {
                QualitySettings.vSyncCount = 0;
            }

            if (targetFrameRate > 0)
            {
                Application.targetFrameRate = targetFrameRate;
            }

            if (keepScreenAwake)
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (lockPortrait && Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }
        }
    }
}
