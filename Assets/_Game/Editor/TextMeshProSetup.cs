using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Imports the TextMeshPro Essential Resources without anyone having to remember the menu.
    ///
    /// TMP's code ships with the uGUI package and compiles whether or not the resources are
    /// present, but a TMP_Text with no default font asset renders nothing at all - a silent,
    /// completely blank HUD. The scene builder therefore calls <see cref="EnsureImported"/>
    /// before it builds any text, and fails loudly rather than producing an invisible UI.
    /// </summary>
    public static class TextMeshProSetup
    {
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>Where the Essential Resources import always puts the default font asset.</summary>
        private const string DefaultFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string PackageName = "com.unity.ugui";
        private const string RelativePackagePath = "Package Resources/TMP Essential Resources.unitypackage";

        /// <summary>True when TMP has a usable default font asset in the project.</summary>
        public static bool IsImported =>
            File.Exists(SettingsPath) || AssetDatabase.LoadAssetAtPath<Object>(SettingsPath) != null;

        [MenuItem("Little Farm Story/Import TextMeshPro Essentials", false, 60)]
        public static void ImportMenu()
        {
            if (IsImported)
            {
                Debug.Log("Little Farm Story: TextMeshPro Essential Resources are already imported.");
                return;
            }

            EnsureImported();
        }

        /// <summary>
        /// Imports the resources if they are missing. Returns true when the project has them
        /// afterwards. The import is synchronous (interactive: false) so the scene builder can
        /// rely on the font existing on the next line.
        /// </summary>
        public static bool EnsureImported()
        {
            if (IsImported)
            {
                return true;
            }

            string packagePath = FindEssentialsPackage();

            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError(
                    "Little Farm Story: could not find '" + RelativePackagePath + "' inside the " +
                    PackageName + " package. Import it by hand from " +
                    "Window > TextMeshPro > Import TMP Essential Resources, then rebuild the scene.");
                return false;
            }

            Debug.Log("Little Farm Story: importing TextMeshPro Essential Resources from " + packagePath);
            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();

            if (!IsImported)
            {
                Debug.LogError(
                    "Little Farm Story: the TMP Essential Resources import did not complete. " +
                    "Unity usually finishes it on the next asset refresh - focus the Editor and " +
                    "rebuild the scene. If it still fails, import it by hand from " +
                    "Window > TextMeshPro > Import TMP Essential Resources.");
                return false;
            }

            Debug.Log("Little Farm Story: TextMeshPro Essential Resources imported.");
            return true;
        }

        /// <summary>
        /// Finds the font asset every generated label will use, deterministically.
        ///
        /// A TMP_Text added from editor code does not reliably inherit TMP's default font: the
        /// component picks one up in its own Awake, which has not run yet, and picks up nothing
        /// at all if the Essential Resources were missing when the project last loaded. Leaving
        /// that to chance is what produced a HUD of invisible labels, so the font is now looked
        /// up here and assigned explicitly to every label.
        ///
        /// Ordered cheapest-and-most-certain first. The asset path is tried before
        /// <c>TMP_Settings</c> on purpose: touching TMP_Settings when the resources are absent
        /// makes TMP log its own error, which buries the useful one.
        /// Returns null - loudly - rather than inventing a font.
        /// </summary>
        public static TMP_FontAsset ResolveDefaultFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);

            if (font != null)
            {
                return font;
            }

            // Any font asset the project does have, so a project that renamed or replaced the
            // default still builds a readable HUD.
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

            for (int i = 0; i < guids.Length; i++)
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));

                if (font != null)
                {
                    Debug.LogWarning(
                        "Little Farm Story: the default TMP font was not at " + DefaultFontPath +
                        "; using '" + font.name + "' instead.");
                    return font;
                }
            }

            if (IsImported)
            {
                font = TMP_Settings.defaultFontAsset;

                if (font != null)
                {
                    return font;
                }
            }

            Debug.LogError(
                "Little Farm Story: no TMP_FontAsset exists in this project, so every HUD label " +
                "would render blank. Import them from Window > TextMeshPro > " +
                "Import TMP Essential Resources, then rebuild the scene.");

            return null;
        }

        private static string FindEssentialsPackage()
        {
            // The resolved package lives under Library/PackageCache with a hash suffix, so the
            // folder name cannot be hard-coded.
            string cacheRoot = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");

            if (Directory.Exists(cacheRoot))
            {
                string match = Directory
                    .GetDirectories(cacheRoot, PackageName + "*")
                    .Select(directory => Path.Combine(directory, RelativePackagePath))
                    .FirstOrDefault(File.Exists);

                if (!string.IsNullOrEmpty(match))
                {
                    return match;
                }
            }

            // Embedded or local package layouts.
            string embedded = Path.Combine(
                Directory.GetCurrentDirectory(), "Packages", PackageName, RelativePackagePath);

            return File.Exists(embedded) ? embedded : null;
        }
    }
}
