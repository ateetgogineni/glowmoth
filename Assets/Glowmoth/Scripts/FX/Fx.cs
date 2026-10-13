using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// Pooled sprite particles driven entirely by code: a fixed ring buffer of SpriteRenderers updated in one loop.
    /// Used for lamp sparks, firefly pickups, the moth trail, deaths, wins and portal jumps.
    /// Nothing is allocated after startup; emitting simply reuses the oldest slot.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }

        const int Capacity = 160;

        struct Particle
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector2 pos, vel;
            public float life, maxLife, size, gravity, spin, angle;
            public Color color;
            public bool alive;
        }

        readonly Particle[] particles = new Particle[Capacity];
        int cursor;

        public static Fx Create(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<Fx>();
            Instance.Init();
            return Instance;
        }

        void Init()
        {
            for (int i = 0; i < Capacity; i++)
            {
                var sr = Make.Sprite("P", transform, SpriteFactory.Glow, 30);
                sr.enabled = false;
                particles[i].t = sr.transform;
                particles[i].sr = sr;
            }
        }

        /// <summary>Radial burst of `count` particles.</summary>
        public void Burst(Vector2 pos, int count, Color color, float speed, float life, float size, float gravity = 0f, Sprite sprite = null)
        {
            for (int n = 0; n < count; n++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float s = speed * (0.4f + 0.6f * Random.value);
                Emit(pos, new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s), color, life * (0.7f + 0.6f * Random.value), size, gravity, sprite);
            }
        }

        public void Emit(Vector2 pos, Vector2 vel, Color color, float life, float size, float gravity = 0f, Sprite sprite = null)
        {
            ref Particle p = ref particles[cursor];
            cursor = (cursor + 1) % Capacity;
            p.pos = pos;
            p.vel = vel;
            p.life = life;
            p.maxLife = life;
            p.size = size;
            p.gravity = gravity;
            p.color = color;
            p.alive = true;
            p.sr.sprite = sprite != null ? sprite : SpriteFactory.Glow;
            p.spin = sprite != null ? (Random.value - 0.5f) * 360f : 0f;
            p.angle = 0f;
            p.t.localRotation = Quaternion.identity;
            p.sr.enabled = true;
        }

        public void ClearAll()
        {
            for (int i = 0; i < Capacity; i++)
            {
                particles[i].alive = false;
                particles[i].sr.enabled = false;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float drag = 1f - Mathf.Min(1f, 1.8f * dt);

            for (int i = 0; i < Capacity; i++)
            {
                ref Particle p = ref particles[i];
                if (!p.alive) continue;

                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.alive = false;
                    p.sr.enabled = false;
                    continue;
                }

                p.vel.y -= p.gravity * dt;
                p.vel *= drag;
                p.pos += p.vel * dt;

                float k = p.life / p.maxLife;
                float s = p.size * (0.3f + 0.7f * k);
                p.t.localPosition = new Vector3(p.pos.x, p.pos.y, 0f);
                p.t.localScale = new Vector3(s, s, 1f);
                if (p.spin != 0f)
                {
                    p.angle += p.spin * dt;
                    p.t.localRotation = Quaternion.Euler(0f, 0f, p.angle);
                }
                Color c = p.color;
                c.a *= k;
                p.sr.color = c;
            }
        }
    }
}
