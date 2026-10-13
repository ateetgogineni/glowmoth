using System.Collections.Generic;
using UnityEngine;

namespace Glowmoth
{
    /// <summary>A wall rectangle. If Move is non-zero it slides from Center to Center + Move and back.</summary>
    public readonly struct WallDef
    {
        public readonly Vector2 Center, Size, Move;
        public readonly float Period, Phase;

        public WallDef(Vector2 center, Vector2 size) : this(center, size, Vector2.zero, 1f, 0f) { }

        public WallDef(Vector2 center, Vector2 size, Vector2 move, float period, float phase)
        {
            Center = center;
            Size = size;
            Move = move;
            Period = period;
            Phase = phase;
        }
    }

    public readonly struct RectDef
    {
        public readonly Vector2 Center, Size;
        public RectDef(Vector2 center, Vector2 size) { Center = center; Size = size; }
    }

    public readonly struct SpiderDef
    {
        public readonly Vector2 A, B;
        public readonly float Speed;
        public SpiderDef(Vector2 a, Vector2 b, float speed) { A = a; B = b; Speed = speed; }
    }

    public readonly struct GustDef
    {
        public readonly Vector2 Center, Size, Force;
        public GustDef(Vector2 center, Vector2 size, Vector2 force) { Center = center; Size = size; Force = force; }
    }

    public readonly struct PortalDef
    {
        public readonly Vector2 A, B;
        public PortalDef(Vector2 a, Vector2 b) { A = a; B = b; }
    }

    /// <summary>
    /// Everything needed to build one level. Built with a small fluent API in LevelDatabase so levels read
    /// like a description:  new LevelData("Name", par).Moth(0,-5).Home(0,5).Wall(...)
    /// </summary>
    public sealed class LevelData
    {
        public readonly string Name;
        public readonly int Par;           // lamps placed for the "efficient" star
        public string Hint = "";
        public float LampLife;             // seconds a lamp burns (0 = forever)
        public Vector2 MothStart, Goal;

        public readonly List<WallDef> Walls = new List<WallDef>();
        public readonly List<RectDef> Thorns = new List<RectDef>();
        public readonly List<SpiderDef> Spiders = new List<SpiderDef>();
        public readonly List<GustDef> Gusts = new List<GustDef>();
        public readonly List<PortalDef> Portals = new List<PortalDef>();
        public readonly List<Vector2> Fireflies = new List<Vector2>();

        public LevelData(string name, int par)
        {
            Name = name;
            Par = par;
        }

        public LevelData Moth(float x, float y) { MothStart = new Vector2(x, y); return this; }
        public LevelData Home(float x, float y) { Goal = new Vector2(x, y); return this; }
        public LevelData Tip(string text) { Hint = text; return this; }
        public LevelData Candles(float seconds) { LampLife = seconds; return this; }

        public LevelData Wall(float cx, float cy, float w, float h)
        {
            Walls.Add(new WallDef(new Vector2(cx, cy), new Vector2(w, h)));
            return this;
        }

        /// <summary>A sliding wall: starts at (cx, cy) and travels by (dx, dy) and back every `period` seconds.</summary>
        public LevelData Slider(float cx, float cy, float w, float h, float dx, float dy, float period, float phase = 0f)
        {
            Walls.Add(new WallDef(new Vector2(cx, cy), new Vector2(w, h), new Vector2(dx, dy), period, phase));
            return this;
        }

        public LevelData Thorn(float cx, float cy, float w, float h)
        {
            Thorns.Add(new RectDef(new Vector2(cx, cy), new Vector2(w, h)));
            return this;
        }

        public LevelData Spider(float ax, float ay, float bx, float by, float speed)
        {
            Spiders.Add(new SpiderDef(new Vector2(ax, ay), new Vector2(bx, by), speed));
            return this;
        }

        public LevelData Gust(float cx, float cy, float w, float h, float fx, float fy)
        {
            Gusts.Add(new GustDef(new Vector2(cx, cy), new Vector2(w, h), new Vector2(fx, fy)));
            return this;
        }

        public LevelData Portal(float ax, float ay, float bx, float by)
        {
            Portals.Add(new PortalDef(new Vector2(ax, ay), new Vector2(bx, by)));
            return this;
        }

        public LevelData Fly(float x, float y)
        {
            Fireflies.Add(new Vector2(x, y));
            return this;
        }
    }
}
