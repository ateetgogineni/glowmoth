using UnityEngine;

namespace Glowmoth
{
    // Visual "prefabs" built from code. Each class owns a small GameObject hierarchy of SpriteRenderers that all
    // share the runtime atlas. Logic lives in LevelController; these classes only hold state and animate.
    //
    // Sorting orders (back to front): background -100, motes -90, drafts -20/-19, rails -15, walls -11/-10,
    // thorns -9/-8, portals -6/-5, goal -4..-2, fireflies 0/1, spiders 2/3, lamp 5..9, moth 18..20, particles 30.

    static class Make
    {
        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public static void Line(SpriteRenderer sr, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 d = b - a;
            Transform t = sr.transform;
            t.localPosition = a + d * 0.5f;
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(d.magnitude, thickness, 1f);
        }
    }

    /// <summary>Base for pooled level objects: one root GameObject toggled on/off.</summary>
    abstract class PooledEntity
    {
        public readonly GameObject Root;
        protected readonly Transform T;

        protected PooledEntity(string name, Transform parent)
        {
            Root = new GameObject(name);
            T = Root.transform;
            T.SetParent(parent, false);
            Root.SetActive(false);
        }

        public void Activate() { Root.SetActive(true); }
        public void Deactivate() { Root.SetActive(false); }
    }

    // ------------------------------------------------------------------------------------------ moth

    sealed class MothView
    {
        readonly Transform root, wingL, wingR;
        readonly SpriteRenderer body, wl, wr, glow;
        float flap, angle;

        public MothView(Transform parent)
        {
            root = new GameObject("Moth").transform;
            root.SetParent(parent, false);
            glow = Make.Sprite("Glow", root, SpriteFactory.Glow, 18);
            glow.transform.localScale = Vector3.one * 1.9f;
            wl = Make.Sprite("WingL", root, SpriteFactory.MothWing, 19);
            wl.flipX = true;
            wingL = wl.transform;
            wingL.localPosition = new Vector3(-0.03f, 0.02f, 0f);
            wr = Make.Sprite("WingR", root, SpriteFactory.MothWing, 19);
            wingR = wr.transform;
            wingR.localPosition = new Vector3(0.03f, 0.02f, 0f);
            body = Make.Sprite("Body", root, SpriteFactory.MothBody, 20);
            body.transform.localScale = Vector3.one * 0.62f;
        }

        public void SetColors(in WorldPalette p)
        {
            body.color = p.moth;
            wl.color = p.mothWing;
            wr.color = p.mothWing;
            glow.color = WorldPalette.WithAlpha(p.mothWing, 0.32f);
        }

        public void Show(bool on) { root.gameObject.SetActive(on); }

        /// <summary>Positions the moth, flaps wings faster when flying fast and tilts toward its heading.</summary>
        public void Pose(Vector2 pos, Vector2 vel, float dt, float scale)
        {
            root.localPosition = pos;
            float speed = vel.magnitude;
            flap += dt * (9f + speed * 3.2f);
            float w = 0.62f * (0.3f + 0.7f * Mathf.Abs(Mathf.Sin(flap)));
            wingL.localScale = new Vector3(w, 0.62f, 1f);
            wingR.localScale = new Vector3(w, 0.62f, 1f);
            float target = speed > 0.25f ? Mathf.Atan2(-vel.x, vel.y) * Mathf.Rad2Deg * 0.6f : 0f;
            angle = Mathf.LerpAngle(angle, target, Mathf.Min(1f, dt * 6f));
            root.localRotation = Quaternion.Euler(0f, 0f, angle);
            root.localScale = new Vector3(scale, scale, 1f);
        }
    }

    // ------------------------------------------------------------------------------------------ lamp

    sealed class LampView
    {
        readonly Transform root;
        readonly SpriteRenderer glow, core, frame, ring, beam;
        Color beamColor;
        float pop, t;
        bool lit;

