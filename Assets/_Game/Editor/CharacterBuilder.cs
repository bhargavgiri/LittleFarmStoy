using LittleFarmStory.Animals;
using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Builds the farmer and the animals as SEGMENTED RIGS.
    ///
    /// The Phase 4A actors were collapsed into a single combined mesh, which made animation
    /// structurally impossible. Here each body part is its own joint transform holding one
    /// combined renderer, so limbs can rotate while the renderer count stays low:
    ///
    ///   farmer  7 renderers   (was 17 loose objects, unanimatable)
    ///   chicken 4 renderers   (was 1, unanimatable)
    ///   cow     7 renderers   (was 1, unanimatable)
    ///
    /// Combining per joint rather than per actor is the trade that buys animation and still
    /// reduces the farmer's draw calls.
    /// </summary>
    public static class CharacterBuilder
    {
        public const string CharacterFolder = ProtoAssets.PrefabsFolder + "/Characters";

        // ================================================================ segment helper

        /// <summary>
        /// Creates a joint transform, fills it with geometry, and collapses that geometry into
        /// a single renderer. The returned transform is the joint the animator rotates.
        /// </summary>
        private static Transform Segment(
            string segmentName, Transform parent, Vector3 localPosition,
            string meshAssetName, bool castShadows, System.Action<Transform> populate)
        {
            GameObject joint = new GameObject(segmentName);
            joint.transform.SetParent(parent, false);
            joint.transform.localPosition = localPosition;

            populate(joint.transform);

            if (!StylizedMeshLibrary.CombineIntoSingleRenderer(joint, meshAssetName))
            {
                // A single-part segment does not need combining; make sure its shadow
                // setting still matches what the caller asked for.
                MeshRenderer single = joint.GetComponentInChildren<MeshRenderer>();
                if (single != null)
                {
                    ProtoAssets.ApplyMobileRendererSettings(single, castShadows);
                }

                return joint.transform;
            }

            MeshRenderer renderer = joint.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                ProtoAssets.ApplyMobileRendererSettings(renderer, castShadows);
            }

            return joint.transform;
        }

        private static GameObject SavePrefab(GameObject temp, string prefabName)
        {
            ProtoAssets.EnsureFolder(CharacterFolder);

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

        /// <summary>
        /// Attaches the Phase 5 gameplay layer to a finished animal rig: a trigger the existing
        /// InteractionController can find, plus the controller and the interaction handler.
        ///
        /// The AnimalDefinition and the habitat are deliberately left empty here - a prefab
        /// cannot know which pen it will live in. The scene builder wires those per instance.
        /// </summary>
        private static void AttachAnimalGameplay(
            GameObject root, AnimalIdleAnimator animator, string label,
            float triggerRadius, float triggerHeight,
            string productIconName, float badgeHeight)
        {
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = triggerRadius;
            trigger.center = new Vector3(0f, triggerHeight, 0f);

            AnimalController controller = root.AddComponent<AnimalController>();
            controller.EditorConfigure(null, null, animator, string.Empty);

            AttachReadyBadge(root.transform, productIconName, badgeHeight);

            AnimalInteraction interaction = root.AddComponent<AnimalInteraction>();
            interaction.EditorConfigure(controller);
            interaction.SetLabel(label);

            // An animal must outrank the pen's own area trigger, which it stands inside.
            interaction.SetPriority(LittleFarmStory.Interaction.InteractableBase.Priority.Animal);
        }

        /// <summary>
        /// A small floating badge over the animal, shown only while a product is waiting. It
        /// self-wires to the AnimalController above it, so this just builds the geometry.
        /// </summary>
        private static void AttachReadyBadge(Transform parent, string iconName, float height)
        {
            Sprite icon = UiIconLibrary.Get(iconName);

            if (icon == null)
            {
                Debug.LogError("Little Farm Story: ready-badge icon '" + iconName + "' is missing.");
                return;
            }

            GameObject root = ProtoAssets.Empty("ReadyMarker", parent, new Vector3(0f, height, 0f));

            GameObject badgeGo = new GameObject("Badge");
            badgeGo.transform.SetParent(root.transform, false);
            badgeGo.transform.localScale = Vector3.one * 0.6f;

            SpriteRenderer badge = badgeGo.AddComponent<SpriteRenderer>();
            badge.sprite = icon;
            badge.sortingOrder = 100;

            LittleFarmStory.UI.ReadyMarker marker = root.AddComponent<LittleFarmStory.UI.ReadyMarker>();
            marker.EditorConfigure(badge, 0.1f, 0.9f);
        }

        private static void SetRef(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError("Little Farm Story: missing serialized property '" + property + "'.");
                return;
            }

            p.objectReferenceValue = value;
        }

        // ================================================================ farmer

        /// <summary>
        /// The farmer, as a reusable prefab. Visual only - no gameplay components, so the
        /// same prefab can dress an NPC later without dragging the player systems along.
        ///
        /// Proportions are deliberately stylised: roughly one head to four body heights,
        /// chunky limbs, and a hat that is part of the silhouette rather than an accessory.
        /// </summary>
        public static GameObject BuildFarmerPrefab(ProtoPalette p)
        {
            GameObject root = new GameObject("Farmer");
            Transform rig = ProtoAssets.Empty("Rig", root.transform, Vector3.zero).transform;

            // ---- hips: the pelvis block the legs hang from
            Transform hips = Segment("Hips", rig, new Vector3(0f, 0.80f, 0f),
                "Mesh_Farmer_Hips", true, t => FarmerHips(t, p));

            Transform legLeft = Segment("Leg_L", hips, new Vector3(-0.15f, 0f, 0f),
                "Mesh_Farmer_Leg_L", true, t => FarmerLeg(t, p));
            Transform legRight = Segment("Leg_R", hips, new Vector3(0.15f, 0f, 0f),
                "Mesh_Farmer_Leg_R", true, t => FarmerLeg(t, p));

            // ---- torso, shoulders land at y 1.14
            Transform torso = Segment("Torso", rig, new Vector3(0f, 0.74f, 0f),
                "Mesh_Farmer_Torso", true, t => FarmerTorso(t, p));

            Transform armLeft = Segment("Arm_L", torso, new Vector3(-0.33f, 0.40f, 0f),
                "Mesh_Farmer_Arm_L", true, t => FarmerArm(t, p, -1f));
            Transform armRight = Segment("Arm_R", torso, new Vector3(0.33f, 0.40f, 0f),
                "Mesh_Farmer_Arm_R", true, t => FarmerArm(t, p, 1f));

            Transform head = Segment("Head", torso, new Vector3(0f, 0.50f, 0f),
                "Mesh_Farmer_Head", true, t => FarmerHead(t, p));

            LittleFarmStory.Player.FarmerLimbAnimator animator =
                root.AddComponent<LittleFarmStory.Player.FarmerLimbAnimator>();

            SerializedObject so = new SerializedObject(animator);
            SetRef(so, "legLeft", legLeft);
            SetRef(so, "legRight", legRight);
            SetRef(so, "armLeft", armLeft);
            SetRef(so, "armRight", armRight);
            SetRef(so, "head", head);
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, "Farmer");
        }

        // ---- farmer parts. The local origin of each segment is its joint.

        private static void FarmerHips(Transform t, ProtoPalette p)
        {
            Mesh drum = StylizedMeshLibrary.Drum();

            ProtoAssets.MeshObject(drum, "Seat", t, new Vector3(0f, -0.06f, 0f),
                new Vector3(0.56f, 0.34f, 0.44f), p.Denim, true);
        }

        /// <summary>One leg: thigh, shin and boot, built downwards from the hip joint.</summary>
        private static void FarmerLeg(Transform t, ProtoPalette p)
        {
            Mesh drum = StylizedMeshLibrary.Drum();
            Mesh egg = StylizedMeshLibrary.Egg();
            Mesh wedge = StylizedMeshLibrary.Wedge();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.3f);

            ProtoAssets.MeshObject(drum, "Thigh", t, new Vector3(0f, -0.20f, 0f),
                new Vector3(0.26f, 0.44f, 0.26f), p.Denim, true);

            ProtoAssets.MeshObject(drum, "Shin", t, new Vector3(0f, -0.52f, 0.005f),
                new Vector3(0.22f, 0.34f, 0.22f), p.Denim, true);

            // Turn-up cuff: a small detail that stops the leg reading as a plain tube.
            ProtoAssets.MeshObject(drum, "Cuff", t, new Vector3(0f, -0.655f, 0.005f),
                new Vector3(0.245f, 0.09f, 0.245f), p.Shirt, false);

            // Boot: rounded upper with a wedge toe projecting forward and a flat sole.
            ProtoAssets.MeshObject(egg, "BootUpper", t, new Vector3(0f, -0.725f, 0.015f),
                new Vector3(0.25f, 0.17f, 0.28f), p.Boots, true);

            ProtoAssets.MeshObject(wedge, "BootToe", t, new Vector3(0f, -0.755f, 0.10f),
                new Vector3(0.25f, 0.13f, 0.20f), p.Boots, false);

            ProtoAssets.MeshObject(box, "Sole", t, new Vector3(0f, -0.79f, 0.035f),
                new Vector3(0.27f, 0.05f, 0.34f), p.WoodDark, false);
        }

        /// <summary>Torso: shirt, dungarees, bib and straps.</summary>
        private static void FarmerTorso(Transform t, ProtoPalette p)
        {
            Mesh egg = StylizedMeshLibrary.Egg();
            Mesh drum = StylizedMeshLibrary.Drum();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.22f);

            // Chest, wider at the shoulders and tapering to the waist.
            ProtoAssets.MeshObject(egg, "Chest", t, new Vector3(0f, 0.30f, 0f),
                new Vector3(0.62f, 0.58f, 0.44f), p.Shirt, true);

            ProtoAssets.MeshObject(drum, "Dungarees", t, new Vector3(0f, 0.13f, 0f),
                new Vector3(0.58f, 0.34f, 0.44f), p.Denim, true);

            ProtoAssets.MeshObject(box, "Bib", t, new Vector3(0f, 0.34f, 0.20f),
                new Vector3(0.32f, 0.26f, 0.06f), p.Denim, false);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject strap = ProtoAssets.MeshObject(box, "Strap_" + side, t,
                    new Vector3(0.12f * side, 0.45f, 0.16f),
                    new Vector3(0.08f, 0.30f, 0.06f), p.Denim, false);
                strap.transform.localRotation = Quaternion.Euler(-8f, 0f, side * 5f);

                ProtoAssets.MeshObject(box, "Buckle_" + side, t,
                    new Vector3(0.12f * side, 0.32f, 0.205f),
                    new Vector3(0.07f, 0.06f, 0.04f), p.Amber, false);
            }

            // Collar closes the gap the head sits in.
            ProtoAssets.MeshObject(drum, "Collar", t, new Vector3(0f, 0.50f, 0f),
                new Vector3(0.30f, 0.10f, 0.28f), p.Shirt, false);
        }

        /// <summary>One arm: sleeve, forearm and a mitten hand, built down from the shoulder.</summary>
        private static void FarmerArm(Transform t, ProtoPalette p, float side)
        {
            Mesh drum = StylizedMeshLibrary.Drum();
            Mesh pear = StylizedMeshLibrary.Pear();

            // Shoulder cap hides the joint seam against the chest.
            ProtoAssets.MeshObject(drum, "Shoulder", t, Vector3.zero,
                new Vector3(0.22f, 0.20f, 0.22f), p.Shirt, true);

            ProtoAssets.MeshObject(drum, "Sleeve", t, new Vector3(0.01f * side, -0.15f, 0f),
                new Vector3(0.19f, 0.28f, 0.19f), p.Shirt, true);

            ProtoAssets.MeshObject(drum, "Forearm", t, new Vector3(0.02f * side, -0.32f, 0f),
                new Vector3(0.16f, 0.22f, 0.16f), p.Skin, true);

            // Mitten hand: deliberately fingerless, which reads better at this scale than
            // any attempt at digits.
            GameObject hand = ProtoAssets.MeshObject(pear, "Hand", t,
                new Vector3(0.025f * side, -0.45f, 0.01f),
                new Vector3(0.17f, 0.19f, 0.14f), p.Skin, false);
            hand.transform.localRotation = Quaternion.Euler(12f, 0f, side * -6f);
        }

        /// <summary>Head, face and hat - the whole identity of the character.</summary>
        private static void FarmerHead(Transform t, ProtoPalette p)
        {
            Mesh egg = StylizedMeshLibrary.Egg();
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh teardrop = StylizedMeshLibrary.Teardrop();
            Mesh wedge = StylizedMeshLibrary.Wedge();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.3f);
            Mesh cone = StylizedMeshLibrary.Cone(14);
            Mesh drum = StylizedMeshLibrary.Drum();
            Mesh cyl = StylizedMeshLibrary.Cylinder(14);

            // ---- skull: an egg, not a ball, and slightly flattened front to back
            ProtoAssets.MeshObject(egg, "Skull", t, new Vector3(0f, 0.20f, 0f),
                new Vector3(0.46f, 0.48f, 0.43f), p.Skin, true);

            // Jaw fills the chin so the head is not a perfect ovoid.
            ProtoAssets.MeshObject(sphere, "Jaw", t, new Vector3(0f, 0.09f, 0.03f),
                new Vector3(0.36f, 0.24f, 0.34f), p.Skin, false);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject ear = ProtoAssets.MeshObject(teardrop, "Ear_" + side, t,
                    new Vector3(0.225f * side, 0.20f, -0.01f),
                    new Vector3(0.10f, 0.15f, 0.08f), p.Skin, false);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, side * 14f);

                // ---- face. Features sit proud of the skull so they never z-fight.
                ProtoAssets.MeshObject(sphere, "Eye_" + side, t,
                    new Vector3(0.105f * side, 0.235f, 0.185f),
                    new Vector3(0.085f, 0.10f, 0.05f), p.Charcoal, false);

                // A tiny catchlight is the cheapest way to make eyes look alive.
                ProtoAssets.MeshObject(sphere, "Catchlight_" + side, t,
                    new Vector3(0.125f * side, 0.262f, 0.205f),
                    Vector3.one * 0.028f, p.White, false);

                GameObject brow = ProtoAssets.MeshObject(box, "Brow_" + side, t,
                    new Vector3(0.105f * side, 0.305f, 0.185f),
                    new Vector3(0.10f, 0.028f, 0.04f), p.WoodDark, false);
                brow.transform.localRotation = Quaternion.Euler(0f, 0f, side * -9f);

                ProtoAssets.MeshObject(sphere, "Cheek_" + side, t,
                    new Vector3(0.145f * side, 0.175f, 0.155f),
                    new Vector3(0.11f, 0.075f, 0.06f), p.Muzzle, false);

                ProtoAssets.MeshObject(sphere, "Sideburn_" + side, t,
                    new Vector3(0.20f * side, 0.215f, 0.03f),
                    new Vector3(0.08f, 0.13f, 0.14f), p.WoodDark, false);
            }

            GameObject nose = ProtoAssets.MeshObject(wedge, "Nose", t,
                new Vector3(0f, 0.198f, 0.20f), new Vector3(0.09f, 0.085f, 0.09f), p.Skin, false);
            nose.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);

            // Mouth: a shallow tilted bar rather than a hole, so it reads as a smile.
            GameObject mouth = ProtoAssets.MeshObject(box, "Mouth", t,
                new Vector3(0f, 0.138f, 0.185f), new Vector3(0.10f, 0.026f, 0.03f), p.WoodDark, false);
            mouth.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            ProtoAssets.MeshObject(sphere, "Hair_Back", t, new Vector3(0f, 0.255f, -0.09f),
                new Vector3(0.44f, 0.30f, 0.30f), p.WoodDark, false);

            // ---- straw hat: coned brim with a curled rim, short crown, contrasting band
            ProtoAssets.MeshObject(cone, "HatBrim", t, new Vector3(0f, 0.415f, 0f),
                new Vector3(0.84f, 0.15f, 0.84f), p.Straw, true);

            // The rim curl is what makes the hat read as woven straw rather than a funnel.
            ProtoAssets.MeshObject(cyl, "HatRim", t, new Vector3(0f, 0.372f, 0f),
                new Vector3(0.84f, 0.05f, 0.84f), p.Straw, false);

            ProtoAssets.MeshObject(drum, "HatCrown", t, new Vector3(0f, 0.485f, 0f),
                new Vector3(0.42f, 0.20f, 0.40f), p.Straw, true);

            ProtoAssets.MeshObject(cyl, "HatBand", t, new Vector3(0f, 0.437f, 0f),
                new Vector3(0.44f, 0.055f, 0.42f), p.RoofTerracotta, false);

            // A dent in the crown; farm hats are never pristine.
            ProtoAssets.MeshObject(sphere, "CrownDent", t, new Vector3(0.05f, 0.585f, -0.02f),
                new Vector3(0.20f, 0.07f, 0.18f), p.Straw, false);
        }

        // ================================================================ chicken

        /// <summary>
        /// A chicken variant. Both variants share every mesh and differ only in plumage
        /// material, so the second variant costs no extra geometry.
        /// </summary>
        public static GameObject BuildChickenPrefab(ProtoPalette p, string variantName, Material plumage)
        {
            GameObject root = new GameObject(variantName);

            Transform body = Segment("Body", root.transform, new Vector3(0f, 0.34f, 0f),
                "Mesh_" + variantName + "_Body", true, t => ChickenBody(t, p, plumage));

            Transform head = Segment("Head", body, new Vector3(0f, 0.19f, 0.17f),
                "Mesh_" + variantName + "_Head", true, t => ChickenHead(t, p, plumage));

            Transform legLeft = Segment("Leg_L", root.transform, new Vector3(-0.09f, 0.16f, 0.01f),
                "Mesh_Chicken_Leg_L", false, t => ChickenLeg(t, p));
            Transform legRight = Segment("Leg_R", root.transform, new Vector3(0.09f, 0.16f, 0.01f),
                "Mesh_Chicken_Leg_R", false, t => ChickenLeg(t, p));

            AnimalIdleAnimator animator = root.AddComponent<AnimalIdleAnimator>();
            SerializedObject so = new SerializedObject(animator);
            SetRef(so, "body", body);
            SetRef(so, "head", head);

            SerializedProperty legsProperty = so.FindProperty("legs");
            legsProperty.arraySize = 2;
            legsProperty.GetArrayElementAtIndex(0).objectReferenceValue = legLeft;
            legsProperty.GetArrayElementAtIndex(1).objectReferenceValue = legRight;

            // Chickens peck often and sharply; the cow below grazes slowly.
            so.FindProperty("dipInterval").floatValue = 4.2f;
            so.FindProperty("dipAngle").floatValue = 46f;
            so.FindProperty("dipDuration").floatValue = 0.42f;
            so.FindProperty("headSwayRate").floatValue = 0.55f;
            so.FindProperty("headSwayAngle").floatValue = 11f;
            so.FindProperty("breathRate").floatValue = 0.7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            AttachAnimalGameplay(root, animator, "Chicken", 0.55f, 0.35f,
                UiIconLibrary.Names.Egg, 0.95f);

            return SavePrefab(root, variantName);
        }

        private static void ChickenBody(Transform t, ProtoPalette p, Material plumage)
        {
            Mesh egg = StylizedMeshLibrary.Egg();
            Mesh teardrop = StylizedMeshLibrary.Teardrop();

            // Plump body, long axis front-to-back and tilted nose-down like a real hen.
            // The Egg mesh runs along its local Y, so Y carries the body LENGTH; tipping it
            // -72 degrees lays the hen forward with her fat breast leading and tail raised.
            GameObject body = ProtoAssets.MeshObject(egg, "Body", t, Vector3.zero,
                new Vector3(0.40f, 0.56f, 0.42f), plumage, true);
            body.transform.localRotation = Quaternion.Euler(-72f, 0f, 0f);

            // Wings as flattened teardrops swept back along the flanks.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject wing = ProtoAssets.MeshObject(teardrop, "Wing_" + side, t,
                    new Vector3(0.175f * side, 0.02f, -0.02f),
                    new Vector3(0.09f, 0.34f, 0.24f), plumage, false);
                wing.transform.localRotation = Quaternion.Euler(-82f, 0f, side * 10f);
            }

            // Tail: three stacked feather blades, the classic hen fan.
            for (int i = 0; i < 3; i++)
            {
                GameObject feather = ProtoAssets.MeshObject(teardrop, "Tail_" + i, t,
                    new Vector3((i - 1) * 0.055f, 0.16f, -0.27f),
                    new Vector3(0.07f, 0.30f, 0.13f), plumage, false);
                feather.transform.localRotation = Quaternion.Euler(-38f, (i - 1) * 13f, (i - 1) * 8f);
            }
        }

        private static void ChickenHead(Transform t, ProtoPalette p, Material plumage)
        {
            Mesh egg = StylizedMeshLibrary.Egg();
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh wedge = StylizedMeshLibrary.Wedge();
            Mesh teardrop = StylizedMeshLibrary.Teardrop();
            Mesh drum = StylizedMeshLibrary.Drum();

            // Neck bridges body and head so no gap opens when the head turns.
            GameObject neck = ProtoAssets.MeshObject(drum, "Neck", t, new Vector3(0f, -0.08f, -0.05f),
                new Vector3(0.15f, 0.18f, 0.15f), plumage, false);
            neck.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);

            ProtoAssets.MeshObject(egg, "Skull", t, Vector3.zero,
                new Vector3(0.22f, 0.24f, 0.23f), plumage, true);

            GameObject beak = ProtoAssets.MeshObject(wedge, "Beak", t, new Vector3(0f, -0.015f, 0.135f),
                new Vector3(0.10f, 0.075f, 0.14f), p.Beak, false);
            beak.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);

            // Comb: three lobes of decreasing size along the crown.
            for (int i = 0; i < 3; i++)
            {
                ProtoAssets.MeshObject(teardrop, "Comb_" + i, t,
                    new Vector3(0f, 0.125f - i * 0.012f, 0.055f - i * 0.055f),
                    new Vector3(0.035f, 0.09f - i * 0.012f, 0.055f), p.Comb, false);
            }

            ProtoAssets.MeshObject(teardrop, "Wattle", t, new Vector3(0f, -0.095f, 0.085f),
                new Vector3(0.05f, 0.08f, 0.04f), p.Comb, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(sphere, "Eye_" + side, t,
                    new Vector3(0.075f * side, 0.035f, 0.075f),
                    Vector3.one * 0.042f, p.Charcoal, false);

                ProtoAssets.MeshObject(sphere, "Catchlight_" + side, t,
                    new Vector3(0.085f * side, 0.048f, 0.09f),
                    Vector3.one * 0.016f, p.White, false);
            }
        }

        private static void ChickenLeg(Transform t, ProtoPalette p)
        {
            Mesh drum = StylizedMeshLibrary.Drum();
            Mesh box = StylizedMeshLibrary.ChamferBox(0.3f);

            ProtoAssets.MeshObject(drum, "Shank", t, new Vector3(0f, -0.07f, 0f),
                new Vector3(0.045f, 0.16f, 0.045f), p.Beak, false);

            // Three forward toes and one back: the shape that says "bird foot" at a glance.
            for (int i = -1; i <= 1; i++)
            {
                GameObject toe = ProtoAssets.MeshObject(box, "Toe_" + i, t,
                    new Vector3(i * 0.032f, -0.152f, 0.05f),
                    new Vector3(0.026f, 0.022f, 0.10f), p.Beak, false);
                toe.transform.localRotation = Quaternion.Euler(0f, i * 22f, 0f);
            }

            ProtoAssets.MeshObject(box, "Spur", t, new Vector3(0f, -0.152f, -0.035f),
                new Vector3(0.024f, 0.022f, 0.06f), p.Beak, false);
        }

        // ================================================================ cow

        /// <summary>
        /// The cow: a wide rounded body on short legs with a large readable head.
        /// Deliberately much bulkier than the farmer so the two never read as the same mass.
        /// </summary>
        public static GameObject BuildCowPrefab(ProtoPalette p)
        {
            GameObject root = new GameObject("Cow");

            Transform body = Segment("Body", root.transform, new Vector3(0f, 0.98f, 0f),
                "Mesh_Cow_Body", true, t => CowBody(t, p));

            Transform head = Segment("Head", body, new Vector3(0f, 0.18f, 0.92f),
                "Mesh_Cow_Head", true, t => CowHead(t, p));

            Transform tail = Segment("Tail", body, new Vector3(0f, 0.16f, -0.86f),
                "Mesh_Cow_Tail", false, t => CowTail(t, p));

            float[] lx = { -0.34f, 0.34f, -0.34f, 0.34f };
            float[] lz = { 0.58f, 0.58f, -0.58f, -0.58f };
            string[] legNames = { "Leg_FL", "Leg_FR", "Leg_BL", "Leg_BR" };
            Transform[] legs = new Transform[4];

            for (int i = 0; i < 4; i++)
            {
                legs[i] = Segment(legNames[i], root.transform, new Vector3(lx[i], 0.62f, lz[i]),
                    "Mesh_Cow_Leg_" + i, true, t => CowLeg(t, p));
            }

            AnimalIdleAnimator animator = root.AddComponent<AnimalIdleAnimator>();
            SerializedObject so = new SerializedObject(animator);
            SetRef(so, "body", body);
            SetRef(so, "head", head);
            SetRef(so, "tail", tail);

            SerializedProperty legsProperty = so.FindProperty("legs");
            legsProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                legsProperty.GetArrayElementAtIndex(i).objectReferenceValue = legs[i];
            }

            // Slow, heavy idle: a long graze rather than a quick peck.
            so.FindProperty("dipInterval").floatValue = 7.5f;
            so.FindProperty("dipAngle").floatValue = 26f;
            so.FindProperty("dipDuration").floatValue = 2.2f;
            so.FindProperty("headSwayRate").floatValue = 0.22f;
            so.FindProperty("headSwayAngle").floatValue = 6f;
            so.FindProperty("breathRate").floatValue = 0.3f;
            so.FindProperty("tailRate").floatValue = 0.62f;
            so.FindProperty("tailAngle").floatValue = 16f;
            so.ApplyModifiedPropertiesWithoutUndo();

            AttachAnimalGameplay(root, animator, "Cow", 1.1f, 0.9f,
                UiIconLibrary.Names.Milk, 2.05f);

            return SavePrefab(root, "Cow");
        }

        private static void CowBody(Transform t, ProtoPalette p)
        {
            Mesh egg = StylizedMeshLibrary.Egg();
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh drum = StylizedMeshLibrary.Drum();

            // Barrel body. Y is the Egg's long axis, so Y carries the 1.9 m body LENGTH and
            // the -90 degree tilt lays it front-to-back; height and width come from X and Z.
            GameObject barrel = ProtoAssets.MeshObject(egg, "Barrel", t, Vector3.zero,
                new Vector3(0.96f, 1.9f, 0.92f), p.White, true);
            barrel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // Shoulder and haunch swellings give the body a real animal profile.
            ProtoAssets.MeshObject(sphere, "Shoulder", t, new Vector3(0f, 0.14f, 0.52f),
                new Vector3(0.94f, 0.82f, 0.7f), p.White, true);
            ProtoAssets.MeshObject(sphere, "Haunch", t, new Vector3(0f, 0.12f, -0.52f),
                new Vector3(0.96f, 0.86f, 0.72f), p.White, true);

            // Irregular patches, sunk into the surface so they never float.
            AddPatch(t, sphere, p, new Vector3(0.42f, 0.20f, 0.36f), new Vector3(0.32f, 0.44f, 0.58f), 18f);
            AddPatch(t, sphere, p, new Vector3(-0.46f, 0.06f, -0.20f), new Vector3(0.28f, 0.50f, 0.66f), -12f);
            AddPatch(t, sphere, p, new Vector3(0.10f, 0.44f, -0.44f), new Vector3(0.52f, 0.26f, 0.50f), 30f);
            AddPatch(t, sphere, p, new Vector3(-0.30f, 0.38f, 0.62f), new Vector3(0.34f, 0.24f, 0.36f), 0f);

            ProtoAssets.MeshObject(drum, "Udder", t, new Vector3(0f, -0.40f, -0.28f),
                new Vector3(0.36f, 0.26f, 0.42f), p.Muzzle, false);
        }

        private static void AddPatch(
            Transform t, Mesh sphere, ProtoPalette p, Vector3 position, Vector3 scale, float yaw)
        {
            GameObject patch = ProtoAssets.MeshObject(sphere, "Patch", t, position, scale, p.Charcoal, false);
            patch.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void CowHead(Transform t, ProtoPalette p)
        {
            Mesh egg = StylizedMeshLibrary.Egg();
            Mesh pear = StylizedMeshLibrary.Pear();
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();
            Mesh teardrop = StylizedMeshLibrary.Teardrop();
            Mesh crescent = StylizedMeshLibrary.Crescent(62f);
            Mesh drum = StylizedMeshLibrary.Drum();

            // Neck fills the gap to the shoulder so head rotation never opens a seam.
            GameObject neck = ProtoAssets.MeshObject(drum, "Neck", t, new Vector3(0f, -0.14f, -0.24f),
                new Vector3(0.54f, 0.42f, 0.5f), p.White, true);
            neck.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);

            GameObject skull = ProtoAssets.MeshObject(egg, "Skull", t, Vector3.zero,
                new Vector3(0.56f, 0.62f, 0.56f), p.White, true);
            skull.transform.localRotation = Quaternion.Euler(-74f, 0f, 0f);

            // Broad soft muzzle - the single most recognisable part of a cow's face.
            GameObject muzzle = ProtoAssets.MeshObject(pear, "Muzzle", t, new Vector3(0f, -0.12f, 0.30f),
                new Vector3(0.44f, 0.34f, 0.36f), p.Muzzle, false);
            muzzle.transform.localRotation = Quaternion.Euler(-84f, 0f, 0f);

            ProtoAssets.MeshObject(sphere, "Blaze", t, new Vector3(0f, 0.20f, 0.20f),
                new Vector3(0.30f, 0.26f, 0.22f), p.Charcoal, false);

            for (int side = -1; side <= 1; side += 2)
            {
                ProtoAssets.MeshObject(sphere, "Nostril_" + side, t,
                    new Vector3(0.095f * side, -0.14f, 0.44f),
                    new Vector3(0.07f, 0.055f, 0.05f), p.Charcoal, false);

                ProtoAssets.MeshObject(sphere, "Eye_" + side, t,
                    new Vector3(0.20f * side, 0.10f, 0.24f),
                    new Vector3(0.11f, 0.12f, 0.08f), p.Charcoal, false);

                ProtoAssets.MeshObject(sphere, "Catchlight_" + side, t,
                    new Vector3(0.225f * side, 0.135f, 0.275f),
                    Vector3.one * 0.038f, p.White, false);

                // Ears sit below and outside the horns, angled down and back.
                GameObject ear = ProtoAssets.MeshObject(teardrop, "Ear_" + side, t,
                    new Vector3(0.31f * side, 0.16f, -0.04f),
                    new Vector3(0.10f, 0.24f, 0.16f), p.White, false);
                ear.transform.localRotation = Quaternion.Euler(6f, 0f, side * 104f);

                // Short curved horns sweeping up and outwards. The Crescent mesh always
                // curves towards -X, so the left horn is flipped 180 degrees in Y first;
                // without that the two horns curl in opposite directions.
                GameObject horn = ProtoAssets.MeshObject(crescent, "Horn_" + side, t,
                    new Vector3(0.155f * side, 0.30f, -0.02f),
                    new Vector3(0.11f, 0.26f, 0.11f), p.Horn, false);
                horn.transform.localRotation =
                    Quaternion.Euler(-10f, side > 0f ? 0f : 180f, side * -34f);
            }

            // Forelock tuft between the horns.
            ProtoAssets.MeshObject(sphere, "Forelock", t, new Vector3(0f, 0.315f, 0.09f),
                new Vector3(0.24f, 0.13f, 0.18f), p.Charcoal, false);
        }

        private static void CowTail(Transform t, ProtoPalette p)
        {
            Mesh drum = StylizedMeshLibrary.Drum();
            Mesh teardrop = StylizedMeshLibrary.Teardrop();

            GameObject shaft = ProtoAssets.MeshObject(drum, "Shaft", t, new Vector3(0f, -0.24f, -0.06f),
                new Vector3(0.09f, 0.55f, 0.09f), p.White, false);
            shaft.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            GameObject tuft = ProtoAssets.MeshObject(teardrop, "Tuft", t, new Vector3(0f, -0.54f, -0.13f),
                new Vector3(0.15f, 0.24f, 0.15f), p.Charcoal, false);
            tuft.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        }

        private static void CowLeg(Transform t, ProtoPalette p)
        {
            Mesh drum = StylizedMeshLibrary.Drum();
            Mesh egg = StylizedMeshLibrary.Egg();

            ProtoAssets.MeshObject(drum, "Upper", t, new Vector3(0f, -0.16f, 0f),
                new Vector3(0.26f, 0.34f, 0.26f), p.White, true);

            ProtoAssets.MeshObject(drum, "Lower", t, new Vector3(0f, -0.42f, 0f),
                new Vector3(0.20f, 0.28f, 0.20f), p.White, true);

            // Dark sock and hoof: the value break at the bottom of the leg reads well against
            // grass and stops the legs vanishing into the ground.
            ProtoAssets.MeshObject(drum, "Sock", t, new Vector3(0f, -0.55f, 0f),
                new Vector3(0.21f, 0.12f, 0.21f), p.Charcoal, false);

            ProtoAssets.MeshObject(egg, "Hoof", t, new Vector3(0f, -0.605f, 0.01f),
                new Vector3(0.23f, 0.14f, 0.25f), p.Charcoal, false);
        }
    }
}
