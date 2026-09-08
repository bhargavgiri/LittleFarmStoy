using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Tunes the project's existing URP asset for the farm's look and camera distance.
    ///
    /// These are edits to YOUR settings asset, not a replacement - the asset is loaded as a
    /// plain ScriptableObject and edited through SerializedObject, so this file needs no URP
    /// assembly reference and cannot break if the package version changes shape. Every value
    /// is listed in the log so it can be reverted by hand.
    /// </summary>
    public static class UrpQualitySetup
    {
        private const string MobileRenderPipelinePath = "Assets/Settings/Mobile_RPAsset.asset";
        private const string PcRenderPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("Little Farm Story/Apply URP Quality Settings", false, 41)]
        public static void Apply()
        {
            ApplyTo(MobileRenderPipelinePath, isMobileTier: true);
            ApplyTo(PcRenderPipelinePath, isMobileTier: false);
            AssetDatabase.SaveAssets();
        }

        private static void ApplyTo(string path, bool isMobileTier)
        {
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null)
            {
                Debug.LogWarning("Little Farm Story: could not find the render pipeline asset at " + path + "; skipping.");
                return;
            }

            SerializedObject so = new SerializedObject(asset);
            int applied = 0;

            // The gameplay camera now sits 26 units from its focus, so a 50 unit shadow
            // distance would drop shadows out in the middle of the screen.
            applied += SetFloat(so, "m_ShadowDistance", 78f);

            // The Phase 0 audit found soft shadows requested on the light but disabled here,
            // which silently rendered them hard. The stylised look needs them soft.
            applied += SetBool(so, "m_SoftShadowsSupported", true);
            applied += SetInt(so, "m_SoftShadowQuality", 1);

            // One cascade at 78 units needs more resolution than 1024 to avoid blocky edges.
            applied += SetInt(so, "m_MainLightShadowmapResolution", 2048);

            // Nothing in the game bloom-tests or tone-maps, so HDR is pure bandwidth cost.
            applied += SetBool(so, "m_SupportsHDR", false);

            if (isMobileTier)
            {
                // 0.8 render scale was visibly soft on a portrait phone. 0.9 keeps most of the
                // saving while sharpening the UI-adjacent edges. Lower it again if you need
                // headroom on low-end devices.
                applied += SetFloat(so, "m_RenderScale", 0.9f);
            }

            if (applied > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);

                Debug.Log("Little Farm Story: updated " + applied + " render pipeline settings in " + path +
                          " (shadow distance 78, soft shadows on, shadow map 2048, HDR off" +
                          (isMobileTier ? ", render scale 0.9" : "") + ").");
            }
        }

        private static int SetFloat(SerializedObject so, string property, float value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null || Mathf.Approximately(p.floatValue, value))
            {
                return 0;
            }

            p.floatValue = value;
            return 1;
        }

        private static int SetInt(SerializedObject so, string property, int value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null || p.intValue == value)
            {
                return 0;
            }

            p.intValue = value;
            return 1;
        }

        private static int SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                return 0;
            }

            // Some of these are serialized as ints rather than bools depending on the version.
            if (p.propertyType == SerializedPropertyType.Boolean)
            {
                if (p.boolValue == value)
                {
                    return 0;
                }

                p.boolValue = value;
                return 1;
            }

            if (p.propertyType == SerializedPropertyType.Integer)
            {
                int target = value ? 1 : 0;
                if (p.intValue == target)
                {
                    return 0;
                }

                p.intValue = target;
                return 1;
            }

            return 0;
        }
    }
}