        public LampView(Transform parent)
        {
            root = new GameObject("Lamp").transform;
            root.SetParent(parent, false);
            glow = Make.Sprite("Glow", root, SpriteFactory.Glow, 5);
            core = Make.Sprite("Flame", root, SpriteFactory.Circle, 7);
            core.transform.localScale = Vector3.one * 0.34f;
            frame = Make.Sprite("Lantern", root, SpriteFactory.Lantern, 8);
            frame.transform.localScale = Vector3.one * 0.95f;
            ring = Make.Sprite("Fuel", root, SpriteFactory.Ring, 9);
            beam = Make.Sprite("Beam", parent, SpriteFactory.Pixel, 6);
            Hide();
        }

        public void SetColors(in WorldPalette p)
        {
            glow.color = WorldPalette.WithAlpha(p.lamp, 0.32f);
            core.color = p.lamp;
            frame.color = p.wallDark;
            ring.color = WorldPalette.WithAlpha(p.lamp, 0.6f);
            beamColor = WorldPalette.WithAlpha(p.lamp, 0.22f);
        }

        public void Place(Vector2 p)
        {
            root.localPosition = p;
            root.gameObject.SetActive(true);
            lit = true;
            pop = 0f;
        }

        /// <summary>Moves without the placement pop (used by the menu demo).</summary>
        public void Move(Vector2 p) { root.localPosition = p; }

        public void Extinguish() { lit = false; }

        public void Hide()
        {
            lit = false;
            pop = 0f;
            root.gameObject.SetActive(false);
            beam.enabled = false;
        }

        public void Animate(float dt, float fuel01, bool showFuel, bool beamOn, Vector2 mothPos)
        {
            if (!root.gameObject.activeSelf) return;
            t += dt;

            // Pop in with overshoot when placed; shrink away when extinguished.
            pop = lit ? Mathf.Min(1f, pop + dt * 5f) : Mathf.Max(0f, pop - dt * 3f);
            if (!lit && pop <= 0f) { Hide(); return; }
            float s = lit ? UIKit.EaseOutBack(pop) : pop;
            root.localScale = new Vector3(s, s, 1f);

            float flicker = 1f + 0.06f * Mathf.Sin(t * 9f) + 0.04f * Mathf.Sin(t * 23f);
            glow.transform.localScale = Vector3.one * (3.4f * flicker);

            ring.enabled = showFuel && lit;
            if (ring.enabled) ring.transform.localScale = Vector3.one * (0.6f + 0.9f * fuel01);

            beam.enabled = beamOn && lit;
            if (beam.enabled)
            {
                Make.Line(beam, root.localPosition, mothPos, 0.06f);
                Color c = beamColor;
                c.a *= 0.6f + 0.4f * Mathf.Sin(t * 6f);
                beam.color = c;
            }
        }
    }

    // ------------------------------------------------------------------------------------------ goal

    sealed class GoalView
    {
        readonly Transform root, flowerT;
        readonly SpriteRenderer glow, flower, core;

        public GoalView(Transform parent)
        {
            root = new GameObject("Goal").transform;
            root.SetParent(parent, false);
            glow = Make.Sprite("Glow", root, SpriteFactory.Glow, -4);
            flower = Make.Sprite("Flower", root, SpriteFactory.Flower, -3);
            flowerT = flower.transform;
            core = Make.Sprite("Core", root, SpriteFactory.Circle, -2);
            core.transform.localScale = Vector3.one * 0.3f;
            Show(false);
        }

        public void Setup(Vector2 pos, in WorldPalette p)
        {
            root.localPosition = pos;
            glow.color = WorldPalette.WithAlpha(p.goal, 0.35f);
            flower.color = p.goal;
            core.color = p.lamp;
            Show(true);
        }

        public void Show(bool on) { root.gameObject.SetActive(on); }

