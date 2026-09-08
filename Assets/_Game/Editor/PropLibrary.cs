using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Builds the reusable decoration prefabs once, then the scene instantiates them.
    /// Every prop is collapsed into a single renderer with per-material submeshes, so a farm
    /// with dozens of trees and bushes stays cheap.
    /// </summary>
    public static class PropLibrary
    {
        public const string PropFolder = ProtoAssets.PrefabsFolder + "/Props";

        public class Props
        {
            public GameObject TreeRound;
            public GameObject TreeTall;
            public GameObject TreeYoung;
            public GameObject BushLarge;
            public GameObject BushSmall;
            public GameObject Rock;
            public GameObject RockSmall;
            public GameObject Flowers;
            public GameObject HayBale;
            public GameObject Barrel;
            public GameObject Crate;
            public GameObject Reeds;
        }

        public static Props BuildAll(ProtoPalette p)
        {
            ProtoAssets.EnsureFolder(PropFolder);

            return new Props
            {
                TreeRound = Build("Prop_TreeRound", root => Tree(root, p, 2.3f, 1.85f, 0)),
                TreeTall = Build("Prop_TreeTall", root => Tree(root, p, 3.2f, 1.45f, 1)),
                TreeYoung = Build("Prop_TreeYoung", root => Tree(root, p, 1.5f, 1.15f, 2)),
                BushLarge = Build("Prop_BushLarge", root => Bush(root, p, 1f)),
                BushSmall = Build("Prop_BushSmall", root => Bush(root, p, 0.66f)),
                Rock = Build("Prop_Rock", root => Rock(root, p, 1f)),
                RockSmall = Build("Prop_RockSmall", root => Rock(root, p, 0.55f)),
                Flowers = Build("Prop_Flowers", root => Flowers(root, p)),
                HayBale = Build("Prop_HayBale", root => HayBale(root, p)),
                Barrel = Build("Prop_Barrel", root => Barrel(root, p)),
                Crate = Build("Prop_Crate", root => Crate(root, p)),
                Reeds = Build("Prop_Reeds", root => Reeds(root, p))
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

        // ================================================================ vegetation

        /// <summary>
        /// Stylised tree: a tapered trunk with a root flare and an overlapping cluster of
        /// faceted spheres. Three tonal greens give the crown depth without any texture.
        /// </summary>
        private static void Tree(Transform root, ProtoPalette p, float trunkHeight, float crownRadius, int variant)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh taper = StylizedMeshLibrary.Tapered(0.62f);
            Mesh flare = StylizedMeshLibrary.Tapered(0.45f);

            // Root flare, then the trunk proper.
            ProtoAssets.MeshObject(flare, "Flare", root, new Vector3(0f, 0.14f, 0f),
                new Vector3(0.62f, 0.28f, 0.62f), p.WoodDark, false);

            ProtoAssets.MeshObject(taper, "Trunk", root, new Vector3(0f, trunkHeight * 0.5f + 0.1f, 0f),
                new Vector3(0.38f, trunkHeight, 0.38f), p.WoodDark, true);

            float crownY = trunkHeight + crownRadius * 0.62f;

            // Main mass, slightly squashed so it reads as a canopy rather than a ball.
            ProtoAssets.MeshObject(sphere, "Crown", root, new Vector3(0f, crownY, 0f),
                new Vector3(crownRadius * 2f, crownRadius * 1.62f, crownRadius * 2f), p.LeafMid, true);

            // Two offset lobes break the outline.
            float lobe = crownRadius * 1.18f;
            ProtoAssets.MeshObject(sphere, "Lobe_A", root,
                new Vector3(crownRadius * 0.62f, crownY - crownRadius * 0.34f, crownRadius * 0.34f),
                new Vector3(lobe, lobe * 0.88f, lobe), p.LeafDeep, false);

            ProtoAssets.MeshObject(sphere, "Lobe_B", root,
                new Vector3(-crownRadius * 0.58f, crownY - crownRadius * 0.2f, -crownRadius * 0.42f),
                new Vector3(lobe * 0.92f, lobe * 0.84f, lobe * 0.92f), p.LeafDeep, false);

            // Sunlit cap catches the key light from above.
            ProtoAssets.MeshObject(sphere, "Highlight", root,
                new Vector3(-crownRadius * 0.16f, crownY + crownRadius * 0.56f, crownRadius * 0.12f),
                Vector3.one * (crownRadius * 1.1f), p.LeafBright, false);

            if (variant == 1)
            {
                // Tall variant gets a second, smaller tier for a distinct silhouette.
                ProtoAssets.MeshObject(sphere, "Tier", root,
                    new Vector3(0f, crownY + crownRadius * 1.05f, 0f),
                    Vector3.one * (crownRadius * 1.15f), p.LeafMid, false);
            }
        }

        private static void Bush(Transform root, ProtoPalette p, float scale)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(sphere, "Body", root, new Vector3(0f, 0.42f * scale, 0f),
                new Vector3(1.5f, 0.98f, 1.4f) * scale, p.LeafMid, true);

            ProtoAssets.MeshObject(sphere, "Lobe", root,
                new Vector3(0.42f * scale, 0.3f * scale, 0.26f * scale),
                new Vector3(0.95f, 0.78f, 0.95f) * scale, p.LeafDeep, false);

            ProtoAssets.MeshObject(sphere, "Cap", root,
                new Vector3(-0.2f * scale, 0.72f * scale, -0.1f * scale),
                new Vector3(0.8f, 0.6f, 0.8f) * scale, p.LeafBright, false);
        }

        private static void Flowers(Transform root, ProtoPalette p)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh disc = StylizedMeshLibrary.Disc(8);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.2f);

            ProtoAssets.MeshObject(sphere, "Tuft", root, new Vector3(0f, 0.12f, 0f),
                new Vector3(0.75f, 0.3f, 0.7f), p.LeafBright, false);

            Material[] petals = { p.Amber, p.Muzzle, p.White };
            float[] xs = { -0.22f, 0.05f, 0.26f };
            float[] zs = { 0.1f, -0.2f, 0.16f };
            float[] heights = { 0.34f, 0.42f, 0.3f };

            for (int i = 0; i < 3; i++)
            {
                ProtoAssets.MeshObject(box, "Stem_" + i, root,
                    new Vector3(xs[i], heights[i] * 0.5f, zs[i]),
                    new Vector3(0.035f, heights[i], 0.035f), p.LeafDeep, false);

                ProtoAssets.MeshObject(disc, "Petal_" + i, root,
                    new Vector3(xs[i], heights[i] + 0.02f, zs[i]),
                    new Vector3(0.2f, 1f, 0.2f), petals[i], false);
            }
        }

        private static void Reeds(Transform root, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.25f);

            float[] xs = { -0.16f, 0f, 0.15f, 0.06f };
            float[] zs = { 0.04f, -0.12f, 0.1f, 0.2f };
            float[] heights = { 0.62f, 0.85f, 0.7f, 0.5f };
            float[] tilts = { -9f, 4f, 11f, -5f };

            for (int i = 0; i < xs.Length; i++)
            {
                GameObject blade = ProtoAssets.MeshObject(box, "Reed_" + i, root,
                    new Vector3(xs[i], heights[i] * 0.5f, zs[i]),
                    new Vector3(0.05f, heights[i], 0.05f),
                    i % 2 == 0 ? p.LeafDeep : p.Stem, false);
                blade.transform.localRotation = Quaternion.Euler(tilts[i], i * 40f, tilts[i] * 0.6f);
            }
        }

        // ================================================================ stone

        private static void Rock(Transform root, ProtoPalette p, float scale)
        {
            Mesh pebble = StylizedMeshLibrary.Pebble();

            GameObject main = ProtoAssets.MeshObject(pebble, "Main", root,
                new Vector3(0f, 0.3f * scale, 0f),
                new Vector3(1.3f, 0.82f, 1.05f) * scale, p.Stone, true);
            main.transform.localRotation = Quaternion.Euler(0f, 24f, 7f);

            GameObject chip = ProtoAssets.MeshObject(pebble, "Chip", root,
                new Vector3(0.46f * scale, 0.14f * scale, -0.3f * scale),
                new Vector3(0.62f, 0.44f, 0.58f) * scale, p.Stone, false);
            chip.transform.localRotation = Quaternion.Euler(0f, -38f, -12f);
        }

        // ================================================================ farm containers

        private static void HayBale(Transform root, ProtoPalette p)
        {
            Mesh barrel = StylizedMeshLibrary.Barrel(10);
            Mesh cylinder = StylizedMeshLibrary.Cylinder(10);

            // Laid on its side, the way a round bale actually sits.
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
                new Vector3(0.86f, 0.05f, 0.86f), p.Metal, false);

            ProtoAssets.MeshObject(cylinder, "Band_High", root, new Vector3(0f, 0.82f, 0f),
                new Vector3(0.86f, 0.05f, 0.86f), p.Metal, false);

            ProtoAssets.MeshObject(cylinder, "Lid", root, new Vector3(0f, 1.09f, 0f),
                new Vector3(0.72f, 0.03f, 0.72f), p.WoodDark, false);
        }

        private static void Crate(Transform root, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);

            ProtoAssets.MeshObject(box, "Body", root, new Vector3(0f, 0.4f, 0f),
                new Vector3(0.9f, 0.8f, 0.9f), p.WoodLight, true);

            // Corner posts and a lid rim give it carpentry rather than a plain cube.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -0.42f : 0.42f;
                float sz = (i < 2) ? -0.42f : 0.42f;
                ProtoAssets.MeshObject(box, "Post_" + i, root, new Vector3(sx, 0.4f, sz),
                    new Vector3(0.12f, 0.82f, 0.12f), p.Wood, false);
            }

            ProtoAssets.MeshObject(box, "Rim", root, new Vector3(0f, 0.8f, 0f),
                new Vector3(0.96f, 0.09f, 0.96f), p.Wood, false);
        }
    }
}
