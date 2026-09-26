using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Procedural mesh generation for the stylised farm look: chamfered boxes, lathed solids,
    /// low-poly spheres, cones and prisms. Meshes are saved as shared assets so hundreds of
    /// props reference the same few meshes.
    ///
    /// Three things make this safe to author without being able to look at the result:
    ///  - every solid is built from convex pieces, and each piece has its triangle winding
    ///    corrected against its own centre, which is exact - so a hand-derived vertex order
    ///    can never produce inside-out or invisible geometry;
    ///  - closed meshes additionally get a signed-volume check as a backstop;
    ///  - vertices are never shared between faces, so RecalculateNormals always produces the
    ///    faceted flat shading the art direction wants.
    /// </summary>
    public static class StylizedMeshLibrary
    {
        public const string MeshFolder = ProtoAssets.GameRoot + "/Art/Meshes";

        // ================================================================ builder

        /// <summary>Accumulates flat-shaded triangles. One vertex per corner per face.</summary>
        private class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int index = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
            }

            /// <summary>Quad as two triangles, corners given in order around the face.</summary>
            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                AddTriangle(a, b, c);
                AddTriangle(a, c, d);
            }

            /// <summary>Convex fan around a centre point.</summary>
            public void AddFan(Vector3 centre, IList<Vector3> rim, bool reverse)
            {
                for (int i = 0; i < rim.Count; i++)
                {
                    Vector3 current = rim[i];
                    Vector3 next = rim[(i + 1) % rim.Count];

                    if (reverse)
                    {
                        AddTriangle(centre, next, current);
                    }
                    else
                    {
                        AddTriangle(centre, current, next);
                    }
                }
            }

            /// <summary>Index of the next triangle to be added, for scoping a convex piece.</summary>
            public int Marker => triangles.Count;

            /// <summary>
            /// Forces every triangle added since <paramref name="fromIndex"/> to face away from
            /// <paramref name="centre"/>. Exact for a convex piece whose interior contains the
            /// centre - which is how every solid here is built - and removes any need to derive
            /// winding by hand.
            /// </summary>
            public void EnforceOutwardFrom(Vector3 centre, int fromIndex)
            {
                for (int i = fromIndex; i < triangles.Count; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];

                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    Vector3 outward = (a + b + c) / 3f - centre;

                    if (Vector3.Dot(normal, outward) >= 0f)
                    {
                        continue;
                    }

                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                }
            }

            /// <summary>Axis-aligned box with guaranteed outward winding.</summary>
            public void AddBox(Vector3 centre, Vector3 size)
            {
                int marker = Marker;
                Vector3 h = size * 0.5f;

                Vector3 p000 = centre + new Vector3(-h.x, -h.y, -h.z);
                Vector3 p100 = centre + new Vector3(h.x, -h.y, -h.z);
                Vector3 p110 = centre + new Vector3(h.x, h.y, -h.z);
                Vector3 p010 = centre + new Vector3(-h.x, h.y, -h.z);
                Vector3 p001 = centre + new Vector3(-h.x, -h.y, h.z);
                Vector3 p101 = centre + new Vector3(h.x, -h.y, h.z);
                Vector3 p111 = centre + new Vector3(h.x, h.y, h.z);
                Vector3 p011 = centre + new Vector3(-h.x, h.y, h.z);

                AddQuad(p000, p100, p110, p010);
                AddQuad(p001, p101, p111, p011);
                AddQuad(p000, p001, p011, p010);
                AddQuad(p100, p101, p111, p110);
                AddQuad(p010, p110, p111, p011);
                AddQuad(p000, p100, p101, p001);

                EnforceOutwardFrom(centre, marker);
            }

            public Mesh ToMesh(string meshName, bool closedSolid)
            {
                Mesh mesh = new Mesh { name = meshName };

                if (vertices.Count > 65000)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);

                if (closedSolid)
                {
                    EnsureOutwardWinding(mesh);
                }

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// Flips every triangle if the mesh encloses a negative signed volume, i.e. if it was
        /// built inside-out. Uses the divergence theorem, so it is exact for closed meshes.
        /// </summary>
        private static void EnsureOutwardWinding(Mesh mesh)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            double volume = 0.0;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]];
                Vector3 b = verts[tris[i + 1]];
                Vector3 c = verts[tris[i + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c));
            }

            if (volume >= 0.0)
            {
                return;
            }

            for (int i = 0; i < tris.Length; i += 3)
            {
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }

            mesh.SetTriangles(tris, 0);
        }

        /// <summary>
        /// Flips an open mesh (a disc, a plane) if its average face normal opposes the
        /// expected direction. The volume test does not apply to open surfaces.
        /// </summary>
        private static void EnsureFacing(Mesh mesh, Vector3 expectedNormal)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            Vector3 accumulated = Vector3.zero;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]];
                Vector3 b = verts[tris[i + 1]];
                Vector3 c = verts[tris[i + 2]];
                accumulated += Vector3.Cross(b - a, c - a);
            }

            if (Vector3.Dot(accumulated, expectedNormal) >= 0f)
            {
                return;
            }

            for (int i = 0; i < tris.Length; i += 3)
            {
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }

            mesh.SetTriangles(tris, 0);
        }

        // ================================================================ asset cache

        private static Mesh SaveOrLoad(string assetName, System.Func<Mesh> generate)
        {
            ProtoAssets.EnsureFolder(MeshFolder);
            string path = MeshFolder + "/" + assetName + ".asset";

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            Mesh mesh = generate();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static string Key(params float[] values)
        {
            string key = string.Empty;
            for (int i = 0; i < values.Length; i++)
            {
                key += "_" + Mathf.RoundToInt(values[i] * 1000f);
            }

            return key;
        }

        // ================================================================ chamfered box

        /// <summary>
        /// Unit-sized chamfered box (1x1x1) with the given bevel, so it can be scaled freely
        /// like a primitive cube. The bevel is what gives every solid its soft lit edge.
        /// 44 triangles.
        /// </summary>
        public static Mesh ChamferBox(float bevel = 0.08f)
        {
            bevel = Mathf.Clamp(bevel, 0.01f, 0.45f);
            return SaveOrLoad("Mesh_ChamferBox" + Key(bevel), () => BuildChamferBox(bevel));
        }

        private static Mesh BuildChamferBox(float bevel)
        {
            MeshBuilder builder = new MeshBuilder();
            int marker = builder.Marker;

            const float h = 0.5f;
            float i = h - bevel; // inset extent

            // Six inset faces.
            AddAxisFace(builder, 0, +1f, h, i);
            AddAxisFace(builder, 0, -1f, h, i);
            AddAxisFace(builder, 1, +1f, h, i);
            AddAxisFace(builder, 1, -1f, h, i);
            AddAxisFace(builder, 2, +1f, h, i);
            AddAxisFace(builder, 2, -1f, h, i);

            // Twelve bevel quads, one per cube edge, plus eight corner triangles.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 fromX = new Vector3(sx * h, sy * i, sz * i);
                        Vector3 fromY = new Vector3(sx * i, sy * h, sz * i);
                        Vector3 fromZ = new Vector3(sx * i, sy * i, sz * h);
                        builder.AddTriangle(fromX, fromY, fromZ);
                    }
                }
            }

            AddBevelEdges(builder, h, i);

            // The box is convex and centred on the origin, so this is an exact correction
            // for every face at once - no hand-derived winding to get wrong.
            builder.EnforceOutwardFrom(Vector3.zero, marker);

            return builder.ToMesh("ChamferBox", true);
        }

        /// <summary>One inset face of the chamfered box, perpendicular to the given axis.</summary>
        private static void AddAxisFace(MeshBuilder builder, int axis, float sign, float h, float i)
        {
            Vector3 normal = Vector3.zero;
            normal[axis] = sign * h;

            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            Vector3 du = Vector3.zero;
            du[u] = i;
            Vector3 dv = Vector3.zero;
            dv[v] = i;

            builder.AddQuad(
                normal - du - dv,
                normal + du - dv,
                normal + du + dv,
                normal - du + dv);
        }

        /// <summary>The twelve bevel quads running along each cube edge.</summary>
        private static void AddBevelEdges(MeshBuilder builder, float h, float i)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                for (int su = -1; su <= 1; su += 2)
                {
                    for (int sv = -1; sv <= 1; sv += 2)
                    {
                        Vector3 a = Vector3.zero;
                        a[axis] = -i;
                        a[u] = su * h;
                        a[v] = sv * i;

                        Vector3 b = Vector3.zero;
                        b[axis] = +i;
                        b[u] = su * h;
                        b[v] = sv * i;

                        Vector3 c = Vector3.zero;
                        c[axis] = +i;
                        c[u] = su * i;
                        c[v] = sv * h;

                        Vector3 d = Vector3.zero;
                        d[axis] = -i;
                        d[u] = su * i;
                        d[v] = sv * h;

                        builder.AddQuad(a, b, c, d);
                    }
                }
            }
        }

        // ================================================================ lathe

        /// <summary>
        /// Revolves a profile around the Y axis. Profile points are (radius, height).
        /// This one function covers trunks, stalks, barrels, silos, animal bodies and spheres.
        /// The mesh is centred on the profile height range and normalised to unit height.
        /// </summary>
        public static Mesh Lathe(string assetName, Vector2[] profile, int segments)
        {
            return SaveOrLoad(assetName, () => BuildLathe(profile, segments));
        }

        private static Mesh BuildLathe(Vector2[] profile, int segments)
        {
            MeshBuilder builder = new MeshBuilder();
            segments = Mathf.Max(3, segments);

            Vector3[][] rings = new Vector3[profile.Length][];

            for (int p = 0; p < profile.Length; p++)
            {
                rings[p] = new Vector3[segments];
                for (int s = 0; s < segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    rings[p][s] = new Vector3(
                        Mathf.Cos(angle) * profile[p].x,
                        profile[p].y,
                        Mathf.Sin(angle) * profile[p].x);
                }
            }

            for (int p = 0; p < profile.Length - 1; p++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;

                    Vector3 a = rings[p][s];
                    Vector3 b = rings[p][next];
                    Vector3 c = rings[p + 1][next];
                    Vector3 d = rings[p + 1][s];

                    // Degenerate strip where the profile pinches to the axis: emit a triangle.
                    bool bottomPinched = profile[p].x < 0.0001f;
                    bool topPinched = profile[p + 1].x < 0.0001f;

                    if (bottomPinched && topPinched)
                    {
                        continue;
                    }

                    if (bottomPinched)
                    {
                        builder.AddTriangle(new Vector3(0f, profile[p].y, 0f), c, d);
                    }
                    else if (topPinched)
                    {
                        builder.AddTriangle(a, b, new Vector3(0f, profile[p + 1].y, 0f));
                    }
                    else
                    {
                        builder.AddQuad(a, b, c, d);
                    }
                }
            }

            // Caps, only where the profile does not already close on the axis.
            if (profile[0].x > 0.0001f)
            {
                builder.AddFan(new Vector3(0f, profile[0].y, 0f), rings[0], false);
            }

            int last = profile.Length - 1;
            if (profile[last].x > 0.0001f)
            {
                builder.AddFan(new Vector3(0f, profile[last].y, 0f), rings[last], true);
            }

            // Enforce winding from the profile's own centre rather than the origin, so an
            // off-centre profile (a dome, a cap) is still corrected exactly.
            float minY = profile[0].y;
            float maxY = profile[0].y;
            for (int k = 1; k < profile.Length; k++)
            {
                minY = Mathf.Min(minY, profile[k].y);
                maxY = Mathf.Max(maxY, profile[k].y);
            }

            builder.EnforceOutwardFrom(new Vector3(0f, (minY + maxY) * 0.5f, 0f), 0);

            return builder.ToMesh("Lathe", true);
        }

        // ================================================================ common solids

        /// <summary>Unit-height, unit-diameter cylinder centred on the origin.</summary>
        public static Mesh Cylinder(int segments = 12)
        {
            return Lathe("Mesh_Cylinder_" + segments, new[]
            {
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f)
            }, segments);
        }

        /// <summary>Unit cone, base at -0.5, tip at +0.5.</summary>
        public static Mesh Cone(int segments = 12)
        {
            return Lathe("Mesh_Cone_" + segments, new[]
            {
                new Vector2(0.5f, -0.5f),
                new Vector2(0f, 0.5f)
            }, segments);
        }

        /// <summary>Truncated cone. topScale 1 = cylinder, 0 = cone.</summary>
        public static Mesh Tapered(float topScale, int segments = 12)
        {
            topScale = Mathf.Clamp01(topScale);
            return Lathe("Mesh_Tapered" + Key(topScale) + "_" + segments, new[]
            {
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f * topScale, 0.5f)
            }, segments);
        }

        /// <summary>
        /// Cylinder matching Unity's built-in primitive exactly: height 2, diameter 1.
        /// Lets every existing ProtoAssets.Cylinder call keep its scale values untouched.
        /// </summary>
        public static Mesh UnityStyleCylinder(int segments = 12)
        {
            return Lathe("Mesh_UnityCylinder_" + segments, new[]
            {
                new Vector2(0.5f, -1f),
                new Vector2(0.5f, 1f)
            }, segments);
        }

        /// <summary>
        /// Capsule matching Unity's built-in primitive: height 2, diameter 1, so the cylindrical
        /// mid-section runs from -0.5 to 0.5 with hemispherical caps of radius 0.5.
        /// </summary>
        public static Mesh UnityStyleCapsule(int capRings = 3, int segments = 12)
        {
            capRings = Mathf.Max(1, capRings);

            List<Vector2> profile = new List<Vector2>();

            // Bottom cap, pole upwards to the waist.
            for (int i = 0; i <= capRings; i++)
            {
                float t = i / (float)capRings;
                float angle = Mathf.PI * 0.5f * t;
                profile.Add(new Vector2(Mathf.Sin(angle) * 0.5f, -0.5f - Mathf.Cos(angle) * 0.5f));
            }

            // Top cap.
            for (int i = 0; i <= capRings; i++)
            {
                float t = i / (float)capRings;
                float angle = Mathf.PI * 0.5f * t;
                profile.Add(new Vector2(Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            return Lathe("Mesh_UnityCapsule_" + capRings + "_" + segments, profile.ToArray(), segments);
        }

        /// <summary>
        /// Faceted low-poly sphere built as a lathed semicircle.
        /// At the default 6 rings / 10 segments this is ~110 triangles against 768 for
        /// Unity's built-in sphere, and the facets suit the stylised look.
        /// </summary>
        public static Mesh LowPolySphere(int rings = 6, int segments = 10)
        {
            rings = Mathf.Max(2, rings);
            segments = Mathf.Max(4, segments);

            return Lathe("Mesh_Sphere_" + rings + "_" + segments, BuildSphereProfile(rings), segments);
        }

        private static Vector2[] BuildSphereProfile(int rings)
        {
            Vector2[] profile = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float angle = Mathf.PI * t; // 0 at the bottom pole, PI at the top
                profile[i] = new Vector2(Mathf.Sin(angle) * 0.5f, -Mathf.Cos(angle) * 0.5f);
            }

            return profile;
        }

        /// <summary>Rounded pebble / blob: a squashed sphere with an irregular waist.</summary>
        public static Mesh Pebble(int segments = 8)
        {
            return Lathe("Mesh_Pebble_" + segments, new[]
            {
                new Vector2(0f, -0.5f),
                new Vector2(0.34f, -0.34f),
                new Vector2(0.50f, -0.05f),
                new Vector2(0.44f, 0.22f),
                new Vector2(0.24f, 0.42f),
                new Vector2(0f, 0.5f)
            }, segments);
        }

        /// <summary>Barrel silhouette with a bulged waist.</summary>
        public static Mesh Barrel(int segments = 12)
        {
            return Lathe("Mesh_Barrel_" + segments, new[]
            {
                new Vector2(0.40f, -0.5f),
                new Vector2(0.48f, -0.28f),
                new Vector2(0.50f, 0f),
                new Vector2(0.48f, 0.28f),
                new Vector2(0.40f, 0.5f)
            }, segments);
        }

        /// <summary>Dome for silo caps and haystacks. Flat base, rounded top.</summary>
        public static Mesh Dome(int rings = 4, int segments = 12)
        {
            Vector2[] profile = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float angle = Mathf.PI * 0.5f * t;
                profile[i] = new Vector2(Mathf.Cos(angle) * 0.5f, -0.5f + Mathf.Sin(angle));
            }

            return Lathe("Mesh_Dome_" + rings + "_" + segments, profile, segments);
        }

        /// <summary>
        /// Spindle used for wheat ears and corn cobs: pinched at both ends, fat in the middle.
        /// </summary>
        public static Mesh Spindle(int segments = 7)
        {
            return Lathe("Mesh_Spindle_" + segments, new[]
            {
                new Vector2(0f, -0.5f),
                new Vector2(0.30f, -0.28f),
                new Vector2(0.50f, 0f),
                new Vector2(0.34f, 0.30f),
                new Vector2(0f, 0.5f)
            }, segments);
        }

        // ================================================================ farm plot soil

        /// <summary>
        /// A 1x1x1 slab whose top face carries raised furrow ridges running along Z.
        /// Used as the farm plot soil so a plot reads as ploughed earth from geometry alone,
        /// which lets FarmPlot keep doing nothing but tint a single renderer.
        /// </summary>
        public static Mesh FurrowedSlab(int ridges = 4)
        {
            ridges = Mathf.Clamp(ridges, 1, 8);
            return SaveOrLoad("Mesh_FurrowedSlab_" + ridges, () => BuildFurrowedSlab(ridges));
        }

        private static Mesh BuildFurrowedSlab(int ridges)
        {
            MeshBuilder builder = new MeshBuilder();

            const float slabTop = 0.14f;   // top of the flat base, in unit space
            const float crest = 0.5f;      // crest of the ridges

            // Built as overlapping convex pieces rather than one hand-wound shell: each piece
            // gets its winding enforced from its own centre, which is exact, and the overlaps
            // are invisible because the whole slab is one material.
            // Base slab spans y from -0.5 up to slabTop.
            builder.AddBox(new Vector3(0f, (slabTop - 0.5f) * 0.5f, 0f),
                new Vector3(1f, slabTop + 0.5f, 1f));

            float pitch = 1f / ridges;
            float ridgeHeight = crest - slabTop;

            for (int i = 0; i < ridges; i++)
            {
                float x = -0.5f + pitch * (i + 0.5f);
                AddRidge(builder, x, slabTop, ridgeHeight, pitch * 0.62f, pitch * 0.3f);
            }

            return builder.ToMesh("FurrowedSlab", true);
        }

        /// <summary>
        /// One trapezoidal furrow ridge running the full length in Z: wide at the soil,
        /// narrower at the crest. Convex, so its winding is enforced exactly.
        /// </summary>
        private static void AddRidge(
            MeshBuilder builder, float x, float baseY, float height, float baseWidth, float topWidth)
        {
            int marker = builder.Marker;

            float b = baseWidth * 0.5f;
            float t = topWidth * 0.5f;
            float topY = baseY + height;
            const float z = 0.5f;

            Vector3 a0 = new Vector3(x - b, baseY, -z);
            Vector3 a1 = new Vector3(x + b, baseY, -z);
            Vector3 b0 = new Vector3(x - b, baseY, z);
            Vector3 b1 = new Vector3(x + b, baseY, z);

            Vector3 c0 = new Vector3(x - t, topY, -z);
            Vector3 c1 = new Vector3(x + t, topY, -z);
            Vector3 d0 = new Vector3(x - t, topY, z);
            Vector3 d1 = new Vector3(x + t, topY, z);

            builder.AddQuad(c0, c1, d1, d0);   // crest
            builder.AddQuad(a0, c0, d0, b0);   // left flank
            builder.AddQuad(a1, b1, d1, c1);   // right flank
            builder.AddQuad(a0, a1, c1, c0);   // near gable
            builder.AddQuad(b0, d0, d1, b1);   // far gable
            builder.AddQuad(a0, b0, b1, a1);   // underside

            builder.EnforceOutwardFrom(new Vector3(x, baseY + height * 0.4f, 0f), marker);
        }

        // ================================================================ phase 4A shapes

        /// <summary>
        /// Gambrel roof: the four-slope barn profile, shallow on top and steep at the eaves.
        /// This one shape is what makes a barn read as a barn from across the farm, which a
        /// plain triangular prism never does. Unit sized, ridge along X.
        /// </summary>
        public static Mesh GambrelPrism(float shoulderHeight = 0.55f, float shoulderWidth = 0.72f)
        {
            shoulderHeight = Mathf.Clamp(shoulderHeight, 0.2f, 0.85f);
            shoulderWidth = Mathf.Clamp(shoulderWidth, 0.3f, 0.95f);

            return SaveOrLoad("Mesh_Gambrel" + Key(shoulderHeight, shoulderWidth),
                () => BuildExtrudedProfile("Gambrel", new[]
                {
                    new Vector2(-0.5f, -0.5f),
                    new Vector2(-0.5f * shoulderWidth, -0.5f + shoulderHeight),
                    new Vector2(0f, 0.5f),
                    new Vector2(0.5f * shoulderWidth, -0.5f + shoulderHeight),
                    new Vector2(0.5f, -0.5f)
                }));
        }

        /// <summary>
        /// Hipped roof: slopes on all four sides meeting at a short ridge. Softer and more
        /// domestic than a gable, which is what separates the farmhouse from the barn.
        /// </summary>
        public static Mesh HipRoof(float ridgeFraction = 0.42f)
        {
            ridgeFraction = Mathf.Clamp(ridgeFraction, 0.05f, 0.9f);
            return SaveOrLoad("Mesh_HipRoof" + Key(ridgeFraction), () => BuildHipRoof(ridgeFraction));
        }

        private static Mesh BuildHipRoof(float ridgeFraction)
        {
            MeshBuilder builder = new MeshBuilder();
            int marker = builder.Marker;

            const float h = 0.5f;
            float r = h * ridgeFraction;

            Vector3 bbl = new Vector3(-h, -h, -h);
            Vector3 bbr = new Vector3(h, -h, -h);
            Vector3 bfl = new Vector3(-h, -h, h);
            Vector3 bfr = new Vector3(h, -h, h);

            Vector3 ridgeL = new Vector3(-r, h, 0f);
            Vector3 ridgeR = new Vector3(r, h, 0f);

            builder.AddQuad(bfl, bfr, ridgeR, ridgeL);   // front slope
            builder.AddQuad(bbr, bbl, ridgeL, ridgeR);   // back slope
            builder.AddTriangle(bbl, bfl, ridgeL);       // left hip
            builder.AddTriangle(bfr, bbr, ridgeR);       // right hip
            builder.AddQuad(bbl, bbr, bfr, bfl);         // underside

            builder.EnforceOutwardFrom(new Vector3(0f, -0.1f, 0f), marker);
            return builder.ToMesh("HipRoof", true);
        }

        /// <summary>
        /// Extrudes a closed 2D profile (XY) along Z by one unit. Shared by the roof shapes.
        /// The profile must be convex for the winding correction to be exact.
        /// </summary>
        private static Mesh BuildExtrudedProfile(string meshName, Vector2[] profile)
        {
            MeshBuilder builder = new MeshBuilder();
            int marker = builder.Marker;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < profile.Length; i++)
            {
                centroid += new Vector3(profile[i].x, profile[i].y, 0f);
            }

            centroid /= profile.Length;

            const float back = -0.5f;
            const float front = 0.5f;

            // Side walls of the extrusion.
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 a = profile[i];
                Vector2 b = profile[(i + 1) % profile.Length];

                builder.AddQuad(
                    new Vector3(a.x, a.y, back),
                    new Vector3(b.x, b.y, back),
                    new Vector3(b.x, b.y, front),
                    new Vector3(a.x, a.y, front));
            }

            // End caps as fans from the profile centroid.
            Vector3[] backRim = new Vector3[profile.Length];
            Vector3[] frontRim = new Vector3[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                backRim[i] = new Vector3(profile[i].x, profile[i].y, back);
                frontRim[i] = new Vector3(profile[i].x, profile[i].y, front);
            }

            builder.AddFan(new Vector3(centroid.x, centroid.y, back), backRim, false);
            builder.AddFan(new Vector3(centroid.x, centroid.y, front), frontRim, false);

            builder.EnforceOutwardFrom(new Vector3(centroid.x, centroid.y, 0f), marker);
            return builder.ToMesh(meshName, true);
        }

        /// <summary>
        /// Flat disc with a seeded, irregular rim. Ground patches, pond shores and path blobs
        /// built from this stop reading as circles, which is what makes the terrain organic.
        /// </summary>
        public static Mesh IrregularDisc(int seed, int segments = 14, float jitter = 0.22f)
        {
            return SaveOrLoad("Mesh_IrregDisc_" + seed + "_" + segments + Key(jitter), () =>
            {
                MeshBuilder builder = new MeshBuilder();
                System.Random random = new System.Random(seed);

                Vector3[] rim = new Vector3[segments];
                for (int s = 0; s < segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    float radius = 0.5f * (1f - jitter * 0.5f + (float)random.NextDouble() * jitter);
                    rim[s] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }

                builder.AddFan(Vector3.zero, rim, false);

                Mesh mesh = builder.ToMesh("IrregularDisc", false);
                EnsureFacing(mesh, Vector3.up);
                mesh.RecalculateNormals();
                return mesh;
            });
        }

        /// <summary>
        /// A soft landscape mound: a lathed dome with a seeded irregular rim so a row of them
        /// reads as rolling ground rather than a line of identical bumps.
        /// Purely decorative - these live outside the walkable boundary.
        /// </summary>
        public static Mesh Mound(int seed, int rings = 4, int segments = 12)
        {
            rings = Mathf.Max(2, rings);
            segments = Mathf.Max(4, segments);

            return SaveOrLoad("Mesh_Mound_" + seed + "_" + rings + "_" + segments, () =>
            {
                MeshBuilder builder = new MeshBuilder();
                System.Random random = new System.Random(seed);

                // Per-column radius scale, smoothed around the ring so the silhouette undulates.
                float[] columnScale = new float[segments];
                for (int s = 0; s < segments; s++)
                {
                    columnScale[s] = 0.82f + (float)random.NextDouble() * 0.36f;
                }

                // Rings stop short of the pole so no ring collapses to zero radius; the
                // apex is then closed with a single fan. This avoids degenerate triangles,
                // which would otherwise give the summit zero-length normals and shade black.
                Vector3[][] ringVerts = new Vector3[rings][];
                for (int r = 0; r < rings; r++)
                {
                    float t = r / (float)rings;
                    float angle = Mathf.PI * 0.5f * t;
                    float ringRadius = Mathf.Cos(angle) * 0.5f;
                    float ringHeight = Mathf.Sin(angle) * 0.5f - 0.5f;

                    ringVerts[r] = new Vector3[segments];
                    for (int s = 0; s < segments; s++)
                    {
                        float a = s / (float)segments * Mathf.PI * 2f;
                        float radius = ringRadius * columnScale[s];
                        ringVerts[r][s] = new Vector3(Mathf.Cos(a) * radius, ringHeight, Mathf.Sin(a) * radius);
                    }
                }

                for (int r = 0; r < rings - 1; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int next = (s + 1) % segments;
                        builder.AddQuad(
                            ringVerts[r][s], ringVerts[r][next],
                            ringVerts[r + 1][next], ringVerts[r + 1][s]);
                    }
                }

                // Close the summit and the base.
                builder.AddFan(new Vector3(0f, 0f, 0f), ringVerts[rings - 1], false);
                builder.AddFan(new Vector3(0f, -0.5f, 0f), ringVerts[0], true);

                builder.EnforceOutwardFrom(new Vector3(0f, -0.25f, 0f), 0);
                return builder.ToMesh("Mound", true);
            });
        }

        /// <summary>
        /// A plank: a box bevelled only along its long edges, so fence rails and decking read
        /// as sawn timber rather than as chamfered bricks. Unit sized, length along Z.
        /// </summary>
        public static Mesh Plank(float bevel = 0.12f)
        {
            bevel = Mathf.Clamp(bevel, 0.02f, 0.4f);
            return SaveOrLoad("Mesh_Plank" + Key(bevel), () =>
            {
                MeshBuilder builder = new MeshBuilder();
                int marker = builder.Marker;

                float h = 0.5f;
                float i = h - bevel;

                // Octagonal cross-section in XY, extruded along Z.
                Vector2[] section =
                {
                    new Vector2(-i, -h), new Vector2(i, -h),
                    new Vector2(h, -i), new Vector2(h, i),
                    new Vector2(i, h), new Vector2(-i, h),
                    new Vector2(-h, i), new Vector2(-h, -i)
                };

                Vector3[] backRim = new Vector3[section.Length];
                Vector3[] frontRim = new Vector3[section.Length];

                for (int s = 0; s < section.Length; s++)
                {
                    backRim[s] = new Vector3(section[s].x, section[s].y, -h);
                    frontRim[s] = new Vector3(section[s].x, section[s].y, h);
                }

                for (int s = 0; s < section.Length; s++)
                {
                    int next = (s + 1) % section.Length;
                    builder.AddQuad(backRim[s], backRim[next], frontRim[next], frontRim[s]);
                }

                builder.AddFan(new Vector3(0f, 0f, -h), backRim, false);
                builder.AddFan(new Vector3(0f, 0f, h), frontRim, false);

                builder.EnforceOutwardFrom(Vector3.zero, marker);
                return builder.ToMesh("Plank", true);
            });
        }

        /// <summary>
        /// A box with a batter: wider at the base than the top. Building bodies built from this
        /// sit into the ground instead of looking like a crate dropped on the lawn.
        /// </summary>
        public static Mesh Trapezoid(float topScale = 0.9f)
        {
            // Values above 1 flare outwards, which is what buckets, tubs and barrow trays
            // need; below 1 gives a building batter. Both stay convex, so the winding
            // correction remains exact.
            topScale = Mathf.Clamp(topScale, 0.2f, 2.2f);
            return SaveOrLoad("Mesh_Trapezoid" + Key(topScale), () =>
            {
                MeshBuilder builder = new MeshBuilder();
                int marker = builder.Marker;

                const float h = 0.5f;
                float t = h * topScale;

                Vector3 b0 = new Vector3(-h, -h, -h);
                Vector3 b1 = new Vector3(h, -h, -h);
                Vector3 b2 = new Vector3(h, -h, h);
                Vector3 b3 = new Vector3(-h, -h, h);

                Vector3 t0 = new Vector3(-t, h, -t);
                Vector3 t1 = new Vector3(t, h, -t);
                Vector3 t2 = new Vector3(t, h, t);
                Vector3 t3 = new Vector3(-t, h, t);

                builder.AddQuad(b0, b1, t1, t0);
                builder.AddQuad(b1, b2, t2, t1);
                builder.AddQuad(b2, b3, t3, t2);
                builder.AddQuad(b3, b0, t0, t3);
                builder.AddQuad(t0, t1, t2, t3);
                builder.AddQuad(b3, b2, b1, b0);

                builder.EnforceOutwardFrom(Vector3.zero, marker);
                return builder.ToMesh("Trapezoid", true);
            });
        }

        // ================================================================ organic actor shapes

        /// <summary>
        /// Ovoid: taller than wide, fattest below centre. The default head and body shape for
        /// every actor. A sphere reads as a ball; an egg reads as a skull or a torso, and that
        /// difference is most of what separates a designed character from assembled primitives.
        /// </summary>
        public static Mesh Egg(int rings = 7, int segments = 10)
        {
            rings = Mathf.Max(3, rings);
            segments = Mathf.Max(5, segments);

            Vector2[] profile = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float angle = Mathf.PI * t;

                // Radius falls off faster towards the top than the bottom.
                float bias = Mathf.Lerp(1.08f, 0.82f, t * t);
                profile[i] = new Vector2(Mathf.Sin(angle) * 0.5f * bias, -Mathf.Cos(angle) * 0.5f);
            }

            return Lathe("Mesh_Egg_" + rings + "_" + segments, profile, segments);
        }

        /// <summary>
        /// Fat-bottomed ovoid with a rounded shoulder: muzzles, hands, wattles, udders.
        /// Wider and squatter than <see cref="Egg"/>.
        /// </summary>
        public static Mesh Pear(int segments = 9)
        {
            return Lathe("Mesh_Pear_" + segments, new[]
            {
                new Vector2(0f, -0.5f),
                new Vector2(0.30f, -0.40f),
                new Vector2(0.46f, -0.20f),
                new Vector2(0.50f, 0.02f),
                new Vector2(0.42f, 0.24f),
                new Vector2(0.26f, 0.40f),
                new Vector2(0f, 0.5f)
            }, segments);
        }

        /// <summary>Rounded at the base, drawn to a point on top. Comb lobes, tail tufts, ears.</summary>
        public static Mesh Teardrop(int segments = 8)
        {
            return Lathe("Mesh_Teardrop_" + segments, new[]
            {
                new Vector2(0f, -0.5f),
                new Vector2(0.32f, -0.36f),
                new Vector2(0.48f, -0.12f),
                new Vector2(0.44f, 0.10f),
                new Vector2(0.28f, 0.30f),
                new Vector2(0f, 0.5f)
            }, segments);
        }

        /// <summary>
        /// A cylinder with domed caps and a slight waist: the workhorse limb segment.
        /// Chunky, and it never shows a hard rim where it meets the next joint.
        /// </summary>
        public static Mesh Drum(int segments = 9)
        {
            return Lathe("Mesh_Drum_" + segments, new[]
            {
                new Vector2(0f, -0.5f),
                new Vector2(0.34f, -0.44f),
                new Vector2(0.48f, -0.32f),
                new Vector2(0.50f, 0f),
                new Vector2(0.48f, 0.32f),
                new Vector2(0.34f, 0.44f),
                new Vector2(0f, 0.5f)
            }, segments);
        }

        /// <summary>
        /// Triangular wedge pointing along +Z, tapering to a short vertical edge rather than a
        /// needle point so the silhouette stays readable. Beaks, boot soles, hooves, eyebrows.
        /// </summary>
        public static Mesh Wedge(float tipHeight = 0.28f)
        {
            tipHeight = Mathf.Clamp(tipHeight, 0.02f, 0.9f);

            return SaveOrLoad("Mesh_Wedge" + Key(tipHeight), () =>
            {
                MeshBuilder builder = new MeshBuilder();
                int marker = builder.Marker;

                const float h = 0.5f;
                float tip = h * tipHeight;

                Vector3 backBL = new Vector3(-h, -h, -h);
                Vector3 backBR = new Vector3(h, -h, -h);
                Vector3 backTR = new Vector3(h, h, -h);
                Vector3 backTL = new Vector3(-h, h, -h);
                Vector3 tipB = new Vector3(0f, -tip, h);
                Vector3 tipT = new Vector3(0f, tip, h);

                builder.AddQuad(backBL, backBR, backTR, backTL);  // back
                builder.AddQuad(backBL, backTL, tipT, tipB);      // left flank
                builder.AddQuad(backBR, tipB, tipT, backTR);      // right flank
                builder.AddTriangle(backTL, backTR, tipT);        // top
                builder.AddTriangle(backBL, tipB, backBR);        // bottom

                builder.EnforceOutwardFrom(new Vector3(0f, 0f, -0.15f), marker);
                return builder.ToMesh("Wedge", true);
            });
        }

        /// <summary>
        /// A tapering horn that curves as it rises, following an exact circular arc of unit
        /// length. Straight cones read as spikes; the sweep is what makes a horn a horn.
        /// Base sits at y = -0.5, curving towards -X.
        /// </summary>
        public static Mesh Crescent(float sweepDegrees = 55f, int rings = 6, int segments = 7)
        {
            sweepDegrees = Mathf.Clamp(sweepDegrees, 0f, 150f);
            rings = Mathf.Max(3, rings);
            segments = Mathf.Max(4, segments);

            return SaveOrLoad("Mesh_Crescent" + Key(sweepDegrees) + "_" + rings + "_" + segments, () =>
            {
                MeshBuilder builder = new MeshBuilder();

                float sweep = sweepDegrees * Mathf.Deg2Rad;
                bool curved = sweep > 0.01f;
                float arcRadius = curved ? 1f / sweep : 0f;

                Vector3[][] ringVerts = new Vector3[rings + 1][];
                Vector3[] ringCentres = new Vector3[rings + 1];

                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    float a = sweep * t;

                    // Exact arc: centre of curvature sits at (-arcRadius, -0.5, 0).
                    Vector3 centre = curved
                        ? new Vector3(-arcRadius + arcRadius * Mathf.Cos(a), -0.5f + arcRadius * Mathf.Sin(a), 0f)
                        : new Vector3(0f, -0.5f + t, 0f);

                    Vector3 direction = curved
                        ? new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f)
                        : Vector3.up;

                    Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
                    Vector3 forward = Vector3.Cross(right, direction).normalized;

                    float radius = Mathf.Lerp(0.5f, 0.05f, t * t);

                    ringCentres[r] = centre;
                    ringVerts[r] = new Vector3[segments];

                    for (int s = 0; s < segments; s++)
                    {
                        float angle = s / (float)segments * Mathf.PI * 2f;
                        ringVerts[r][s] = centre
                            + right * (Mathf.Cos(angle) * radius)
                            + forward * (Mathf.Sin(angle) * radius);
                    }
                }

                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int next = (s + 1) % segments;
                        builder.AddQuad(
                            ringVerts[r][s], ringVerts[r][next],
                            ringVerts[r + 1][next], ringVerts[r + 1][s]);
                    }
                }

                builder.AddFan(ringCentres[0], ringVerts[0], true);
                builder.AddFan(ringCentres[rings], ringVerts[rings], false);

                // A swept horn is not star-shaped about any single point, so the per-face
                // correction would be unreliable here. The closed-volume test in ToMesh is
                // the right backstop, and it is exact for a closed mesh like this one.
                return builder.ToMesh("Crescent", true);
            });
        }

        // ================================================================ prism (roofs)

        /// <summary>
        /// Triangular prism with the ridge running along X, unit sized.
        /// Used for every pitched roof so a roof is one mesh instead of two rotated slabs.
        /// </summary>
        public static Mesh RoofPrism()
        {
            return SaveOrLoad("Mesh_RoofPrism", BuildRoofPrism);
        }

        private static Mesh BuildRoofPrism()
        {
            MeshBuilder builder = new MeshBuilder();
            int marker = builder.Marker;

            const float h = 0.5f;

            Vector3 leftBackBottom = new Vector3(-h, -h, -h);
            Vector3 rightBackBottom = new Vector3(h, -h, -h);
            Vector3 leftFrontBottom = new Vector3(-h, -h, h);
            Vector3 rightFrontBottom = new Vector3(h, -h, h);
            Vector3 leftRidge = new Vector3(-h, h, 0f);
            Vector3 rightRidge = new Vector3(h, h, 0f);

            // Two sloping faces.
            builder.AddQuad(leftFrontBottom, rightFrontBottom, rightRidge, leftRidge);
            builder.AddQuad(rightBackBottom, leftBackBottom, leftRidge, rightRidge);

            // Gable ends.
            builder.AddTriangle(leftBackBottom, leftFrontBottom, leftRidge);
            builder.AddTriangle(rightFrontBottom, rightBackBottom, rightRidge);

            // Underside.
            builder.AddQuad(leftBackBottom, rightBackBottom, rightFrontBottom, leftFrontBottom);

            // Convex, and its interior contains this point.
            builder.EnforceOutwardFrom(new Vector3(0f, -0.1f, 0f), marker);

            return builder.ToMesh("RoofPrism", true);
        }

        // ================================================================ flat shapes

        /// <summary>Flat horizontal disc of unit diameter facing +Y. For ponds and shadows.</summary>
        public static Mesh Disc(int segments = 16)
        {
            return SaveOrLoad("Mesh_Disc_" + segments, () =>
            {
                MeshBuilder builder = new MeshBuilder();

                Vector3[] rim = new Vector3[segments];
                for (int s = 0; s < segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    rim[s] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
                }

                builder.AddFan(Vector3.zero, rim, false);

                Mesh mesh = builder.ToMesh("Disc", false);
                EnsureFacing(mesh, Vector3.up);
                mesh.RecalculateNormals();
                return mesh;
            });
        }

        /// <summary>
        /// Flat leaf blade: a tapered quad strip with a slight curve, facing +Y.
        /// Double sided so it reads from any camera angle without a two-sided shader.
        /// </summary>
        public static Mesh LeafBlade(int segments = 4, float curve = 0.25f)
        {
            return SaveOrLoad("Mesh_Leaf_" + segments + Key(curve), () =>
            {
                MeshBuilder builder = new MeshBuilder();

                for (int i = 0; i < segments; i++)
                {
                    float t0 = i / (float)segments;
                    float t1 = (i + 1) / (float)segments;

                    float w0 = Mathf.Sin(t0 * Mathf.PI) * 0.5f + 0.04f;
                    float w1 = Mathf.Sin(t1 * Mathf.PI) * 0.5f + 0.04f;

                    if (i == segments - 1)
                    {
                        w1 = 0f;
                    }

                    float y0 = -curve * t0 * t0;
                    float y1 = -curve * t1 * t1;

                    Vector3 a = new Vector3(-w0, y0, t0);
                    Vector3 b = new Vector3(w0, y0, t0);
                    Vector3 c = new Vector3(w1, y1, t1);
                    Vector3 d = new Vector3(-w1, y1, t1);

                    builder.AddQuad(a, b, c, d);
                    builder.AddQuad(d, c, b, a); // back face
                }

                return builder.ToMesh("Leaf", false);
            });
        }

        // ================================================================ combining

        /// <summary>
        /// Collapses a prop hierarchy into a single MeshRenderer with one submesh per material.
        /// This is what keeps a mature wheat plot at two draw calls instead of nine.
        /// Falls back to leaving the hierarchy untouched if anything looks wrong.
        /// </summary>
        public static bool CombineIntoSingleRenderer(GameObject root, string meshAssetName)
        {
            if (root == null)
            {
                return false;
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(false);
            if (filters.Length < 2)
            {
                return false;
            }

            // Group source meshes by material, preserving first-seen material order.
            List<Material> materials = new List<Material>();
            List<List<CombineInstance>> groups = new List<List<CombineInstance>>();

            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;

            // Combining collapses many renderers into one, so the single surviving renderer
            // has to inherit shadow casting from the parts. Without this a whole tree would
            // silently stop casting because the merged renderer defaulted to off.
            bool anyCastsShadows = false;

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

                if (filter.sharedMesh == null || renderer == null || renderer.sharedMaterial == null)
                {
                    return false;
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    anyCastsShadows = true;
                }

                Material material = renderer.sharedMaterial;
                int index = materials.IndexOf(material);

                if (index < 0)
                {
                    materials.Add(material);
                    groups.Add(new List<CombineInstance>());
                    index = materials.Count - 1;
                }

                groups[index].Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    subMeshIndex = 0,
                    transform = worldToRoot * filter.transform.localToWorldMatrix
                });
            }

            // Pass 1: one merged mesh per material.
            Mesh[] perMaterial = new Mesh[groups.Count];
            for (int i = 0; i < groups.Count; i++)
            {
                Mesh merged = new Mesh();
                merged.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                merged.CombineMeshes(groups[i].ToArray(), true, true);
                perMaterial[i] = merged;
            }

            // Pass 2: stitch those into one mesh with a submesh each.
            CombineInstance[] finalCombine = new CombineInstance[perMaterial.Length];
            for (int i = 0; i < perMaterial.Length; i++)
            {
                finalCombine[i] = new CombineInstance
                {
                    mesh = perMaterial[i],
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                };
            }

            Mesh combined = new Mesh { name = meshAssetName };
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.CombineMeshes(finalCombine, false, true);
            combined.RecalculateNormals();
            combined.RecalculateBounds();

            for (int i = 0; i < perMaterial.Length; i++)
            {
                Object.DestroyImmediate(perMaterial[i]);
            }

            if (combined.vertexCount == 0)
            {
                Debug.LogWarning("Little Farm Story: combining '" + meshAssetName + "' produced an empty mesh; keeping the original hierarchy.");
                Object.DestroyImmediate(combined);
                return false;
            }

            ProtoAssets.EnsureFolder(MeshFolder);
            string path = MeshFolder + "/" + meshAssetName + ".asset";

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(combined, path);

            // Remove the source children, then host the combined mesh on the root itself.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child.GetComponentInChildren<MeshFilter>(true) != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            MeshFilter rootFilter = root.GetComponent<MeshFilter>();
            if (rootFilter == null)
            {
                rootFilter = root.AddComponent<MeshFilter>();
            }

            rootFilter.sharedMesh = combined;

            MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
            if (rootRenderer == null)
            {
                rootRenderer = root.AddComponent<MeshRenderer>();
            }

            rootRenderer.sharedMaterials = materials.ToArray();
            ProtoAssets.ApplyMobileRendererSettings(rootRenderer, anyCastsShadows);

            return true;
        }
    }
}
