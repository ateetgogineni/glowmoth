using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// Generates ALL game art at startup by rasterising signed-distance shapes into ONE 512x512 texture
    /// (a runtime sprite atlas). Every world sprite and UI image shares this texture and the default sprite
    /// material, so the game batches into a handful of draw calls and ships with zero image files.
    ///
    /// Shapes are drawn white with anti-aliased alpha; colour comes from the world palette through
    /// SpriteRenderer.color / Image.color, which keeps the flat-vector style consistent everywhere.
    /// </summary>
    public static class SpriteFactory
    {
        public const int AtlasSize = 512;
        const int Cell = 64;
        const int Cols = AtlasSize / Cell;

        public static Texture2D Atlas { get; private set; }

        public static Sprite Pixel, Circle, Glow, Ring, Panel, MothBody, MothWing, Star, StarOutline,
                             Thorn, Spider, Flower, Lantern, Portal, Streak,
                             IconPause, IconPlay, IconRestart, IconHome, IconLock, IconSoundOn,
                             IconSoundOff, IconMusic, IconGrid, IconArrow;

        delegate float Sdf(float x, float y);      // signed distance, normalised cell space [-1, 1]
        delegate float AlphaFn(float x, float y);  // direct alpha 0..1 for soft shapes

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static Color32[] pixels;
        static int nextCell;

        /// <summary>Builds the atlas once. Safe to call repeatedly.</summary>
        public static void Build()
        {
            if (Atlas == null) Generate(false);
        }

        /// <summary>Editor helper: regenerates a readable atlas and returns it as PNG bytes.</summary>
        public static byte[] ExportAtlasPng()
        {
            Generate(true);
            return Atlas.EncodeToPNG();
        }

        static void Generate(bool readable)
        {
            pixels = new Color32[AtlasSize * AtlasSize];
            var clear = new Color32(255, 255, 255, 0); // white RGB everywhere avoids dark fringes when filtering
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            nextCell = 0;

            Atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
            {
                name = "GlowmothAtlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // --- Basic shapes ---
            Pixel = SolidPixel();
            Circle = Shape((x, y) => Len(x, y) - 0.9f);
            Glow = Soft((x, y) =>
            {
                float r = Len(x, y) / 0.96f;
                if (r >= 1f) return 0f;
                float k = 1f - r;
                return k * k;
            });
            Ring = Shape((x, y) => Mathf.Abs(Len(x, y) - 0.8f) - 0.09f);
            Panel = Shape((x, y) => RoundBox(x, y, 0.94f, 0.94f, 0.42f), Center, new Vector4(16, 16, 16, 16));
            Streak = Soft((x, y) =>
            {
                float along = 1f - Mathf.Abs(x);
                if (along <= 0f) return 0f;
                return along * along * Mathf.Exp(-(y * y) / 0.03f);
            });

            // --- Characters and level objects ---
            MothBody = Shape(MothBodySdf);
            MothWing = Shape(MothWingSdf, new Vector2(0.08f, 0.5f)); // pivot at the wing root
            Star = Shape(StarSdf);
            StarOutline = Shape((x, y) => Mathf.Abs(StarSdf(x * 1.12f, y * 1.12f) / 1.12f) - 0.06f);
            Thorn = Shape(ThornSdf);
            Spider = Shape(SpiderSdf);
            Flower = Shape(FlowerSdf);
            Lantern = Shape(LanternSdf);
            Portal = Soft(PortalAlpha);

            // --- UI icons ---
            IconPause = Shape((x, y) => Mathf.Min(RoundBox(x - 0.3f, y, 0.15f, 0.6f, 0.06f), RoundBox(x + 0.3f, y, 0.15f, 0.6f, 0.06f)));
            IconPlay = Shape((x, y) => Polygon(x, y, PlayPts) - 0.06f);
            IconRestart = Shape((x, y) => Mathf.Min(Arc(x, y, 0.52f, 0.11f, 0.8f, -0.35f), Polygon(x, y, RestartHead)));
            IconHome = Shape(HomeSdf);
            IconLock = Shape(LockSdf);
            IconSoundOn = Shape((x, y) => Mathf.Min(Polygon(x + 0.2f, y, SpeakerPts),
                Mathf.Min(Arc(x + 0.14f, y, 0.42f, 0.065f, -0.8f, 0.8f), Arc(x + 0.14f, y, 0.72f, 0.065f, -0.75f, 0.75f))));
            IconSoundOff = Shape((x, y) => Mathf.Min(Polygon(x + 0.24f, y, SpeakerPts),
                Mathf.Min(Capsule(x, y, 0.3f, -0.28f, 0.8f, 0.28f, 0.075f), Capsule(x, y, 0.3f, 0.28f, 0.8f, -0.28f, 0.075f))));
            IconMusic = Shape((x, y) => Mathf.Min(Ellipse(x, y, -0.24f, -0.5f, 0.27f, 0.21f, -20f),
                Mathf.Min(Box(x, y - 0.08f, 0.055f, 0.6f), Capsule(x, y, 0f, 0.5f, 0.42f, 0.22f, 0.075f))));
            IconGrid = Shape((x, y) =>
            {
                float ax = Mathf.Abs(x) - 0.36f, ay = Mathf.Abs(y) - 0.36f;
                return RoundBox(ax, ay, 0.27f, 0.27f, 0.08f);
            });
            IconArrow = Shape((x, y) => Polygon(x, y, ArrowPts) - 0.03f);

            Atlas.SetPixels32(pixels);
            Atlas.Apply(false, !readable); // non-readable frees the CPU copy at runtime
            pixels = null;
        }

        // ------------------------------------------------------------------ rasterising

        static RectInt NextCell()
        {
            int col = nextCell % Cols, row = nextCell / Cols;
            nextCell++;
            return new RectInt(col * Cell, row * Cell, Cell, Cell);
        }

        static Sprite Shape(Sdf sdf)
        {
            return Shape(sdf, Center, Vector4.zero);
        }

        static Sprite Shape(Sdf sdf, Vector2 pivot)
        {
            return Shape(sdf, pivot, Vector4.zero);
        }

        static Sprite Shape(Sdf sdf, Vector2 pivot, Vector4 border)
        {
            RectInt r = NextCell();
            float aa = 2.4f / Cell; // ~1.2px soft edge
            for (int y = 0; y < Cell; y++)
            {
                float ny = (y + 0.5f) / Cell * 2f - 1f;
                for (int x = 0; x < Cell; x++)
                {
                    float nx = (x + 0.5f) / Cell * 2f - 1f;
                    float a = Mathf.Clamp01(0.5f - sdf(nx, ny) / aa);
                    Write(r.x + x, r.y + y, a);
                }
            }
            return CreateSprite(r, pivot, border);
        }

        static Sprite Soft(AlphaFn alpha)
        {
            RectInt r = NextCell();
            for (int y = 0; y < Cell; y++)
            {
                float ny = (y + 0.5f) / Cell * 2f - 1f;
                for (int x = 0; x < Cell; x++)
                {
                    float nx = (x + 0.5f) / Cell * 2f - 1f;
                    Write(r.x + x, r.y + y, Mathf.Clamp01(alpha(nx, ny)));
                }
            }
            return CreateSprite(r, Center, Vector4.zero);
        }

        static Sprite SolidPixel()
        {
            RectInt r = NextCell();
            for (int y = 16; y < 48; y++)
                for (int x = 16; x < 48; x++)
                    Write(r.x + x, r.y + y, 1f);
            // Sample only the inner 16x16 so bilinear filtering never reaches the transparent edge.
            return Sprite.Create(Atlas, new Rect(r.x + 24, r.y + 24, 16, 16), Center, 16f, 0, SpriteMeshType.FullRect);
        }

        static void Write(int x, int y, float alpha)
        {
            if (alpha <= 0f) return;
            int idx = y * AtlasSize + x;
            byte b = (byte)(alpha * 255f + 0.5f);
            if (b > pixels[idx].a) pixels[idx].a = b;
        }

        static Sprite CreateSprite(RectInt r, Vector2 pivot, Vector4 border)
        {
            // pixelsPerUnit = cell size, so every sprite is exactly 1x1 world unit before scaling.
            // FullRect meshes are required for Sliced/Tiled draw modes and are cheapest for small quads.
            return Sprite.Create(Atlas, new Rect(r.x, r.y, r.width, r.height), pivot, Cell, 0, SpriteMeshType.FullRect, border);
        }

        // ------------------------------------------------------------------ SDF primitives

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float Box(float x, float y, float hx, float hy)
        {
            float dx = Mathf.Abs(x) - hx, dy = Mathf.Abs(y) - hy;
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
        }

        static float RoundBox(float x, float y, float hx, float hy, float r) => Box(x, y, hx - r, hy - r) - r;

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry, float rotDeg)
        {
            float a = rotDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            float px = x - cx, py = y - cy;
            float lx = c * px + s * py;
            float ly = -s * px + c * py;
            return (Len(lx / rx, ly / ry) - 1f) * Mathf.Min(rx, ry); // approximate, fine for art
        }

        /// <summary>Arc of radius R and half-thickness w running counter-clockwise from angle a0 to a1 (radians).</summary>
        static float Arc(float x, float y, float R, float w, float a0, float a1)
        {
            float ang = Mathf.Atan2(y, x);
            float span = Mathf.Repeat(a1 - a0, Mathf.PI * 2f);
            if (Mathf.Repeat(ang - a0, Mathf.PI * 2f) <= span) return Mathf.Abs(Len(x, y) - R) - w;
            float d0 = Len(x - Mathf.Cos(a0) * R, y - Mathf.Sin(a0) * R);
            float d1 = Len(x - Mathf.Cos(a1) * R, y - Mathf.Sin(a1) * R);
            return Mathf.Min(d0, d1) - w;
        }

        /// <summary>Exact signed distance to a polygon (Inigo Quilez).</summary>
        static float Polygon(float x, float y, Vector2[] v)
        {
            var p = new Vector2(x, y);
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        // ------------------------------------------------------------------ shape definitions

        static readonly Vector2[] StarPts = MakeStar(0.95f, 0.42f);
        static readonly Vector2[] PlayPts = { new Vector2(-0.42f, -0.62f), new Vector2(0.66f, 0f), new Vector2(-0.42f, 0.62f) };
        static readonly Vector2[] RestartHead = { new Vector2(0.24f, -0.1f), new Vector2(0.84f, -0.1f), new Vector2(0.54f, -0.5f) };
        static readonly Vector2[] ThornPts = { new Vector2(-0.96f, -0.62f), new Vector2(0.96f, -0.62f), new Vector2(0f, 0.94f) };
        static readonly Vector2[] RoofPts = { new Vector2(-0.8f, 0.02f), new Vector2(0f, 0.76f), new Vector2(0.8f, 0.02f) };
        static readonly Vector2[] SpeakerPts =
        {
            new Vector2(-0.72f, -0.22f), new Vector2(-0.38f, -0.22f), new Vector2(0.02f, -0.58f),
            new Vector2(0.02f, 0.58f), new Vector2(-0.38f, 0.22f), new Vector2(-0.72f, 0.22f)
        };
        static readonly Vector2[] ArrowPts =
        {
            new Vector2(-0.62f, -0.17f), new Vector2(0.08f, -0.17f), new Vector2(0.08f, -0.5f), new Vector2(0.66f, 0f),
            new Vector2(0.08f, 0.5f), new Vector2(0.08f, 0.17f), new Vector2(-0.62f, 0.17f)
        };

        // Spider legs (right side; mirrored with |x|): root -> knee -> foot
        static readonly Vector2[] LegRoot = { new Vector2(0.22f, 0.10f), new Vector2(0.25f, -0.05f), new Vector2(0.25f, -0.20f), new Vector2(0.22f, -0.32f) };
        static readonly Vector2[] LegKnee = { new Vector2(0.60f, 0.45f), new Vector2(0.70f, 0.15f), new Vector2(0.70f, -0.20f), new Vector2(0.58f, -0.50f) };
        static readonly Vector2[] LegFoot = { new Vector2(0.80f, 0.70f), new Vector2(0.93f, 0.05f), new Vector2(0.93f, -0.40f), new Vector2(0.78f, -0.85f) };

        static Vector2[] MakeStar(float outer, float inner)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad;
                float r = (i % 2 == 0) ? outer : inner;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.04f);
            }
            return pts;
        }

        static float StarSdf(float x, float y) => Polygon(x, y, StarPts) - 0.02f;

        static float MothBodySdf(float x, float y)
        {
            float body = Ellipse(x, y, 0f, -0.18f, 0.24f, 0.62f, 0f);
            float head = Len(x, y - 0.56f) - 0.22f;
            float antenna = Capsule(Mathf.Abs(x), y, 0.07f, 0.7f, 0.32f, 0.93f, 0.045f);
            return Mathf.Min(Mathf.Min(body, head), antenna);
        }

        static float MothWingSdf(float x, float y)
        {
            float upper = Ellipse(x, y, 0.02f, 0.22f, 0.84f, 0.54f, 22f);
            float lower = Ellipse(x, y, -0.16f, -0.42f, 0.6f, 0.4f, -28f);
            float spot = Len(x - 0.3f, y - 0.3f) - 0.14f; // "eye spot" cut-out
            return Mathf.Max(Mathf.Min(upper, lower), -spot);
        }

        static float ThornSdf(float x, float y)
        {
            return Mathf.Min(Polygon(x, y, ThornPts), Box(x, y + 0.8f, 1f, 0.18f));
        }

        static float SpiderSdf(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float d = Mathf.Min(Len(x, y + 0.12f) - 0.36f, Len(x, y - 0.36f) - 0.22f);
            for (int i = 0; i < 4; i++)
            {
                d = Mathf.Min(d, Capsule(ax, y, LegRoot[i].x, LegRoot[i].y, LegKnee[i].x, LegKnee[i].y, 0.05f));
                d = Mathf.Min(d, Capsule(ax, y, LegKnee[i].x, LegKnee[i].y, LegFoot[i].x, LegFoot[i].y, 0.045f));
            }
            float eyes = Len(ax - 0.08f, y - 0.42f) - 0.055f;
            return Mathf.Max(d, -eyes);
        }

        static float FlowerSdf(float x, float y)
        {
            float d = Len(x, y) - 0.3f;
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                d = Mathf.Min(d, Len(x - Mathf.Cos(a) * 0.52f, y - Mathf.Sin(a) * 0.52f) - 0.34f);
            }
            return Mathf.Max(d, -(Len(x, y) - 0.13f));
        }

        static float LanternSdf(float x, float y)
        {
            float frame = Mathf.Max(RoundBox(x, y + 0.04f, 0.44f, 0.58f, 0.16f), -RoundBox(x, y + 0.04f, 0.29f, 0.42f, 0.1f));
            float cap = RoundBox(x, y - 0.66f, 0.3f, 0.08f, 0.04f);
            float baseBar = RoundBox(x, y + 0.7f, 0.34f, 0.08f, 0.04f);
            float handle = Mathf.Max(Mathf.Abs(Len(x, y - 0.74f) - 0.16f) - 0.045f, 0.74f - y);
            float bar = Box(x, y + 0.04f, 0.025f, 0.42f);
            return Mathf.Min(Mathf.Min(Mathf.Min(frame, cap), Mathf.Min(baseBar, handle)), bar);
        }

        static float PortalAlpha(float x, float y)
        {
            float r = Len(x, y);
            if (r >= 0.97f) return 0f;
            float ang = Mathf.Atan2(y, x);
            float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.66f) / 0.3f);
            float swirl = 0.5f + 0.5f * Mathf.Sin(ang * 3f + r * 10f);
            float inner = Mathf.Clamp01(1f - r / 0.66f) * 0.35f;
            return Mathf.Clamp01(ring * (0.35f + 0.65f * swirl) + inner) * Mathf.Clamp01((0.97f - r) * 20f);
        }

        static float HomeSdf(float x, float y)
        {
            float house = Mathf.Min(Polygon(x, y, RoofPts), RoundBox(x, y + 0.36f, 0.52f, 0.4f, 0.06f));
            return Mathf.Max(house, -RoundBox(x, y + 0.5f, 0.15f, 0.26f, 0.05f));
        }

        static float LockSdf(float x, float y)
        {
            float shackle = Mathf.Min(Arc(x, y - 0.12f, 0.3f, 0.09f, 0f, Mathf.PI), Box(Mathf.Abs(x) - 0.3f, y - 0.02f, 0.09f, 0.12f));
            float body = RoundBox(x, y + 0.3f, 0.52f, 0.38f, 0.1f);
            float keyhole = Mathf.Min(Len(x, y + 0.24f) - 0.1f, Box(x, y + 0.42f, 0.04f, 0.12f));
            return Mathf.Min(shackle, Mathf.Max(body, -keyhole));
        }
    }
}
