using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// The small props that do the visual storytelling: a wheelbarrow parked by the fields,
    /// a woodpile behind the house, buckets at the trough. These are what turn "a box with a
    /// roof" into "somewhere a person works".
    ///
    /// Same rules as PropLibrary: shared meshes, shared materials, one renderer per prefab.
    /// </summary>
    public static class PropLibraryExtra
    {
        public class Extras
        {
            public GameObject Wheelbarrow;
            public GameObject Bucket;
            public GameObject WateringCan;
            public GameObject Bench;
            public GameObject LogPile;
            public GameObject Sack;
            public GameObject Basket;
            public GameObject Trough;
            public GameObject Lantern;
            public GameObject ToolRack;
        }

        public static Extras BuildAll(ProtoPalette p)
        {
            ProtoAssets.EnsureFolder(PropLibrary.PropFolder);

            return new Extras
            {
                Wheelbarrow = Build("Prop_Wheelbarrow", root => Wheelbarrow(root, p)),
                Bucket = Build("Prop_Bucket", root => Bucket(root, p)),
                WateringCan = Build("Prop_WateringCan", root => WateringCan(root, p)),
                Bench = Build("Prop_Bench", root => Bench(root, p)),
                LogPile = Build("Prop_LogPile", root => LogPile(root, p)),
                Sack = Build("Prop_Sack", root => Sack(root, p)),
                Basket = Build("Prop_Basket", root => Basket(root, p)),
                Trough = Build("Prop_Trough", root => Trough(root, p)),
                Lantern = Build("Prop_Lantern", root => Lantern(root, p)),
                ToolRack = Build("Prop_ToolRack", root => ToolRack(root, p))
            };
        }

        private static GameObject Build(string prefabName, System.Action<Transform> populate)
        {
            GameObject temp = new GameObject(prefabName);
            populate(temp.transform);

            StylizedMeshLibrary.CombineIntoSingleRenderer(temp, "Mesh_" + prefabName);

            string path = PropLibrary.PropFolder + "/" + prefabName + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path, out bool success);
            Object.DestroyImmediate(temp);

            if (!success || asset == null)
            {
                Debug.LogError("Little Farm Story: failed to save prop prefab " + path);
                return null;
            }

            return asset;
        }

        // ================================================================ props

        private static void Wheelbarrow(Transform root, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.16f);
            Mesh trap = StylizedMeshLibrary.Trapezoid(1.35f);
            Mesh cyl = StylizedMeshLibrary.Cylinder(10);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);

            // Tray flares outward towards the top, like a real barrow.
            ProtoAssets.MeshObject(trap, "Tray", root, new Vector3(0f, 0.46f, 0.06f),
                new Vector3(0.62f, 0.4f, 0.92f), p.Metal, true);

            ProtoAssets.MeshObject(box, "TrayLip", root, new Vector3(0f, 0.66f, 0.06f),
                new Vector3(0.86f, 0.06f, 1.2f), p.Amber, false);

            GameObject wheel = ProtoAssets.MeshObject(cyl, "Wheel", root,
                new Vector3(0f, 0.22f, 0.72f), new Vector3(0.44f, 0.1f, 0.44f), p.Charcoal, false);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(plank, "Handle_" + side, root,
                    new Vector3(0.26f * side, 0.5f, -0.5f),
                    new Vector3(0.07f, 0.07f, 1.1f), p.Wood, false);

                GameObject leg = ProtoAssets.MeshObject(plank, "Leg_" + side, root,
                    new Vector3(0.24f * side, 0.16f, -0.34f),
                    new Vector3(0.07f, 0.07f, 0.36f), p.Wood, false);
                leg.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
            }
        }

        private static void Bucket(Transform root, ProtoPalette p)
        {
            Mesh trap = StylizedMeshLibrary.Trapezoid(1.28f);
            Mesh cyl = StylizedMeshLibrary.Cylinder(10);

            ProtoAssets.MeshObject(trap, "Body", root, new Vector3(0f, 0.19f, 0f),
                new Vector3(0.32f, 0.38f, 0.32f), p.Metal, true);
            ProtoAssets.MeshObject(cyl, "Rim", root, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.42f, 0.04f, 0.42f), p.Charcoal, false);
            ProtoAssets.MeshObject(cyl, "Handle", root, new Vector3(0f, 0.5f, 0f),
                new Vector3(0.38f, 0.03f, 0.06f), p.Charcoal, false);
        }

        private static void WateringCan(Transform root, ProtoPalette p)
        {
            Mesh cyl = StylizedMeshLibrary.Cylinder(10);
            Mesh taper = StylizedMeshLibrary.Tapered(0.5f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(cyl, "Body", root, new Vector3(0f, 0.2f, 0f),
                new Vector3(0.36f, 0.4f, 0.36f), p.RoofTeal, true);
            ProtoAssets.MeshObject(cyl, "Rim", root, new Vector3(0f, 0.41f, 0f),
                new Vector3(0.4f, 0.04f, 0.4f), p.Metal, false);

            GameObject spout = ProtoAssets.MeshObject(taper, "Spout", root,
                new Vector3(0.3f, 0.32f, 0f), new Vector3(0.11f, 0.5f, 0.11f), p.RoofTeal, false);
            spout.transform.localRotation = Quaternion.Euler(0f, 0f, -62f);

            ProtoAssets.MeshObject(sphere, "Rose", root, new Vector3(0.52f, 0.42f, 0f),
                new Vector3(0.16f, 0.1f, 0.16f), p.Metal, false);

            GameObject handle = ProtoAssets.MeshObject(cyl, "Handle", root,
                new Vector3(-0.02f, 0.52f, 0f), new Vector3(0.3f, 0.035f, 0.05f), p.Metal, false);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        private static void Bench(Transform root, ProtoPalette p)
        {
            Mesh plank = StylizedMeshLibrary.Plank(0.22f);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.18f);

            for (int i = 0; i < 2; i++)
            {
                ProtoAssets.MeshObject(plank, "Seat_" + i, root,
                    new Vector3(0f, 0.44f, -0.14f + i * 0.28f),
                    new Vector3(0.24f, 0.09f, 1.7f), p.WoodLight, true);
            }

            for (int i = 0; i < 2; i++)
            {
                ProtoAssets.MeshObject(plank, "Back_" + i, root,
                    new Vector3(0f, 0.72f + i * 0.2f, -0.3f),
                    new Vector3(0.18f, 0.07f, 1.7f), p.WoodLight, false);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(box, "Leg_" + side, root,
                    new Vector3(0f, 0.22f, 0.7f * side),
                    new Vector3(0.34f, 0.44f, 0.11f), p.Wood, false);

                ProtoAssets.MeshObject(box, "BackPost_" + side, root,
                    new Vector3(0f, 0.68f, 0.66f * side),
                    new Vector3(0.1f, 0.56f, 0.1f), p.Wood, false);
            }
        }

        private static void LogPile(Transform root, ProtoPalette p)
        {
            Mesh cyl = StylizedMeshLibrary.Cylinder(8);

            // Two rows, offset - a stacked woodpile silhouette.
            float[] rowY = { 0.19f, 0.55f };
            int[] rowCount = { 4, 3 };

            for (int r = 0; r < 2; r++)
            {
                for (int i = 0; i < rowCount[r]; i++)
                {
                    float x = -0.58f + i * 0.38f + (r == 1 ? 0.19f : 0f);

                    GameObject log = ProtoAssets.MeshObject(cyl, "Log_" + r + "_" + i, root,
                        new Vector3(x, rowY[r], 0f),
                        new Vector3(0.36f, 0.6f, 0.36f), p.WoodDark, r == 0);
                    log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                    // Pale sawn end so the stack reads as cut timber.
                    ProtoAssets.MeshObject(cyl, "End_" + r + "_" + i, root,
                        new Vector3(x, rowY[r], 0.61f),
                        new Vector3(0.3f, 0.03f, 0.3f), p.WoodLight, false);
                }
            }
        }

        private static void Sack(Transform root, ProtoPalette p)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh trap = StylizedMeshLibrary.Trapezoid(0.55f);

            ProtoAssets.MeshObject(trap, "Body", root, new Vector3(0f, 0.28f, 0f),
                new Vector3(0.56f, 0.56f, 0.5f), p.Straw, true);
            ProtoAssets.MeshObject(sphere, "Neck", root, new Vector3(0f, 0.57f, 0f),
                new Vector3(0.26f, 0.18f, 0.24f), p.Straw, false);
            ProtoAssets.MeshObject(sphere, "Tie", root, new Vector3(0f, 0.63f, 0f),
                new Vector3(0.2f, 0.07f, 0.19f), p.WoodDark, false);
        }

        private static void Basket(Transform root, ProtoPalette p)
        {
            Mesh trap = StylizedMeshLibrary.Trapezoid(1.3f);
            Mesh cyl = StylizedMeshLibrary.Cylinder(10);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(trap, "Body", root, new Vector3(0f, 0.17f, 0f),
                new Vector3(0.44f, 0.34f, 0.44f), p.WoodLight, true);
            ProtoAssets.MeshObject(cyl, "Rim", root, new Vector3(0f, 0.34f, 0f),
                new Vector3(0.6f, 0.05f, 0.6f), p.Wood, false);

            // A little produce showing over the rim.
            ProtoAssets.MeshObject(sphere, "Produce_A", root, new Vector3(-0.1f, 0.38f, 0.06f),
                Vector3.one * 0.2f, p.Tomato, false);
            ProtoAssets.MeshObject(sphere, "Produce_B", root, new Vector3(0.12f, 0.37f, -0.08f),
                Vector3.one * 0.18f, p.Corn, false);
        }

        private static void Trough(Transform root, ProtoPalette p)
        {
            Mesh trap = StylizedMeshLibrary.Trapezoid(1.22f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);

            ProtoAssets.MeshObject(trap, "Body", root, new Vector3(0f, 0.24f, 0f),
                new Vector3(0.7f, 0.48f, 2.2f), p.Wood, true);

            ProtoAssets.MeshObject(box, "Water", root, new Vector3(0f, 0.44f, 0f),
                new Vector3(0.72f, 0.06f, 2.05f), p.WaterShallow, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(plank, "Rail_" + side, root,
                    new Vector3(0.42f * side, 0.48f, 0f),
                    new Vector3(0.1f, 0.08f, 2.3f), p.WoodDark, false);

                ProtoAssets.MeshObject(box, "Leg_" + side, root,
                    new Vector3(0f, 0.09f, 0.95f * side),
                    new Vector3(0.66f, 0.18f, 0.16f), p.WoodDark, false);
            }
        }

        private static void Lantern(Transform root, ProtoPalette p)
        {
            Mesh taper = StylizedMeshLibrary.Tapered(0.85f);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.14f);
            Mesh cone = StylizedMeshLibrary.Cone(8);

            ProtoAssets.MeshObject(taper, "Post", root, new Vector3(0f, 0.85f, 0f),
                new Vector3(0.14f, 1.7f, 0.14f), p.WoodDark, true);
            ProtoAssets.MeshObject(box, "Housing", root, new Vector3(0f, 1.82f, 0f),
                new Vector3(0.26f, 0.34f, 0.26f), p.Charcoal, false);
            ProtoAssets.MeshObject(box, "Glass", root, new Vector3(0f, 1.82f, 0f),
                new Vector3(0.3f, 0.24f, 0.3f), p.Amber, false);
            ProtoAssets.MeshObject(cone, "Cap", root, new Vector3(0f, 2.06f, 0f),
                new Vector3(0.36f, 0.16f, 0.36f), p.Charcoal, false);
        }

        private static void ToolRack(Transform root, ProtoPalette p)
        {
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.8f);

            ProtoAssets.MeshObject(plank, "Board", root, new Vector3(0f, 0.9f, 0f),
                new Vector3(0.1f, 1.1f, 1.4f), p.Wood, true);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(box, "Post_" + side, root,
                    new Vector3(0f, 0.6f, 0.6f * side),
                    new Vector3(0.13f, 1.2f, 0.13f), p.WoodDark, false);
            }

            // A rake and a spade leaning on the rack.
            GameObject rake = ProtoAssets.MeshObject(taper, "RakeShaft", root,
                new Vector3(-0.16f, 0.7f, -0.34f), new Vector3(0.06f, 1.5f, 0.06f), p.WoodLight, false);
            rake.transform.localRotation = Quaternion.Euler(11f, 0f, 8f);

            ProtoAssets.MeshObject(box, "RakeHead", root, new Vector3(-0.24f, 0.02f, -0.44f),
                new Vector3(0.09f, 0.08f, 0.44f), p.Metal, false);

            GameObject spade = ProtoAssets.MeshObject(taper, "SpadeShaft", root,
                new Vector3(-0.16f, 0.68f, 0.36f), new Vector3(0.06f, 1.4f, 0.06f), p.WoodLight, false);
            spade.transform.localRotation = Quaternion.Euler(-9f, 0f, 8f);

            ProtoAssets.MeshObject(box, "SpadeBlade", root, new Vector3(-0.26f, 0.06f, 0.46f),
                new Vector3(0.05f, 0.3f, 0.22f), p.Metal, false);
        }
    }
}
