using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Buildings for the farm. Each one is a hierarchy (not combined) because buildings are
    /// few, static-batched, and easier to tweak when the parts stay separate.
    ///
    /// The shared recipe is: chamfered wall block, single-mesh pitched roof with an overhang,
    /// a fascia board, a recessed door, framed windows and a base trim. That recipe plus a
    /// distinct roof colour is what makes each zone identifiable at a glance.
    /// </summary>
    public static class BuildingBuilder
    {
        /// <summary>
        /// Core structure shared by every building. Returns the root so callers can add
        /// their own details. The door faces -Z.
        /// </summary>
        public static Transform Shell(
            Transform parent, ProtoPalette p, string buildingName, Vector3 centre,
            Vector3 size, Material wall, Material roof, float ridgeHeight, bool addWindows = true)
        {
            Transform root = ProtoAssets.Empty(buildingName, parent, centre).transform;

            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.18f);
            Mesh prism = StylizedMeshLibrary.RoofPrism();

            // ---- walls with a base plinth so the building sits into the ground
            GameObject walls = ProtoAssets.MeshObject(box, "Walls", root,
                new Vector3(0f, size.y * 0.5f, 0f), size, wall, true);
            walls.AddComponent<BoxCollider>();

            ProtoAssets.MeshObject(trimBox, "Plinth", root, new Vector3(0f, 0.16f, 0f),
                new Vector3(size.x + 0.24f, 0.32f, size.z + 0.24f), p.Stone, false);

            // ---- roof: one prism mesh, overhanging on all sides
            float overhangX = size.x + 0.9f;
            float overhangZ = size.z + 0.9f;

            ProtoAssets.MeshObject(prism, "Roof", root,
                new Vector3(0f, size.y + ridgeHeight * 0.5f, 0f),
                new Vector3(overhangX, ridgeHeight, overhangZ), roof, true);

            // Fascia boards along the eaves read as carpentry and hide the roof/wall seam.
            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(trimBox, "Fascia_" + side, root,
                    new Vector3(0f, size.y + 0.02f, side * overhangZ * 0.5f),
                    new Vector3(overhangX, 0.2f, 0.14f), p.Trim, false);
            }

            // ---- door, recessed into a frame
            float frontZ = -size.z * 0.5f;

            ProtoAssets.MeshObject(trimBox, "DoorFrame", root,
                new Vector3(0f, 1.12f, frontZ - 0.04f),
                new Vector3(1.44f, 2.24f, 0.12f), p.Trim, false);

            ProtoAssets.MeshObject(box, "Door", root,
                new Vector3(0f, 1.06f, frontZ - 0.1f),
                new Vector3(1.14f, 2.02f, 0.1f), p.Wood, false);

            ProtoAssets.MeshObject(StylizedMeshLibrary.LowPolySphere(), "DoorKnob", root,
                new Vector3(0.38f, 1.02f, frontZ - 0.18f),
                Vector3.one * 0.11f, p.Amber, false);

            if (!addWindows)
            {
                return root;
            }

            // ---- windows: frame, glass, mullion cross, sill
            float windowY = Mathf.Max(1.6f, size.y * 0.62f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = size.x * 0.3f * side;
                AddWindow(root, p, new Vector3(x, windowY, frontZ - 0.04f));
            }

            return root;
        }

        private static void AddWindow(Transform root, ProtoPalette p, Vector3 position)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.08f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);

            ProtoAssets.MeshObject(trimBox, "WindowFrame", root, position,
                new Vector3(1.02f, 1.02f, 0.1f), p.Trim, false);

            ProtoAssets.MeshObject(box, "WindowGlass", root, position + new Vector3(0f, 0f, -0.05f),
                new Vector3(0.82f, 0.82f, 0.06f), p.Glass, false);

            ProtoAssets.MeshObject(box, "Mullion_V", root, position + new Vector3(0f, 0f, -0.09f),
                new Vector3(0.07f, 0.86f, 0.05f), p.Trim, false);

            ProtoAssets.MeshObject(box, "Mullion_H", root, position + new Vector3(0f, 0f, -0.09f),
                new Vector3(0.86f, 0.07f, 0.05f), p.Trim, false);

            ProtoAssets.MeshObject(trimBox, "Sill", root, position + new Vector3(0f, -0.58f, -0.04f),
                new Vector3(1.2f, 0.12f, 0.22f), p.WoodLight, false);
        }

        // ================================================================ specific buildings

        /// <summary>Farmhouse: teal roof, chimney, porch and a flower box.</summary>
        public static GameObject Farmhouse(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = Shell(parent, p, "Farmhouse", centre,
                new Vector3(7.6f, 3.4f, 6.2f), p.WallCream, p.RoofTeal, 2.1f);

            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.85f);
            Mesh cylinder = StylizedMeshLibrary.Cylinder();

            // Chimney with a cap, offset from the ridge.
            ProtoAssets.MeshObject(box, "Chimney", root, new Vector3(2.3f, 5.1f, 0.9f),
                new Vector3(0.72f, 2.2f, 0.72f), p.RoofTerracotta, true);
            ProtoAssets.MeshObject(trimBox, "ChimneyCap", root, new Vector3(2.3f, 6.24f, 0.9f),
                new Vector3(0.94f, 0.18f, 0.94f), p.Stone, false);

            // Porch: floor, two posts and a canopy.
            ProtoAssets.MeshObject(trimBox, "PorchFloor", root, new Vector3(0f, 0.14f, -4.1f),
                new Vector3(5.2f, 0.28f, 2.2f), p.WoodLight, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(taper, "PorchPost_" + side, root,
                    new Vector3(2.1f * side, 1.32f, -4.9f),
                    new Vector3(0.24f, 2.4f, 0.24f), p.Trim, true);
            }

            ProtoAssets.MeshObject(trimBox, "PorchCanopy", root, new Vector3(0f, 2.62f, -4.5f),
                new Vector3(5.6f, 0.22f, 3.0f), p.RoofTeal, true);

            // Window box of flowers under the left window.
            ProtoAssets.MeshObject(trimBox, "FlowerBox", root, new Vector3(-2.28f, 1.5f, -3.24f),
                new Vector3(1.3f, 0.32f, 0.4f), p.Trim, false);

            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            for (int i = 0; i < 3; i++)
            {
                ProtoAssets.MeshObject(sphere, "Bloom_" + i, root,
                    new Vector3(-2.66f + i * 0.38f, 1.74f, -3.24f),
                    Vector3.one * 0.24f, i == 1 ? p.Muzzle : p.Amber, false);
            }

            ProtoAssets.MeshObject(cylinder, "Doorstep", root, new Vector3(0f, 0.06f, -3.4f),
                new Vector3(1.6f, 0.12f, 1.6f), p.Stone, false);

            return root.gameObject;
        }

        /// <summary>Chicken coop: small, mustard roof, ramp, nesting hatch and a perch.</summary>
        public static GameObject Coop(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = Shell(parent, p, "Coop", centre,
                new Vector3(3.6f, 2.2f, 3.0f), p.WallWarm, p.RoofMustard, 1.2f, addWindows: false);

            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.8f);

            // Ramp up to the entrance, with tread battens.
            GameObject ramp = ProtoAssets.MeshObject(trimBox, "Ramp", root,
                new Vector3(0f, 0.42f, -2.35f), new Vector3(1.1f, 0.12f, 2.1f), p.WoodLight, false);
            ramp.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);

            for (int i = 0; i < 3; i++)
            {
                GameObject batten = ProtoAssets.MeshObject(box, "Batten_" + i, root,
                    new Vector3(0f, 0.62f - i * 0.19f, -1.9f - i * 0.46f),
                    new Vector3(1.1f, 0.06f, 0.1f), p.Wood, false);
                batten.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            }

            // Nesting hatch on the side.
            ProtoAssets.MeshObject(trimBox, "Hatch", root, new Vector3(1.86f, 1.2f, 0.3f),
                new Vector3(0.12f, 0.9f, 1.1f), p.Trim, false);

            // Perch pole out front.
            ProtoAssets.MeshObject(taper, "PerchPost", root, new Vector3(-2.4f, 0.5f, -1.2f),
                new Vector3(0.16f, 1f, 0.16f), p.Wood, false);

            GameObject perch = ProtoAssets.MeshObject(box, "Perch", root,
                new Vector3(-2.4f, 1.0f, -1.2f), new Vector3(1.4f, 0.1f, 0.1f), p.Wood, false);
            perch.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);

            return root.gameObject;
        }

        /// <summary>Cow barn: the big terracotta-roofed landmark with hayloft doors.</summary>
        public static GameObject Barn(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = Shell(parent, p, "Barn", centre,
                new Vector3(7.2f, 3.8f, 5.6f), p.RoofTerracotta, p.WallCream, 2.4f, addWindows: false);

            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);

            float frontZ = -5.6f * 0.5f;

            // Classic cross-braced barn doors, wider than the shell's default door.
            ProtoAssets.MeshObject(trimBox, "BarnDoorFrame", root,
                new Vector3(0f, 1.5f, frontZ - 0.06f), new Vector3(3.4f, 3.0f, 0.14f), p.Trim, false);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.78f * side;

                ProtoAssets.MeshObject(box, "BarnDoor_" + side, root,
                    new Vector3(x, 1.44f, frontZ - 0.14f),
                    new Vector3(1.5f, 2.8f, 0.1f), p.WallCream, false);

                GameObject brace = ProtoAssets.MeshObject(box, "Brace_" + side, root,
                    new Vector3(x, 1.44f, frontZ - 0.21f),
                    new Vector3(0.18f, 3.1f, 0.06f), p.Trim, false);
                brace.transform.localRotation = Quaternion.Euler(0f, 0f, side * 28f);

                ProtoAssets.MeshObject(box, "DoorEdge_" + side, root,
                    new Vector3(x + 0.72f * side, 1.44f, frontZ - 0.2f),
                    new Vector3(0.12f, 2.8f, 0.06f), p.Trim, false);
            }

            // Hayloft opening up in the gable.
            ProtoAssets.MeshObject(trimBox, "LoftFrame", root,
                new Vector3(0f, 4.5f, frontZ + 0.06f), new Vector3(1.5f, 1.4f, 0.16f), p.Trim, false);
            ProtoAssets.MeshObject(box, "LoftDark", root,
                new Vector3(0f, 4.5f, frontZ + 0.14f), new Vector3(1.2f, 1.12f, 0.08f), p.WoodDark, false);

            // Hoist beam.
            ProtoAssets.MeshObject(box, "Hoist", root,
                new Vector3(0f, 5.32f, frontZ - 0.5f), new Vector3(0.16f, 0.16f, 1.3f), p.Trim, false);

            return root.gameObject;
        }

        /// <summary>Market stall: striped canopy, produce counter, hanging sign.</summary>
        public static GameObject MarketStall(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = ProtoAssets.Empty("Market", parent, centre).transform;

            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.86f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            ProtoAssets.MeshObject(trimBox, "Deck", root, new Vector3(0f, 0.14f, 0f),
                new Vector3(8.6f, 0.28f, 7.4f), p.WoodLight, false);

            // Corner posts.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -3.7f : 3.7f;
                float sz = (i < 2) ? -3.1f : 3.1f;

                GameObject post = ProtoAssets.MeshObject(taper, "Post_" + i, root,
                    new Vector3(sx, 1.68f, sz), new Vector3(0.26f, 3.2f, 0.26f), p.Trim, true);
                post.AddComponent<BoxCollider>();
            }

            // Scalloped striped canopy: alternating slabs with a slight pitch.
            for (int i = 0; i < 7; i++)
            {
                Material stripe = (i % 2 == 0) ? p.RoofTerracotta : p.WallCream;
                GameObject slab = ProtoAssets.MeshObject(box, "Canopy_" + i, root,
                    new Vector3(0f, 3.34f + Mathf.Abs(i - 3) * -0.06f, -3.3f + i * 1.1f),
                    new Vector3(8.9f, 0.18f, 1.1f), stripe, true);
                slab.transform.localRotation = Quaternion.Euler((i - 3) * 2f, 0f, 0f);
            }

            // Scallop trim hanging off the front edge.
            for (int i = 0; i < 9; i++)
            {
                ProtoAssets.MeshObject(sphere, "Scallop_" + i, root,
                    new Vector3(-4.0f + i * 1.0f, 3.16f, -3.85f),
                    new Vector3(0.5f, 0.36f, 0.24f), i % 2 == 0 ? p.RoofTerracotta : p.WallCream, false);
            }

            // Counter with a produce display.
            ProtoAssets.MeshObject(box, "Counter", root, new Vector3(0f, 0.78f, -2.6f),
                new Vector3(7.6f, 1.0f, 0.9f), p.Wood, true);
            ProtoAssets.MeshObject(trimBox, "CounterTop", root, new Vector3(0f, 1.32f, -2.6f),
                new Vector3(7.9f, 0.16f, 1.1f), p.WoodLight, false);

            Material[] produce = { p.Tomato, p.Wheat, p.Corn };
            for (int c = 0; c < 3; c++)
            {
                float x = -2.4f + c * 2.4f;

                ProtoAssets.MeshObject(trimBox, "Crate_" + c, root, new Vector3(x, 1.58f, -2.6f),
                    new Vector3(1.2f, 0.36f, 0.8f), p.WoodLight, false);

                for (int b = 0; b < 3; b++)
                {
                    ProtoAssets.MeshObject(sphere, "Produce_" + c + "_" + b, root,
                        new Vector3(x - 0.32f + b * 0.32f, 1.82f, -2.6f + (b % 2) * 0.16f),
                        Vector3.one * 0.28f, produce[c], false);
                }
            }

            // Hanging sign board on the front-left post.
            ProtoAssets.MeshObject(box, "SignArm", root, new Vector3(-3.7f, 3.0f, -3.9f),
                new Vector3(0.1f, 0.1f, 1.4f), p.Trim, false);
            ProtoAssets.MeshObject(trimBox, "SignBoard", root, new Vector3(-3.7f, 2.5f, -4.5f),
                new Vector3(1.9f, 0.9f, 0.14f), p.Amber, true);
            ProtoAssets.MeshObject(trimBox, "SignEdge", root, new Vector3(-3.7f, 2.5f, -4.56f),
                new Vector3(1.6f, 0.62f, 0.06f), p.AmberDeep, false);

            return root.gameObject;
        }

        /// <summary>Grain silo: ribbed cylinder, banded, domed cap, service ladder.</summary>
        public static GameObject Silo(Transform parent, ProtoPalette p, Vector3 centre, float height)
        {
            Transform root = ProtoAssets.Empty("Silo", parent, centre).transform;

            Mesh cylinder = StylizedMeshLibrary.Cylinder(14);
            Mesh dome = StylizedMeshLibrary.Dome(4, 14);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.2f);

            GameObject body = ProtoAssets.MeshObject(cylinder, "Body", root,
                new Vector3(0f, height * 0.5f, 0f),
                new Vector3(3.0f, height, 3.0f), p.Metal, true);
            body.AddComponent<BoxCollider>();

            ProtoAssets.MeshObject(cylinder, "Base", root, new Vector3(0f, 0.2f, 0f),
                new Vector3(3.4f, 0.2f, 3.4f), p.Stone, false);

            // Horizontal bands break the blank cylinder into readable tiers.
            for (int i = 1; i <= 3; i++)
            {
                ProtoAssets.MeshObject(cylinder, "Band_" + i, root,
                    new Vector3(0f, height * i / 4f, 0f),
                    new Vector3(3.08f, 0.07f, 3.08f), p.RoofTeal, false);
            }

            ProtoAssets.MeshObject(dome, "Cap", root, new Vector3(0f, height + 0.68f, 0f),
                new Vector3(3.3f, 1.5f, 3.3f), p.RoofTeal, true);

            ProtoAssets.MeshObject(cylinder, "Finial", root, new Vector3(0f, height + 1.6f, 0f),
                new Vector3(0.24f, 0.28f, 0.24f), p.Trim, false);

            // Ladder: two rails and rungs, on the side facing the farm.
            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(box, "LadderRail_" + side, root,
                    new Vector3(0.24f * side, height * 0.5f, -1.56f),
                    new Vector3(0.08f, height, 0.08f), p.Trim, false);
            }

            int rungs = Mathf.Max(3, Mathf.RoundToInt(height / 0.9f));
            for (int i = 1; i < rungs; i++)
            {
                ProtoAssets.MeshObject(box, "Rung_" + i, root,
                    new Vector3(0f, i * height / rungs, -1.56f),
                    new Vector3(0.56f, 0.06f, 0.06f), p.Trim, false);
            }

            return root.gameObject;
        }

        /// <summary>Open-sided production shelter: the roofed pad machines will occupy later.</summary>
        public static GameObject ProductionShelter(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = ProtoAssets.Empty("Shelter", parent, centre).transform;

            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.86f);
            Mesh prism = StylizedMeshLibrary.RoofPrism();

            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -4.2f : 4.2f;
                float sz = (i < 2) ? -1.9f : 1.9f;

                GameObject post = ProtoAssets.MeshObject(taper, "Post_" + i, root,
                    new Vector3(sx, 1.4f, sz), new Vector3(0.28f, 2.8f, 0.28f), p.Trim, true);
                post.AddComponent<BoxCollider>();
            }

            ProtoAssets.MeshObject(prism, "Roof", root, new Vector3(0f, 3.3f, 0f),
                new Vector3(9.6f, 1.3f, 5.4f), p.RoofMustard, true);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(trimBox, "Beam_" + side, root,
                    new Vector3(0f, 2.72f, side * 1.9f),
                    new Vector3(9.0f, 0.22f, 0.2f), p.Trim, false);
            }

            return root.gameObject;
        }
    }
}
