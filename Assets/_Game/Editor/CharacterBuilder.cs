using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Builds the farmer and the animal props from the stylised mesh library.
    /// These are high-quality placeholders: chunky, readable silhouettes with strong colour
    /// blocking, built to look right from the gameplay camera rather than up close.
    /// Nothing here is rigged - a future Animator-driven model replaces the visual only.
    /// </summary>
    public static class CharacterBuilder
    {
        public const string CharacterFolder = ProtoAssets.PrefabsFolder + "/Characters";

        // ================================================================ farmer

        /// <summary>
        /// Populates the farmer body under the supplied transform.
        /// The caller owns the hierarchy, because PlayerController rotates the parent and
        /// PlayerVisualBob animates this one.
        /// </summary>
        public static void BuildFarmer(Transform bobRoot, ProtoPalette p)
        {
            Mesh box = StylizedMeshLibrary.ChamferBox(0.14f);
            Mesh softBox = StylizedMeshLibrary.ChamferBox(0.28f);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh taper = StylizedMeshLibrary.Tapered(0.72f);
            Mesh cone = StylizedMeshLibrary.Cone(12);

            // ---- legs and boots
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.135f * side;

                ProtoAssets.MeshObject(taper, "Leg_" + side, bobRoot, new Vector3(x, 0.36f, 0f),
                    new Vector3(0.2f, 0.52f, 0.2f), p.Denim, false);

                GameObject boot = ProtoAssets.MeshObject(softBox, "Boot_" + side, bobRoot,
                    new Vector3(x, 0.09f, 0.05f), new Vector3(0.25f, 0.18f, 0.36f), p.Boots, false);
                boot.transform.localRotation = Quaternion.Euler(0f, side * 5f, 0f);
            }

            // ---- torso: a chunky rounded block, shirt over dungarees
            ProtoAssets.MeshObject(softBox, "Torso", bobRoot, new Vector3(0f, 0.86f, 0f),
                new Vector3(0.56f, 0.5f, 0.4f), p.Shirt, true);

            ProtoAssets.MeshObject(softBox, "Dungarees", bobRoot, new Vector3(0f, 0.63f, 0f),
                new Vector3(0.54f, 0.28f, 0.39f), p.Denim, false);

            // Bib and straps read instantly as farm clothing from above.
            ProtoAssets.MeshObject(box, "Bib", bobRoot, new Vector3(0f, 0.88f, 0.2f),
                new Vector3(0.3f, 0.26f, 0.04f), p.Denim, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(box, "Strap_" + side, bobRoot,
                    new Vector3(0.11f * side, 0.98f, 0.19f),
                    new Vector3(0.07f, 0.24f, 0.04f), p.Denim, false);
            }

            ProtoAssets.MeshObject(box, "Button", bobRoot, new Vector3(0f, 0.76f, 0.215f),
                new Vector3(0.06f, 0.06f, 0.03f), p.Amber, false);

            // ---- arms
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 0.34f * side;

                GameObject sleeve = ProtoAssets.MeshObject(taper, "Sleeve_" + side, bobRoot,
                    new Vector3(x, 0.9f, 0f), new Vector3(0.17f, 0.3f, 0.17f), p.Shirt, false);
                sleeve.transform.localRotation = Quaternion.Euler(0f, 0f, side * -9f);

                ProtoAssets.MeshObject(sphere, "Hand_" + side, bobRoot,
                    new Vector3(x + 0.03f * side, 0.72f, 0.02f),
                    Vector3.one * 0.15f, p.Skin, false);
            }

            // ---- head
            ProtoAssets.MeshObject(sphere, "Head", bobRoot, new Vector3(0f, 1.26f, 0f),
                new Vector3(0.44f, 0.46f, 0.42f), p.Skin, true);

            ProtoAssets.MeshObject(sphere, "Nose", bobRoot, new Vector3(0f, 1.24f, 0.19f),
                new Vector3(0.09f, 0.08f, 0.09f), p.Skin, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(sphere, "Eye_" + side, bobRoot,
                    new Vector3(0.1f * side, 1.31f, 0.175f),
                    new Vector3(0.07f, 0.09f, 0.05f), p.Charcoal, false);

                ProtoAssets.MeshObject(sphere, "Ear_" + side, bobRoot,
                    new Vector3(0.215f * side, 1.25f, 0f),
                    new Vector3(0.08f, 0.11f, 0.07f), p.Skin, false);
            }

            // Cheeks warm the face up and stop the head reading as a bare ball.
            // Flattened spheres rather than discs: a disc mesh faces +Y and would be edge-on.
            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(sphere, "Cheek_" + side, bobRoot,
                    new Vector3(0.145f * side, 1.21f, 0.155f),
                    new Vector3(0.11f, 0.08f, 0.06f), p.Muzzle, false);
            }

            // ---- straw hat: coned brim, short crown, ribbon
            // A cone is wide at its base and narrow at its top, which is exactly a hat brim.
            ProtoAssets.MeshObject(cone, "HatBrim", bobRoot,
                new Vector3(0f, 1.47f, 0f), new Vector3(0.78f, 0.16f, 0.78f), p.Straw, true);

            ProtoAssets.MeshObject(taper, "HatCrown", bobRoot, new Vector3(0f, 1.57f, 0f),
                new Vector3(0.42f, 0.2f, 0.42f), p.Straw, false);

            ProtoAssets.MeshObject(taper, "HatBand", bobRoot, new Vector3(0f, 1.51f, 0f),
                new Vector3(0.45f, 0.06f, 0.45f), p.RoofTerracotta, false);
        }

        // ================================================================ animals

        public static GameObject BuildChickenPrefab(ProtoPalette p)
        {
            return BuildPrefab("Prop_Chicken", root => Chicken(root, p));
        }

        public static GameObject BuildCowPrefab(ProtoPalette p)
        {
            return BuildPrefab("Prop_Cow", root => Cow(root, p));
        }

        private static GameObject BuildPrefab(string prefabName, System.Action<Transform> populate)
        {
            ProtoAssets.EnsureFolder(CharacterFolder);

            GameObject temp = new GameObject(prefabName);
            populate(temp.transform);

            StylizedMeshLibrary.CombineIntoSingleRenderer(temp, "Mesh_" + prefabName);

            string path = CharacterFolder + "/" + prefabName + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path, out bool success);
            Object.DestroyImmediate(temp);

            if (!success || asset == null)
            {
                Debug.LogError("Little Farm Story: failed to save character prefab " + path);
                return null;
            }

            return asset;
        }

        /// <summary>Plump rounded hen: egg body, small head, orange beak and legs, red comb.</summary>
        private static void Chicken(Transform root, ProtoPalette p)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.25f);
            Mesh cone = StylizedMeshLibrary.Cone(8);

            ProtoAssets.MeshObject(sphere, "Body", root, new Vector3(0f, 0.33f, -0.03f),
                new Vector3(0.42f, 0.4f, 0.52f), p.White, true);

            // Wing plates catch the light and break the egg outline.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject wing = ProtoAssets.MeshObject(sphere, "Wing_" + side, root,
                    new Vector3(0.19f * side, 0.34f, -0.03f),
                    new Vector3(0.1f, 0.26f, 0.34f), p.White, false);
                wing.transform.localRotation = Quaternion.Euler(0f, 0f, side * 8f);
            }

            GameObject tail = ProtoAssets.MeshObject(box, "Tail", root,
                new Vector3(0f, 0.46f, -0.28f), new Vector3(0.18f, 0.24f, 0.12f), p.White, false);
            tail.transform.localRotation = Quaternion.Euler(38f, 0f, 0f);

            ProtoAssets.MeshObject(sphere, "Head", root, new Vector3(0f, 0.56f, 0.16f),
                new Vector3(0.25f, 0.25f, 0.25f), p.White, false);

            GameObject beak = ProtoAssets.MeshObject(cone, "Beak", root,
                new Vector3(0f, 0.54f, 0.3f), new Vector3(0.1f, 0.13f, 0.1f), p.Beak, false);
            beak.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Comb: three little blobs rather than one slab.
            for (int i = 0; i < 3; i++)
            {
                ProtoAssets.MeshObject(sphere, "Comb_" + i, root,
                    new Vector3(0f, 0.69f - i * 0.012f, 0.2f - i * 0.055f),
                    Vector3.one * (0.08f - i * 0.012f), p.Comb, false);
            }

            ProtoAssets.MeshObject(sphere, "Wattle", root, new Vector3(0f, 0.47f, 0.26f),
                new Vector3(0.06f, 0.08f, 0.05f), p.Comb, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(sphere, "Eye_" + side, root,
                    new Vector3(0.075f * side, 0.6f, 0.25f),
                    Vector3.one * 0.045f, p.Charcoal, false);

                ProtoAssets.MeshObject(box, "Leg_" + side, root,
                    new Vector3(0.09f * side, 0.07f, 0.02f),
                    new Vector3(0.045f, 0.15f, 0.045f), p.Beak, false);

                ProtoAssets.MeshObject(box, "Foot_" + side, root,
                    new Vector3(0.09f * side, 0.015f, 0.06f),
                    new Vector3(0.09f, 0.03f, 0.13f), p.Beak, false);
            }
        }

        /// <summary>
        /// Rounded dairy cow: barrel body with irregular patches, blunt muzzle, ears,
        /// short horns and stubby legs. Reads clearly from directly above.
        /// </summary>
        private static void Cow(Transform root, ProtoPalette p)
        {
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.22f);
            Mesh softBox = StylizedMeshLibrary.ChamferBox(0.3f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.6f);
            Mesh cone = StylizedMeshLibrary.Cone(8);

            // ---- body
            ProtoAssets.MeshObject(softBox, "Body", root, new Vector3(0f, 0.98f, 0f),
                new Vector3(0.98f, 0.86f, 1.76f), p.White, true);

            // Patches sit fractionally proud of the body so they never z-fight.
            AddPatch(root, sphere, p, new Vector3(0.5f, 1.12f, 0.42f), new Vector3(0.14f, 0.44f, 0.6f), 0f);
            AddPatch(root, sphere, p, new Vector3(-0.5f, 1.02f, -0.3f), new Vector3(0.14f, 0.5f, 0.66f), 0f);
            AddPatch(root, sphere, p, new Vector3(0.12f, 1.42f, -0.5f), new Vector3(0.46f, 0.14f, 0.5f), 0f);

            // ---- head and face
            ProtoAssets.MeshObject(softBox, "Head", root, new Vector3(0f, 1.16f, 1.08f),
                new Vector3(0.6f, 0.56f, 0.56f), p.White, true);

            ProtoAssets.MeshObject(sphere, "HeadPatch", root, new Vector3(0f, 1.36f, 1.14f),
                new Vector3(0.5f, 0.24f, 0.44f), p.Charcoal, false);

            ProtoAssets.MeshObject(softBox, "Muzzle", root, new Vector3(0f, 1.02f, 1.36f),
                new Vector3(0.42f, 0.3f, 0.26f), p.Muzzle, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(sphere, "Nostril_" + side, root,
                    new Vector3(0.1f * side, 1.03f, 1.48f),
                    new Vector3(0.06f, 0.05f, 0.04f), p.Charcoal, false);

                ProtoAssets.MeshObject(sphere, "Eye_" + side, root,
                    new Vector3(0.19f * side, 1.26f, 1.3f),
                    new Vector3(0.09f, 0.1f, 0.06f), p.Charcoal, false);

                GameObject ear = ProtoAssets.MeshObject(sphere, "Ear_" + side, root,
                    new Vector3(0.34f * side, 1.34f, 1.0f),
                    new Vector3(0.24f, 0.11f, 0.16f), p.White, false);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, side * 22f);

                GameObject horn = ProtoAssets.MeshObject(cone, "Horn_" + side,
                    root, new Vector3(0.19f * side, 1.5f, 1.02f),
                    new Vector3(0.12f, 0.18f, 0.12f), p.Horn, false);
                horn.transform.localRotation = Quaternion.Euler(-14f, 0f, side * 26f);
            }

            // ---- legs
            float[] lx = { -0.32f, 0.32f, -0.32f, 0.32f };
            float[] lz = { 0.6f, 0.6f, -0.6f, -0.6f };

            for (int i = 0; i < 4; i++)
            {
                ProtoAssets.MeshObject(taper, "Leg_" + i, root,
                    new Vector3(lx[i], 0.3f, lz[i]),
                    new Vector3(0.22f, 0.6f, 0.22f), p.White, false);

                ProtoAssets.MeshObject(box, "Hoof_" + i, root,
                    new Vector3(lx[i], 0.06f, lz[i]),
                    new Vector3(0.24f, 0.12f, 0.24f), p.Charcoal, false);
            }

            // ---- tail
            GameObject tail = ProtoAssets.MeshObject(taper, "Tail", root,
                new Vector3(0f, 1.12f, -0.92f), new Vector3(0.09f, 0.6f, 0.09f), p.White, false);
            tail.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);

            ProtoAssets.MeshObject(sphere, "TailTuft", root, new Vector3(0f, 0.86f, -1.05f),
                new Vector3(0.14f, 0.18f, 0.14f), p.Charcoal, false);

            // ---- udder, small but it completes the read
            ProtoAssets.MeshObject(sphere, "Udder", root, new Vector3(0f, 0.62f, -0.28f),
                new Vector3(0.34f, 0.24f, 0.34f), p.Muzzle, false);
        }

        private static void AddPatch(
            Transform root, Mesh sphere, ProtoPalette p, Vector3 position, Vector3 scale, float yaw)
        {
            GameObject patch = ProtoAssets.MeshObject(sphere, "Patch", root, position, scale, p.Charcoal, false);
            patch.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
