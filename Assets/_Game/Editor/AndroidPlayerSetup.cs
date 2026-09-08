using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Applies the Android / portrait player settings this project targets.
    /// Everything here is reversible from Project Settings; nothing is destructive.
    /// </summary>
    public static class AndroidPlayerSetup
    {
        private const string DefaultBundleId = "com.littlefarmstory.game";

        [MenuItem("Little Farm Story/Apply Android Player Settings", false, 40)]
        public static void Apply()
        {
            // --- orientation: portrait only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // --- rendering
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3
            });

            // --- android build config
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            string bundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrEmpty(bundleId) || bundleId.Contains("DefaultCompany"))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, DefaultBundleId);
                Debug.Log("Little Farm Story: Android application id set to " + DefaultBundleId + " (change it to your own studio id before publishing).");
            }

            // --- quality: Android already defaults to the Mobile quality level in this project
            QualitySettings.vSyncCount = 0;

            AssetDatabase.SaveAssets();
            Debug.Log("Little Farm Story: Android player settings applied (portrait, Linear, Vulkan+GLES3, IL2CPP, ARM64, minSdk 26).");
        }
    }
}
