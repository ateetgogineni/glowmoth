using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Glowmoth
{
    /// <summary>
    /// Builds a level from LevelData using pooled entities and runs the whole simulation in a single Update:
    /// input -> lamp -> moth steering -> walls -> hazards/pickups/goal.
    ///
    /// Core rule: the moth steers toward the lamp ONLY while it has a clear line of sight to it.
    /// Walls block both movement and light; drafts push; thorns and spiders end the run; portals teleport.
    /// No Rigidbody/Collider components are used; all collision is simple circle/rectangle math.
    /// </summary>
    public sealed class LevelController : MonoBehaviour
    {
        public event System.Action<int> Won;        // stars earned (1-3)
        public event System.Action<string> Lost;    // reason shown to the player
        public event System.Action StatsChanged;    // lamps used / fireflies changed (HUD refresh)

        public int Taps { get; private set; }
        public int FirefliesCollected { get; private set; }
        public int FireflyTotal { get; private set; }
        public int Par => data != null ? data.Par : 0;

        enum Phase { Idle, Demo, Playing, Winning, Dying, Done }

        Camera cam;
        Phase phase = Phase.Idle;
        LevelData data;
        WorldPalette pal;
        float time, phaseTimer, trailTimer, winScale = 1f;
        string deathReason;

        // Moth simulation state
        Vector2 mothPos, mothVel, winFrom;
        bool mothSeesLamp, portalArmed;

        // Lamp state
        bool lampOn;
        Vector2 lampPos;
        float lampTimeLeft;

        // Views
        MothView moth;
        LampView lamp;
        GoalView goal;

        // Pools + active lists (lists are pre-sized; they never grow during play)
        Pool<WallEntity> wallPool;
        Pool<ThornEntity> thornPool;
        Pool<SpiderEntity> spiderPool;
        Pool<GustEntity> gustPool;
        Pool<PortalEntity> portalPool;
        Pool<FireflyEntity> fireflyPool;
        readonly List<WallEntity> walls = new List<WallEntity>(24);
        readonly List<ThornEntity> thorns = new List<ThornEntity>(8);
        readonly List<SpiderEntity> spiders = new List<SpiderEntity>(6);
        readonly List<GustEntity> gusts = new List<GustEntity>(4);
        readonly List<PortalEntity> portals = new List<PortalEntity>(4);
        readonly List<FireflyEntity> fireflies = new List<FireflyEntity>(6);

        public static LevelController Create(Transform parent, Camera camera)
        {
            var go = new GameObject("Level");
            go.transform.SetParent(parent, false);
            var lc = go.AddComponent<LevelController>();
            lc.cam = camera;
            lc.Init();
            return lc;
        }

        void Init()
        {
            Transform root = transform;
            goal = new GoalView(root);
            lamp = new LampView(root);
            moth = new MothView(root);
            moth.Show(false);

            // Prewarm enough for the busiest level so loading never instantiates.
            wallPool = new Pool<WallEntity>(() => new WallEntity(root), e => e.Activate(), e => e.Deactivate(), 12);
            thornPool = new Pool<ThornEntity>(() => new ThornEntity(root), e => e.Activate(), e => e.Deactivate(), 4);
            spiderPool = new Pool<SpiderEntity>(() => new SpiderEntity(root), e => e.Activate(), e => e.Deactivate(), 3);
            gustPool = new Pool<GustEntity>(() => new GustEntity(root), e => e.Activate(), e => e.Deactivate(), 2);
            portalPool = new Pool<PortalEntity>(() => new PortalEntity(root), e => e.Activate(), e => e.Deactivate(), 2);
            fireflyPool = new Pool<FireflyEntity>(() => new FireflyEntity(root), e => e.Activate(), e => e.Deactivate(), 4);
        }

        // ------------------------------------------------------------------------------ loading

        public void Load(int index)
        {
            Clear();
            data = LevelDatabase.Get(index);
            pal = Palettes.ForLevel(index);

            // Border walls just outside the playfield.
            float hw = GameConfig.FieldHalfWidth, hh = GameConfig.FieldHalfHeight, b = GameConfig.BorderThickness;
            AddWall(new WallDef(new Vector2(-hw - b * 0.5f, 0f), new Vector2(b, hh * 2f + b * 2f)), true);
            AddWall(new WallDef(new Vector2(hw + b * 0.5f, 0f), new Vector2(b, hh * 2f + b * 2f)), true);
            AddWall(new WallDef(new Vector2(0f, -hh - b * 0.5f), new Vector2(hw * 2f + b * 2f, b)), true);
            AddWall(new WallDef(new Vector2(0f, hh + b * 0.5f), new Vector2(hw * 2f + b * 2f, b)), true);

            for (int i = 0; i < data.Walls.Count; i++) AddWall(data.Walls[i], false);
            for (int i = 0; i < data.Thorns.Count; i++)
            {
                var e = thornPool.Get();
                e.Setup(data.Thorns[i], pal);
                thorns.Add(e);
            }
            for (int i = 0; i < data.Spiders.Count; i++)
            {
                var e = spiderPool.Get();
                e.Setup(data.Spiders[i], pal);
                spiders.Add(e);
            }
            for (int i = 0; i < data.Gusts.Count; i++)
            {
                var e = gustPool.Get();
                e.Setup(data.Gusts[i], pal);
                gusts.Add(e);
            }
            for (int i = 0; i < data.Portals.Count; i++)
            {
                var e = portalPool.Get();
                e.Setup(data.Portals[i], i % 2 == 0 ? pal.mothWing : pal.firefly);
                portals.Add(e);
            }
            for (int i = 0; i < data.Fireflies.Count; i++)
            {
                var e = fireflyPool.Get();
                e.Setup(data.Fireflies[i], pal, i);
                fireflies.Add(e);
            }

            goal.Setup(data.Goal, pal);
            lamp.SetColors(pal);
            moth.SetColors(pal);
            moth.Show(true);

            mothPos = data.MothStart;
            mothVel = Vector2.zero;
            mothSeesLamp = false;
            portalArmed = true;
            lampOn = false;
            Taps = 0;
            FirefliesCollected = 0;
            FireflyTotal = data.Fireflies.Count;
            time = 0f;
            winScale = 1f;
            phase = Phase.Playing;
            TickEntities(0f);
            moth.Pose(mothPos, mothVel, 0f, 1f);
            if (StatsChanged != null) StatsChanged();
        }

        void AddWall(in WallDef d, bool border)
        {
            var e = wallPool.Get();
            e.Setup(d, pal, border);
            walls.Add(e);
        }

        /// <summary>Menu backdrop: an empty field where the moth chases a lamp drifting in a figure-eight.</summary>
        public void StartDemo()
        {
            Clear();
            data = null;
            pal = Palettes.Worlds[0];
            moth.SetColors(pal);
            moth.Show(true);
            lamp.SetColors(pal);
            mothPos = new Vector2(0f, -3f);
            mothVel = Vector2.zero;
            time = 0f;
            lampOn = true;
            lampPos = DemoLampPosition(0f);
            lamp.Place(lampPos);
            phase = Phase.Demo;
        }

        public void Clear()
        {
            wallPool.ReleaseAll(walls);
            thornPool.ReleaseAll(thorns);
            spiderPool.ReleaseAll(spiders);
            gustPool.ReleaseAll(gusts);
            portalPool.ReleaseAll(portals);
            fireflyPool.ReleaseAll(fireflies);
            goal.Show(false);
            lamp.Hide();
            moth.Show(false);
            lampOn = false;
            phase = Phase.Idle;
            if (Fx.Instance != null) Fx.Instance.ClearAll();
        }

        // ------------------------------------------------------------------------------ main loop

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || phase == Phase.Idle) return; // paused (timeScale 0) or nothing loaded
            if (dt > 1f / 30f) dt = 1f / 30f;             // avoid tunnelling after a hitch
            time += dt;

            TickEntities(dt);

            switch (phase)
            {
                case Phase.Demo:
                    UpdateDemo(dt);
                    break;
                case Phase.Playing:
                    HandleInput();
                    UpdateLampFuel(dt);
                    float step = dt / GameConfig.SubSteps;
                    for (int i = 0; i < GameConfig.SubSteps && phase == Phase.Playing; i++)
                    {
                        StepMoth(step);
                        CheckContacts();
                    }
                    EmitTrail(dt);
                    break;
                case Phase.Winning:
                    UpdateWinning(dt);
                    break;
                case Phase.Dying:
                    UpdateDying(dt);
                    break;
            }

            if (phase != Phase.Dying) moth.Pose(mothPos, mothVel, dt, winScale);
            bool showFuel = data != null && data.LampLife > 0f;
            float fuel = showFuel ? Mathf.Clamp01(lampTimeLeft / data.LampLife) : 1f;
            lamp.Animate(dt, fuel, showFuel, mothSeesLamp && (phase == Phase.Playing || phase == Phase.Demo), mothPos);
            goal.Tick(time);
        }

        void TickEntities(float dt)
        {
            for (int i = 0; i < walls.Count; i++) walls[i].Tick(time);
            for (int i = 0; i < spiders.Count; i++) spiders[i].Tick(time);
            for (int i = 0; i < gusts.Count; i++) gusts[i].Tick(dt);
            for (int i = 0; i < portals.Count; i++) portals[i].Tick(dt);
            for (int i = 0; i < fireflies.Count; i++) fireflies[i].Tick(time);
        }

        // ------------------------------------------------------------------------------ input & lamp

        void HandleInput()
        {
            if (!TryGetTap(out Vector2 screen)) return;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            PlaceLamp(new Vector2(w.x, w.y));
        }

        /// <summary>Returns a new tap/click that is not on top of UI. Uses touches on devices and the mouse in the editor.</summary>
        static bool TryGetTap(out Vector2 screen)
        {
            screen = default;
            EventSystem es = EventSystem.current;
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase != TouchPhase.Began) return false;
                if (es != null && es.IsPointerOverGameObject(t.fingerId)) return false;
                screen = t.position;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (es != null && es.IsPointerOverGameObject()) return false;
                screen = Input.mousePosition;
                return true;
            }
            return false;
        }

        /// <summary>Places the lamp as if the player tapped this world position (used by AutoPlayCapture).</summary>
        public void PlaceLampAt(Vector2 worldPos)
        {
            if (phase == Phase.Playing) PlaceLamp(worldPos);
        }

        void PlaceLamp(Vector2 p)
        {
            if (Mathf.Abs(p.x) > GameConfig.FieldHalfWidth || Mathf.Abs(p.y) > GameConfig.FieldHalfHeight) return;

            var clearance = new Vector2(GameConfig.LampClearance, GameConfig.LampClearance);
            for (int i = 0; i < walls.Count; i++)
            {
                if (CollisionMath.PointInRect(p, walls[i].Center, walls[i].Half + clearance))
                {
                    // Can't light a lamp inside a wall: puff of smoke, no tap counted.
                    Fx.Instance.Burst(p, 8, new Color(1f, 1f, 1f, 0.5f), 1.5f, 0.35f, 0.25f);
                    AudioManager.Instance.Play(SfxSynth.Fizzle, 0.6f);
                    return;
                }
            }

            lampOn = true;
            lampPos = p;
            lampTimeLeft = data.LampLife;
            Taps++;
            lamp.Place(p);
            Fx.Instance.Burst(p, 10, pal.lamp, 2.2f, 0.45f, 0.35f);
            AudioManager.Instance.Play(SfxSynth.Tap, 0.7f);
            if (StatsChanged != null) StatsChanged();
        }

        void UpdateLampFuel(float dt)
        {
            if (!lampOn || data.LampLife <= 0f) return;
            lampTimeLeft -= dt;
            if (lampTimeLeft > 0f) return;
            lampOn = false;
            lamp.Extinguish();
            Fx.Instance.Burst(lampPos, 6, new Color(1f, 1f, 1f, 0.35f), 0.8f, 0.6f, 0.3f, -0.8f);
        }

        // ------------------------------------------------------------------------------ moth simulation

        void StepMoth(float dt)
        {
            mothSeesLamp = lampOn && HasLineOfSight(mothPos, lampPos);
            Steer(dt);

            for (int i = 0; i < gusts.Count; i++)
            {
                GustEntity g = gusts[i];
                if (CollisionMath.PointInRect(mothPos, g.Center, g.Half)) mothVel += g.Force * dt;
            }

            float max = GameConfig.MothMaxSpeed * 1.35f;
            float sq = mothVel.sqrMagnitude;
            if (sq > max * max) mothVel *= max / Mathf.Sqrt(sq);

            mothPos += mothVel * dt;

            for (int i = 0; i < walls.Count; i++)
            {
                WallEntity w = walls[i];
                if (CollisionMath.CircleRect(mothPos, GameConfig.MothRadius, w.Center, w.Half, out Vector2 n, out float pen))
                {
                    mothPos += n * pen;
                    float vn = Vector2.Dot(mothVel, n);
                    if (vn < 0f) mothVel -= n * (vn * (1f + GameConfig.WallBounce));
                }
            }
        }

        /// <summary>"Arrive" steering toward a visible lamp; otherwise the moth glides and slows down.</summary>
        void Steer(float dt)
        {
            if (mothSeesLamp)
            {
                Vector2 to = lampPos - mothPos;
                float dist = to.magnitude;
                float speed = GameConfig.MothMaxSpeed * Mathf.Clamp01(dist / GameConfig.ArriveRadius);
                Vector2 desired = dist > 1e-4f ? to * (speed / dist) : Vector2.zero;
                mothVel += (desired - mothVel) * Mathf.Min(1f, GameConfig.MothSteer * dt);
            }
            else
            {
                mothVel *= 1f - Mathf.Min(1f, GameConfig.MothDrag * dt);
            }
        }

        bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            for (int i = 0; i < walls.Count; i++)
                if (CollisionMath.SegmentHitsRect(from, to, walls[i].Center, walls[i].Half)) return false;
            return true;
        }

        void CheckContacts()
        {
            float hr = GameConfig.MothRadius * GameConfig.HazardForgiveness;

            for (int i = 0; i < thorns.Count; i++)
            {
                if (CollisionMath.CircleOverlapsRect(mothPos, hr, thorns[i].Center, thorns[i].Half))
                {
                    Die("Ouch! The thorns got you.");
                    return;
                }
            }

            float spiderReach = hr + GameConfig.SpiderRadius;
            for (int i = 0; i < spiders.Count; i++)
            {
                if ((mothPos - spiders[i].Position).sqrMagnitude < spiderReach * spiderReach)
                {
                    Die("Caught in a spider's web!");
                    return;
                }
            }

            CheckPortals();

            float pickup = GameConfig.MothRadius + GameConfig.FireflyRadius;
            for (int i = 0; i < fireflies.Count; i++)
            {
                FireflyEntity f = fireflies[i];
                if (f.Active && (mothPos - f.Position).sqrMagnitude < pickup * pickup) CollectFirefly(f);
            }

            if ((mothPos - data.Goal).sqrMagnitude < GameConfig.GoalRadius * GameConfig.GoalRadius) Win();
        }

        void CheckPortals()
        {
            float enter = GameConfig.PortalRadius * GameConfig.PortalRadius;
            float rearm = (GameConfig.PortalRadius + 0.15f) * (GameConfig.PortalRadius + 0.15f);
            bool nearAny = false;

            for (int i = 0; i < portals.Count; i++)
            {
                PortalEntity p = portals[i];
                float da = (mothPos - p.A).sqrMagnitude;
                float db = (mothPos - p.B).sqrMagnitude;
                if (portalArmed)
                {
                    if (da < enter) { Teleport(p.A, p.B, p); return; }
                    if (db < enter) { Teleport(p.B, p.A, p); return; }
                }
                if (da < rearm || db < rearm) nearAny = true;
            }
            // Must leave every portal before another jump, so the moth can't bounce back and forth.
            if (!nearAny) portalArmed = true;
        }

        void Teleport(Vector2 from, Vector2 to, PortalEntity portal)
        {
            Vector2 dir = mothVel.sqrMagnitude > 0.01f ? mothVel.normalized : Vector2.up;
            Fx.Instance.Burst(from, 12, pal.mothWing, 2f, 0.4f, 0.3f);
            mothPos = to + dir * (GameConfig.PortalRadius * 0.6f);
            portalArmed = false;
            portal.Flash();
            Fx.Instance.Burst(to, 12, pal.mothWing, 2f, 0.4f, 0.3f);
            AudioManager.Instance.Play(SfxSynth.Portal, 0.6f);
        }

        void CollectFirefly(FireflyEntity f)
        {
            f.Collect();
            FirefliesCollected++;
            Fx.Instance.Burst(f.Position, 14, pal.firefly, 2.6f, 0.5f, 0.3f, 0f, SpriteFactory.Star);
            AudioManager.Instance.Play(SfxSynth.Collect, 0.7f, 0f);
            if (StatsChanged != null) StatsChanged();
        }

        void EmitTrail(float dt)
        {
            trailTimer += dt;
            if (trailTimer < 0.06f || mothVel.sqrMagnitude < 0.2f) return;
            trailTimer = 0f;
            Vector2 jitter = new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(-0.08f, 0.08f));
            Fx.Instance.Emit(mothPos + jitter, -mothVel * 0.1f, WorldPalette.WithAlpha(pal.mothWing, 0.45f), 0.5f, 0.28f);
        }

        // ------------------------------------------------------------------------------ win / lose

        void Win()
        {
            phase = Phase.Winning;
            phaseTimer = 0f;
            winFrom = mothPos;
            lampOn = false;
            lamp.Extinguish();
            Fx.Instance.Burst(data.Goal, 26, pal.goal, 3.2f, 0.8f, 0.4f, 0f, SpriteFactory.Star);
            AudioManager.Instance.Play(SfxSynth.Win, 0.8f, 0f);
        }

        void UpdateWinning(float dt)
        {
            phaseTimer += dt;
            float t = Mathf.Clamp01(phaseTimer / 0.8f);
            float e = t * t * (3f - 2f * t);
            mothPos = Vector2.Lerp(winFrom, data.Goal, e);
            mothVel = (data.Goal - winFrom) * (1f - e); // keeps the wings beating as it settles
            winScale = 1f - 0.75f * e;
            if (phaseTimer < GameConfig.WinDuration) return;
            phase = Phase.Done;
            if (Won != null) Won(ComputeStars());
        }

        int ComputeStars()
        {
            int stars = 1;                                                           // reached home
            if (FireflyTotal == 0 || FirefliesCollected >= FireflyTotal) stars++;    // every firefly
            if (Taps <= data.Par) stars++;                                           // lamps within par
            return stars;
        }

        void Die(string reason)
        {
            phase = Phase.Dying;
            phaseTimer = 0f;
            deathReason = reason;
            lampOn = false;
            lamp.Extinguish();
            moth.Show(false);
            Fx.Instance.Burst(mothPos, 18, pal.hazard, 3f, 0.6f, 0.35f);
            Fx.Instance.Burst(mothPos, 10, pal.mothWing, 2f, 0.8f, 0.3f, 1.5f);
            AudioManager.Instance.Play(SfxSynth.Die, 0.7f, 0f);
        }

        void UpdateDying(float dt)
        {
            phaseTimer += dt;
            if (phaseTimer < GameConfig.DeathDuration) return;
            phase = Phase.Done;
            if (Lost != null) Lost(deathReason);
        }

        // ------------------------------------------------------------------------------ menu demo

        static Vector2 DemoLampPosition(float t)
        {
            return new Vector2(Mathf.Sin(t * 0.45f) * 2.8f, Mathf.Sin(t * 0.9f) * 3f - 1.5f);
        }

        void UpdateDemo(float dt)
        {
            lampPos = DemoLampPosition(time);
            lamp.Move(lampPos);
            mothSeesLamp = true;
            Steer(dt);
            mothPos += mothVel * dt;
            EmitTrail(dt);
        }
    }
}
