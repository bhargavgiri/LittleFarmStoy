using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// The single source of truth for Little Farm Story's colour identity.
    /// Every material in the game is created here, on one of three smoothness finishes, so the
    /// farm reads as one deliberate scheme rather than a pile of individually tinted objects.
    ///
    /// Direction: sunny mid-morning, hand-painted toy farm. Warm chocolate earth against bright
    /// grass, pale wheat-sand paths as the connective tissue, cream buildings so the saturated
    /// roofs carry the accent.
    /// </summary>
    public class ProtoPalette
    {
        // ---- grass
        public Material GrassLight;
        public Material GrassMid;
        public Material GrassDeep;

        // ---- earth
        public Material Soil;
        public Material SoilTilled;
        public Material Path;
        public Material PathEdge;

        // ---- timber and stone
        public Material Wood;
        public Material WoodDark;
        public Material WoodLight;
        public Material Stone;
        public Material Metal;
        public Material Concrete;

        // ---- buildings
        public Material WallCream;
        public Material WallWarm;
        public Material RoofTerracotta;
        public Material RoofTeal;
        public Material RoofMustard;
        public Material Trim;
        public Material Glass;
        public Material FencePaint;

        // ---- plants
        public Material LeafBright;
        public Material LeafMid;
        public Material LeafDeep;
        public Material Stem;

        // ---- crops
        public Material Wheat;
        public Material WheatStraw;
        public Material Tomato;
        public Material Corn;
        public Material CornHusk;

        // ---- characters
        public Material Skin;
        public Material Shirt;
        public Material Denim;
        public Material Straw;
        public Material Boots;
        public Material White;
        public Material Charcoal;
        public Material Beak;
        public Material Comb;
        /// <summary>Second plumage tone, so a flock is not all one colour.</summary>
        public Material ChickenBrown;
        public Material Muzzle;
        public Material Horn;

        // ---- accents and water
        public Material Amber;
        public Material AmberDeep;
        public Material Water;
        public Material WaterShallow;

        /// <summary>Convenience: the colour a crop marker or sign uses for each field.</summary>
        public static readonly Color WheatAccent = Hex("E8C55A");
        public static readonly Color TomatoAccent = Hex("E04B3C");
        public static readonly Color CornAccent = Hex("F5D046");

        public static Color Hex(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out Color color)
                ? color
                : Color.magenta;
        }

        public static ProtoPalette Create()
        {
            const ProtoAssets.Finish matte = ProtoAssets.Finish.Matte;
            const ProtoAssets.Finish soft = ProtoAssets.Finish.Soft;
            const ProtoAssets.Finish sheen = ProtoAssets.Finish.Sheen;

            return new ProtoPalette
            {
                // grass
                GrassLight = ProtoAssets.Lit("GrassLight", Hex("7CC15A"), matte),
                GrassMid = ProtoAssets.Lit("GrassMid", Hex("63AC48"), matte),
                GrassDeep = ProtoAssets.Lit("GrassDeep", Hex("4E8F39"), matte),

                // earth
                Soil = ProtoAssets.Lit("Soil", Hex("6B4A2F"), matte),
                SoilTilled = ProtoAssets.Lit("SoilTilled", Hex("4E3320"), matte),
                Path = ProtoAssets.Lit("Path", Hex("E3C98F"), matte),
                PathEdge = ProtoAssets.Lit("PathEdge", Hex("CBAE74"), matte),

                // timber and stone
                Wood = ProtoAssets.Lit("Wood", Hex("A9713F"), soft),
                WoodDark = ProtoAssets.Lit("WoodDark", Hex("7A4E2A"), soft),
                WoodLight = ProtoAssets.Lit("WoodLight", Hex("C99863"), soft),
                Stone = ProtoAssets.Lit("Stone", Hex("B8B2A6"), matte),
                Metal = ProtoAssets.Lit("Metal", Hex("9AA2A8"), soft),
                Concrete = ProtoAssets.Lit("Concrete", Hex("C9C3B4"), matte),

                // buildings
                WallCream = ProtoAssets.Lit("WallCream", Hex("FAF3E0"), soft),
                WallWarm = ProtoAssets.Lit("WallWarm", Hex("F2E2C4"), soft),
                RoofTerracotta = ProtoAssets.Lit("RoofTerracotta", Hex("D9603F"), soft),
                RoofTeal = ProtoAssets.Lit("RoofTeal", Hex("3FA9A0"), soft),
                RoofMustard = ProtoAssets.Lit("RoofMustard", Hex("E8B23C"), soft),
                Trim = ProtoAssets.Lit("Trim", Hex("8C5A33"), soft),
                Glass = ProtoAssets.Lit("Glass", Hex("A8D8EA"), sheen),
                FencePaint = ProtoAssets.Lit("FencePaint", Hex("F6EFDD"), soft),

                // plants
                LeafBright = ProtoAssets.Lit("LeafBright", Hex("8FD46A"), matte),
                LeafMid = ProtoAssets.Lit("LeafMid", Hex("6FBF52"), matte),
                LeafDeep = ProtoAssets.Lit("LeafDeep", Hex("4E9B3C"), matte),
                Stem = ProtoAssets.Lit("Stem", Hex("5FA544"), matte),

                // crops
                Wheat = ProtoAssets.Lit("Wheat", WheatAccent, matte),
                WheatStraw = ProtoAssets.Lit("WheatStraw", Hex("D6A94A"), matte),
                Tomato = ProtoAssets.Lit("Tomato", TomatoAccent, soft),
                Corn = ProtoAssets.Lit("Corn", CornAccent, matte),
                CornHusk = ProtoAssets.Lit("CornHusk", Hex("A8C85A"), matte),

                // characters
                Skin = ProtoAssets.Lit("Skin", Hex("F0C199"), matte),
                Shirt = ProtoAssets.Lit("Shirt", Hex("4F9DD9"), matte),
                Denim = ProtoAssets.Lit("Denim", Hex("3B5D8C"), matte),
                Straw = ProtoAssets.Lit("Straw", Hex("EBCB78"), matte),
                Boots = ProtoAssets.Lit("Boots", Hex("6B4326"), soft),
                White = ProtoAssets.Lit("White", Hex("FBFAF6"), matte),
                Charcoal = ProtoAssets.Lit("Charcoal", Hex("33302C"), matte),
                Beak = ProtoAssets.Lit("Beak", Hex("F2A33C"), soft),
                Comb = ProtoAssets.Lit("Comb", Hex("E05252"), matte),
                ChickenBrown = ProtoAssets.Lit("ChickenBrown", Hex("C98B52"), matte),
                Muzzle = ProtoAssets.Lit("Muzzle", Hex("F0A0A5"), matte),
                Horn = ProtoAssets.Lit("Horn", Hex("E8DCC0"), soft),

                // accents and water
                Amber = ProtoAssets.Lit("Amber", Hex("F5A623"), soft),
                AmberDeep = ProtoAssets.Lit("AmberDeep", Hex("D4820C"), soft),
                Water = ProtoAssets.Lit("Water", Hex("4FB3E8"), sheen),
                WaterShallow = ProtoAssets.Lit("WaterShallow", Hex("74C9EE"), sheen)
            };
        }
    }
}
