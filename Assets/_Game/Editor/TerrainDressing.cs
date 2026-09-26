using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Everything that gives the world depth: rolling background hills, ground tone breakup,
    /// and organic edges on the path network.
    ///
    /// SAFETY RULE: all raised geometry lives OUTSIDE the boundary fence. The walkable ground
    /// remains a single flat box collider, so CharacterController behaviour is bit-identical
    /// to the checkpoint. Nothing here is walkable and nothing here has a collider.
    /// </summary>
    public static class TerrainDressing
    {
        /// <summary>Beyond this radius from the origin, geometry is scenery the player never reaches.</summary>
        private const float SafeRadius = 34f;

        // ================================================================ background relief

        /// <summary>
        /// A ring of low hills beyond the fence. This is the single biggest depth cue in the
        /// scene: it replaces an empty flat horizon with overlapping masses that recede into
        /// the fog, and it costs about a dozen draw calls.
        /// </summary>
        public static void BuildBackgroundHills(Transform parent, ProtoPalette p)
        {
            Transform root = ProtoAssets.Empty("BackgroundHills", parent, Vector3.zero).transform;

            // Inner belt: closer, smaller, lighter - reads as the next field over.
            RingOfHills(root, p, 12, 41f, 4.5f, 9f, 15f, 3.2f, p.GrassMid, 101);

            // Outer belt: larger, deeper toned, heavily fogged - reads as distance.
            RingOfHills(root, p, 10, 60f, 7f, 20f, 30f, 7f, p.GrassDeep, 202);

            // A couple of taller far masses break the horizon line asymmetrically.
            Mesh mound = StylizedMeshLibrary.Mound(77, 5, 14);

            ProtoAssets.MeshObject(mound, "FarMass_A", root, new Vector3(-46f, 2f, 62f),
                new Vector3(52f, 13f, 40f), p.GrassDeep, false);
            ProtoAssets.MeshObject(mound, "FarMass_B", root, new Vector3(58f, 1.5f, 38f),
                new Vector3(44f, 10f, 34f), p.GrassDeep, false);
            ProtoAssets.MeshObject(mound, "FarMass_C", root, new Vector3(12f, 1.5f, 74f),
                new Vector3(60f, 11f, 36f), p.GrassDeep, false);
        }

        private static void RingOfHills(
            Transform root, ProtoPalette p, int count, float radius, float radiusJitter,
            float minWidth, float maxWidth, float maxHeight, Material material, int seed)
        {
            System.Random random = new System.Random(seed);

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + (float)random.NextDouble() * 0.3f;
                float r = radius + (float)random.NextDouble() * radiusJitter;

                Vector3 position = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

                // Never place relief where the player could reach or see the seam.
                if (position.magnitude < SafeRadius)
                {
                    continue;
                }

                float width = Mathf.Lerp(minWidth, maxWidth, (float)random.NextDouble());
                float height = Mathf.Lerp(maxHeight * 0.35f, maxHeight, (float)random.NextDouble());

                Mesh mound = StylizedMeshLibrary.Mound(seed + i, 4, 12);

                // Sunk slightly so the base never shows a hard rim against the ground plane.
                ProtoAssets.MeshObject(mound, "Hill_" + i, root,
                    position + new Vector3(0f, -height * 0.12f, 0f),
                    new Vector3(width, height, width * (0.7f + (float)random.NextDouble() * 0.5f)),
                    material, false)
                    .transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            }
        }

        // ================================================================ ground breakup

        /// <summary>
        /// Irregular tonal patches over the lawn. Using seeded irregular discs rather than
        /// circles is what stops the ground reading as a stack of flat decals.
        /// </summary>
        public static void ScatterGroundPatches(Transform parent, ProtoPalette p)
        {
            Transform root = ProtoAssets.Empty("GroundPatches", parent, Vector3.zero).transform;

            // Placed to avoid the paths (|x|<4 or |z|<4) and the field pads.
            (Vector3 pos, float size, int tone)[] patches =
            {
                (new Vector3(-24f, 0f, 26f), 11f, 0), (new Vector3(23f, 0f, 27f), 9f, 1),
                (new Vector3(-27f, 0f, -14f), 12f, 0), (new Vector3(26f, 0f, -26f), 10f, 1),
                (new Vector3(-9f, 0f, -27f), 8f, 0), (new Vector3(10f, 0f, 27f), 9f, 1),
                (new Vector3(-13f, 0f, 1.5f), 7f, 0), (new Vector3(13f, 0f, -2.5f), 8f, 1),
                (new Vector3(-25f, 0f, 12f), 9f, 1), (new Vector3(25f, 0f, 12f), 7f, 0),
                (new Vector3(-8f, 0f, 12f), 6f, 1), (new Vector3(8.5f, 0f, -12f), 7f, 0),
                (new Vector3(-28f, 0f, -26f), 10f, 1), (new Vector3(28f, 0f, 5f), 8f, 0)
            };

            for (int i = 0; i < patches.Length; i++)
            {
                Mesh disc = StylizedMeshLibrary.IrregularDisc(300 + i, 14, 0.3f);
                Material tone = patches[i].tone == 0 ? p.GrassMid : p.GrassDeep;
                float size = patches[i].size;

                ProtoAssets.MeshObject(disc, "Patch_" + i, root,
                    patches[i].pos + new Vector3(0f, 0.024f + i * 0.0006f, 0f),
                    new Vector3(size, 1f, size * 0.82f), tone, false)
                    .transform.localRotation = Quaternion.Euler(0f, i * 47f % 360f, 0f);
            }

            // A few brighter sunlit patches on top, smaller and warmer.
            for (int i = 0; i < 7; i++)
            {
                Mesh disc = StylizedMeshLibrary.IrregularDisc(400 + i, 12, 0.34f);
                float angle = i / 7f * Mathf.PI * 2f + 0.7f;
                float r = 17f + (i % 3) * 5f;
                float size = 5f + (i % 3) * 1.6f;

                ProtoAssets.MeshObject(disc, "Sunlit_" + i, root,
                    new Vector3(Mathf.Cos(angle) * r, 0.034f + i * 0.0006f, Mathf.Sin(angle) * r),
                    new Vector3(size, 1f, size * 0.86f), p.GrassLight, false);
            }
        }

        // ================================================================ path organics

        /// <summary>
        /// Softens a straight path run by scattering irregular blobs of the same sand along
        /// its edges. The strip underneath stays rectangular so walkability is unchanged; only
        /// the visible outline becomes organic.
        /// </summary>
        public static void SoftenPathEdges(
            Transform parent, ProtoPalette p, string label, Vector3 from, Vector3 to,
            float halfWidth, int blobCount, int seed)
        {
            Transform root = ProtoAssets.Empty("PathEdge_" + label, parent, Vector3.zero).transform;

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.1f)
            {
                return;
            }

            Vector3 dir = delta / length;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            System.Random random = new System.Random(seed);

            for (int i = 0; i < blobCount; i++)
            {
                float t = (i + 0.5f) / blobCount;
                float sign = (i % 2 == 0) ? 1f : -1f;

                // Blob straddles the edge so it eats into the grass without leaving a gap.
                float offset = halfWidth * (0.82f + (float)random.NextDouble() * 0.3f);
                float size = 1.9f + (float)random.NextDouble() * 2.1f;

                Vector3 position = from + dir * (length * t) + side * (offset * sign);

                Mesh disc = StylizedMeshLibrary.IrregularDisc(seed * 31 + i, 10, 0.38f);

                ProtoAssets.MeshObject(disc, "Blob_" + i, root,
                    position + new Vector3(0f, FarmEnvironmentBuilder.PathY + 0.002f, 0f),
                    new Vector3(size, 1f, size * 0.85f),
                    (i % 3 == 0) ? p.PathEdge : p.Path, false)
                    .transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            }
        }

        /// <summary>
        /// Loose stones along a path run. Cheap, and it makes a flat sand strip read as a
        /// worn track rather than a painted rectangle.
        /// </summary>
        public static void ScatterPathStones(
            Transform parent, ProtoPalette p, string label, Vector3 from, Vector3 to,
            float halfWidth, int count, int seed)
        {
            Transform root = ProtoAssets.Empty("PathStones_" + label, parent, Vector3.zero).transform;

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.1f)
            {
                return;
            }

            Vector3 dir = delta / length;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            System.Random random = new System.Random(seed);
            Mesh pebble = StylizedMeshLibrary.Pebble();

            for (int i = 0; i < count; i++)
            {
                float t = (float)random.NextDouble();
                float sign = (i % 2 == 0) ? 1f : -1f;
                float offset = halfWidth * (0.55f + (float)random.NextDouble() * 0.5f) * sign;
                float size = 0.22f + (float)random.NextDouble() * 0.3f;

                Vector3 position = from + dir * (length * t) + side * offset;

                ProtoAssets.MeshObject(pebble, "Stone_" + i, root,
                    position + new Vector3(0f, FarmEnvironmentBuilder.PathY + size * 0.14f, 0f),
                    new Vector3(size * 1.3f, size * 0.6f, size), p.Stone, false)
                    .transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 6f);
            }
        }
    }
}
