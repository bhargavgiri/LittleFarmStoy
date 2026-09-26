using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Builds the reusable vegetation and container prefabs once; the scene then instantiates
    /// them. Every prop is collapsed into a single renderer with per-material submeshes, so a
    /// treeline of forty trees costs forty draw calls rather than two hundred.
    ///
    /// The guiding rule for Phase 4A: variants must differ in SILHOUETTE, not just in scale.
    /// Five trees that are all "trunk plus a ball" read as one tree at three sizes.
    /// </summary>
    public static class PropLibrary
    {
        public const string PropFolder = ProtoAssets.PrefabsFolder + "/Props";

        public class Props
        {
            // ---- trees, five distinct silhouettes
            public GameObject TreeBroadleaf;
            public GameObject TreeTall;
            public GameObject TreeFruit;
            public GameObject TreeConifer;
            public GameObject TreeSapling;

            // ---- groundcover
            public GameObject BushRound;
            public GameObject BushWide;
            public GameObject BushSprig;
            public GameObject Boulder;
            public GameObject Rock;
            public GameObject Pebbles;
            public GameObject FlowersWhite;
            public GameObject FlowersAmber;
            public GameObject FlowersPink;
            public GameObject Reeds;

            // ---- farm containers
            public GameObject HayBale;
            public GameObject Barrel;
            public GameObject Crate;
        }

        public static Props BuildAll(ProtoPalette p)
        {
            ProtoAssets.EnsureFolder(PropFolder);

            return new Props
            {
                TreeBroadleaf = Build("Prop_TreeBroadleaf", root => Broadleaf(root, p)),
                TreeTall = Build("Prop_TreeTall", root => TallTree(root, p)),
                TreeFruit = Build("Prop_TreeFruit", root => FruitTree(root, p)),
                TreeConifer = Build("Prop_TreeConifer", root => Conifer(root, p)),
                TreeSapling = Build("Prop_TreeSapling", root => Sapling(root, p)),

                BushRound = Build("Prop_BushRound", root => BushRoundShape(root, p)),
                BushWide = Build("Prop_BushWide", root => BushWideShape(root, p)),
                BushSprig = Build("Prop_BushSprig", root => BushSprigShape(root, p)),

                Boulder = Build("Prop_Boulder", root => RockCluster(root, p, 1.6f, 3)),
                Rock = Build("Prop_Rock", root => RockCluster(root, p, 1f, 2)),
                Pebbles = Build("Prop_Pebbles", root => RockCluster(root, p, 0.42f, 4)),

                FlowersWhite = Build("Prop_FlowersWhite", root => FlowerCluster(root, p, p.White)),
                FlowersAmber = Build("Prop_FlowersAmber", root => FlowerCluster(root, p, p.Amber)),
                FlowersPink = Build("Prop_FlowersPink", root => FlowerCluster(root, p, p.Muzzle)),
                Reeds = Build("Prop_Reeds", root => Reeds(root, p)),

                HayBale = Build("Prop_HayBale", root => HayBale(root, p)),
                Barrel = Build("Prop_Barrel", root => Barrel(root, p)),
                Crate = Build("Prop_Crate", root => Crate(root, p))
            };
        }

        private static GameObject Build(string prefabName, System.Action<Transform> populate)
        {
            GameObject temp = new GameObject(prefabName);
            populate(temp.transform);

            StylizedMeshLibrary.CombineIntoSingleRenderer(temp, "Mesh_" + prefabName);

            string path = PropFolder + "/" + prefabName + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path, out bool success);
            Object.DestroyImmediate(temp);

            if (!success || asset == null)
            {
                Debug.LogError("Little Farm Story: failed to save prop prefab " + path);
                return null;
            }

            return asset;
        }

        // ================================================================ trunks

        /// <summary>
        /// A trunk with a flared base and a slight kink, so it reads as grown rather than
        /// extruded. Returns the height at which foliage should start.
        /// </summary>
        private static float Trunk(Transform root, ProtoPalette p, float height, float radius, float lean)
        {
            Mesh flare = StylizedMeshLibrary.Tapered(0.5f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.68f);

            ProtoAssets.MeshObject(flare, "Flare", root, new Vector3(0f, height * 0.09f, 0f),
                new Vector3(radius * 2.1f, height * 0.18f, radius * 2.1f), p.WoodDark, false);

            GameObject lower = ProtoAssets.MeshObject(taper, "Trunk_Lower", root,
                new Vector3(0f, height * 0.36f, 0f),
                new Vector3(radius * 1.35f, height * 0.5f, radius * 1.35f), p.WoodDark, true);
            lower.transform.localRotation = Quaternion.Euler(0f, 0f, lean * 0.35f);

            GameObject upper = ProtoAssets.MeshObject(taper, "Trunk_Upper", root,
                new Vector3(Mathf.Sin(lean * Mathf.Deg2Rad) * height * 0.3f, height * 0.76f, 0f),
                new Vector3(radius, height * 0.42f, radius), p.WoodDark, true);
            upper.transform.localRotation = Quaternion.Euler(0f, 0f, lean);

            return height;
        }

        /// <summary>Two stubby branches, which is most of what sells a tree in silhouette.</summary>
        private static void Branches(Transform root, ProtoPalette p, float height, float reach)
        {
            Mesh taper = StylizedMeshLibrary.Tapered(0.5f);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject branch = ProtoAssets.MeshObject(taper, "Branch_" + side, root,
                    new Vector3(reach * 0.5f * side, height * 0.82f, reach * 0.16f * side),
                    new Vector3(0.14f, reach, 0.14f), p.WoodDark, false);
                branch.transform.localRotation = Quaternion.Euler(0f, side * 26f, side * 62f);
            }
        }

        // ================================================================ trees

        /// <summary>Broadleaf: wide layered crown, the default farm tree.</summary>
        private static void Broadleaf(Transform root, ProtoPalette p)
        {
            const float height = 2.6f;
            Trunk(root, p, height, 0.2f, 4f);
            Branches(root, p, height, 0.9f);

            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            // Three overlapping lobes plus a sunlit cap: a canopy, not a ball.
            ProtoAssets.MeshObject(sphere, "Canopy_Core", root, new Vector3(0f, height + 0.72f, 0f),
                new Vector3(3.5f, 2.5f, 3.4f), p.LeafMid, true);

            ProtoAssets.MeshObject(sphere, "Canopy_L", root, new Vector3(-1.12f, height + 0.32f, 0.35f),
                new Vector3(2.1f, 1.7f, 2.1f), p.LeafDeep, true);

            ProtoAssets.MeshObject(sphere, "Canopy_R", root, new Vector3(1.18f, height + 0.44f, -0.42f),
                new Vector3(2.3f, 1.8f, 2.2f), p.LeafDeep, true);

            ProtoAssets.MeshObject(sphere, "Canopy_Cap", root, new Vector3(-0.22f, height + 1.5f, 0.18f),
                new Vector3(2.2f, 1.5f, 2.1f), p.LeafBright, false);

            ProtoAssets.MeshObject(sphere, "Canopy_Front", root, new Vector3(0.3f, height + 0.6f, 1.15f),
                new Vector3(1.7f, 1.4f, 1.7f), p.LeafBright, false);
        }

        /// <summary>Tall: narrow vertical column. Reads instantly against a low horizon.</summary>
        private static void TallTree(Transform root, ProtoPalette p)
        {
            const float height = 3.9f;
            Trunk(root, p, height, 0.17f, -3f);

            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            // Four stacked, shrinking lobes give a poplar-like column.
            float[] heights = { 0.5f, 1.35f, 2.1f, 2.7f };
            float[] widths = { 2.0f, 1.85f, 1.45f, 0.95f };
            float[] offsets = { 0.16f, -0.14f, 0.1f, 0f };

            for (int i = 0; i < heights.Length; i++)
            {
                Material leaf = i % 2 == 0 ? p.LeafMid : p.LeafDeep;
                ProtoAssets.MeshObject(sphere, "Tier_" + i, root,
                    new Vector3(offsets[i], height * 0.55f + heights[i], offsets[i] * 0.6f),
                    new Vector3(widths[i], widths[i] * 1.15f, widths[i]), leaf, i < 2);
            }

            ProtoAssets.MeshObject(sphere, "Tip", root, new Vector3(0f, height * 0.55f + 3.2f, 0f),
                new Vector3(0.7f, 0.9f, 0.7f), p.LeafBright, false);
        }

        /// <summary>Fruit tree: low, wide, spreading, dotted with fruit.</summary>
        private static void FruitTree(Transform root, ProtoPalette p)
        {
            const float height = 1.9f;
            Trunk(root, p, height, 0.19f, 6f);
            Branches(root, p, height, 1.1f);

            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(sphere, "Canopy_Core", root, new Vector3(0f, height + 0.42f, 0f),
                new Vector3(3.8f, 1.9f, 3.6f), p.LeafBright, true);

            ProtoAssets.MeshObject(sphere, "Canopy_L", root, new Vector3(-1.3f, height + 0.18f, 0.5f),
                new Vector3(2.2f, 1.5f, 2.1f), p.LeafMid, true);

            ProtoAssets.MeshObject(sphere, "Canopy_R", root, new Vector3(1.35f, height + 0.26f, -0.45f),
                new Vector3(2.1f, 1.5f, 2.2f), p.LeafMid, true);

            // Fruit reads as colour speckle from the gameplay camera.
            float[] fx = { -1.15f, 0.9f, 0.1f, -0.5f, 1.3f };
            float[] fz = { 0.7f, -0.85f, 1.25f, -1.1f, 0.35f };
            float[] fy = { 0.05f, 0.18f, -0.02f, 0.12f, -0.06f };

            for (int i = 0; i < fx.Length; i++)
            {
                ProtoAssets.MeshObject(sphere, "Fruit_" + i, root,
                    new Vector3(fx[i], height + 0.28f + fy[i], fz[i]),
                    Vector3.one * 0.3f, p.Tomato, false);
            }
        }

        /// <summary>Conifer: stacked cones. The only pointed silhouette in the set.</summary>
        private static void Conifer(Transform root, ProtoPalette p)
        {
            Mesh cone = StylizedMeshLibrary.Cone(10);
            Mesh taper = StylizedMeshLibrary.Tapered(0.55f);

            ProtoAssets.MeshObject(taper, "Trunk", root, new Vector3(0f, 0.55f, 0f),
                new Vector3(0.34f, 1.1f, 0.34f), p.WoodDark, true);

            float[] y = { 1.05f, 1.95f, 2.75f, 3.4f };
            float[] w = { 2.7f, 2.2f, 1.6f, 1.0f };
            float[] hgt = { 1.5f, 1.3f, 1.1f, 0.9f };

            for (int i = 0; i < y.Length; i++)
            {
                Material leaf = i % 2 == 0 ? p.LeafDeep : p.LeafMid;
                GameObject skirt = ProtoAssets.MeshObject(cone, "Skirt_" + i, root,
                    new Vector3(0f, y[i], 0f), new Vector3(w[i], hgt[i], w[i]), leaf, i < 3);
                skirt.transform.localRotation = Quaternion.Euler(0f, i * 24f, 0f);
            }
        }

        /// <summary>Sapling: small, sparse, for filling gaps without adding mass.</summary>
        private static void Sapling(Transform root, ProtoPalette p)
        {
            Mesh taper = StylizedMeshLibrary.Tapered(0.6f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(taper, "Trunk", root, new Vector3(0f, 0.5f, 0f),
                new Vector3(0.16f, 1.0f, 0.16f), p.WoodDark, false);

            ProtoAssets.MeshObject(sphere, "Canopy", root, new Vector3(0f, 1.32f, 0f),
                new Vector3(1.5f, 1.2f, 1.45f), p.LeafBright, true);

            ProtoAssets.MeshObject(sphere, "Lobe", root, new Vector3(0.42f, 1.05f, -0.28f),
                new Vector3(0.95f, 0.8f, 0.95f), p.LeafMid, false);
        }

        // ================================================================ bushes

        private static void BushRoundShape(Transform root, ProtoPalette p)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(sphere, "Body", root, new Vector3(0f, 0.46f, 0f),
                new Vector3(1.5f, 1.05f, 1.45f), p.LeafMid, true);
            ProtoAssets.MeshObject(sphere, "Lobe_A", root, new Vector3(0.44f, 0.32f, 0.28f),
                new Vector3(0.98f, 0.8f, 0.98f), p.LeafDeep, false);
            ProtoAssets.MeshObject(sphere, "Lobe_B", root, new Vector3(-0.38f, 0.3f, -0.32f),
                new Vector3(0.9f, 0.75f, 0.9f), p.LeafDeep, false);
            ProtoAssets.MeshObject(sphere, "Cap", root, new Vector3(-0.12f, 0.82f, 0.1f),
                new Vector3(0.85f, 0.62f, 0.85f), p.LeafBright, false);
        }

        /// <summary>Wide, low hedge-like mass for filling along fences and building bases.</summary>
        private static void BushWideShape(Transform root, ProtoPalette p)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            for (int i = 0; i < 4; i++)
            {
                float x = -0.78f + i * 0.52f;
                float size = 1.15f - Mathf.Abs(i - 1.5f) * 0.16f;

                ProtoAssets.MeshObject(sphere, "Mass_" + i, root,
                    new Vector3(x, 0.34f + (i % 2) * 0.08f, (i % 2 == 0 ? 0.12f : -0.14f)),
                    new Vector3(size, size * 0.72f, size * 0.92f),
                    i % 2 == 0 ? p.LeafMid : p.LeafDeep, i < 2);
            }

            ProtoAssets.MeshObject(sphere, "Cap", root, new Vector3(-0.1f, 0.62f, 0f),
                new Vector3(0.9f, 0.5f, 0.8f), p.LeafBright, false);
        }

        /// <summary>Small sprig for scattering. Cheap, breaks up bare ground.</summary>
        private static void BushSprigShape(Transform root, ProtoPalette p)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(sphere, "Body", root, new Vector3(0f, 0.24f, 0f),
                new Vector3(0.8f, 0.5f, 0.75f), p.LeafDeep, false);
            ProtoAssets.MeshObject(sphere, "Tuft", root, new Vector3(0.16f, 0.42f, -0.1f),
                new Vector3(0.55f, 0.42f, 0.55f), p.LeafBright, false);
        }

        // ================================================================ stone

        /// <summary>
        /// A cluster of pebbles at a given scale. One function covers boulders, rocks and
        /// gravel simply by changing size and count.
        /// </summary>
        private static void RockCluster(Transform root, ProtoPalette p, float scale, int count)
        {
            Mesh pebble = StylizedMeshLibrary.Pebble();

            float[] px = { 0f, 0.55f, -0.48f, 0.2f };
            float[] pz = { 0f, -0.34f, -0.18f, 0.5f };
            float[] ps = { 1f, 0.6f, 0.48f, 0.34f };
            float[] yaw = { 24f, -38f, 62f, 12f };

            for (int i = 0; i < Mathf.Min(count, 4); i++)
            {
                float s = scale * ps[i];

                GameObject rock = ProtoAssets.MeshObject(pebble, "Rock_" + i, root,
                    new Vector3(px[i] * scale, 0.3f * s, pz[i] * scale),
                    new Vector3(1.3f, 0.85f, 1.1f) * s, p.Stone, i == 0 && scale > 0.5f);

                rock.transform.localRotation = Quaternion.Euler(0f, yaw[i], (i % 2 == 0 ? 8f : -6f));
            }
        }

        // ================================================================ flowers

        /// <summary>
        /// A clump of blooms on a leafy base. Colour is a parameter so three variants share
        /// one construction and one mesh set.
        /// </summary>
        private static void FlowerCluster(Transform root, ProtoPalette p, Material bloom)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.25f);

            ProtoAssets.MeshObject(sphere, "Foliage", root, new Vector3(0f, 0.13f, 0f),
                new Vector3(0.85f, 0.32f, 0.8f), p.LeafBright, false);
            ProtoAssets.MeshObject(sphere, "Foliage_B", root, new Vector3(0.28f, 0.1f, -0.2f),
                new Vector3(0.6f, 0.26f, 0.58f), p.LeafMid, false);

            float[] xs = { -0.24f, 0.04f, 0.27f, -0.06f, 0.18f };
            float[] zs = { 0.12f, -0.22f, 0.16f, 0.28f, -0.05f };
            float[] hs = { 0.36f, 0.46f, 0.32f, 0.4f, 0.28f };

            for (int i = 0; i < xs.Length; i++)
            {
                ProtoAssets.MeshObject(box, "Stem_" + i, root,
                    new Vector3(xs[i], hs[i] * 0.5f, zs[i]),
                    new Vector3(0.035f, hs[i], 0.035f), p.LeafDeep, false);

                ProtoAssets.MeshObject(sphere, "Bloom_" + i, root,
                    new Vector3(xs[i], hs[i] + 0.05f, zs[i]),
                    new Vector3(0.19f, 0.13f, 0.19f), bloom, false);
            }
        }

        private static void Reeds(Transform root, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.25f);
            Mesh spindle = StylizedMeshLibrary.Spindle();

            float[] xs = { -0.18f, 0f, 0.16f, 0.07f, -0.08f };
            float[] zs = { 0.05f, -0.13f, 0.11f, 0.22f, -0.2f };
            float[] heights = { 0.68f, 0.95f, 0.78f, 0.55f, 0.85f };
            float[] tilts = { -9f, 4f, 11f, -5f, 7f };

            for (int i = 0; i < xs.Length; i++)
            {
                GameObject blade = ProtoAssets.MeshObject(box, "Reed_" + i, root,
                    new Vector3(xs[i], heights[i] * 0.5f, zs[i]),
                    new Vector3(0.05f, heights[i], 0.05f),
                    i % 2 == 0 ? p.LeafDeep : p.Stem, false);
                blade.transform.localRotation = Quaternion.Euler(tilts[i], i * 40f, tilts[i] * 0.6f);

                // Two of the reeds get a cattail head.
                if (i % 2 == 0)
                {
                    ProtoAssets.MeshObject(spindle, "Head_" + i, root,
                        new Vector3(xs[i], heights[i] + 0.08f, zs[i]),
                        new Vector3(0.09f, 0.24f, 0.09f), p.WoodDark, false);
                }
            }
        }

        // ================================================================ farm containers

        private static void HayBale(Transform root, ProtoPalette p)
        {
            Mesh barrel = StylizedMeshLibrary.Barrel(10);
            Mesh cylinder = StylizedMeshLibrary.Cylinder(10);

            GameObject body = ProtoAssets.MeshObject(barrel, "Bale", root, new Vector3(0f, 0.68f, 0f),
                new Vector3(1.35f, 1.5f, 1.35f), p.Straw, true);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            for (int i = 0; i < 2; i++)
            {
                GameObject band = ProtoAssets.MeshObject(cylinder, "Band_" + i, root,
                    new Vector3(0f, 0.68f, -0.34f + i * 0.68f),
                    new Vector3(1.38f, 0.06f, 1.38f), p.WoodDark, false);
                band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static void Barrel(Transform root, ProtoPalette p)
        {
            Mesh barrel = StylizedMeshLibrary.Barrel();
            Mesh cylinder = StylizedMeshLibrary.Cylinder();

            ProtoAssets.MeshObject(barrel, "Body", root, new Vector3(0f, 0.55f, 0f),
                new Vector3(0.86f, 1.1f, 0.86f), p.Wood, true);
            ProtoAssets.MeshObject(cylinder, "Band_Low", root, new Vector3(0f, 0.28f, 0f),
                new Vector3(0.88f, 0.05f, 0.88f), p.Metal, false);
            ProtoAssets.MeshObject(cylinder, "Band_High", root, new Vector3(0f, 0.82f, 0f),
                new Vector3(0.88f, 0.05f, 0.88f), p.Metal, false);
            ProtoAssets.MeshObject(cylinder, "Lid", root, new Vector3(0f, 1.09f, 0f),
                new Vector3(0.72f, 0.03f, 0.72f), p.WoodDark, false);
        }

        private static void Crate(Transform root, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh plank = StylizedMeshLibrary.Plank(0.18f);

            ProtoAssets.MeshObject(box, "Body", root, new Vector3(0f, 0.4f, 0f),
                new Vector3(0.9f, 0.8f, 0.9f), p.WoodLight, true);

            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -0.42f : 0.42f;
                float sz = (i < 2) ? -0.42f : 0.42f;
                ProtoAssets.MeshObject(box, "Post_" + i, root, new Vector3(sx, 0.4f, sz),
                    new Vector3(0.12f, 0.82f, 0.12f), p.Wood, false);
            }

            // Slats across two faces so it reads as a crate rather than a painted cube.
            for (int i = 0; i < 2; i++)
            {
                float y = 0.24f + i * 0.34f;

                GameObject slat = ProtoAssets.MeshObject(plank, "Slat_F" + i, root,
                    new Vector3(0f, y, -0.47f), new Vector3(0.1f, 0.14f, 0.86f), p.Wood, false);
                slat.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                ProtoAssets.MeshObject(plank, "Slat_S" + i, root,
                    new Vector3(0.47f, y, 0f), new Vector3(0.1f, 0.14f, 0.86f), p.Wood, false);
            }

            ProtoAssets.MeshObject(box, "Rim", root, new Vector3(0f, 0.81f, 0f),
                new Vector3(0.96f, 0.09f, 0.96f), p.Wood, false);
        }
    }
}
