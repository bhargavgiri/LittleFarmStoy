using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Generates the UI icon set as PNG sprites.
    ///
    /// The project ships no external art, so icons are painted here from signed distance
    /// fields. That is not a compromise for its own sake: SDFs give exact antialiasing at any
    /// size, and describing every glyph with the same handful of primitives and the same stroke
    /// weight is what makes a set look designed rather than collected.
    ///
    /// These are honest placeholders for hand-authored artwork. The architecture is what
    /// matters: every icon is a named sprite asset, so replacing one is dropping a PNG in
    /// place, with no code change anywhere.
    /// </summary>
    public static class UiIconLibrary
    {
        public const int IconSize = 128;

        private const string IconFolder = ProtoAssets.UiFolder + "/Icons";

        /// <summary>Signed distance to a shape: negative inside, in the icon's [-1,1] space.</summary>
        private delegate float Field(Vector2 p);

        private readonly struct Layer
        {
            public readonly Field Shape;
            public readonly Color Color;

            public Layer(Field shape, Color color)
            {
                Shape = shape;
                Color = color;
            }
        }

        /// <summary>Every icon the HUD can ask for. The name is the sprite asset's file name.</summary>
        public static class Names
        {
            public const string Coin = "Coin";
            public const string Level = "Level";
            public const string Seed = "Seed";
            public const string Wheat = "Wheat";
            public const string Corn = "Corn";
            public const string Egg = "Egg";
            public const string Milk = "Milk";
            public const string Till = "Till";
            public const string Plant = "Plant";
            public const string Harvest = "Harvest";
            public const string Feed = "Feed";
            public const string Collect = "Collect";
            public const string Menu = "Menu";
            public const string Bag = "Bag";
            public const string Close = "Close";
            public const string Hand = "Hand";
        }

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Builds every icon once and returns them by name.</summary>
        public static Dictionary<string, Sprite> BuildAll()
        {
            Cache.Clear();
            ProtoAssets.EnsureFolder(IconFolder);

            Icon(Names.Coin, Coin);
            Icon(Names.Level, Level);
            Icon(Names.Seed, Seed);
            Icon(Names.Wheat, Wheat);
            Icon(Names.Corn, Corn);
            Icon(Names.Egg, Egg);
            Icon(Names.Milk, Milk);
            Icon(Names.Till, Till);
            Icon(Names.Plant, Plant);
            Icon(Names.Harvest, Harvest);
            Icon(Names.Feed, Feed);
            Icon(Names.Collect, Collect);
            Icon(Names.Menu, Menu);
            Icon(Names.Bag, Bag);
            Icon(Names.Close, Close);
            Icon(Names.Hand, Hand);

            return Cache;
        }

        public static Sprite Get(string iconName)
        {
            if (Cache.TryGetValue(iconName, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            string path = IconFolder + "/Icon_" + iconName + ".png";
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite != null)
            {
                Cache[iconName] = sprite;
            }

            return sprite;
        }

        // ================================================================ painting

        private static void Icon(string iconName, Func<List<Layer>> build)
        {
            string path = IconFolder + "/Icon_" + iconName + ".png";
            Cache[iconName] = Paint(path, build());
        }

        /// <summary>
        /// Rasterises the layers back to front. Coverage comes from the distance field itself
        /// rather than from supersampling, which is both cheaper and cleaner: the edge is
        /// exactly one pixel wide at every scale and every curvature.
        /// </summary>
        private static Sprite Paint(string path, List<Layer> layers)
        {
            Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[IconSize * IconSize];

            // One pixel expressed in the icon's [-1,1] coordinate space.
            float pixel = 2f / IconSize;

            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    Vector2 p = new Vector2(
                        (x + 0.5f) * pixel - 1f,
                        (y + 0.5f) * pixel - 1f);

                    Color acc = new Color(0f, 0f, 0f, 0f);

                    for (int i = 0; i < layers.Count; i++)
                    {
                        float distance = layers[i].Shape(p);
                        float coverage = Mathf.Clamp01(0.5f - distance / pixel);

                        if (coverage <= 0f)
                        {
                            continue;
                        }

                        Color source = layers[i].Color;
                        float alpha = source.a * coverage;

                        // Straight-alpha "over", accumulated in premultiplied space so stacked
                        // translucent layers composite correctly.
                        float outAlpha = alpha + acc.a * (1f - alpha);

                        if (outAlpha <= 0.0001f)
                        {
                            continue;
                        }

                        acc.r = (source.r * alpha + acc.r * acc.a * (1f - alpha)) / outAlpha;
                        acc.g = (source.g * alpha + acc.g * acc.a * (1f - alpha)) / outAlpha;
                        acc.b = (source.b * alpha + acc.b * acc.a * (1f - alpha)) / outAlpha;
                        acc.a = outAlpha;
                    }

                    pixels[y * IconSize + x] = acc;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ================================================================ distance primitives

        private static Field Circle(Vector2 centre, float radius)
        {
            return p => (p - centre).magnitude - radius;
        }

        /// <summary>Ellipse, approximated by scaling space. Exact enough for a 128px glyph.</summary>
        private static Field Ellipse(Vector2 centre, Vector2 radii)
        {
            return p =>
            {
                Vector2 d = p - centre;
                Vector2 scaled = new Vector2(d.x / radii.x, d.y / radii.y);
                float k = scaled.magnitude;
                return (k - 1f) * Mathf.Min(radii.x, radii.y);
            };
        }

        private static Field RoundedBox(Vector2 centre, Vector2 halfExtents, float radius)
        {
            return p =>
            {
                Vector2 d = new Vector2(
                    Mathf.Abs(p.x - centre.x) - halfExtents.x + radius,
                    Mathf.Abs(p.y - centre.y) - halfExtents.y + radius);

                float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
                return outside + inside - radius;
            };
        }

        /// <summary>A capsule: the stroke primitive every icon's lines are made from.</summary>
        private static Field Segment(Vector2 a, Vector2 b, float thickness)
        {
            return p =>
            {
                Vector2 pa = p - a;
                Vector2 ba = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                return (pa - ba * h).magnitude - thickness;
            };
        }

        /// <summary>Exact five-pointed star. <paramref name="inner"/> is the waist ratio.</summary>
        private static Field Star(Vector2 centre, float radius, float inner)
        {
            Vector2 k1 = new Vector2(0.809016994375f, -0.587785252292f);
            Vector2 k2 = new Vector2(-k1.x, k1.y);

            return point =>
            {
                Vector2 p = point - centre;
                p.x = Mathf.Abs(p.x);
                p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
                p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
                p.x = Mathf.Abs(p.x);
                p.y -= radius;

                Vector2 ba = inner * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
                float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, radius);

                return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
            };
        }

        private static Field Union(params Field[] fields)
        {
            return p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < fields.Length; i++)
                {
                    d = Mathf.Min(d, fields[i](p));
                }

                return d;
            };
        }

        private static Field Subtract(Field shape, Field hole)
        {
            return p => Mathf.Max(shape(p), -hole(p));
        }

        private static Field Intersect(Field a, Field b)
        {
            return p => Mathf.Max(a(p), b(p));
        }

        /// <summary>Outline of a shape: an annulus of the given thickness around its edge.</summary>
        private static Field Ring(Field shape, float thickness)
        {
            return p => Mathf.Abs(shape(p)) - thickness;
        }

        private static Field Rotate(Field shape, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);

            return p => shape(new Vector2(p.x * c + p.y * s, -p.x * s + p.y * c));
        }

        // ================================================================ the glyphs

        // A shared vocabulary keeps the set coherent: one stroke weight, one corner radius,
        // and the same deep-shade-behind-light-shade treatment on every solid form.
        private const float Stroke = 0.085f;
        private const float Corner = 0.10f;

        private static List<Layer> L(params Layer[] layers) => new List<Layer>(layers);

        private static Layer Fill(Field shape, Color color) => new Layer(shape, color);

        private static List<Layer> Coin()
        {
            Color gold = UiPalette.Hex("F5C542");
            Color goldDeep = UiPalette.Hex("D89A18");

            return L(
                Fill(Circle(Vector2.zero, 0.78f), goldDeep),
                Fill(Circle(new Vector2(0f, 0.05f), 0.68f), gold),
                Fill(Ring(Circle(Vector2.zero, 0.46f), 0.055f), goldDeep),
                // Highlight: the glint that stops a flat disc reading as a button.
                Fill(Ellipse(new Vector2(-0.24f, 0.34f), new Vector2(0.22f, 0.12f)),
                    new Color(1f, 1f, 1f, 0.55f)));
        }

        private static List<Layer> Level()
        {
            Color amber = UiPalette.Hex("FFC94A");
            Color amberDeep = UiPalette.Hex("E08A16");

            return L(
                Fill(Star(new Vector2(0f, -0.02f), 0.88f, 0.42f), amberDeep),
                Fill(Star(new Vector2(0f, 0.04f), 0.76f, 0.42f), amber));
        }

        private static List<Layer> Seed()
        {
            Color husk = UiPalette.Hex("B07A42");
            Color huskDeep = UiPalette.Hex("7E5327");
            Color sprout = UiPalette.Hex("6FBF52");

            return L(
                // Two seeds and a first shoot: "this is what you plant", not "this is a bean".
                Fill(Rotate(Ellipse(new Vector2(-0.02f, -0.30f), new Vector2(0.30f, 0.42f)), -18f), huskDeep),
                Fill(Rotate(Ellipse(new Vector2(-0.05f, -0.26f), new Vector2(0.24f, 0.35f)), -18f), husk),
                Fill(Segment(new Vector2(0.06f, -0.05f), new Vector2(0.10f, 0.42f), 0.055f), sprout),
                Fill(Rotate(Ellipse(new Vector2(0.34f, 0.36f), new Vector2(0.26f, 0.13f)), 24f), sprout));
        }

        private static List<Layer> Wheat()
        {
            Color straw = UiPalette.Hex("E8B84B");
            Color strawDeep = UiPalette.Hex("B8862A");
            Color stem = UiPalette.Hex("8FA83E");

            List<Layer> layers = L(
                Fill(Segment(new Vector2(0f, -0.85f), new Vector2(0f, 0.35f), 0.055f), stem));

            // Four grain pairs climbing the stalk, each smaller than the last.
            for (int i = 0; i < 4; i++)
            {
                float y = -0.30f + i * 0.28f;
                float size = 0.30f - i * 0.035f;

                layers.Add(Fill(Rotate(Ellipse(new Vector2(-0.20f, y), new Vector2(0.12f, size)), 26f), strawDeep));
                layers.Add(Fill(Rotate(Ellipse(new Vector2(-0.22f, y), new Vector2(0.10f, size * 0.9f)), 26f), straw));
                layers.Add(Fill(Rotate(Ellipse(new Vector2(0.20f, y), new Vector2(0.12f, size)), -26f), strawDeep));
                layers.Add(Fill(Rotate(Ellipse(new Vector2(0.22f, y), new Vector2(0.10f, size * 0.9f)), -26f), straw));
            }

            layers.Add(Fill(Ellipse(new Vector2(0f, 0.60f), new Vector2(0.13f, 0.30f)), straw));
            return layers;
        }

        private static List<Layer> Corn()
        {
            Color kernel = UiPalette.Hex("F3CB24");
            Color kernelDeep = UiPalette.Hex("C98E12");
            Color husk = UiPalette.Hex("7FB04A");

            return L(
                Fill(Rotate(Ellipse(new Vector2(-0.34f, -0.10f), new Vector2(0.18f, 0.52f)), 22f), husk),
                Fill(Rotate(Ellipse(new Vector2(0.34f, -0.10f), new Vector2(0.18f, 0.52f)), -22f), husk),
                Fill(Ellipse(new Vector2(0f, 0.02f), new Vector2(0.34f, 0.74f)), kernelDeep),
                Fill(Ellipse(new Vector2(-0.03f, 0.04f), new Vector2(0.27f, 0.66f)), kernel),
                Fill(Segment(new Vector2(0f, 0.62f), new Vector2(0f, 0.92f), 0.05f), husk));
        }

        private static List<Layer> Egg()
        {
            Color shell = UiPalette.Hex("FFF3DE");
            Color shellDeep = UiPalette.Hex("E3C9A6");

            return L(
                Fill(Ellipse(new Vector2(0f, -0.06f), new Vector2(0.58f, 0.78f)), shellDeep),
                Fill(Ellipse(new Vector2(-0.03f, -0.02f), new Vector2(0.50f, 0.70f)), shell),
                Fill(Ellipse(new Vector2(-0.18f, 0.30f), new Vector2(0.15f, 0.22f)),
                    new Color(1f, 1f, 1f, 0.75f)));
        }

        private static List<Layer> Milk()
        {
            Color glass = UiPalette.Hex("EAF2F6");
            Color milk = UiPalette.Hex("FFFFFF");
            Color band = UiPalette.Hex("7FB8D8");

            Field bottle = RoundedBox(new Vector2(0f, -0.18f), new Vector2(0.42f, 0.58f), 0.18f);
            Field neck = RoundedBox(new Vector2(0f, 0.52f), new Vector2(0.20f, 0.26f), 0.08f);

            return L(
                Fill(Union(bottle, neck), band),
                Fill(Union(
                        RoundedBox(new Vector2(0f, -0.18f), new Vector2(0.35f, 0.51f), 0.15f),
                        RoundedBox(new Vector2(0f, 0.50f), new Vector2(0.14f, 0.22f), 0.06f)),
                    glass),
                Fill(RoundedBox(new Vector2(0f, -0.28f), new Vector2(0.33f, 0.40f), 0.14f), milk),
                Fill(Ellipse(new Vector2(-0.16f, -0.10f), new Vector2(0.07f, 0.20f)),
                    new Color(0.78f, 0.86f, 0.92f, 0.8f)));
        }

        private static List<Layer> Till()
        {
            Color wood = UiPalette.Hex("A9713F");
            Color metal = UiPalette.Hex("9AA6B2");
            Color soil = UiPalette.Hex("7A5433");

            return L(
                Fill(Segment(new Vector2(-0.55f, 0.70f), new Vector2(0.22f, -0.18f), Stroke * 0.75f), wood),
                Fill(RoundedBox(new Vector2(0.34f, -0.32f), new Vector2(0.30f, 0.13f), 0.05f), metal),
                // Three furrows: the icon says "turn the soil", not "hold a tool".
                Fill(Segment(new Vector2(-0.62f, -0.70f), new Vector2(-0.18f, -0.70f), 0.055f), soil),
                Fill(Segment(new Vector2(-0.10f, -0.78f), new Vector2(0.40f, -0.78f), 0.055f), soil),
                Fill(Segment(new Vector2(0.18f, -0.58f), new Vector2(0.68f, -0.58f), 0.055f), soil));
        }

        private static List<Layer> Plant()
        {
            Color soil = UiPalette.Hex("7A5433");
            Color sprout = UiPalette.Hex("6FBF52");
            Color sproutDeep = UiPalette.Hex("48923A");

            return L(
                Fill(Segment(new Vector2(0f, -0.30f), new Vector2(0f, 0.30f), 0.06f), sproutDeep),
                Fill(Rotate(Ellipse(new Vector2(-0.32f, 0.22f), new Vector2(0.28f, 0.14f)), -22f), sprout),
                Fill(Rotate(Ellipse(new Vector2(0.32f, 0.40f), new Vector2(0.28f, 0.14f)), 22f), sprout),
                Fill(RoundedBox(new Vector2(0f, -0.66f), new Vector2(0.72f, 0.16f), 0.08f), soil));
        }

        private static List<Layer> Harvest()
        {
            Color blade = UiPalette.Hex("C3CDD6");
            Color bladeDeep = UiPalette.Hex("8A97A4");
            Color wood = UiPalette.Hex("A9713F");

            // A sickle: a crescent cut from two offset circles, plus a handle.
            Field crescent = Subtract(
                Circle(new Vector2(0.02f, 0.18f), 0.72f),
                Circle(new Vector2(0.22f, 0.36f), 0.62f));

            return L(
                Fill(crescent, bladeDeep),
                Fill(Intersect(crescent, Circle(new Vector2(-0.10f, 0.10f), 0.85f)), blade),
                Fill(Segment(new Vector2(-0.34f, -0.34f), new Vector2(0.28f, -0.82f), Stroke * 0.8f), wood));
        }

        private static List<Layer> Feed()
        {
            Color bowl = UiPalette.Hex("C98B53");
            Color bowlDeep = UiPalette.Hex("8E5C2F");
            Color grain = UiPalette.Hex("E8B84B");

            Field bowlShape = Intersect(
                Ellipse(new Vector2(0f, -0.10f), new Vector2(0.78f, 0.62f)),
                RoundedBox(new Vector2(0f, -0.46f), new Vector2(0.9f, 0.42f), 0.02f));

            return L(
                Fill(bowlShape, bowlDeep),
                Fill(Ellipse(new Vector2(0f, -0.16f), new Vector2(0.62f, 0.16f)), bowl),
                Fill(Circle(new Vector2(-0.26f, 0.22f), 0.13f), grain),
                Fill(Circle(new Vector2(0.04f, 0.40f), 0.13f), grain),
                Fill(Circle(new Vector2(0.32f, 0.20f), 0.13f), grain));
        }

        private static List<Layer> Collect()
        {
            Color basket = UiPalette.Hex("C98B53");
            Color basketDeep = UiPalette.Hex("8E5C2F");
            Color arrow = UiPalette.Hex("6FBF52");

            Field basketShape = Intersect(
                Ellipse(new Vector2(0f, -0.30f), new Vector2(0.72f, 0.58f)),
                RoundedBox(new Vector2(0f, -0.58f), new Vector2(0.9f, 0.36f), 0.02f));

            return L(
                Fill(basketShape, basketDeep),
                Fill(Ellipse(new Vector2(0f, -0.30f), new Vector2(0.58f, 0.13f)), basket),
                // Downward arrow into the basket: collecting, not merely owning.
                Fill(Segment(new Vector2(0f, 0.72f), new Vector2(0f, 0.06f), 0.075f), arrow),
                Fill(Segment(new Vector2(-0.28f, 0.28f), new Vector2(0f, 0.02f), 0.075f), arrow),
                Fill(Segment(new Vector2(0.28f, 0.28f), new Vector2(0f, 0.02f), 0.075f), arrow));
        }

        private static List<Layer> Menu()
        {
            Color ink = UiPalette.TextPrimary;

            return L(
                Fill(RoundedBox(new Vector2(0f, 0.42f), new Vector2(0.60f, 0.085f), 0.085f), ink),
                Fill(RoundedBox(new Vector2(0f, 0.00f), new Vector2(0.60f, 0.085f), 0.085f), ink),
                Fill(RoundedBox(new Vector2(0f, -0.42f), new Vector2(0.60f, 0.085f), 0.085f), ink));
        }

        private static List<Layer> Bag()
        {
            Color canvas = UiPalette.Hex("C98B53");
            Color canvasDeep = UiPalette.Hex("8E5C2F");

            return L(
                Fill(Ring(Circle(new Vector2(0f, 0.42f), 0.34f), 0.065f), canvasDeep),
                Fill(RoundedBox(new Vector2(0f, -0.22f), new Vector2(0.68f, 0.52f), Corner * 2f), canvasDeep),
                Fill(RoundedBox(new Vector2(0f, -0.20f), new Vector2(0.58f, 0.44f), Corner * 1.8f), canvas),
                Fill(RoundedBox(new Vector2(0f, -0.06f), new Vector2(0.60f, 0.055f), 0.055f), canvasDeep));
        }

        private static List<Layer> Close()
        {
            Color ink = UiPalette.TextPrimary;

            return L(
                Fill(Segment(new Vector2(-0.42f, 0.42f), new Vector2(0.42f, -0.42f), 0.085f), ink),
                Fill(Segment(new Vector2(0.42f, 0.42f), new Vector2(-0.42f, -0.42f), 0.085f), ink));
        }

        private static List<Layer> Hand()
        {
            Color skin = UiPalette.Hex("F2C79A");
            Color skinDeep = UiPalette.Hex("C99A6B");

            return L(
                Fill(RoundedBox(new Vector2(0f, -0.28f), new Vector2(0.40f, 0.34f), 0.16f), skinDeep),
                Fill(RoundedBox(new Vector2(0f, -0.26f), new Vector2(0.34f, 0.28f), 0.14f), skin),
                Fill(RoundedBox(new Vector2(-0.22f, 0.22f), new Vector2(0.10f, 0.34f), 0.10f), skin),
                Fill(RoundedBox(new Vector2(0.02f, 0.30f), new Vector2(0.10f, 0.42f), 0.10f), skin),
                Fill(RoundedBox(new Vector2(0.26f, 0.20f), new Vector2(0.10f, 0.32f), 0.10f), skin));
        }
    }
}
