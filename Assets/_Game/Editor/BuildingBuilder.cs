using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// The farm's structures. Buildings stay as hierarchies rather than combined meshes:
    /// there are only six of them, they are static-batched, and keeping the parts separate
    /// makes them far easier to adjust.
    ///
    /// The Phase 4A rule is that each landmark must be identifiable by SILHOUETTE alone.
    /// Previously every building was the same chamfered box under a triangular prism, and
    /// only the roof colour differed. Now:
    ///   Farmhouse - L-shaped plan, hipped roof, chimney, porch
    ///   Barn      - gambrel (four-slope) roof, the unmistakable barn profile
    ///   Coop      - small, raised on legs, ramp, lean-to roof
    ///   Market    - open frame, scalloped striped canopy
    ///   Silo      - tall ribbed cylinder with a conical cap
    /// </summary>
    public static class BuildingBuilder
    {
        // ================================================================ shared parts

        /// <summary>
        /// A wall block with a batter, a stone plinth and corner boards. This is the common
        /// vocabulary; the roof on top is what differentiates each building.
        /// </summary>
        private static void WallBlock(
            Transform root, ProtoPalette p, string blockName, Vector3 centre, Vector3 size,
            Material wall, bool cornerBoards = true)
        {
            Mesh trap = StylizedMeshLibrary.Trapezoid(0.965f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.18f);

            GameObject walls = ProtoAssets.MeshObject(trap, blockName, root,
                centre + new Vector3(0f, size.y * 0.5f, 0f), size, wall, true);
            walls.AddComponent<BoxCollider>();

            ProtoAssets.MeshObject(trimBox, blockName + "_Plinth", root,
                centre + new Vector3(0f, 0.17f, 0f),
                new Vector3(size.x + 0.3f, 0.34f, size.z + 0.3f), p.Stone, false);

            if (!cornerBoards)
            {
                return;
            }

            // Corner boards catch the light and stop the walls reading as a plain slab.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sz = (i < 2) ? -1f : 1f;

                ProtoAssets.MeshObject(trimBox, blockName + "_Corner" + i, root,
                    centre + new Vector3(sx * size.x * 0.485f, size.y * 0.5f, sz * size.z * 0.485f),
                    new Vector3(0.2f, size.y * 0.96f, 0.2f), p.Trim, false);
            }
        }

        /// <summary>Framed window with a mullion cross and a sill.</summary>
        private static void Window(Transform root, ProtoPalette p, Vector3 position, float scale = 1f)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.08f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);

            ProtoAssets.MeshObject(trimBox, "WindowFrame", root, position,
                new Vector3(1.02f, 1.02f, 0.12f) * scale, p.Trim, false);
            ProtoAssets.MeshObject(box, "WindowGlass", root, position + new Vector3(0f, 0f, -0.05f * scale),
                new Vector3(0.8f, 0.8f, 0.06f) * scale, p.Glass, false);
            ProtoAssets.MeshObject(box, "Mullion_V", root, position + new Vector3(0f, 0f, -0.09f * scale),
                new Vector3(0.07f, 0.84f, 0.05f) * scale, p.Trim, false);
            ProtoAssets.MeshObject(box, "Mullion_H", root, position + new Vector3(0f, 0f, -0.09f * scale),
                new Vector3(0.84f, 0.07f, 0.05f) * scale, p.Trim, false);
            ProtoAssets.MeshObject(trimBox, "Sill", root, position + new Vector3(0f, -0.58f * scale, -0.05f * scale),
                new Vector3(1.24f, 0.12f, 0.24f) * scale, p.WoodLight, false);
        }

        /// <summary>Recessed door in a frame, with a knob and a step.</summary>
        private static void Door(Transform root, ProtoPalette p, Vector3 position, float width, float height)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.08f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh cyl = StylizedMeshLibrary.Cylinder(12);

            ProtoAssets.MeshObject(trimBox, "DoorFrame", root, position + new Vector3(0f, height * 0.5f, 0f),
                new Vector3(width + 0.3f, height + 0.24f, 0.14f), p.Trim, false);
            ProtoAssets.MeshObject(box, "Door", root, position + new Vector3(0f, height * 0.5f, -0.06f),
                new Vector3(width, height, 0.1f), p.Wood, false);
            ProtoAssets.MeshObject(box, "DoorPanel", root, position + new Vector3(0f, height * 0.58f, -0.11f),
                new Vector3(width * 0.6f, height * 0.42f, 0.05f), p.WoodDark, false);
            ProtoAssets.MeshObject(sphere, "Knob", root,
                position + new Vector3(width * 0.32f, height * 0.46f, -0.14f),
                Vector3.one * 0.11f, p.Amber, false);
            ProtoAssets.MeshObject(cyl, "Step", root, position + new Vector3(0f, 0.06f, -0.5f),
                new Vector3(width + 0.8f, 0.12f, 1.1f), p.Stone, false);
        }

        // ================================================================ farmhouse

        /// <summary>
        /// The player's home: an L-shaped plan under a hipped roof, with a porch, a chimney
        /// and a dormer. The plan break and the hip are what separate it from every other
        /// building on the farm.
        /// </summary>
        public static GameObject Farmhouse(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = ProtoAssets.Empty("Farmhouse", parent, centre).transform;

            Mesh hip = StylizedMeshLibrary.HipRoof(0.42f);
            Mesh prism = StylizedMeshLibrary.RoofPrism();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.86f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            // ---- main wing, running east-west
            Vector3 mainSize = new Vector3(7.4f, 3.3f, 5.4f);
            WallBlock(root, p, "Main", Vector3.zero, mainSize, p.WallCream);

            ProtoAssets.MeshObject(hip, "Roof_Main", root, new Vector3(0f, mainSize.y + 1.05f, 0f),
                new Vector3(mainSize.x + 1.1f, 2.1f, mainSize.z + 1.1f), p.RoofTeal, true);

            // ---- cross wing, projecting south: the L that makes the plan read as a house
            Vector3 wingSize = new Vector3(3.6f, 3.0f, 3.4f);
            Vector3 wingCentre = new Vector3(-1.9f, 0f, -4.0f);
            WallBlock(root, p, "Wing", wingCentre, wingSize, p.WallWarm);

            GameObject wingRoof = ProtoAssets.MeshObject(prism, "Roof_Wing", root,
                wingCentre + new Vector3(0f, wingSize.y + 0.85f, 0f),
                new Vector3(wingSize.z + 0.9f, 1.7f, wingSize.x + 0.9f), p.RoofTeal, true);
            wingRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // ---- chimney on the main ridge
            ProtoAssets.MeshObject(box, "Chimney", root, new Vector3(2.6f, 4.9f, 0.6f),
                new Vector3(0.78f, 2.4f, 0.78f), p.RoofTerracotta, true);
            ProtoAssets.MeshObject(trimBox, "ChimneyCap", root, new Vector3(2.6f, 6.16f, 0.6f),
                new Vector3(1.02f, 0.2f, 1.02f), p.Stone, false);

            // ---- dormer in the main roof, facing the approach
            ProtoAssets.MeshObject(box, "DormerBody", root, new Vector3(1.5f, 4.05f, -2.35f),
                new Vector3(1.5f, 1.1f, 1.2f), p.WallCream, false);
            GameObject dormerRoof = ProtoAssets.MeshObject(prism, "DormerRoof", root,
                new Vector3(1.5f, 4.78f, -2.35f), new Vector3(1.9f, 0.7f, 1.6f), p.RoofTeal, false);
            dormerRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Window(root, p, new Vector3(1.5f, 4.05f, -2.96f), 0.62f);

            // ---- porch across the front of the main wing
            ProtoAssets.MeshObject(plank, "PorchDeck", root, new Vector3(1.9f, 0.16f, -3.8f),
                new Vector3(4.4f, 0.3f, 2.6f), p.WoodLight, false);

            for (int i = 0; i < 2; i++)
            {
                float x = 0.5f + i * 2.9f;

                ProtoAssets.MeshObject(taper, "PorchPost_" + i, root, new Vector3(x, 1.42f, -4.85f),
                    new Vector3(0.26f, 2.5f, 0.26f), p.Trim, true);

                ProtoAssets.MeshObject(plank, "PorchRail_" + i, root, new Vector3(x, 0.85f, -4.85f),
                    new Vector3(0.09f, 0.12f, 2.7f), p.WoodLight, false);
            }

            GameObject porchRoof = ProtoAssets.MeshObject(prism, "PorchRoof", root,
                new Vector3(1.9f, 2.95f, -4.3f), new Vector3(5.2f, 0.85f, 3.4f), p.RoofTeal, true);
            porchRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // ---- openings
            Door(root, p, new Vector3(1.9f, 0.3f, -2.72f), 1.25f, 2.15f);
            Window(root, p, new Vector3(-1.6f, 1.9f, -2.72f));
            Window(root, p, new Vector3(-3.9f, 1.75f, -5.72f), 0.85f);

            // ---- window box under the front window
            ProtoAssets.MeshObject(trimBox, "FlowerBox", root, new Vector3(-1.6f, 1.28f, -2.94f),
                new Vector3(1.35f, 0.34f, 0.46f), p.Trim, false);

            for (int i = 0; i < 4; i++)
            {
                ProtoAssets.MeshObject(sphere, "Bloom_" + i, root,
                    new Vector3(-2.06f + i * 0.31f, 1.53f, -2.94f),
                    Vector3.one * (0.2f + (i % 2) * 0.05f),
                    i % 2 == 0 ? p.Muzzle : p.Amber, false);
            }

            return root.gameObject;
        }

        // ================================================================ barn

        /// <summary>
        /// The cow barn: a tall gambrel roof over a big cross-braced door and a hayloft.
        /// The gambrel is doing the heavy lifting - no other building on the farm has that
        /// double-slope profile, so the barn is identifiable from anywhere.
        /// </summary>
        public static GameObject Barn(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = ProtoAssets.Empty("Barn", parent, centre).transform;

            Mesh gambrel = StylizedMeshLibrary.GambrelPrism(0.5f, 0.68f);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.09f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);

            Vector3 size = new Vector3(7.6f, 3.4f, 6.0f);
            WallBlock(root, p, "Walls", Vector3.zero, size, p.RoofTerracotta);

            // Gambrel ridge runs along X; the prism extrudes along Z, so no rotation needed.
            ProtoAssets.MeshObject(gambrel, "Roof", root, new Vector3(0f, size.y + 1.5f, 0f),
                new Vector3(size.x + 1.0f, 3.0f, size.z + 1.0f), p.WallCream, true);

            // Eaves boards along both long sides.
            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(trimBox, "Eave_" + side, root,
                    new Vector3(0f, size.y + 0.04f, side * (size.z + 1.0f) * 0.5f),
                    new Vector3(size.x + 1.1f, 0.24f, 0.18f), p.Trim, false);
            }

            float frontZ = -size.z * 0.5f;

            // ---- big double doors with cross-bracing
            ProtoAssets.MeshObject(trimBox, "DoorFrame", root,
                new Vector3(0f, 1.6f, frontZ - 0.06f), new Vector3(3.7f, 3.2f, 0.16f), p.Trim, false);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.83f * side;

                ProtoAssets.MeshObject(box, "Door_" + side, root,
                    new Vector3(x, 1.52f, frontZ - 0.15f),
                    new Vector3(1.58f, 2.95f, 0.1f), p.WallCream, false);

                GameObject brace = ProtoAssets.MeshObject(box, "Brace_" + side, root,
                    new Vector3(x, 1.52f, frontZ - 0.22f),
                    new Vector3(0.19f, 3.3f, 0.06f), p.Trim, false);
                brace.transform.localRotation = Quaternion.Euler(0f, 0f, side * 28f);

                ProtoAssets.MeshObject(box, "DoorEdge_" + side, root,
                    new Vector3(x + 0.76f * side, 1.52f, frontZ - 0.21f),
                    new Vector3(0.13f, 2.95f, 0.06f), p.Trim, false);

                ProtoAssets.MeshObject(box, "DoorRail_" + side, root,
                    new Vector3(x, 1.52f, frontZ - 0.21f),
                    new Vector3(1.6f, 0.13f, 0.06f), p.Trim, false);
            }

            // Sliding-door track above the doors - a small detail that reads as "barn".
            ProtoAssets.MeshObject(plank, "DoorTrack", root,
                new Vector3(0f, 3.22f, frontZ - 0.26f),
                new Vector3(0.12f, 0.12f, 4.0f), p.Metal, false);

            // ---- hayloft opening high in the gable, with a hoist beam
            ProtoAssets.MeshObject(trimBox, "LoftFrame", root,
                new Vector3(0f, 4.9f, frontZ + 0.5f), new Vector3(1.7f, 1.6f, 0.2f), p.Trim, false);
            ProtoAssets.MeshObject(box, "LoftOpening", root,
                new Vector3(0f, 4.9f, frontZ + 0.58f), new Vector3(1.34f, 1.24f, 0.1f), p.WoodDark, false);
            ProtoAssets.MeshObject(plank, "HoistBeam", root,
                new Vector3(0f, 5.95f, frontZ - 0.3f), new Vector3(0.18f, 0.18f, 1.9f), p.Trim, false);

            // ---- side windows
            Window(root, p, new Vector3(-2.7f, 2.2f, frontZ - 0.02f), 0.8f);
            Window(root, p, new Vector3(2.7f, 2.2f, frontZ - 0.02f), 0.8f);

            return root.gameObject;
        }

        // ================================================================ coop

        /// <summary>
        /// The chicken coop: small, raised on legs with a ramp and a lean-to roof. Being the
        /// only building off the ground makes it instantly distinct despite its size.
        /// </summary>
        public static GameObject Coop(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = ProtoAssets.Empty("Coop", parent, centre).transform;

            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);
            Mesh prism = StylizedMeshLibrary.RoofPrism();
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh cyl = StylizedMeshLibrary.Cylinder(10);

            const float floorY = 0.85f;

            // ---- stilts and floor
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1.5f : 1.5f;
                float sz = (i < 2) ? -1.2f : 1.2f;

                ProtoAssets.MeshObject(box, "Stilt_" + i, root,
                    new Vector3(sx, floorY * 0.5f, sz),
                    new Vector3(0.22f, floorY, 0.22f), p.WoodDark, false);
            }

            ProtoAssets.MeshObject(plank, "Floor", root, new Vector3(0f, floorY + 0.09f, 0f),
                new Vector3(3.7f, 0.18f, 3.1f), p.WoodLight, true);

            // ---- body
            Vector3 size = new Vector3(3.4f, 1.9f, 2.8f);
            GameObject walls = ProtoAssets.MeshObject(
                StylizedMeshLibrary.Trapezoid(0.97f), "Walls", root,
                new Vector3(0f, floorY + 0.18f + size.y * 0.5f, 0f), size, p.WallWarm, true);
            walls.AddComponent<BoxCollider>();

            // Plank lines on the walls.
            for (int i = 0; i < 3; i++)
            {
                ProtoAssets.MeshObject(plank, "Board_" + i, root,
                    new Vector3(0f, floorY + 0.6f + i * 0.52f, -size.z * 0.5f - 0.03f),
                    new Vector3(0.06f, 0.09f, size.x - 0.1f), p.Trim, false);
            }

            GameObject roof = ProtoAssets.MeshObject(prism, "Roof", root,
                new Vector3(0f, floorY + 0.18f + size.y + 0.62f, 0f),
                new Vector3(size.z + 0.9f, 1.25f, size.x + 0.9f), p.RoofMustard, true);
            roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // ---- pop-hole and ramp
            ProtoAssets.MeshObject(trimBox, "PopHoleFrame", root,
                new Vector3(-0.85f, floorY + 0.62f, -size.z * 0.5f - 0.04f),
                new Vector3(0.86f, 0.96f, 0.12f), p.Trim, false);
            ProtoAssets.MeshObject(box, "PopHole", root,
                new Vector3(-0.85f, floorY + 0.6f, -size.z * 0.5f - 0.09f),
                new Vector3(0.64f, 0.76f, 0.06f), p.WoodDark, false);

            GameObject ramp = ProtoAssets.MeshObject(plank, "Ramp", root,
                new Vector3(-0.85f, floorY * 0.52f, -2.5f),
                new Vector3(0.9f, 0.12f, 2.3f), p.WoodLight, false);
            ramp.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);

            for (int i = 0; i < 4; i++)
            {
                GameObject batten = ProtoAssets.MeshObject(box, "Batten_" + i, root,
                    new Vector3(-0.85f, floorY * 0.52f + 0.42f - i * 0.24f, -1.75f - i * 0.48f),
                    new Vector3(0.9f, 0.05f, 0.1f), p.Wood, false);
                batten.transform.localRotation = Quaternion.Euler(26f, 0f, 0f);
            }

            // ---- nest box bumped out of the side, with a lift-up lid
            ProtoAssets.MeshObject(box, "NestBox", root,
                new Vector3(1.98f, floorY + 0.85f, 0.35f),
                new Vector3(0.9f, 0.95f, 1.7f), p.WallCream, true);
            GameObject lid = ProtoAssets.MeshObject(plank, "NestLid", root,
                new Vector3(2.0f, floorY + 1.38f, 0.35f),
                new Vector3(1.15f, 0.12f, 1.9f), p.RoofMustard, false);
            lid.transform.localRotation = Quaternion.Euler(0f, 0f, -13f);

            // ---- small window and a perch bar
            Window(root, p, new Vector3(0.75f, floorY + 1.15f, -size.z * 0.5f - 0.04f), 0.5f);

            ProtoAssets.MeshObject(cyl, "Perch", root, new Vector3(-1.6f, 0.55f, -1.9f),
                new Vector3(0.09f, 1.1f, 0.09f), p.Wood, false);
            GameObject perchBar = ProtoAssets.MeshObject(cyl, "PerchBar", root,
                new Vector3(-0.9f, 1.1f, -1.9f), new Vector3(0.07f, 1.5f, 0.07f), p.Wood, false);
            perchBar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            ProtoAssets.MeshObject(sphere, "Finial", root,
                new Vector3(0f, floorY + 0.18f + size.y + 1.35f, 0f),
                Vector3.one * 0.24f, p.RoofTerracotta, false);

            return root.gameObject;
        }

        // ================================================================ market

        /// <summary>Open market stall: striped scalloped canopy, produce counter, hanging sign.</summary>
        public static GameObject MarketStall(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = ProtoAssets.Empty("Market", parent, centre).transform;

            Mesh box = StylizedMeshLibrary.ChamferBox(0.1f);
            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.86f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh trap = StylizedMeshLibrary.Trapezoid(1.3f);

            ProtoAssets.MeshObject(plank, "Deck", root, new Vector3(0f, 0.15f, 0f),
                new Vector3(8.8f, 0.3f, 7.4f), p.WoodLight, false);

            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -3.8f : 3.8f;
                float sz = (i < 2) ? -3.1f : 3.1f;

                GameObject post = ProtoAssets.MeshObject(taper, "Post_" + i, root,
                    new Vector3(sx, 1.75f, sz), new Vector3(0.28f, 3.3f, 0.28f), p.Trim, true);
                post.AddComponent<BoxCollider>();
            }

            // Striped canopy with a slight pitch front to back.
            for (int i = 0; i < 7; i++)
            {
                Material stripe = (i % 2 == 0) ? p.RoofTerracotta : p.WallCream;
                GameObject slab = ProtoAssets.MeshObject(box, "Canopy_" + i, root,
                    new Vector3(0f, 3.5f - Mathf.Abs(i - 3) * 0.07f, -3.3f + i * 1.1f),
                    new Vector3(9.0f, 0.2f, 1.1f), stripe, i % 2 == 0);
                slab.transform.localRotation = Quaternion.Euler((i - 3) * 2.2f, 0f, 0f);
            }

            for (int i = 0; i < 9; i++)
            {
                ProtoAssets.MeshObject(sphere, "Scallop_" + i, root,
                    new Vector3(-4.0f + i * 1.0f, 3.3f, -3.88f),
                    new Vector3(0.52f, 0.38f, 0.26f),
                    i % 2 == 0 ? p.RoofTerracotta : p.WallCream, false);
            }

            // Counter, back shelving, and produce.
            ProtoAssets.MeshObject(box, "Counter", root, new Vector3(0f, 0.82f, -2.6f),
                new Vector3(7.6f, 1.05f, 0.95f), p.Wood, true);
            ProtoAssets.MeshObject(plank, "CounterTop", root, new Vector3(0f, 1.38f, -2.6f),
                new Vector3(1.2f, 0.16f, 7.9f), p.WoodLight, false);

            for (int i = 0; i < 2; i++)
            {
                ProtoAssets.MeshObject(plank, "Shelf_" + i, root,
                    new Vector3(0f, 1.55f + i * 0.78f, 2.9f),
                    new Vector3(0.7f, 0.13f, 6.6f), p.WoodLight, false);
            }

            Material[] produce = { p.Tomato, p.Wheat, p.Corn };

            for (int c = 0; c < 3; c++)
            {
                float x = -2.4f + c * 2.4f;

                ProtoAssets.MeshObject(trap, "Crate_" + c, root, new Vector3(x, 1.62f, -2.6f),
                    new Vector3(1.25f, 0.4f, 0.85f), p.WoodLight, false);

                for (int b = 0; b < 3; b++)
                {
                    ProtoAssets.MeshObject(sphere, "Produce_" + c + "_" + b, root,
                        new Vector3(x - 0.3f + b * 0.3f, 1.9f, -2.6f + (b % 2) * 0.16f),
                        Vector3.one * 0.29f, produce[c], false);
                }

                // Matching baskets on the back shelf tie the composition together.
                ProtoAssets.MeshObject(trap, "Basket_" + c, root, new Vector3(x, 1.78f, 2.9f),
                    new Vector3(0.7f, 0.34f, 0.7f), p.Wood, false);
                ProtoAssets.MeshObject(sphere, "ShelfProduce_" + c, root,
                    new Vector3(x, 1.98f, 2.9f), Vector3.one * 0.26f, produce[c], false);
            }

            // Hanging sign on the front-left post.
            ProtoAssets.MeshObject(plank, "SignArm", root, new Vector3(-3.8f, 3.15f, -3.95f),
                new Vector3(0.1f, 0.1f, 1.3f), p.Trim, false);
            ProtoAssets.MeshObject(trimBox, "SignBoard", root, new Vector3(-3.8f, 2.6f, -4.5f),
                new Vector3(2.0f, 0.95f, 0.15f), p.Amber, true);
            ProtoAssets.MeshObject(trimBox, "SignInlay", root, new Vector3(-3.8f, 2.6f, -4.58f),
                new Vector3(1.66f, 0.64f, 0.06f), p.AmberDeep, false);

            return root.gameObject;
        }

        // ================================================================ silo

        /// <summary>Grain silo: ribbed cylinder, banded, conical cap, service ladder.</summary>
        public static GameObject Silo(Transform parent, ProtoPalette p, Vector3 centre, float height)
        {
            Transform root = ProtoAssets.Empty("Silo", parent, centre).transform;

            Mesh cylinder = StylizedMeshLibrary.Cylinder(14);
            Mesh cone = StylizedMeshLibrary.Cone(14);
            Mesh box = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);

            GameObject body = ProtoAssets.MeshObject(cylinder, "Body", root,
                new Vector3(0f, height * 0.5f, 0f),
                new Vector3(3.0f, height, 3.0f), p.Metal, true);
            body.AddComponent<BoxCollider>();

            ProtoAssets.MeshObject(cylinder, "Base", root, new Vector3(0f, 0.22f, 0f),
                new Vector3(3.5f, 0.44f, 3.5f), p.Stone, false);

            // Vertical ribs break the blank cylinder up in the light.
            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                GameObject rib = ProtoAssets.MeshObject(box, "Rib_" + i, root,
                    new Vector3(Mathf.Cos(angle) * 1.48f, height * 0.5f, Mathf.Sin(angle) * 1.48f),
                    new Vector3(0.16f, height * 0.94f, 0.16f), p.Concrete, false);
                rib.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            }

            for (int i = 1; i <= 3; i++)
            {
                ProtoAssets.MeshObject(cylinder, "Band_" + i, root,
                    new Vector3(0f, height * i / 4f, 0f),
                    new Vector3(3.16f, 0.1f, 3.16f), p.RoofTeal, false);
            }

            // Conical cap reads better against the sky than a dome.
            ProtoAssets.MeshObject(cone, "Cap", root, new Vector3(0f, height + 0.8f, 0f),
                new Vector3(3.5f, 1.6f, 3.5f), p.RoofTeal, true);
            ProtoAssets.MeshObject(cylinder, "Finial", root, new Vector3(0f, height + 1.75f, 0f),
                new Vector3(0.26f, 0.34f, 0.26f), p.Trim, false);

            // Ladder facing the yard.
            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(plank, "LadderRail_" + side, root,
                    new Vector3(0.26f * side, height * 0.5f, -1.62f),
                    new Vector3(0.08f, 0.08f, height), p.Trim, false).transform.localRotation =
                    Quaternion.Euler(90f, 0f, 0f);
            }

            int rungs = Mathf.Max(3, Mathf.RoundToInt(height / 0.85f));
            for (int i = 1; i < rungs; i++)
            {
                ProtoAssets.MeshObject(box, "Rung_" + i, root,
                    new Vector3(0f, i * height / rungs, -1.62f),
                    new Vector3(0.6f, 0.07f, 0.07f), p.Trim, false);
            }

            // Discharge chute out of the base towards the machine pads.
            GameObject chute = ProtoAssets.MeshObject(
                StylizedMeshLibrary.Tapered(0.6f), "Chute", root,
                new Vector3(-1.7f, 1.15f, 0f), new Vector3(0.7f, 1.6f, 0.7f), p.Metal, false);
            chute.transform.localRotation = Quaternion.Euler(0f, 0f, 52f);

            return root.gameObject;
        }

        // ================================================================ production shelter

        /// <summary>Open-sided shelter: the roofed pad the production machines will occupy.</summary>
        public static GameObject ProductionShelter(Transform parent, ProtoPalette p, Vector3 centre)
        {
            Transform root = ProtoAssets.Empty("Shelter", parent, centre).transform;

            Mesh trimBox = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.86f);
            Mesh prism = StylizedMeshLibrary.RoofPrism();
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);

            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -4.2f : 4.2f;
                float sz = (i < 2) ? -1.9f : 1.9f;

                GameObject post = ProtoAssets.MeshObject(taper, "Post_" + i, root,
                    new Vector3(sx, 1.45f, sz), new Vector3(0.3f, 2.9f, 0.3f), p.Trim, true);
                post.AddComponent<BoxCollider>();

                // Knee braces at each post head.
                GameObject brace = ProtoAssets.MeshObject(plank, "Brace_" + i, root,
                    new Vector3(sx - 0.5f * Mathf.Sign(sx), 2.5f, sz),
                    new Vector3(0.09f, 0.09f, 0.9f), p.Trim, false);
                brace.transform.localRotation = Quaternion.Euler(0f, 90f, sx < 0f ? 45f : -45f);
            }

            ProtoAssets.MeshObject(prism, "Roof", root, new Vector3(0f, 3.35f, 0f),
                new Vector3(9.8f, 1.4f, 5.6f), p.RoofMustard, true);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(trimBox, "Beam_" + side, root,
                    new Vector3(0f, 2.8f, side * 1.9f),
                    new Vector3(9.0f, 0.24f, 0.22f), p.Trim, false);
            }

            return root.gameObject;
        }
    }
}
