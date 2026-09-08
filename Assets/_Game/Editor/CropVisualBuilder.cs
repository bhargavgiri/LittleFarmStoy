using System;
using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Generates the growth-stage prefabs for each crop.
    ///
    /// Two rules keep this compatible with the gameplay architecture:
    ///  - the crop shapes live here, never in FarmPlot, which only ever asks
    ///    CropDefinition for a stage prefab and parents it to CropAnchor;
    ///  - every stage prefab is collapsed into one renderer with per-material submeshes,
    ///    so a fully grown 20-plot wheat field costs about two draw calls per plot
    ///    instead of one per stalk.
    /// </summary>
    public static class CropVisualBuilder
    {
        private const string CropPrefabFolder = ProtoAssets.PrefabsFolder + "/Crops";

        /// <summary>Which plant silhouette a crop uses. Data, not behaviour.</summary>
        public enum CropShape
        {
            Grain = 0,
            Bush = 1,
            Stalk = 2
        }

        /// <summary>
        /// Builds one prefab per visual stage. Index 0 is the freshly sown seed,
        /// the last index is the mature, harvest-ready crop.
        /// </summary>
        public static GameObject[] BuildStagePrefabs(
            string cropId, ProtoPalette p, CropShape shape, Color produceColor, int stageCount)
        {
            ProtoAssets.EnsureFolder(CropPrefabFolder);
            stageCount = Mathf.Max(2, stageCount);

            Material produce = ProtoAssets.Lit("Crop_" + cropId, produceColor, ProtoAssets.Finish.Matte);
            GameObject[] prefabs = new GameObject[stageCount];

            for (int i = 0; i < stageCount; i++)
            {
                string prefabName = "Crop_" + cropId + "_Stage" + i;
                GameObject temp = new GameObject(prefabName);

                // t is 0 at the first green shoot and 1 at maturity.
                float t = stageCount > 1 ? i / (float)(stageCount - 1) : 1f;
                bool mature = i == stageCount - 1;

                if (i == 0)
                {
                    SownStage(temp.transform, p);
                }
                else
                {
                    switch (shape)
                    {
                        case CropShape.Bush:
                            BushCrop(temp.transform, p, produce, t, mature);
                            break;
                        case CropShape.Stalk:
                            StalkCrop(temp.transform, p, produce, t, mature);
                            break;
                        default:
                            GrainCrop(temp.transform, p, produce, t, mature);
                            break;
                    }
                }

                StylizedMeshLibrary.CombineIntoSingleRenderer(temp, "Mesh_" + prefabName);

                string path = CropPrefabFolder + "/" + prefabName + ".prefab";
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path, out bool success);
                UnityEngine.Object.DestroyImmediate(temp);

                if (!success || asset == null)
                {
                    Debug.LogError("Little Farm Story: failed to save crop stage prefab " + path);
                    return null;
                }

                prefabs[i] = asset;
            }

            return prefabs;
        }

        // ================================================================ stage 0

        /// <summary>Freshly sown: a mound of turned earth with three furrow ridges.</summary>
        private static void SownStage(Transform root, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.3f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            for (int i = -1; i <= 1; i++)
            {
                ProtoAssets.MeshObject(box, "Furrow_" + i, root,
                    new Vector3(i * 0.42f, 0.05f, 0f),
                    new Vector3(0.3f, 0.1f, 1.5f), p.SoilTilled, false);
            }

            // A couple of seeds showing, so "planted" is visibly different from "tilled".
            ProtoAssets.MeshObject(sphere, "Seed_A", root, new Vector3(-0.14f, 0.11f, 0.22f),
                Vector3.one * 0.09f, p.WheatStraw, false);
            ProtoAssets.MeshObject(sphere, "Seed_B", root, new Vector3(0.18f, 0.11f, -0.16f),
                Vector3.one * 0.09f, p.WheatStraw, false);
        }

        // ================================================================ grain (wheat)

        /// <summary>
        /// Wheat: a clump of slim stalks that green up, then straighten and ripen to gold
        /// with a spindle-shaped ear and a pair of whiskers at the top.
        /// </summary>
        private static void GrainCrop(Transform root, ProtoPalette p, Material produce, float t, bool mature)
        {
            Mesh stalkMesh = StylizedMeshLibrary.Tapered(0.55f);
            Mesh ear = StylizedMeshLibrary.Spindle();
            Mesh blade = StylizedMeshLibrary.ChamferBox(0.3f);

            float height = Mathf.Lerp(0.24f, 0.86f, t);
            Material stemMaterial = mature ? p.WheatStraw : p.Stem;

            // Five stalks on a small ring: dense enough to read as a clump, cheap once combined.
            int count = mature ? 5 : (t > 0.5f ? 4 : 3);

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f + 0.6f;
                float radius = 0.2f + (i % 2) * 0.08f;
                Vector3 basePos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                float stalkHeight = height * (0.86f + (i % 3) * 0.07f);
                float lean = (i % 2 == 0 ? 1f : -1f) * (mature ? 5f : 9f);

                GameObject stalk = ProtoAssets.MeshObject(stalkMesh, "Stalk_" + i, root,
                    basePos + new Vector3(0f, stalkHeight * 0.5f, 0f),
                    new Vector3(0.055f, stalkHeight, 0.055f), stemMaterial, false);
                stalk.transform.localRotation = Quaternion.Euler(lean * 0.5f, angle * Mathf.Rad2Deg, lean);

                if (mature)
                {
                    // Ripe ear, plus two whiskers that give wheat its unmistakable outline.
                    ProtoAssets.MeshObject(ear, "Ear_" + i, root,
                        basePos + new Vector3(0f, stalkHeight + 0.11f, 0f),
                        new Vector3(0.15f, 0.34f, 0.15f), produce, false);

                    for (int w = -1; w <= 1; w += 2)
                    {
                        GameObject whisker = ProtoAssets.MeshObject(blade, "Whisker_" + i + "_" + w, root,
                            basePos + new Vector3(w * 0.05f, stalkHeight + 0.32f, 0f),
                            new Vector3(0.018f, 0.17f, 0.018f), produce, false);
                        whisker.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, w * 17f);
                    }
                }
                else if (t > 0.5f)
                {
                    // Half-grown heads: green, shorter, no whiskers yet.
                    ProtoAssets.MeshObject(ear, "Bud_" + i, root,
                        basePos + new Vector3(0f, stalkHeight + 0.07f, 0f),
                        new Vector3(0.1f, 0.2f, 0.1f), p.LeafDeep, false);
                }
            }

            if (!mature)
            {
                // Low leaves at the base so early stages are not just bare sticks.
                for (int i = 0; i < 2; i++)
                {
                    GameObject leaf = ProtoAssets.MeshObject(blade, "Leaf_" + i, root,
                        new Vector3(i == 0 ? -0.24f : 0.24f, height * 0.3f, 0f),
                        new Vector3(0.26f, 0.05f, 0.09f), p.LeafMid, false);
                    leaf.transform.localRotation = Quaternion.Euler(0f, i * 64f, i == 0 ? 26f : -26f);
                }
            }
        }

        // ================================================================ bush (tomato)

        /// <summary>
        /// Tomato: a leafy bush on a cane, hung with round fruit that only appear at maturity.
        /// </summary>
        private static void BushCrop(Transform root, ProtoPalette p, Material produce, float t, bool mature)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.3f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.6f);

            float height = Mathf.Lerp(0.2f, 0.66f, t);

            // Support cane, the way tomatoes are actually staked.
            if (t > 0.45f)
            {
                ProtoAssets.MeshObject(box, "Cane", root, new Vector3(0.24f, height * 0.62f, -0.1f),
                    new Vector3(0.045f, height * 1.25f, 0.045f), p.WoodLight, false);
            }

            ProtoAssets.MeshObject(taper, "Stem", root, new Vector3(0f, height * 0.45f, 0f),
                new Vector3(0.07f, height * 0.9f, 0.07f), p.Stem, false);

            // Foliage: overlapping squashed spheres in two greens.
            int lobes = mature ? 5 : (t > 0.5f ? 4 : 2);
            float spread = Mathf.Lerp(0.16f, 0.34f, t);

            for (int i = 0; i < lobes; i++)
            {
                float angle = i / (float)lobes * Mathf.PI * 2f + 0.4f;
                float lobeSize = Mathf.Lerp(0.24f, 0.46f, t) * (0.85f + (i % 2) * 0.2f);

                ProtoAssets.MeshObject(sphere, "Leaf_" + i, root,
                    new Vector3(
                        Mathf.Cos(angle) * spread,
                        height * (0.6f + (i % 3) * 0.14f),
                        Mathf.Sin(angle) * spread),
                    new Vector3(lobeSize, lobeSize * 0.62f, lobeSize),
                    i % 2 == 0 ? p.LeafMid : p.LeafDeep, false);
            }

            if (!mature)
            {
                return;
            }

            // Fruit clusters, sized so they read from the gameplay camera.
            float[] fx = { -0.24f, 0.2f, 0.02f, -0.1f };
            float[] fz = { 0.14f, -0.06f, -0.26f, 0.28f };
            float[] fy = { 0.36f, 0.5f, 0.3f, 0.46f };

            for (int i = 0; i < 4; i++)
            {
                ProtoAssets.MeshObject(sphere, "Fruit_" + i, root,
                    new Vector3(fx[i], fy[i], fz[i]),
                    Vector3.one * (i % 2 == 0 ? 0.2f : 0.17f), produce, false);

                // Green calyx on top of each fruit.
                ProtoAssets.MeshObject(sphere, "Calyx_" + i, root,
                    new Vector3(fx[i], fy[i] + 0.09f, fz[i]),
                    new Vector3(0.1f, 0.05f, 0.1f), p.LeafDeep, false);
            }
        }

        // ================================================================ stalk (corn)

        /// <summary>
        /// Corn: tall stalks with long arching leaf blades and a cob with a tassel on top.
        /// The tallest crop, which makes the corn field readable from across the farm.
        /// </summary>
        private static void StalkCrop(Transform root, ProtoPalette p, Material produce, float t, bool mature)
        {
            Mesh taper = StylizedMeshLibrary.Tapered(0.5f);
            Mesh leafMesh = StylizedMeshLibrary.LeafBlade(4, 0.3f);
            Mesh cob = StylizedMeshLibrary.Spindle();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.3f);

            float height = Mathf.Lerp(0.3f, 1.15f, t);
            int stalks = mature ? 3 : (t > 0.5f ? 2 : 1);

            for (int i = 0; i < stalks; i++)
            {
                float angle = i / Mathf.Max(1f, stalks) * Mathf.PI * 2f + 0.9f;
                float radius = stalks == 1 ? 0f : 0.22f;
                Vector3 basePos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                float stalkHeight = height * (0.9f + (i % 2) * 0.1f);

                ProtoAssets.MeshObject(taper, "Stalk_" + i, root,
                    basePos + new Vector3(0f, stalkHeight * 0.5f, 0f),
                    new Vector3(0.085f, stalkHeight, 0.085f), p.Stem, false);

                // Three arching blades per stalk, alternating sides up the stem.
                int blades = mature ? 4 : 3;
                for (int b = 0; b < blades; b++)
                {
                    float up = stalkHeight * (0.32f + b * 0.19f);
                    float yaw = angle * Mathf.Rad2Deg + b * 96f;
                    float bladeLength = Mathf.Lerp(0.3f, 0.62f, t) * (1f - b * 0.1f);

                    GameObject leaf = ProtoAssets.MeshObject(leafMesh, "Blade_" + i + "_" + b, root,
                        basePos + new Vector3(0f, up, 0f),
                        new Vector3(0.22f, 1f, bladeLength),
                        b % 2 == 0 ? p.LeafMid : p.CornHusk, false);
                    leaf.transform.localRotation = Quaternion.Euler(-14f, yaw, 0f);
                }

                if (!mature)
                {
                    continue;
                }

                // Cob hugging the stalk, wrapped in husk, plus the tassel crown.
                Vector3 cobPos = basePos + new Vector3(
                    Mathf.Cos(angle) * 0.1f, stalkHeight * 0.62f, Mathf.Sin(angle) * 0.1f);

                GameObject ear = ProtoAssets.MeshObject(cob, "Cob_" + i, root, cobPos,
                    new Vector3(0.17f, 0.42f, 0.17f), produce, false);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);

                ProtoAssets.MeshObject(cob, "Husk_" + i, root, cobPos + new Vector3(0f, -0.06f, 0f),
                    new Vector3(0.19f, 0.32f, 0.19f), p.CornHusk, false);

                GameObject tassel = ProtoAssets.MeshObject(box, "Tassel_" + i, root,
                    basePos + new Vector3(0f, stalkHeight + 0.12f, 0f),
                    new Vector3(0.05f, 0.22f, 0.05f), p.WheatStraw, false);
                tassel.transform.localRotation = Quaternion.Euler(9f, YawSeed(i), 4f);
            }
        }

        private static float YawSeed(int index)
        {
            return index * 47f % 360f;
        }

        /// <summary>Maps a crop id to its silhouette. Adding a crop is a line here, nothing more.</summary>
        public static CropShape ShapeForCrop(string cropId)
        {
            if (string.Equals(cropId, "tomato", StringComparison.OrdinalIgnoreCase))
            {
                return CropShape.Bush;
            }

            if (string.Equals(cropId, "corn", StringComparison.OrdinalIgnoreCase))
            {
                return CropShape.Stalk;
            }

            return CropShape.Grain;
        }
    }
}
