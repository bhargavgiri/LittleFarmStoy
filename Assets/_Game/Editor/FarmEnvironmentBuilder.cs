using LittleFarmStory.World;
using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// The farm ground, the path network, the boundary and the pond.
    /// Everything is built from the shared stylised meshes; there is not a single texture
    /// anywhere in the 3D scene.
    /// </summary>
    public static class FarmEnvironmentBuilder
    {
        /// <summary>
        /// The visible ground is far larger than the fenced farm: at the gameplay camera pitch
        /// the view reaches roughly 20 m past the focus point, and the focus is clamped to
        /// +/-26, so anything smaller lets the player see the world edge.
        /// </summary>
        public const float GroundHalfSize = 62f;

        public const float BoundaryHalfSize = 31f;
        public const float PathY = 0.04f;
        public const float PadY = 0.06f;

        private const float PathWidth = 5.4f;
        private const float SpurWidth = 3.9f;

        // ================================================================ ground

        public static void BuildGround(Transform parent, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.02f);

            // The one walkable surface, and the only ground collider in the scene.
            GameObject ground = ProtoAssets.MeshObject(box, "Ground", parent,
                new Vector3(0f, -0.5f, 0f),
                new Vector3(GroundHalfSize * 2f, 1f, GroundHalfSize * 2f), p.GrassMid, false);
            ground.AddComponent<BoxCollider>();
            ground.isStatic = true;

            // Deeper apron outside the fence: separates "our farm" from "the world" and reads
            // as distance once the fog takes hold.
            ProtoAssets.MeshObject(box, "OuterField", parent, new Vector3(0f, -0.02f, 0f),
                new Vector3(GroundHalfSize * 2f - 2f, 0.02f, GroundHalfSize * 2f - 2f),
                p.GrassDeep, false);

            // The farm proper: a lighter, mown-looking pad inside the boundary.
            ProtoAssets.MeshObject(box, "FarmLawn", parent, new Vector3(0f, 0.01f, 0f),
                new Vector3(BoundaryHalfSize * 2f + 4f, 0.02f, BoundaryHalfSize * 2f + 4f),
                p.GrassLight, false);

            TerrainDressing.ScatterGroundPatches(parent, p);
        }

        // ================================================================ paths

        public static void BuildPaths(Transform parent, ProtoPalette p)
        {
            Transform root = ProtoAssets.Empty("Paths", parent, Vector3.zero).transform;
            float span = BoundaryHalfSize * 2f + 6f;

            // Two main avenues crossing at the plaza.
            PathStrip(root, p, "Path_EastWest", new Vector3(0f, PathY, 0f), new Vector2(span, PathWidth));
            PathStrip(root, p, "Path_NorthSouth", new Vector3(0f, PathY, 0f), new Vector2(PathWidth, span));

            // Organic edges over the straight strips. The strip stays rectangular so
            // walkability is untouched; only the visible outline becomes irregular.
            float half = span * 0.5f;
            TerrainDressing.SoftenPathEdges(root, p, "EW",
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), PathWidth * 0.5f, 20, 11);
            TerrainDressing.SoftenPathEdges(root, p, "NS",
                new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half), PathWidth * 0.5f, 20, 12);
            TerrainDressing.ScatterPathStones(root, p, "EW",
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), PathWidth * 0.55f, 16, 13);
            TerrainDressing.ScatterPathStones(root, p, "NS",
                new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half), PathWidth * 0.55f, 16, 14);

            // Rounded plaza at the crossroads: the player's home base.
            Mesh disc = StylizedMeshLibrary.Disc(28);
            Mesh irregular = StylizedMeshLibrary.IrregularDisc(500, 20, 0.12f);

            ProtoAssets.MeshObject(irregular, "Plaza_Edge", root,
                new Vector3(0f, PathY + 0.008f, 0f), new Vector3(16.2f, 1f, 16.2f), p.PathEdge, false);
            ProtoAssets.MeshObject(disc, "Plaza", root,
                new Vector3(0f, PathY + 0.016f, 0f), new Vector3(14.4f, 1f, 14.4f), p.Path, false);
            ProtoAssets.MeshObject(disc, "Plaza_Inner", root,
                new Vector3(0f, PathY + 0.024f, 0f), new Vector3(6.8f, 1f, 6.8f), p.PathEdge, false);

            // Cobble ring around the plaza centre.
            Mesh pebble = StylizedMeshLibrary.Pebble();
            for (int i = 0; i < 18; i++)
            {
                float angle = i / 18f * Mathf.PI * 2f;
                ProtoAssets.MeshObject(pebble, "Cobble_" + i, root,
                    new Vector3(Mathf.Cos(angle) * 3.6f, PathY + 0.05f, Mathf.Sin(angle) * 3.6f),
                    new Vector3(0.5f, 0.16f, 0.42f), p.Stone, false)
                    .transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            }

            // Spurs out to each zone, all the same width so the network reads as designed.
            Spur(root, p, "ToWheat", -6.1f, 17.5f, 8.4f, 21);
            Spur(root, p, "ToTomato", -6.1f, 6.5f, 8.4f, 22);
            Spur(root, p, "ToCorn", 6.1f, 17.5f, 8.4f, 23);
            Spur(root, p, "ToProduction", 6.1f, 6.5f, 8.4f, 24);
            Spur(root, p, "ToChicken", -6.1f, -7.5f, 8.4f, 25);
            Spur(root, p, "ToCow", -6.1f, -20f, 8.4f, 26);
            Spur(root, p, "ToMarket", 6.1f, -8f, 8.4f, 27);
            Spur(root, p, "ToHome", 6.1f, -20f, 8.4f, 28);
        }

        private static void Spur(
            Transform parent, ProtoPalette p, string spurName, float x, float z, float length, int seed)
        {
            PathStrip(parent, p, "Path_" + spurName, new Vector3(x, PathY, z), new Vector2(length, SpurWidth));

            float half = length * 0.5f;
            Mesh disc = StylizedMeshLibrary.IrregularDisc(seed * 7, 12, 0.2f);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(disc, spurName + "_Cap" + side, parent,
                    new Vector3(x + half * side, PathY + 0.004f, z),
                    new Vector3(SpurWidth, 1f, SpurWidth), p.Path, false);
            }

            TerrainDressing.SoftenPathEdges(parent, p, spurName,
                new Vector3(x - half, 0f, z), new Vector3(x + half, 0f, z), SpurWidth * 0.5f, 7, seed);
        }

        /// <summary>A path segment: a sand strip with a slightly darker border behind it.</summary>
        private static void PathStrip(
            Transform parent, ProtoPalette p, string stripName, Vector3 centre, Vector2 size)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.04f);

            ProtoAssets.MeshObject(box, stripName + "_Edge", parent,
                new Vector3(centre.x, PathY - 0.004f, centre.z),
                new Vector3(size.x + 0.8f, 0.05f, size.y + 0.8f), p.PathEdge, false);

            ProtoAssets.MeshObject(box, stripName, parent, centre,
                new Vector3(size.x, 0.06f, size.y), p.Path, false);
        }

        // ================================================================ field beds

        /// <summary>
        /// A raised planting bed: soil inside a proper timber frame with corner posts, so a
        /// field reads as a built thing rather than a brown rectangle painted on the lawn.
        /// </summary>
        public static GameObject BuildFieldPad(
            Transform parent, ProtoPalette p, string padName, Vector3 centre, Vector2 size)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.05f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);
            Mesh post = StylizedMeshLibrary.ChamferBox(0.2f);

            // Slightly proud of the lawn: the shadow under the frame is what sells the depth.
            GameObject pad = ProtoAssets.MeshObject(box, padName, parent,
                new Vector3(centre.x, PadY + 0.05f, centre.z),
                new Vector3(size.x, 0.2f, size.y), p.Soil, false);

            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;

            // Frame boards, doubled so the edge has a visible thickness.
            for (int tier = 0; tier < 2; tier++)
            {
                float y = 0.14f + tier * 0.18f;
                float inset = tier * 0.03f;

                ProtoAssets.MeshObject(plank, padName + "_N" + tier, parent,
                    new Vector3(centre.x, y, centre.z + halfZ - inset),
                    new Vector3(0.3f, 0.2f, size.x + 0.55f), p.WoodLight, tier == 1)
                    .transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                ProtoAssets.MeshObject(plank, padName + "_S" + tier, parent,
                    new Vector3(centre.x, y, centre.z - halfZ + inset),
                    new Vector3(0.3f, 0.2f, size.x + 0.55f), p.WoodLight, tier == 1)
                    .transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                ProtoAssets.MeshObject(plank, padName + "_E" + tier, parent,
                    new Vector3(centre.x + halfX - inset, y, centre.z),
                    new Vector3(0.3f, 0.2f, size.y + 0.55f), p.WoodLight, tier == 1);

                ProtoAssets.MeshObject(plank, padName + "_W" + tier, parent,
                    new Vector3(centre.x - halfX + inset, y, centre.z),
                    new Vector3(0.3f, 0.2f, size.y + 0.55f), p.WoodLight, tier == 1);
            }

            // Corner posts with chamfered caps join the frame together.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -halfX : halfX;
                float sz = (i < 2) ? -halfZ : halfZ;

                ProtoAssets.MeshObject(post, padName + "_Post" + i, parent,
                    new Vector3(centre.x + sx, 0.26f, centre.z + sz),
                    new Vector3(0.46f, 0.52f, 0.46f), p.Wood, true);

                ProtoAssets.MeshObject(post, padName + "_Cap" + i, parent,
                    new Vector3(centre.x + sx, 0.54f, centre.z + sz),
                    new Vector3(0.56f, 0.1f, 0.56f), p.WoodDark, false);
            }

            return pad;
        }

        // ================================================================ fences

        public static void BuildBoundary(Transform parent, ProtoPalette p)
        {
            float h = BoundaryHalfSize;
            Transform fenceRoot = ProtoAssets.Empty("Boundary", parent, Vector3.zero).transform;

            // Gaps where the two avenues leave the farm, so the fence never crosses a path.
            FenceLine(fenceRoot, p, "Fence_N_W", new Vector3(-h, 0f, h), new Vector3(-3.4f, 0f, h), 3.2f, 1.4f);
            FenceLine(fenceRoot, p, "Fence_N_E", new Vector3(3.4f, 0f, h), new Vector3(h, 0f, h), 3.2f, 1.4f);
            FenceLine(fenceRoot, p, "Fence_S_W", new Vector3(-h, 0f, -h), new Vector3(-3.4f, 0f, -h), 3.2f, 1.4f);
            FenceLine(fenceRoot, p, "Fence_S_E", new Vector3(3.4f, 0f, -h), new Vector3(h, 0f, -h), 3.2f, 1.4f);
            FenceLine(fenceRoot, p, "Fence_E_S", new Vector3(h, 0f, -h), new Vector3(h, 0f, -3.4f), 3.2f, 1.4f);
            FenceLine(fenceRoot, p, "Fence_E_N", new Vector3(h, 0f, 3.4f), new Vector3(h, 0f, h), 3.2f, 1.4f);
            FenceLine(fenceRoot, p, "Fence_W_S", new Vector3(-h, 0f, -h), new Vector3(-h, 0f, -3.4f), 3.2f, 1.4f);
            FenceLine(fenceRoot, p, "Fence_W_N", new Vector3(-h, 0f, 3.4f), new Vector3(-h, 0f, h), 3.2f, 1.4f);

            BuildGateposts(fenceRoot, p, h);

            // One invisible collider per side keeps the player in without ~90 post colliders.
            Transform walls = ProtoAssets.Empty("BoundaryWalls", parent, Vector3.zero).transform;
            Wall(walls, "Wall_N", new Vector3(0f, 1.5f, h + 0.6f), new Vector3(h * 2f + 2f, 3f, 1f));
            Wall(walls, "Wall_S", new Vector3(0f, 1.5f, -h - 0.6f), new Vector3(h * 2f + 2f, 3f, 1f));
            Wall(walls, "Wall_E", new Vector3(h + 0.6f, 1.5f, 0f), new Vector3(1f, 3f, h * 2f + 2f));
            Wall(walls, "Wall_W", new Vector3(-h - 0.6f, 1.5f, 0f), new Vector3(1f, 3f, h * 2f + 2f));
        }

        /// <summary>Chunky capped posts flanking each of the four road openings.</summary>
        private static void BuildGateposts(Transform root, ProtoPalette p, float h)
        {
            Mesh post = StylizedMeshLibrary.Trapezoid(0.86f);
            Mesh cap = StylizedMeshLibrary.Cone(6);
            Mesh trim = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            Vector3[] gates =
            {
                new Vector3(-3.4f, 0f, h), new Vector3(3.4f, 0f, h),
                new Vector3(-3.4f, 0f, -h), new Vector3(3.4f, 0f, -h),
                new Vector3(h, 0f, -3.4f), new Vector3(h, 0f, 3.4f),
                new Vector3(-h, 0f, -3.4f), new Vector3(-h, 0f, 3.4f)
            };

            for (int i = 0; i < gates.Length; i++)
            {
                ProtoAssets.MeshObject(post, "GatePost_" + i, root,
                    gates[i] + new Vector3(0f, 1.05f, 0f),
                    new Vector3(0.46f, 2.1f, 0.46f), p.FencePaint, true);

                ProtoAssets.MeshObject(trim, "GateCollar_" + i, root,
                    gates[i] + new Vector3(0f, 2.06f, 0f),
                    new Vector3(0.58f, 0.14f, 0.58f), p.Trim, false);

                ProtoAssets.MeshObject(cap, "GateCap_" + i, root,
                    gates[i] + new Vector3(0f, 2.3f, 0f),
                    new Vector3(0.54f, 0.38f, 0.54f), p.RoofTerracotta, false);

                ProtoAssets.MeshObject(sphere, "GateFinial_" + i, root,
                    gates[i] + new Vector3(0f, 2.55f, 0f),
                    Vector3.one * 0.18f, p.Amber, false);
            }
        }

        private static void Wall(Transform parent, string wallName, Vector3 centre, Vector3 size)
        {
            GameObject go = ProtoAssets.Empty(wallName, parent, centre);
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            go.isStatic = true;
        }

        public static void FenceRect(
            Transform parent, ProtoPalette p, string fenceName, Vector3 centre,
            Vector2 size, float spacing, float height, float gateWidth = 0f)
        {
            Transform root = ProtoAssets.Empty(fenceName, parent, centre).transform;
            float hx = size.x * 0.5f;
            float hz = size.y * 0.5f;

            Vector3 nw = new Vector3(-hx, 0f, hz);
            Vector3 ne = new Vector3(hx, 0f, hz);
            Vector3 sw = new Vector3(-hx, 0f, -hz);
            Vector3 se = new Vector3(hx, 0f, -hz);

            FenceLine(root, p, fenceName + "_N", nw, ne, spacing, height);
            FenceLine(root, p, fenceName + "_E", se, ne, spacing, height);
            FenceLine(root, p, fenceName + "_S", sw, se, spacing, height);

            if (gateWidth > 0.1f)
            {
                float gap = gateWidth * 0.5f;
                FenceLine(root, p, fenceName + "_W1", sw, new Vector3(-hx, 0f, -gap), spacing, height);
                FenceLine(root, p, fenceName + "_W2", new Vector3(-hx, 0f, gap), nw, spacing, height);

                Mesh post = StylizedMeshLibrary.Trapezoid(0.88f);
                Mesh cap = StylizedMeshLibrary.Cone(6);

                for (int side = -1; side <= 1; side += 2)
                {
                    ProtoAssets.MeshObject(post, fenceName + "_GatePost" + side, root,
                        new Vector3(-hx, height * 0.68f, gap * side),
                        new Vector3(0.34f, height * 1.36f, 0.34f), p.FencePaint, true);

                    ProtoAssets.MeshObject(cap, fenceName + "_GateCap" + side, root,
                        new Vector3(-hx, height * 1.42f, gap * side),
                        new Vector3(0.42f, 0.26f, 0.42f), p.Trim, false);
                }
            }
            else
            {
                FenceLine(root, p, fenceName + "_W", sw, nw, spacing, height);
            }
        }

        /// <summary>
        /// A run of fence: tapered posts with pointed caps and three sawn rails.
        /// The whole run is collapsed into a single renderer, so a pen costs one draw call
        /// and needs no per-post colliders.
        /// </summary>
        public static void FenceLine(
            Transform parent, ProtoPalette p, string lineName,
            Vector3 from, Vector3 to, float spacing, float height)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.4f)
            {
                return;
            }

            Vector3 dir = delta / length;
            Transform root = ProtoAssets.Empty(lineName, parent, Vector3.zero).transform;

            Mesh post = StylizedMeshLibrary.Trapezoid(0.82f);
            Mesh cap = StylizedMeshLibrary.Cone(5);
            Mesh plank = StylizedMeshLibrary.Plank(0.22f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            int posts = Mathf.Max(2, Mathf.RoundToInt(length / spacing) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 pos = from + dir * (length * i / (posts - 1));

                ProtoAssets.MeshObject(post, "Post_" + i, root,
                    pos + Vector3.up * (height * 0.5f),
                    new Vector3(0.24f, height, 0.24f), p.FencePaint, false);

                ProtoAssets.MeshObject(cap, "Cap_" + i, root,
                    pos + Vector3.up * (height + 0.1f),
                    new Vector3(0.3f, 0.22f, 0.3f), p.FencePaint, false);
            }

            Vector3 mid = from + dir * (length * 0.5f);

            // Three rails read as a proper post-and-rail fence rather than a hurdle.
            float[] railHeights = { 0.3f, 0.58f, 0.86f };
            for (int r = 0; r < railHeights.Length; r++)
            {
                GameObject rail = ProtoAssets.MeshObject(plank, "Rail_" + r, root,
                    mid + Vector3.up * (height * railHeights[r]),
                    new Vector3(0.1f, 0.15f, length), p.FencePaint, false);
                rail.transform.localRotation = rot;
            }

            StylizedMeshLibrary.CombineIntoSingleRenderer(root.gameObject, "Mesh_" + lineName);
        }

        // ================================================================ water

        /// <summary>
        /// Stylised pond: an irregular shoreline in four layers, with reeds, rocks and lily
        /// pads. No reflections, no transparency, no animation - just readable blue.
        /// </summary>
        public static void Pond(
            Transform parent, ProtoPalette p, Vector3 centre, float radius, PropLibrary.Props props)
        {
            Transform root = ProtoAssets.Empty("Pond", parent, centre).transform;

            // Each layer uses a different irregular outline, so the shore never reads circular.
            Mesh bank = StylizedMeshLibrary.IrregularDisc(601, 18, 0.26f);
            Mesh shore = StylizedMeshLibrary.IrregularDisc(602, 18, 0.24f);
            Mesh shallow = StylizedMeshLibrary.IrregularDisc(603, 18, 0.2f);
            Mesh deep = StylizedMeshLibrary.IrregularDisc(604, 16, 0.22f);
            Mesh disc = StylizedMeshLibrary.Disc(12);

            ProtoAssets.MeshObject(bank, "Bank", root, new Vector3(0f, 0.03f, 0f),
                new Vector3(radius * 2.6f, 1f, radius * 2.35f), p.GrassDeep, false);
            ProtoAssets.MeshObject(shore, "Shore", root, new Vector3(0f, 0.05f, 0f),
                new Vector3(radius * 2.2f, 1f, radius * 2.0f), p.PathEdge, false);
            ProtoAssets.MeshObject(shallow, "WaterShallow", root, new Vector3(0f, 0.07f, 0f),
                new Vector3(radius * 1.94f, 1f, radius * 1.78f), p.WaterShallow, false);
            ProtoAssets.MeshObject(deep, "WaterDeep", root, new Vector3(0.1f, 0.085f, 0.2f),
                new Vector3(radius * 1.5f, 1f, radius * 1.32f), p.Water, false);

            // Lily pads.
            float[] lx = { 0.42f, 0.64f, -0.5f };
            float[] lz = { -0.3f, -0.05f, 0.34f };
            float[] ls = { 0.95f, 0.66f, 0.8f };

            for (int i = 0; i < 3; i++)
            {
                ProtoAssets.MeshObject(disc, "Lily_" + i, root,
                    new Vector3(radius * lx[i], 0.1f, radius * lz[i]),
                    new Vector3(ls[i], 1f, ls[i] * 0.88f),
                    i % 2 == 0 ? p.LeafDeep : p.LeafMid, false);
            }

            if (props == null)
            {
                return;
            }

            // Reeds and stones break the shoreline silhouette.
            PlaceProp(props.Reeds, root, new Vector3(-radius * 0.95f, 0.06f, radius * 0.45f), 1.35f, 24f);
            PlaceProp(props.Reeds, root, new Vector3(-radius * 0.5f, 0.06f, radius * 1.0f), 1.15f, -40f);
            PlaceProp(props.Reeds, root, new Vector3(radius * 0.9f, 0.06f, radius * 0.6f), 1.25f, 130f);
            PlaceProp(props.Reeds, root, new Vector3(radius * 0.2f, 0.06f, -radius * 1.05f), 1.05f, 210f);
            PlaceProp(props.Pebbles, root, new Vector3(radius * 1.1f, 0.02f, -radius * 0.7f), 1.1f, 20f);
            PlaceProp(props.Rock, root, new Vector3(-radius * 1.15f, 0.02f, -radius * 0.5f), 0.85f, -60f);
            PlaceProp(props.BushSprig, root, new Vector3(-radius * 0.2f, 0.02f, radius * 1.2f), 1f, 15f);
        }

        // ================================================================ prop placement

        /// <summary>Instantiates a prop prefab. Returns null safely when the prefab is missing.</summary>
        public static GameObject PlaceProp(
            GameObject prefab, Transform parent, Vector3 position, float scale, float yaw)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                return null;
            }

            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            return instance;
        }

        // ================================================================ interaction points

        /// <summary>
        /// Creates a named, trigger-collider interaction point for an area
        /// (market, coop, barn...).
        /// </summary>
        public static FarmLandmark Landmark(
            Transform parent, string objectName, string id, string label,
            FarmLandmark.LandmarkKind kind, Vector3 position, Vector3 triggerSize, int layer)
        {
            GameObject go = ProtoAssets.Empty(objectName, parent, position);
            if (layer >= 0)
            {
                go.layer = layer;
            }

            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;
            trigger.center = new Vector3(0f, triggerSize.y * 0.5f, 0f);

            FarmLandmark landmark = go.AddComponent<FarmLandmark>();
            landmark.EditorConfigure(id, label, kind);
            return landmark;
        }

        /// <summary>
        /// Signpost with a painted board. Colour plus position is what makes each zone
        /// identifiable without reading the HUD.
        /// </summary>
        public static void Signpost(
            Transform parent, ProtoPalette p, string signName, Vector3 position, Material accent, float yaw)
        {
            Transform root = ProtoAssets.Empty(signName, parent, position).transform;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Mesh box = StylizedMeshLibrary.ChamferBox(0.18f);
            Mesh plank = StylizedMeshLibrary.Plank(0.2f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.78f);
            Mesh cone = StylizedMeshLibrary.Cone(6);

            ProtoAssets.MeshObject(taper, "Post", root, new Vector3(0f, 0.95f, 0f),
                new Vector3(0.26f, 1.9f, 0.26f), p.Wood, true);

            ProtoAssets.MeshObject(cone, "PostCap", root, new Vector3(0f, 2.02f, 0f),
                new Vector3(0.32f, 0.2f, 0.32f), p.WoodDark, false);

            // Board with a raised painted panel and a timber edge.
            ProtoAssets.MeshObject(plank, "Board", root, new Vector3(0f, 1.72f, 0.04f),
                new Vector3(0.16f, 1.0f, 2.1f), p.WoodLight, true)
                .transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            ProtoAssets.MeshObject(box, "Panel", root, new Vector3(0f, 1.72f, -0.06f),
                new Vector3(1.74f, 0.66f, 0.12f), accent, false);

            ProtoAssets.MeshObject(box, "Ledge", root, new Vector3(0f, 2.26f, 0.02f),
                new Vector3(2.3f, 0.14f, 0.3f), p.Trim, false);

            // Small support brackets under the board.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject bracket = ProtoAssets.MeshObject(box, "Bracket_" + side, root,
                    new Vector3(0.42f * side, 1.28f, 0f),
                    new Vector3(0.5f, 0.1f, 0.1f), p.WoodDark, false);
                bracket.transform.localRotation = Quaternion.Euler(0f, 0f, side * 38f);
            }
        }
    }
}