        public void Tick(float t)
        {
            if (!root.gameObject.activeSelf) return;
            flowerT.localRotation = Quaternion.Euler(0f, 0f, t * 25f);
            flowerT.localScale = Vector3.one * (1.15f * (1f + 0.06f * Mathf.Sin(t * 3f)));
            glow.transform.localScale = Vector3.one * (2.8f * (1f + 0.08f * Mathf.Sin(t * 2f)));
        }
    }

    // ------------------------------------------------------------------------------------------ walls

    sealed class WallEntity : PooledEntity
    {
        readonly Transform mover;
        readonly SpriteRenderer body, shadow, rail;
        Vector2 basePos, move;
        float period, phase;

        public Vector2 Center;
        public Vector2 Half;
        public bool Moving { get; private set; }

        public WallEntity(Transform parent) : base("Wall", parent)
        {
            mover = new GameObject("Mover").transform;
            mover.SetParent(T, false);
            shadow = Make.Sprite("Shadow", mover, SpriteFactory.Panel, -11);
            shadow.drawMode = SpriteDrawMode.Sliced;
            shadow.transform.localPosition = new Vector3(0.06f, -0.08f, 0f);
            body = Make.Sprite("Body", mover, SpriteFactory.Panel, -10);
            body.drawMode = SpriteDrawMode.Sliced;
            rail = Make.Sprite("Rail", T, SpriteFactory.Pixel, -15);
        }

        public void Setup(in WallDef d, in WorldPalette p, bool isBorder)
        {
            basePos = d.Center;
            Center = d.Center;
            Half = d.Size * 0.5f;
            move = d.Move;
            period = Mathf.Max(0.1f, d.Period);
            phase = d.Phase;
            Moving = move.sqrMagnitude > 0f;

            T.localPosition = basePos;
            mover.localPosition = Vector3.zero;
            body.size = d.Size;
            shadow.size = d.Size;
            body.color = isBorder ? p.wallDark : p.wall;
            shadow.color = WorldPalette.WithAlpha(p.wallDark, 0.55f);
            shadow.enabled = !isBorder;

            rail.enabled = Moving;
            if (Moving)
            {
                Make.Line(rail, Vector2.zero, move, 0.08f);
                rail.color = WorldPalette.WithAlpha(p.wall, 0.35f);
            }
        }

        /// <summary>Sliding walls ease back and forth: position = base + move * (1 - cos) / 2.</summary>
        public void Tick(float time)
        {
            if (!Moving) return;
            float s = 0.5f - 0.5f * Mathf.Cos((time / period + phase) * Mathf.PI * 2f);
            Vector2 offset = move * s;
            mover.localPosition = offset;
            Center = basePos + offset;
        }
    }

    // ------------------------------------------------------------------------------------------ thorns

    sealed class ThornEntity : PooledEntity
    {
        readonly SpriteRenderer bed, spikes;
        public Vector2 Center;
        public Vector2 Half;

        public ThornEntity(Transform parent) : base("Thorns", parent)
        {
            bed = Make.Sprite("Bed", T, SpriteFactory.Panel, -9);
            bed.drawMode = SpriteDrawMode.Sliced;
            spikes = Make.Sprite("Spikes", T, SpriteFactory.Thorn, -8);
            spikes.drawMode = SpriteDrawMode.Tiled;
            spikes.tileMode = SpriteTileMode.Continuous;
        }

        public void Setup(in RectDef d, in WorldPalette p)
        {
            Center = d.Center;
            Half = d.Size * 0.5f;
            T.localPosition = d.Center;
            bed.size = d.Size;
            bed.color = WorldPalette.WithAlpha(p.hazard * 0.55f, 0.9f);

            // Tile square spikes along the long side. Horizontal strips point up; vertical strips are rotated
            // so the spikes face the middle of the playfield (away from the wall they sit on).
            bool horizontal = d.Size.x >= d.Size.y;
            float thick = horizontal ? d.Size.y : d.Size.x;
            float length = horizontal ? d.Size.x : d.Size.y;
            float angle = horizontal ? 0f : (d.Center.x < 0f ? -90f : 90f);
            spikes.transform.localScale = new Vector3(thick, thick, 1f);
            spikes.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            spikes.size = new Vector2(length / thick, 1f);
            spikes.color = p.hazard;
        }
    }

