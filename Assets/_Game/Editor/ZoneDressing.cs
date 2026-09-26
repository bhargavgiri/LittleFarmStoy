using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Intentional prop placement, one method per zone.
    ///
    /// Props are grouped into small vignettes - a barrow parked beside a stack of sacks, a
    /// woodpile with a rack of tools next to it - rather than sprinkled evenly. Even spacing
    /// reads as procedural noise; clusters read as somewhere a person actually works, and
    /// they tell the player what a zone is for without a single line of UI text.
    /// </summary>
    public static class ZoneDressing
    {
        private static GameObject Place(
            GameObject prefab, Transform parent, Vector3 position, float scale, float yaw)
        {
            return FarmEnvironmentBuilder.PlaceProp(prefab, parent, position, scale, yaw);
        }

        // ================================================================ fields

        /// <summary>
        /// A working field: a barrow at the near corner, sacks of seed, a tool rack, and a
        /// couple of crates waiting to be filled.
        /// </summary>
        public static void DressField(
            Transform area, PropLibrary.Props props, PropLibraryExtra.Extras extras,
            Vector3 centre, Vector2 padSize, bool eastSide)
        {
            float hx = padSize.x * 0.5f;
            float hz = padSize.y * 0.5f;

            // Everything sits on the outward side, away from the plots, so nothing blocks play.
            float outward = eastSide ? 1f : -1f;
            Vector3 corner = centre + new Vector3(outward * (hx + 1.6f), 0f, -hz - 0.6f);

            Place(extras.Wheelbarrow, area, corner, 1f, eastSide ? 200f : 20f);
            Place(extras.Sack, area, corner + new Vector3(outward * 0.2f, 0f, 1.5f), 1f, 30f);
            Place(extras.Sack, area, corner + new Vector3(outward * 0.85f, 0f, 1.9f), 0.9f, -40f);
            Place(props.Crate, area, corner + new Vector3(outward * -0.1f, 0f, 2.9f), 0.9f, 12f);

            Vector3 farCorner = centre + new Vector3(outward * (hx + 1.5f), 0f, hz + 0.4f);
            Place(extras.ToolRack, area, farCorner, 1f, eastSide ? 90f : -90f);
            Place(extras.Bucket, area, farCorner + new Vector3(outward * 0.7f, 0f, -1.1f), 1f, 0f);

            // Flowers soften the frame corners.
            Place(props.FlowersWhite, area, centre + new Vector3(-hx - 0.9f, 0f, hz + 0.9f), 1.1f, 40f);
            Place(props.FlowersAmber, area, centre + new Vector3(hx + 0.9f, 0f, -hz - 0.9f), 1f, 160f);
            Place(props.BushSprig, area, centre + new Vector3(hx + 1.1f, 0f, hz + 1.1f), 1.1f, 70f);
        }

        // ================================================================ chicken run

        /// <summary>Coop, feed, water, scratch litter and a nesting basket.</summary>
        public static void DressChickenRun(
            Transform area, PropLibrary.Props props, PropLibraryExtra.Extras extras)
        {
            Place(extras.Trough, area, new Vector3(-2.3f, 0.09f, -1.9f), 0.62f, 90f);
            Place(extras.Bucket, area, new Vector3(-3.6f, 0.09f, -0.7f), 1f, 20f);
            Place(extras.Sack, area, new Vector3(-4.0f, 0.09f, 0.6f), 0.95f, -25f);
            Place(extras.Basket, area, new Vector3(-3.2f, 0.09f, 1.5f), 1f, 40f);

            // Straw scattered where the birds scratch.
            Place(props.HayBale, area, new Vector3(4.9f, 0.09f, -2.9f), 0.5f, 20f);
            Place(props.HayBale, area, new Vector3(4.4f, 0.09f, -3.6f), 0.42f, -35f);

            Place(props.BushSprig, area, new Vector3(-5.0f, 0.09f, 3.2f), 1f, 10f);
            Place(props.FlowersWhite, area, new Vector3(5.2f, 0.09f, 3.4f), 1f, 60f);
            Place(extras.Lantern, area, new Vector3(1.4f, 0.09f, -3.9f), 0.85f, 0f);
        }

        // ================================================================ cow pasture

        /// <summary>Barn, hay store, water trough, and a bench in the shade.</summary>
        public static void DressCowPasture(
            Transform area, PropLibrary.Props props, PropLibraryExtra.Extras extras)
        {
            // Hay stack against the barn: three bales, one on top.
            Place(props.HayBale, area, new Vector3(-5.2f, 0.09f, 2.7f), 1f, 12f);
            Place(props.HayBale, area, new Vector3(-5.2f, 0.09f, 1.0f), 1f, -20f);
            Place(props.HayBale, area, new Vector3(-5.3f, 1.35f, 1.85f), 0.92f, 44f);
            Place(extras.Sack, area, new Vector3(-3.9f, 0.09f, 3.4f), 1f, 15f);

            Place(extras.Trough, area, new Vector3(-1.2f, 0.09f, -3.9f), 1.15f, 90f);
            Place(extras.Bucket, area, new Vector3(0.9f, 0.09f, -4.1f), 1.05f, -30f);

            Place(extras.Bench, area, new Vector3(5.6f, 0.09f, -2.4f), 1f, 250f);
            Place(extras.LogPile, area, new Vector3(-5.6f, 0.09f, -3.4f), 1f, 8f);
            Place(props.BushWide, area, new Vector3(6.2f, 0.09f, 1.4f), 1.1f, 30f);
            Place(props.FlowersPink, area, new Vector3(4.6f, 0.09f, -4.2f), 1.05f, 90f);
        }

        // ================================================================ production yard

        /// <summary>Storage, cooperage and tools around the empty machine pads.</summary>
        public static void DressProductionYard(
            Transform area, PropLibrary.Props props, PropLibraryExtra.Extras extras)
        {
            // Stacked crates and barrels at the storage end.
            Place(props.Crate, area, new Vector3(-4.5f, 0.1f, -3.3f), 1.1f, 18f);
            Place(props.Crate, area, new Vector3(-3.6f, 0.1f, -3.9f), 0.95f, -24f);
            Place(props.Crate, area, new Vector3(-4.3f, 0.98f, -3.4f), 0.9f, 40f);
            Place(props.Barrel, area, new Vector3(-5.0f, 0.1f, -2.1f), 1f, 0f);
            Place(props.Barrel, area, new Vector3(-4.2f, 0.1f, -1.5f), 0.92f, 40f);
            Place(extras.Sack, area, new Vector3(-3.2f, 0.1f, -2.4f), 1f, -15f);

            // Working end: barrow, tools, buckets near the silo chute.
            Place(extras.Wheelbarrow, area, new Vector3(2.6f, 0.1f, -3.5f), 1f, 200f);
            Place(extras.ToolRack, area, new Vector3(5.0f, 0.1f, 1.6f), 1f, -90f);
            Place(extras.Bucket, area, new Vector3(3.6f, 0.1f, -3.9f), 1f, 15f);
            Place(extras.Bucket, area, new Vector3(4.1f, 0.1f, -3.4f), 0.9f, -40f);
            Place(extras.Lantern, area, new Vector3(-5.4f, 0.1f, 2.2f), 1f, 0f);
        }

        // ================================================================ market

        /// <summary>Produce, baskets and a bench: somewhere people gather.</summary>
        public static void DressMarket(
            Transform area, PropLibrary.Props props, PropLibraryExtra.Extras extras)
        {
            Place(props.Crate, area, new Vector3(-4.8f, 0.06f, -2.6f), 1.1f, 22f);
            Place(props.Crate, area, new Vector3(-4.3f, 0.06f, -3.7f), 0.9f, -35f);
            Place(props.Crate, area, new Vector3(-4.7f, 0.94f, -2.7f), 0.85f, 8f);
            Place(extras.Basket, area, new Vector3(-3.5f, 0.06f, -3.2f), 1.15f, 40f);
            Place(extras.Basket, area, new Vector3(4.4f, 0.06f, -3.1f), 1.05f, -20f);

            Place(props.Barrel, area, new Vector3(4.9f, 0.06f, -2.6f), 1f, 0f);
            Place(extras.Bench, area, new Vector3(-5.6f, 0.06f, 1.4f), 1f, 90f);
            Place(extras.Lantern, area, new Vector3(5.4f, 0.06f, -4.0f), 1f, 0f);

            Place(props.FlowersAmber, area, new Vector3(5.4f, 0.06f, 2.2f), 1.3f, 0f);
            Place(props.FlowersPink, area, new Vector3(-5.4f, 0.06f, -1.0f), 1.2f, 60f);
            Place(props.BushRound, area, new Vector3(6.0f, 0.06f, 4.0f), 1f, 25f);
        }

        // ================================================================ home

        /// <summary>A lived-in yard: bench, woodpile, watering can, garden beds.</summary>
        public static void DressHome(
            Transform area, PropLibrary.Props props, PropLibraryExtra.Extras extras)
        {
            Place(extras.Bench, area, new Vector3(4.6f, 0.05f, -2.4f), 1f, 250f);
            Place(extras.LogPile, area, new Vector3(-5.2f, 0.05f, 2.6f), 1f, 12f);
            Place(extras.WateringCan, area, new Vector3(2.9f, 0.19f, -3.9f), 1.1f, 30f);
            Place(extras.Bucket, area, new Vector3(-2.9f, 0.05f, -4.6f), 1f, -20f);
            Place(props.Barrel, area, new Vector3(-4.9f, 0.05f, 0.6f), 0.95f, 15f);
            Place(extras.Lantern, area, new Vector3(3.3f, 0.05f, -5.0f), 1f, 0f);

            // Garden planting flanking the approach.
            Place(props.BushRound, area, new Vector3(-3.6f, 0.05f, -3.4f), 1f, 30f);
            Place(props.BushRound, area, new Vector3(3.6f, 0.05f, -3.4f), 1.05f, -50f);
            Place(props.BushWide, area, new Vector3(-5.0f, 0.05f, -1.6f), 1f, 12f);
            Place(props.FlowersPink, area, new Vector3(-2.3f, 0.05f, -4.6f), 1.2f, 0f);
            Place(props.FlowersWhite, area, new Vector3(2.3f, 0.05f, -4.6f), 1.15f, 90f);
            Place(props.FlowersAmber, area, new Vector3(-4.2f, 0.05f, -3.9f), 1f, 45f);

            Place(props.TreeFruit, area, new Vector3(5.4f, 0.05f, 2.0f), 1f, 0f);
            Place(props.TreeSapling, area, new Vector3(-5.8f, 0.05f, 4.2f), 1f, 120f);
        }

        // ================================================================ plaza

        /// <summary>The crossroads: a signpost, a bench, a lantern and planting.</summary>
        public static void DressPlaza(
            Transform parent, ProtoPalette p, PropLibrary.Props props, PropLibraryExtra.Extras extras)
        {
            Transform root = ProtoAssets.Empty("Plaza_Dressing", parent, Vector3.zero).transform;

            BuildCrossroadsSign(root, p, new Vector3(4.6f, 0f, 4.6f));

            Place(extras.Bench, root, new Vector3(-4.8f, 0f, 4.4f), 1f, 135f);
            Place(extras.Bench, root, new Vector3(4.8f, 0f, -4.4f), 1f, -45f);
            Place(extras.Lantern, root, new Vector3(-4.6f, 0f, -4.6f), 1.1f, 0f);

            Place(props.FlowersAmber, root, new Vector3(-3.0f, 0f, 5.6f), 1.15f, 20f);
            Place(props.FlowersWhite, root, new Vector3(3.0f, 0f, 5.6f), 1.1f, 140f);
            Place(props.FlowersPink, root, new Vector3(-3.0f, 0f, -5.6f), 1.15f, 260f);
            Place(props.FlowersAmber, root, new Vector3(3.0f, 0f, -5.6f), 1.05f, 300f);

            Place(props.BushRound, root, new Vector3(-6.4f, 0f, 0.6f), 1f, 30f);
            Place(props.BushRound, root, new Vector3(6.4f, 0f, -0.6f), 1.05f, 210f);
        }

        /// <summary>A four-armed signpost, each arm painted for the quadrant it points to.</summary>
        private static void BuildCrossroadsSign(Transform parent, ProtoPalette p, Vector3 position)
        {
            Transform sign = ProtoAssets.Empty("Crossroads_Signpost", parent, position).transform;

            Mesh plank = StylizedMeshLibrary.Plank(0.22f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.78f);
            Mesh cone = StylizedMeshLibrary.Cone(8);
            Mesh sphere = StylizedMeshLibrary.LowPolySphere();

            GameObject post = ProtoAssets.MeshObject(taper, "Post", sign, new Vector3(0f, 1.6f, 0f),
                new Vector3(0.3f, 3.2f, 0.3f), p.Wood, true);
            post.AddComponent<BoxCollider>();

            ProtoAssets.MeshObject(cone, "Finial", sign, new Vector3(0f, 3.4f, 0f),
                new Vector3(0.4f, 0.42f, 0.4f), p.RoofTerracotta, false);
            ProtoAssets.MeshObject(sphere, "Knob", sign, new Vector3(0f, 3.66f, 0f),
                Vector3.one * 0.2f, p.Amber, false);

            Material[] armColors = { p.Wheat, p.Tomato, p.RoofTeal, p.Amber };
            float[] armYaw = { 0f, 90f, 180f, 270f };
            float[] armHeight = { 2.75f, 2.35f, 1.95f, 1.55f };

            for (int i = 0; i < 4; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, armYaw[i], 0f);

                // Pointed arrow board: a plank plus a cone tip, so it reads as a direction.
                GameObject arm = ProtoAssets.MeshObject(plank, "Arm_" + i, sign,
                    rotation * new Vector3(0.9f, 0f, 0f) + new Vector3(0f, armHeight[i], 0f),
                    new Vector3(0.34f, 0.12f, 1.6f), armColors[i], true);
                arm.transform.localRotation = rotation * Quaternion.Euler(0f, 90f, 0f);

                GameObject tip = ProtoAssets.MeshObject(cone, "Tip_" + i, sign,
                    rotation * new Vector3(1.82f, 0f, 0f) + new Vector3(0f, armHeight[i], 0f),
                    new Vector3(0.36f, 0.34f, 0.36f), armColors[i], false);
                tip.transform.localRotation = rotation * Quaternion.Euler(0f, 0f, -90f);
            }
        }

        // ================================================================ treeline

        /// <summary>
        /// The framing vegetation. Three depth layers - near trees inside the fence, a dense
        /// belt just outside, and a far background band - is what produces overlap and
        /// parallax on a portrait screen instead of a flat empty horizon.
        /// </summary>
        public static void BuildTreeline(Transform parent, PropLibrary.Props props)
        {
            Transform root = ProtoAssets.Empty("Treeline", parent, Vector3.zero).transform;

            GameObject[] kinds =
            {
                props.TreeBroadleaf, props.TreeTall, props.TreeConifer,
                props.TreeBroadleaf, props.TreeFruit, props.TreeTall
            };

            // ---- near layer: inside the fence, framing the corners without blocking play
            (Vector3 pos, int kind, float scale)[] near =
            {
                (new Vector3(-30.2f, 0f, 30.4f), 0, 1.0f), (new Vector3(-21.6f, 0f, 29.6f), 1, 0.92f),
                (new Vector3(-30.0f, 0f, 21.5f), 2, 0.88f), (new Vector3(28.5f, 0f, 28.5f), 1, 0.96f),
                (new Vector3(22.5f, 0f, 29.4f), 0, 1.05f), (new Vector3(29.2f, 0f, 22f), 3, 0.9f),
                (new Vector3(-29f, 0f, -28f), 0, 1.0f), (new Vector3(-23f, 0f, -29.2f), 2, 0.9f),
                (new Vector3(29f, 0f, -29f), 1, 1.02f), (new Vector3(23f, 0f, -29.4f), 0, 0.95f),
                (new Vector3(-29.6f, 0f, 1.5f), 1, 0.9f), (new Vector3(29.6f, 0f, 1.5f), 0, 0.94f),
                (new Vector3(-9.8f, 0f, 28.8f), 0, 0.88f), (new Vector3(10.2f, 0f, 28.4f), 2, 0.95f),
                (new Vector3(-9.4f, 0f, -29f), 1, 0.92f), (new Vector3(9.8f, 0f, -29.2f), 0, 0.98f),
                (new Vector3(-25.4f, 0f, 12.5f), 4, 0.9f), (new Vector3(25.6f, 0f, 12.6f), 0, 0.93f),
                (new Vector3(-25.8f, 0f, -1.5f), 2, 0.86f), (new Vector3(25.8f, 0f, -1.5f), 1, 0.9f)
            };

            for (int i = 0; i < near.Length; i++)
            {
                FarmEnvironmentBuilder.PlaceProp(
                    kinds[near[i].kind], root, near[i].pos, near[i].scale, i * 63f % 360f);
            }

            // ---- middle belt: dense, just outside the fence
            for (int i = 0; i < 26; i++)
            {
                float angle = i / 26f * Mathf.PI * 2f + 0.22f;
                float radius = 36f + (i % 3) * 2.4f;

                FarmEnvironmentBuilder.PlaceProp(
                    kinds[i % kinds.Length], root,
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius),
                    1.0f + (i % 4) * 0.09f, i * 41f % 360f);
            }

            // ---- far band: bigger, sparser, softened by the fog into a horizon mass
            for (int i = 0; i < 22; i++)
            {
                float angle = i / 22f * Mathf.PI * 2f + 0.55f;
                float radius = 47f + (i % 4) * 3.5f;

                FarmEnvironmentBuilder.PlaceProp(
                    (i % 3 == 0) ? props.TreeConifer : props.TreeTall, root,
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius),
                    1.25f + (i % 3) * 0.16f, i * 37f % 360f);
            }
        }

        /// <summary>Bushes, rocks and flowers filling the open lawn between zones.</summary>
        public static void BuildGroundcover(Transform parent, PropLibrary.Props props)
        {
            Transform root = ProtoAssets.Empty("Groundcover", parent, Vector3.zero).transform;

            // Bushes soften the path edges and the zone corners.
            (Vector3 pos, int kind)[] bushes =
            {
                (new Vector3(-8.4f, 0f, 24f), 0), (new Vector3(8.4f, 0f, 24f), 1),
                (new Vector3(-8.4f, 0f, -13.5f), 1), (new Vector3(8.4f, 0f, -13.5f), 0),
                (new Vector3(-5.4f, 0f, 11.5f), 2), (new Vector3(5.4f, 0f, 11.5f), 2),
                (new Vector3(-5.4f, 0f, -11.5f), 2), (new Vector3(5.4f, 0f, -11.5f), 2),
                (new Vector3(-26.5f, 0f, -3f), 1), (new Vector3(26.5f, 0f, -3f), 0),
                (new Vector3(-13.5f, 0f, 27.8f), 0), (new Vector3(13.8f, 0f, 27.8f), 1),
                (new Vector3(-24.8f, 0f, -27f), 1), (new Vector3(24.8f, 0f, -27.5f), 0),
                (new Vector3(-18f, 0f, 27.5f), 2), (new Vector3(18f, 0f, -27.8f), 2),
                (new Vector3(-28f, 0f, 8f), 1), (new Vector3(28f, 0f, -12f), 0)
            };

            GameObject[] bushKinds = { props.BushRound, props.BushWide, props.BushSprig };

            for (int i = 0; i < bushes.Length; i++)
            {
                FarmEnvironmentBuilder.PlaceProp(
                    bushKinds[bushes[i].kind], root, bushes[i].pos,
                    0.92f + (i % 3) * 0.14f, i * 53f % 360f);
            }

            // Flowers along the main avenues, in alternating colours.
            GameObject[] flowerKinds = { props.FlowersWhite, props.FlowersAmber, props.FlowersPink };
            float[] avenue = { 13f, 17f, 21f, 25f, 29f };

            for (int i = 0; i < avenue.Length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    FarmEnvironmentBuilder.PlaceProp(flowerKinds[i % 3], root,
                        new Vector3(3.6f * side, 0f, avenue[i]), 1.05f, i * 71f % 360f);
                    FarmEnvironmentBuilder.PlaceProp(flowerKinds[(i + 1) % 3], root,
                        new Vector3(3.6f * side, 0f, -avenue[i]), 1.0f, i * 89f % 360f);
                    FarmEnvironmentBuilder.PlaceProp(flowerKinds[(i + 2) % 3], root,
                        new Vector3(avenue[i], 0f, 3.6f * side), 1.05f, i * 47f % 360f);
                    FarmEnvironmentBuilder.PlaceProp(flowerKinds[i % 3], root,
                        new Vector3(-avenue[i], 0f, 3.6f * side), 1.0f, i * 59f % 360f);
                }
            }

            // Rock clusters as occasional larger accents.
            FarmEnvironmentBuilder.PlaceProp(props.Boulder, root, new Vector3(-11.8f, 0f, 25f), 1f, 20f);
            FarmEnvironmentBuilder.PlaceProp(props.Pebbles, root, new Vector3(-10.4f, 0f, 24f), 1f, -40f);
            FarmEnvironmentBuilder.PlaceProp(props.Rock, root, new Vector3(12.5f, 0f, -25f), 0.95f, 130f);
            FarmEnvironmentBuilder.PlaceProp(props.Boulder, root, new Vector3(-27.8f, 0f, 6f), 1.1f, 70f);
            FarmEnvironmentBuilder.PlaceProp(props.Rock, root, new Vector3(27.2f, 0f, 17f), 1f, 250f);
            FarmEnvironmentBuilder.PlaceProp(props.Pebbles, root, new Vector3(19f, 0f, 8.5f), 1f, 15f);
            FarmEnvironmentBuilder.PlaceProp(props.Pebbles, root, new Vector3(-19f, 0f, -9.5f), 1f, 195f);
        }
    }
}
