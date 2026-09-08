using LittleFarmStory.World;
using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Generates the farm ground plane, the path network, boundaries and the pond.
    /// Everything is built from the shared stylised meshes; there is not a single texture
    /// anywhere in the 3D scene.
    /// </summary>
    public static class FarmEnvironmentBuilder
    {
        /// <summary>
        /// The visible ground has to be far larger than the fenced farm: at the gameplay
        /// camera pitch the view reaches roughly 20 m past the focus point, and the focus is
        /// already clamped to +/-26, so anything smaller lets the player see the world edge.
        /// </summary>
        public const float GroundHalfSize = 55f;

        public const float BoundaryHalfSize = 31f;
        public const float PathY = 0.04f;
        public const float PadY = 0.06f;

        private const float PathWidth = 5.4f;

        // ================================================================ ground

        public static void BuildGround(Transform parent, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.02f);
            Mesh disc = StylizedMeshLibrary.Disc(20);

            GameObject ground = ProtoAssets.MeshObject(box, "Ground", parent,
                new Vector3(0f, -0.5f, 0f),
                new Vector3(GroundHalfSize * 2f, 1f, GroundHalfSize * 2f), p.GrassMid, false);
            ground.AddComponent<BoxCollider>();
            ground.isStatic = true;

            // A slightly deeper apron outside the fence separates "our farm" from "the world",
            // and reads as distance under the fog.
            ProtoAssets.MeshObject(box, "OuterField", parent, new Vector3(0f, -0.02f, 0f),
                new Vector3(GroundHalfSize * 2f - 2f, 0.02f, GroundHalfSize * 2f - 2f), p.GrassDeep, false);

            // The farm proper: a lighter, mown-looking pad inside the boundary.
            ProtoAssets.MeshObject(box, "FarmLawn", parent, new Vector3(0f, 0.01f, 0f),
                new Vector3(BoundaryHalfSize * 2f + 3f, 0.02f, BoundaryHalfSize * 2f + 3f), p.GrassLight, false);

            // Soft tonal patches so a big flat lawn is not one solid colour.
            Vector3[] patches =
            {
                new Vector3(-24f, 0f, 26f), new Vector3(23f, 0f, 27f),
                new Vector3(-27f, 0f, -14f), new Vector3(26f, 0f, -26f),
                new Vector3(-9f, 0f, -27f), new Vector3(10f, 0f, 27f),
                new Vector3(-13f, 0f, 2f), new Vector3(13f, 0f, -3f)
            };

            for (int i = 0; i < patches.Length; i++)
            {
                float size = 7f + (i % 4) * 2.4f;
                ProtoAssets.MeshObject(disc, "GrassPatch_" + i, parent,
                    patches[i] + new Vector3(0f, 0.022f, 0f),
                    new Vector3(size, 1f, size * 0.78f), p.GrassMid, false);
            }
        }

        // ================================================================ paths

        public static void BuildPaths(Transform parent, ProtoPalette p)
        {
            Transform root = ProtoAssets.Empty("Paths", parent, Vector3.zero).transform;

            float span = BoundaryHalfSize * 2f;

            // Two main avenues crossing at the plaza.
            PathStrip(root, p, "Path_EastWest", new Vector3(0f, PathY, 0f), new Vector2(span, PathWidth));
            PathStrip(root, p, "Path_NorthSouth", new Vector3(0f, PathY, 0f), new Vector2(PathWidth, span));

            // Rounded plaza at the crossroads: the player's home base.
            Mesh disc = StylizedMeshLibrary.Disc(24);
            ProtoAssets.MeshObject(disc, "Plaza_Edge", root, new Vector3(0f, PathY + 0.008f, 0f),
                new Vector3(15.4f, 1f, 15.4f), p.PathEdge, false);
            ProtoAssets.MeshObject(disc, "Plaza", root, new Vector3(0f, PathY + 0.016f, 0f),
                new Vector3(14f, 1f, 14f), p.Path, false);
            ProtoAssets.MeshObject(disc, "Plaza_Inner", root, new Vector3(0f, PathY + 0.024f, 0f),
                new Vector3(6.6f, 1f, 6.6f), p.PathEdge, false);

            // Spurs out to each zone, all the same width so the network reads as designed.
            Spur(root, p, "Path_ToWheat", -6.1f, 17.5f, 8.2f);
            Spur(root, p, "Path_ToTomato", -6.1f, 6.5f, 8.2f);
            Spur(root, p, "Path_ToCorn", 6.1f, 17.5f, 8.2f);
            Spur(root, p, "Path_ToProduction", 6.1f, 6.5f, 8.2f);
            Spur(root, p, "Path_ToChicken", -6.1f, -7.5f, 8.2f);
            Spur(root, p, "Path_ToCow", -6.1f, -20f, 8.2f);
            Spur(root, p, "Path_ToMarket", 6.1f, -8f, 8.2f);
            Spur(root, p, "Path_ToHome", 6.1f, -20f, 8.2f);
        }

        private static void Spur(Transform parent, ProtoPalette p, string spurName, float x, float z, float length)
        {
            PathStrip(parent, p, spurName, new Vector3(x, PathY, z), new Vector2(length, 3.8f));

            // Rounded caps stop the spur ending in a hard rectangle against the grass.
            Mesh disc = StylizedMeshLibrary.Disc(14);
            float half = length * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(disc, spurName + "_Cap" + side, parent,
                    new Vector3(x + half * side, PathY + 0.004f, z),
                    new Vector3(3.8f, 1f, 3.8f), p.Path, false);
            }
        }

        /// <summary>A path segment: a sand strip with a slightly darker border around it.</summary>
        private static void PathStrip(Transform parent, ProtoPalette p, string stripName, Vector3 centre, Vector2 size)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.04f);

            ProtoAssets.MeshObject(box, stripName + "_Edge", parent,
                new Vector3(centre.x, PathY - 0.004f, centre.z),
                new Vector3(size.x + 0.7f, 0.05f, size.y + 0.7f), p.PathEdge, false);

            ProtoAssets.MeshObject(box, stripName, parent, centre,
                new Vector3(size.x, 0.06f, size.y), p.Path, false);
        }

        /// <summary>Flat soil pad that grounds a field, with a timber frame around it.</summary>
        public static GameObject BuildFieldPad(
            Transform parent, ProtoPalette p, string padName, Vector3 centre, Vector2 size)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.05f);
            Mesh beam = StylizedMeshLibrary.ChamferBox(0.22f);

            GameObject pad = ProtoAssets.MeshObject(box, padName, parent,
                new Vector3(centre.x, PadY, centre.z),
                new Vector3(size.x, 0.12f, size.y), p.Soil, false);

            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;

            ProtoAssets.MeshObject(beam, padName + "_EdgeN", parent,
                new Vector3(centre.x, 0.16f, centre.z + halfZ),
                new Vector3(size.x + 0.5f, 0.32f, 0.34f), p.WoodLight, false);
            ProtoAssets.MeshObject(beam, padName + "_EdgeS", parent,
                new Vector3(centre.x, 0.16f, centre.z - halfZ),
                new Vector3(size.x + 0.5f, 0.32f, 0.34f), p.WoodLight, false);
            ProtoAssets.MeshObject(beam, padName + "_EdgeE", parent,
                new Vector3(centre.x + halfX, 0.16f, centre.z),
                new Vector3(0.34f, 0.32f, size.y + 0.5f), p.WoodLight, false);
            ProtoAssets.MeshObject(beam, padName + "_EdgeW", parent,
                new Vector3(centre.x - halfX, 0.16f, centre.z),
                new Vector3(0.34f, 0.32f, size.y + 0.5f), p.WoodLight, false);

            // Corner blocks so the frame reads as joined timber, not four floating planks.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -halfX : halfX;
                float sz = (i < 2) ? -halfZ : halfZ;
                ProtoAssets.MeshObject(beam, padName + "_Corner" + i, parent,
                    new Vector3(centre.x + sx, 0.2f, centre.z + sz),
                    new Vector3(0.46f, 0.4f, 0.46f), p.Wood, false);
            }

            return pad;
        }

        // ================================================================ fences

        public static void BuildBoundary(Transform parent, ProtoPalette p)
        {
            float h = BoundaryHalfSize;
            Transform fenceRoot = ProtoAssets.Empty("Boundary", parent, Vector3.zero).transform;

            // Gaps where the two avenues leave the farm, so the fence does not cross the path.
            FenceLine(fenceRoot, p, "Fence_N_W", new Vector3(-h, 0f, h), new Vector3(-3.2f, 0f, h), 3.4f, 1.35f);
            FenceLine(fenceRoot, p, "Fence_N_E", new Vector3(3.2f, 0f, h), new Vector3(h, 0f, h), 3.4f, 1.35f);
            FenceLine(fenceRoot, p, "Fence_S_W", new Vector3(-h, 0f, -h), new Vector3(-3.2f, 0f, -h), 3.4f, 1.35f);
            FenceLine(fenceRoot, p, "Fence_S_E", new Vector3(3.2f, 0f, -h), new Vector3(h, 0f, -h), 3.4f, 1.35f);
            FenceLine(fenceRoot, p, "Fence_E_S", new Vector3(h, 0f, -h), new Vector3(h, 0f, -3.2f), 3.4f, 1.35f);
            FenceLine(fenceRoot, p, "Fence_E_N", new Vector3(h, 0f, 3.2f), new Vector3(h, 0f, h), 3.4f, 1.35f);
            FenceLine(fenceRoot, p, "Fence_W_S", new Vector3(-h, 0f, -h), new Vector3(-h, 0f, -3.2f), 3.4f, 1.35f);
            FenceLine(fenceRoot, p, "Fence_W_N", new Vector3(-h, 0f, 3.2f), new Vector3(-h, 0f, h), 3.4f, 1.35f);

            // Gateposts flanking each opening.
            Mesh box = StylizedMeshLibrary.ChamferBox(0.16f);
            Mesh cap = StylizedMeshLibrary.Cone(6);

            Vector3[] gates =
            {
                new Vector3(-3.2f, 0f, h), new Vector3(3.2f, 0f, h),
                new Vector3(-3.2f, 0f, -h), new Vector3(3.2f, 0f, -h),
                new Vector3(h, 0f, -3.2f), new Vector3(h, 0f, 3.2f),
                new Vector3(-h, 0f, -3.2f), new Vector3(-h, 0f, 3.2f)
            };

            for (int i = 0; i < gates.Length; i++)
            {
                ProtoAssets.MeshObject(box, "GatePost_" + i, fenceRoot,
                    gates[i] + new Vector3(0f, 0.95f, 0f),
                    new Vector3(0.34f, 1.9f, 0.34f), p.FencePaint, true);

                ProtoAssets.MeshObject(cap, "GateCap_" + i, fenceRoot,
                    gates[i] + new Vector3(0f, 2.06f, 0f),
                    new Vector3(0.42f, 0.3f, 0.42f), p.RoofTerracotta, false);
            }

            // One invisible collider per side keeps the player in without 80 fence colliders.
            Transform walls = ProtoAssets.Empty("BoundaryWalls", parent, Vector3.zero).transform;
            Wall(walls, "Wall_N", new Vector3(0f, 1.5f, h + 0.6f), new Vector3(h * 2f + 2f, 3f, 1f));
            Wall(walls, "Wall_S", new Vector3(0f, 1.5f, -h - 0.6f), new Vector3(h * 2f + 2f, 3f, 1f));
            Wall(walls, "Wall_E", new Vector3(h + 0.6f, 1.5f, 0f), new Vector3(1f, 3f, h * 2f + 2f));
            Wall(walls, "Wall_W", new Vector3(-h - 0.6f, 1.5f, 0f), new Vector3(1f, 3f, h * 2f + 2f));
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

                Mesh box = StylizedMeshLibrary.ChamferBox(0.18f);
                for (int side = -1; side <= 1; side += 2)
                {
                    ProtoAssets.MeshObject(box, fenceName + "_GatePost" + side, root,
                        new Vector3(-hx, height * 0.62f, gap * side),
                        new Vector3(0.26f, height * 1.24f, 0.26f), p.FencePaint, true);
                }
            }
            else
            {
                FenceLine(root, p, fenceName + "_W", sw, nw, spacing, height);
            }
        }

        /// <summary>
        /// A run of fence: chamfered posts with rounded caps and two rails.
        /// The whole run is collapsed into a single renderer, so a pen costs one draw call.
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

            Mesh box = StylizedMeshLibrary.ChamferBox(0.2f);
            Mesh cap = StylizedMeshLibrary.Cone(6);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            int posts = Mathf.Max(2, Mathf.RoundToInt(length / spacing) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 pos = from + dir * (length * i / (posts - 1));

                ProtoAssets.MeshObject(box, "Post_" + i, root,
                    pos + Vector3.up * (height * 0.5f),
                    new Vector3(0.2f, height, 0.2f), p.FencePaint, false);

                ProtoAssets.MeshObject(cap, "Cap_" + i, root,
                    pos + Vector3.up * (height + 0.06f),
                    new Vector3(0.26f, 0.16f, 0.26f), p.FencePaint, false);
            }

            Vector3 mid = from + dir * (length * 0.5f);

            GameObject railLow = ProtoAssets.MeshObject(box, "Rail_Low", root,
                mid + Vector3.up * (height * 0.38f),
                new Vector3(0.11f, 0.16f, length), p.FencePaint, false);
            railLow.transform.localRotation = rot;

            GameObject railHigh = ProtoAssets.MeshObject(box, "Rail_High", root,
                mid + Vector3.up * (height * 0.78f),
                new Vector3(0.11f, 0.16f, length), p.FencePaint, false);
            railHigh.transform.localRotation = rot;

            StylizedMeshLibrary.CombineIntoSingleRenderer(root.gameObject, "Mesh_" + lineName);
        }

        // ================================================================ water

        /// <summary>
        /// Stylised pond: a sand bank, two water tones for depth, a few rocks and reeds.
        /// No reflections, no transparency, no animation - just bright readable blue.
        /// </summary>
        public static void Pond(Transform parent, ProtoPalette p, Vector3 centre, float radius, PropLibrary.Props props)
        {
            Transform root = ProtoAssets.Empty("Pond", parent, centre).transform;
            Mesh disc = StylizedMeshLibrary.Disc(24);

            ProtoAssets.MeshObject(disc, "Bank", root, new Vector3(0f, 0.03f, 0f),
                new Vector3(radius * 2.5f, 1f, radius * 2.3f), p.PathEdge, false);

            ProtoAssets.MeshObject(disc, "Shore", root, new Vector3(0f, 0.05f, 0f),
                new Vector3(radius * 2.16f, 1f, radius * 2f), p.Path, false);

            ProtoAssets.MeshObject(disc, "WaterShallow", root, new Vector3(0f, 0.07f, 0f),
                new Vector3(radius * 2f, 1f, radius * 1.84f), p.WaterShallow, false);

            ProtoAssets.MeshObject(disc, "WaterDeep", root, new Vector3(0f, 0.085f, 0.15f),
                new Vector3(radius * 1.62f, 1f, radius * 1.44f), p.Water, false);

            // A lily-pad cluster and a highlight give the flat disc some life.
            ProtoAssets.MeshObject(disc, "Lily_A", root, new Vector3(radius * 0.42f, 0.1f, -radius * 0.3f),
                new Vector3(0.9f, 1f, 0.8f), p.LeafDeep, false);
            ProtoAssets.MeshObject(disc, "Lily_B", root, new Vector3(radius * 0.62f, 0.1f, -radius * 0.08f),
                new Vector3(0.66f, 1f, 0.6f), p.LeafMid, false);

            if (props == null)
            {
                return;
            }

            PlaceProp(props.Reeds, root, new Vector3(-radius * 0.92f, 0.06f, radius * 0.4f), 1.3f, 24f);
            PlaceProp(props.Reeds, root, new Vector3(-radius * 0.5f, 0.06f, radius * 0.95f), 1.1f, -40f);
            PlaceProp(props.Reeds, root, new Vector3(radius * 0.85f, 0.06f, radius * 0.55f), 1.2f, 130f);
            PlaceProp(props.RockSmall, root, new Vector3(radius * 1.05f, 0.02f, -radius * 0.66f), 1f, 20f);
            PlaceProp(props.Rock, root, new Vector3(-radius * 1.1f, 0.02f, -radius * 0.45f), 0.8f, -60f);
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
        /// Signpost with a coloured board. Colour plus position is what makes each zone
        /// identifiable without reading the HUD.
        /// </summary>
        public static void Signpost(
            Transform parent, ProtoPalette p, string signName, Vector3 position, Material accent, float yaw)
        {
            Transform root = ProtoAssets.Empty(signName, parent, position).transform;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Mesh box = StylizedMeshLibrary.ChamferBox(0.18f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.8f);

            ProtoAssets.MeshObject(taper, "Post", root, new Vector3(0f, 0.85f, 0f),
                new Vector3(0.22f, 1.7f, 0.22f), p.Wood, true);

            ProtoAssets.MeshObject(box, "Board", root, new Vector3(0f, 1.86f, 0.03f),
                new Vector3(2.0f, 0.92f, 0.14f), p.WoodLight, true);

            ProtoAssets.MeshObject(box, "Panel", root, new Vector3(0f, 1.86f, -0.05f),
                new Vector3(1.66f, 0.62f, 0.14f), accent, false);

            ProtoAssets.MeshObject(box, "Cap", root, new Vector3(0f, 2.4f, 0.03f),
                new Vector3(2.2f, 0.16f, 0.24f), p.Trim, false);
        }
    }
}