    // ------------------------------------------------------------------------------------------ spiders

    sealed class SpiderEntity : PooledEntity
    {
        readonly SpriteRenderer body, silk;
        readonly Transform bodyT;
        Vector2 a, b;
        float speed, length;
        public Vector2 Position;

        public SpiderEntity(Transform parent) : base("Spider", parent)
        {
            silk = Make.Sprite("Silk", T, SpriteFactory.Pixel, 2);
            body = Make.Sprite("Body", T, SpriteFactory.Spider, 3);
            bodyT = body.transform;
            bodyT.localScale = Vector3.one * 0.95f;
        }

        public void Setup(in SpiderDef d, in WorldPalette p)
        {
            a = d.A;
            b = d.B;
            speed = d.Speed;
            length = Mathf.Max(0.01f, (b - a).magnitude);
            T.localPosition = Vector3.zero;
            Make.Line(silk, a, b, 0.035f);
            silk.color = WorldPalette.WithAlpha(p.text, 0.22f);
            body.color = p.hazard;
            Position = a;
            bodyT.localPosition = a;
        }

        /// <summary>Patrols A to B and back with eased turns; legs wiggle by rocking the body.</summary>
        public void Tick(float time)
        {
            float u = Mathf.PingPong(time * speed / length, 1f);
            u = u * u * (3f - 2f * u);
            Position = Vector2.Lerp(a, b, u);
            bodyT.localPosition = Position;
            bodyT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 14f) * 7f);
        }
    }

    // ------------------------------------------------------------------------------------------ drafts

    sealed class GustEntity : PooledEntity
    {
        const int StreakCount = 8;
        readonly SpriteRenderer area;
        readonly SpriteRenderer[] streaks = new SpriteRenderer[StreakCount];
        readonly Transform[] streakT = new Transform[StreakCount];
        readonly Vector2[] offsets = new Vector2[StreakCount];
        Vector2 dir;
        float speed;
        Color streakColor;

        public Vector2 Center;
        public Vector2 Half;
        public Vector2 Force;

        public GustEntity(Transform parent) : base("Draft", parent)
        {
            area = Make.Sprite("Area", T, SpriteFactory.Panel, -20);
            area.drawMode = SpriteDrawMode.Sliced;
            for (int i = 0; i < StreakCount; i++)
            {
                streaks[i] = Make.Sprite("Streak", T, SpriteFactory.Streak, -19);
                streakT[i] = streaks[i].transform;
            }
        }

        public void Setup(in GustDef d, in WorldPalette p)
        {
            Center = d.Center;
            Half = d.Size * 0.5f;
            Force = d.Force;
            T.localPosition = d.Center;
            area.size = d.Size;
            area.color = WorldPalette.WithAlpha(p.mote, 0.12f);

            dir = Force.sqrMagnitude > 0f ? Force.normalized : Vector2.right;
            speed = Force.magnitude * 0.9f + 0.5f;
            streakColor = p.mote;
            Quaternion rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            for (int i = 0; i < StreakCount; i++)
            {
                offsets[i] = new Vector2(Random.Range(-Half.x, Half.x), Random.Range(-Half.y, Half.y));
                streakT[i].localRotation = rot;
                streakT[i].localScale = new Vector3(1.1f, 0.22f, 1f);
            }
        }

        /// <summary>Streaks flow with the wind and wrap around inside the draft area.</summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < StreakCount; i++)
            {
                Vector2 o = offsets[i] + dir * (speed * dt);
                if (o.x > Half.x) o.x -= Half.x * 2f; else if (o.x < -Half.x) o.x += Half.x * 2f;
                if (o.y > Half.y) o.y -= Half.y * 2f; else if (o.y < -Half.y) o.y += Half.y * 2f;
                offsets[i] = o;
                streakT[i].localPosition = o;
                float edge = Mathf.Min(Half.x - Mathf.Abs(o.x), Half.y - Mathf.Abs(o.y));
                Color c = streakColor;
                c.a = 0.55f * Mathf.Clamp01(edge / 0.5f);
                streaks[i].color = c;
            }
        }
    }

    // ------------------------------------------------------------------------------------------ portals

    sealed class PortalEntity : PooledEntity
    {
        readonly SpriteRenderer glowA, glowB, swirlA, swirlB;
        float spin, flash;
        public Vector2 A, B;

        public PortalEntity(Transform parent) : base("Portal", parent)
        {
            glowA = Make.Sprite("GlowA", T, SpriteFactory.Glow, -6);
            swirlA = Make.Sprite("SwirlA", T, SpriteFactory.Portal, -5);
            glowB = Make.Sprite("GlowB", T, SpriteFactory.Glow, -6);
            swirlB = Make.Sprite("SwirlB", T, SpriteFactory.Portal, -5);
        }

        public void Setup(in PortalDef d, Color color)
        {
            A = d.A;
            B = d.B;
            T.localPosition = Vector3.zero;
            glowA.transform.localPosition = A;
            swirlA.transform.localPosition = A;
            glowB.transform.localPosition = B;
            swirlB.transform.localPosition = B;
            Color g = WorldPalette.WithAlpha(color, 0.4f);
            glowA.color = g;
            glowB.color = g;
            swirlA.color = color;
            swirlB.color = color;
            flash = 0f;
        }

        public void Flash() { flash = 1f; }

        public void Tick(float dt)
        {
            spin += dt * 140f;
            flash = Mathf.Max(0f, flash - dt * 2.5f);
            float s = 1.25f * (1f + 0.25f * flash);
            swirlA.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
            swirlB.transform.localRotation = Quaternion.Euler(0f, 0f, -spin);
            swirlA.transform.localScale = Vector3.one * s;
            swirlB.transform.localScale = Vector3.one * s;
            Vector3 gs = Vector3.one * (1.9f + 0.6f * flash);
            glowA.transform.localScale = gs;
            glowB.transform.localScale = gs;
        }
    }

    // ------------------------------------------------------------------------------------------ fireflies

    sealed class FireflyEntity : PooledEntity
    {
        readonly SpriteRenderer glow, core;
        Vector2 home;
        float seed;
        Color glowColor;

        public Vector2 Position;
        public bool Active;

        public FireflyEntity(Transform parent) : base("Firefly", parent)
        {
            glow = Make.Sprite("Glow", T, SpriteFactory.Glow, 0);
            core = Make.Sprite("Core", T, SpriteFactory.Circle, 1);
            core.transform.localScale = Vector3.one * 0.22f;
        }

        public void Setup(Vector2 pos, in WorldPalette p, int index)
        {
            home = pos;
            Position = pos;
            seed = index * 1.7f;
            Active = true;
            glowColor = WorldPalette.WithAlpha(p.firefly, 0.5f);
            core.color = p.firefly;
            T.localPosition = pos;
        }

        public void Collect()
        {
            Active = false;
            Deactivate();
        }

        public void Tick(float t)
        {
            if (!Active) return;
            Position = home + new Vector2(Mathf.Sin(t * 1.3f + seed) * 0.12f, Mathf.Sin(t * 2.1f + seed) * 0.15f);
            T.localPosition = Position;
            float twinkle = 0.75f + 0.25f * Mathf.Sin(t * 7f + seed);
            Color c = glowColor;
            c.a *= twinkle;
            glow.color = c;
            glow.transform.localScale = Vector3.one * (1.1f + 0.2f * twinkle);
        }
    }
}
