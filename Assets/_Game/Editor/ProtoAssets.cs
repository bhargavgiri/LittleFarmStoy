using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Creation helpers for the generated farm: URP materials on a fixed set of smoothness
    /// tiers, generated UI sprites, and mesh-backed building blocks.
    ///
    /// The Box/Sphere/Cylinder/Capsule helpers keep the exact size conventions of Unity's
    /// built-in primitives (cube 1x1x1, sphere diameter 1, cylinder and capsule height 2)
    /// but are backed by the stylised low-poly meshes, so every existing call site gains the
    /// chamfered, faceted look without changing a single scale value.
    /// </summary>
    public static class ProtoAssets
    {
        public const string GameRoot = "Assets/_Game";
        public const string MaterialsFolder = GameRoot + "/Materials";
        public const string PrefabsFolder = GameRoot + "/Prefabs";
        public const string ScenesFolder = GameRoot + "/Scenes";
        public const string DataFolder = GameRoot + "/Data";
        public const string UiFolder = GameRoot + "/UI";
        public const string ArtFolder = GameRoot + "/Art";

        private const string LitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>
        /// Three surface finishes for the whole game. Keeping to a fixed set is what stops the
        /// scene turning into a hundred subtly different plastic shaders.
        /// </summary>
        public enum Finish
        {
            /// <summary>Soil, foliage, fabric, chalky paint.</summary>
            Matte = 0,

            /// <summary>Painted wood, plaster walls, tools.</summary>
            Soft = 1,

            /// <summary>Water and glass. Used sparingly.</summary>
            Sheen = 2
        }

        private static float SmoothnessFor(Finish finish)
        {
            switch (finish)
            {
                case Finish.Sheen: return 0.42f;
                case Finish.Soft: return 0.12f;
                default: return 0.03f;
            }
        }

        public static void EnsureFolders()
        {
            EnsureFolder(GameRoot);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(UiFolder);
            EnsureFolder(ArtFolder);
            EnsureFolder(ArtFolder + "/Meshes");
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ---------------------------------------------------------------- materials

        public static Material Lit(string materialName, Color color, Finish finish = Finish.Matte)
        {
            return CreateLit(materialName, color, SmoothnessFor(finish));
        }

        /// <summary>Explicit-smoothness overload, for the rare surface that needs its own value.</summary>
        public static Material Lit(string materialName, Color color, float smoothness)
        {
            return CreateLit(materialName, color, smoothness);
        }

        private static Material CreateLit(string materialName, Color color, float smoothness)
        {
            string path = MaterialsFolder + "/M_" + materialName + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                Debug.LogError("Could not find shader '" + LitShaderName + "'. Is the Universal Render Pipeline package installed?");
                shader = Shader.Find("Standard");
            }

            Material material = existing != null ? existing : new Material(shader);
            material.shader = shader;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_SpecularHighlights"))
            {
                // Stylised surfaces read better without a hot specular dot.
                material.SetFloat("_SpecularHighlights", smoothness > 0.3f ? 1f : 0f);
            }

            material.enableInstancing = true;

            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // ---------------------------------------------------------------- renderer setup

        /// <summary>
        /// The mobile renderer defaults used everywhere: no light probes, no reflection probes,
        /// no motion vectors, and shadow casting only where a silhouette actually matters.
        /// </summary>
        public static void ApplyMobileRendererSettings(Renderer renderer, bool castShadows)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }

        // ---------------------------------------------------------------- mesh objects

        /// <summary>Creates a GameObject rendering the given shared mesh.</summary>
        public static GameObject MeshObject(
            Mesh mesh,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool castShadows = true)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            ApplyMobileRendererSettings(renderer, castShadows);
            return go;
        }

        // ---------------------------------------------------------------- primitive-compatible helpers

        /// <summary>Chamfered cube. Same 1x1x1 size convention as PrimitiveType.Cube.</summary>
        public static GameObject Box(
            string objectName, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool keepCollider = false, bool castShadows = true)
        {
            GameObject go = MeshObject(
                StylizedMeshLibrary.ChamferBox(), objectName, parent, position, scale, material, castShadows);

            if (keepCollider)
            {
                go.AddComponent<BoxCollider>();
            }

            return go;
        }

        /// <summary>Low-poly sphere. Same diameter-1 convention as PrimitiveType.Sphere.</summary>
        public static GameObject Sphere(
            string objectName, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool keepCollider = false, bool castShadows = true)
        {
            GameObject go = MeshObject(
                StylizedMeshLibrary.LowPolySphere(), objectName, parent, position, scale, material, castShadows);

            if (keepCollider)
            {
                SphereCollider collider = go.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
            }

            return go;
        }

        /// <summary>Faceted cylinder. Same height-2 convention as PrimitiveType.Cylinder.</summary>
        public static GameObject Cylinder(
            string objectName, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool keepCollider = false, bool castShadows = true)
        {
            GameObject go = MeshObject(
                StylizedMeshLibrary.UnityStyleCylinder(), objectName, parent, position, scale, material, castShadows);

            if (keepCollider)
            {
                CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
                collider.radius = 0.5f;
                collider.height = 2f;
            }

            return go;
        }

        /// <summary>Faceted capsule. Same height-2 convention as PrimitiveType.Capsule.</summary>
        public static GameObject Capsule(
            string objectName, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool keepCollider = false, bool castShadows = true)
        {
            GameObject go = MeshObject(
                StylizedMeshLibrary.UnityStyleCapsule(), objectName, parent, position, scale, material, castShadows);

            if (keepCollider)
            {
                CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
                collider.radius = 0.5f;
                collider.height = 2f;
            }

            return go;
        }

        public static GameObject Empty(string objectName, Transform parent, Vector3 position)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go;
        }

        public static void MarkStatic(GameObject root)
        {
            StaticEditorFlags flags =
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        // ---------------------------------------------------------------- sprites

        /// <summary>
        /// Generates a soft-edged circle (or ring) sprite PNG so the prototype UI has rounded
        /// shapes without shipping any external art.
        /// </summary>
        public static Sprite CircleSprite(string spriteName, int size = 128, float innerRadius01 = 0f)
        {
            string path = UiFolder + "/" + spriteName + ".png";
            if (!File.Exists(path))
            {
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float outer = size * 0.5f - 1f;
                float inner = outer * innerRadius01;
                Vector2 centre = new Vector2(size * 0.5f, size * 0.5f);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                        float alpha = Mathf.Clamp01(outer - d);

                        if (inner > 0f)
                        {
                            alpha = Mathf.Min(alpha, Mathf.Clamp01(d - inner));
                        }

                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureSpriteImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Generates a 9-sliced rounded rectangle sprite for HUD panels.</summary>
        public static Sprite RoundedBoxSprite(string spriteName, int size = 64, int corner = 20)
        {
            string path = UiFolder + "/" + spriteName + ".png";
            if (!File.Exists(path))
            {
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float radius = corner;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = Mathf.Min(x + 0.5f, size - x - 0.5f);
                        float py = Mathf.Min(y + 0.5f, size - y - 0.5f);
                        float alpha;

                        if (px >= radius || py >= radius)
                        {
                            alpha = 1f;
                        }
                        else
                        {
                            float dx = radius - px;
                            float dy = radius - py;
                            alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                        }

                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureSpriteImporter(path, new Vector4(corner, corner, corner, corner));
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Rounded rectangle with a solid inner fill and a darker outline band, so HUD panels
        /// read as deliberate UI rather than default Unity rectangles.
        /// </summary>
        public static Sprite OutlinedBoxSprite(
            string spriteName, int size = 96, int corner = 28, int outline = 5)
        {
            string path = UiFolder + "/" + spriteName + ".png";
            if (!File.Exists(path))
            {
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float radius = corner;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float px = Mathf.Min(x + 0.5f, size - x - 0.5f);
                        float py = Mathf.Min(y + 0.5f, size - y - 0.5f);

                        // Signed distance to the rounded-rect edge: negative inside.
                        float distance;
                        if (px >= radius || py >= radius)
                        {
                            distance = -Mathf.Min(px, py);
                        }
                        else
                        {
                            float dx = radius - px;
                            float dy = radius - py;
                            distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                        }

                        float alpha = Mathf.Clamp01(-distance);

                        // White fill inside, black band in the outline ring. The UI tints the
                        // fill; the band stays dark because it is multiplied down in alpha.
                        float bandBlend = Mathf.Clamp01((distance + outline) / Mathf.Max(1f, outline));
                        float luminance = Mathf.Lerp(1f, 0.35f, bandBlend);

                        texture.SetPixel(x, y, new Color(luminance, luminance, luminance, alpha));
                    }
                }

                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureSpriteImporter(path, new Vector4(corner, corner, corner, corner));
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureSpriteImporter(string path, Vector4? border = null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (border.HasValue && importer.spriteBorder != border.Value)
            {
                importer.spriteBorder = border.Value;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- layers

        /// <summary>
        /// Ensures a user layer exists and returns its index, or -1 when all 24 slots are taken.
        /// </summary>
        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return -1;
            }

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null)
            {
                return -1;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            Debug.LogWarning("No free user layer slot available for '" + layerName + "'.");
            return -1;
        }
    }
}
