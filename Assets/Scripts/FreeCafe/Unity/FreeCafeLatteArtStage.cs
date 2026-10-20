using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Cup silhouettes drawn around the latte surface. The liquid itself stays on the existing Plane.</summary>
    public enum LatteCupStyle { LatteBowl, Small, Large }

    /// <summary>Procedural, cached art for the latte bench: tabletop, top-down cup and pattern glyphs.</summary>
    public static class FreeCafeLatteArtStage
    {
        /// <summary>Cup textures cover ±Extent bowl radii; the liquid disk is exactly one radius.</summary>
        public const float Extent = 2f;
        private const int CupSize = 512;
        private static readonly Dictionary<LatteCupStyle, Sprite> Cups = new Dictionary<LatteCupStyle, Sprite>();
        private static readonly Dictionary<string, Texture2D> Glyphs = new Dictionary<string, Texture2D>();
        private static Sprite table;

        public static LatteCupStyle StyleFor(string specId)
            => specId == "cup.small" ? LatteCupStyle.Small : specId == "cup.large" ? LatteCupStyle.Large : LatteCupStyle.LatteBowl;

        /// <summary>Relative liquid size, so a demitasse visibly reads smaller than a mug.</summary>
        public static float BowlScale(LatteCupStyle style)
            => style == LatteCupStyle.Small ? .8f : style == LatteCupStyle.Large ? 1f : .92f;

        /// <summary>Outermost painted radius (saucer, coaster or handle) in bowl radii.</summary>
        public static float FootprintRadius(LatteCupStyle style) => style == LatteCupStyle.Large ? 1.72f : 1.68f;

        public static string StyleName(LatteCupStyle style)
            => style == LatteCupStyle.Small ? "小杯" : style == LatteCupStyle.Large ? "大杯" : "拉花杯";

        private struct CupLook
        {
            public Color glaze, rim, accent, saucer, interior;
            public float body, saucerRadius, handleLength, handleWidth;
            public bool hasSaucer, coaster;
        }

        private static CupLook Look(LatteCupStyle style)
        {
            switch (style)
            {
                case LatteCupStyle.Small:
                    return new CupLook { glaze = new Color(.97f, .96f, .93f), rim = new Color(.99f, .98f, .95f), accent = new Color(.18f, .27f, .52f),
                        saucer = new Color(.95f, .94f, .91f), interior = new Color(.20f, .10f, .05f), body = 1.17f, saucerRadius = 1.62f,
                        handleLength = .40f, handleWidth = .13f, hasSaucer = true };
                case LatteCupStyle.Large:
                    return new CupLook { glaze = new Color(.60f, .32f, .19f), rim = new Color(.93f, .86f, .74f), accent = new Color(.42f, .21f, .12f),
                        saucer = new Color(.40f, .27f, .17f), interior = new Color(.20f, .10f, .05f), body = 1.15f, saucerRadius = 1.42f,
                        handleLength = .56f, handleWidth = .17f, coaster = true };
                default:
                    return new CupLook { glaze = new Color(.96f, .92f, .84f), rim = new Color(.99f, .96f, .90f), accent = new Color(.78f, .60f, .28f),
                        saucer = new Color(.95f, .91f, .83f), interior = new Color(.20f, .10f, .05f), body = 1.1f, saucerRadius = 1.62f,
                        handleLength = .48f, handleWidth = .15f, hasSaucer = true };
            }
        }

        public static Sprite Cup(LatteCupStyle style)
        {
            if (Cups.TryGetValue(style, out Sprite cached) && cached != null) return cached;
            var look = Look(style);
            var texture = new Texture2D(CupSize, CupSize, TextureFormat.RGBA32, false) { name = "LatteCup_" + style, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[CupSize * CupSize];
            float unit = CupSize / (Extent * 2), aa = 1.5f / unit;
            Vector2 handleDir = new Vector2(Mathf.Cos(-28f * Mathf.Deg2Rad), Mathf.Sin(-28f * Mathf.Deg2Rad));
            Vector2 light = new Vector2(-.6f, .8f);
            for (int py = 0; py < CupSize; py++)
            for (int px = 0; px < CupSize; px++)
            {
                Vector2 p = new Vector2((px + .5f) / unit - Extent, (py + .5f) / unit - Extent);
                float d = p.magnitude;
                Color c = Color.clear;
                float outer = look.hasSaucer || look.coaster ? look.saucerRadius : look.body;
                // Soft contact shadow toward the lower right.
                float shadow = 1 - Smooth(outer - .1f, outer + .22f, (p - new Vector2(.08f, -.11f)).magnitude);
                c = Over(c, new Color(0, 0, 0, 1), .42f * shadow);
                if (look.hasSaucer || look.coaster)
                {
                    float cover = 1 - Smooth(look.saucerRadius - aa, look.saucerRadius, d);
                    float shade = Mathf.Clamp01(.5f + .5f * Vector2.Dot(p.normalized, light) * Mathf.Clamp01(d / look.saucerRadius));
                    Color plate = look.saucer * Mathf.Lerp(.86f, 1.04f, shade);
                    if (look.coaster)
                    {
                        // Cork coaster: fine speckle instead of glaze.
                        float speckle = Mathf.PerlinNoise(px * .21f, py * .21f) * .16f - .08f;
                        plate = look.saucer * (1 + speckle);
                    }
                    else
                    {
                        float well = Band(d, look.body + .12f, .05f);
                        plate *= 1 - .07f * well;
                        float line = Band(d, look.saucerRadius * .9f, .016f);
                        plate = Color.Lerp(plate, look.accent, .85f * line);
                    }
                    float edge = Band(d, look.saucerRadius - .02f, .025f);
                    plate = Color.Lerp(plate, plate * .72f, edge);
                    c = Over(c, plate, cover);
                    // The cup body casts a shadow on its plate.
                    float cupShadow = 1 - Smooth(look.body - .04f, look.body + .14f, (p - new Vector2(.05f, -.08f)).magnitude);
                    c = Over(c, new Color(0, 0, 0, 1), .28f * cupShadow * cover);
                }
                // Handle: capsule with a hole, drawn before the body so it tucks under the rim.
                Vector2 local = new Vector2(Vector2.Dot(p, handleDir), Vector2.Dot(p, new Vector2(-handleDir.y, handleDir.x)));
                float handle = Capsule(local, look.body - .08f, look.body + look.handleLength, look.handleWidth);
                float hole = Capsule(local, look.body + .04f, look.body + look.handleLength - look.handleWidth * .9f, look.handleWidth * .42f);
                float handleCover = (1 - Smooth(-aa, 0, handle)) * Smooth(-aa, 0, hole);
                if (handleCover > 0)
                {
                    float side = Mathf.Clamp01(.5f - local.y / (look.handleWidth * 2));
                    Color handleColor = Color.Lerp(look.glaze * .78f, look.glaze * 1.02f, side);
                    c = Over(c, handleColor, handleCover);
                }
                // Cup wall seen from above: glaze outside, rim lip, accent line, then the liquid well.
                float bodyCover = 1 - Smooth(look.body - aa, look.body, d);
                if (bodyCover > 0)
                {
                    float shade = .5f + .5f * Vector2.Dot(d > .001f ? p / d : Vector2.zero, light);
                    Color wall = Color.Lerp(look.rim * .84f, look.rim * 1.03f, shade);
                    if (style == LatteCupStyle.Large) wall = Color.Lerp(wall, look.glaze, Band(d, look.body - .015f, .03f));
                    float accent = Band(d, (1 + look.body) * .5f, .012f);
                    wall = Color.Lerp(wall, look.accent, (style == LatteCupStyle.Large ? .35f : .9f) * accent);
                    wall = Color.Lerp(wall, wall * .7f, Band(d, look.body - .01f, .02f));
                    c = Over(c, wall, bodyCover);
                    float inside = 1 - Smooth(1 - aa, 1, d);
                    // Inner shadow keeps the liquid edge from looking pasted on.
                    Color well = look.interior * Mathf.Lerp(1.2f, .7f, Smooth(.8f, 1f, d));
                    c = Over(c, well, inside);
                }
                pixels[py * CupSize + px] = c;
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, CupSize, CupSize), Vector2.one * .5f, unit);
            sprite.name = texture.name;
            Cups[style] = sprite;
            return sprite;
        }

        /// <summary>Dark walnut tabletop with a warm pool of light where the cup sits.</summary>
        public static Sprite Table(Vector2 lightViewport)
        {
            if (table != null) return table;
            const int w = 960, h = 540;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "LatteTable", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[w * h];
            Color walnut = new Color(.27f, .16f, .10f), dark = new Color(.13f, .075f, .045f);
            const int planks = 5;
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                int plank = Mathf.FloorToInt(v * planks);
                float inPlank = v * planks - plank;
                float tone = .9f + .16f * Mathf.PerlinNoise(plank * 3.7f, .5f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float warp = Mathf.PerlinNoise(u * 2.2f + plank * 11, v * 9f) * 6f;
                    float grain = .5f + .5f * Mathf.Sin((v * planks * 26f + warp) * Mathf.PI);
                    float fine = Mathf.PerlinNoise(u * 60f + plank * 5, v * 8f);
                    Color wood = Color.Lerp(dark, walnut, .55f + .25f * grain + .2f * fine) * tone;
                    float seam = Mathf.Clamp01(1 - Mathf.Min(inPlank, 1 - inPlank) * h / planks / 2.2f);
                    wood = Color.Lerp(wood, dark * .55f, seam * .85f);
                    Vector2 q = new Vector2((u - lightViewport.x) * 16f / 9f, v - lightViewport.y);
                    float pool = Mathf.Exp(-q.sqrMagnitude / .16f);
                    float vignette = Mathf.Lerp(1f, .45f, Mathf.Clamp01((new Vector2((u - .5f) * 1.6f, v - .5f).magnitude - .25f) / .6f));
                    Color lit = wood * (.62f + .75f * pool) * vignette;
                    lit += new Color(.10f, .055f, .02f) * pool;
                    lit.a = 1;
                    pixels[y * w + x] = lit;
                }
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            table = Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.one * .5f, 100);
            table.name = texture.name;
            return table;
        }

        /// <summary>Anti-aliased stroke drawing of a template, cached by size and kind.</summary>
        public static Texture2D Glyph(LatteArtTemplateKind kind, int size, float thickness, Color ink)
        {
            string key = kind + ":" + size + ":" + thickness + ":" + ColorUtility.ToHtmlStringRGBA(ink);
            if (Glyphs.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            var path = LatteArtPatternRecognizer.PreviewPath(kind);
            var segments = new List<Vector4>();
            for (int i = 1; i < path.Length; i++)
            {
                if (float.IsNaN(path[i].x) || float.IsNaN(path[i - 1].x)) continue;
                Vector2 a = (path[i - 1] * .8f + Vector2.one * .1f) * size, b = (path[i] * .8f + Vector2.one * .1f) * size;
                segments.Add(new Vector4(a.x, a.y, b.x, b.y));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LatteGlyph_" + kind, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            float radius = thickness * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                float nearest = float.MaxValue;
                foreach (var s in segments) nearest = Mathf.Min(nearest, SegmentDistance(p, new Vector2(s.x, s.y), new Vector2(s.z, s.w)));
                float alpha = Mathf.Clamp01(radius + .5f - nearest);
                pixels[y * size + x] = new Color(ink.r, ink.g, ink.b, ink.a * alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            Glyphs[key] = texture;
            return texture;
        }

        private static Sprite disk, rounded;

        /// <summary>White anti-aliased disk for round UI badges.</summary>
        public static Sprite Disk()
        {
            if (disk != null) return disk;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LatteDisk", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(size * .5f - d));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            disk = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
            return disk;
        }

        /// <summary>Nine-sliced rounded rectangle for cards and buttons (use Image.Type.Sliced).</summary>
        public static Sprite RoundedRect()
        {
            if (rounded != null) return rounded;
            const int size = 48, radius = 14;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LatteRounded", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius, size - radius), cy = Mathf.Clamp(y + .5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius + .5f - d));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            return rounded;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
        private static float Capsule(Vector2 p, float from, float to, float radius)
        {
            float x = Mathf.Clamp(p.x, from, to);
            return Vector2.Distance(p, new Vector2(x, 0)) - radius;
        }
        private static float Smooth(float edge0, float edge1, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(edge0, edge1, x));
        private static float Band(float d, float center, float halfWidth) => Mathf.Clamp01(1 - Mathf.Abs(d - center) / halfWidth);
        private static Color Over(Color dst, Color src, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (alpha <= 0) return dst;
            float outA = alpha + dst.a * (1 - alpha);
            if (outA <= 0) return Color.clear;
            Color result = (src * alpha + dst * dst.a * (1 - alpha)) / outA;
            result.a = outA;
            return result;
        }
    }
}
