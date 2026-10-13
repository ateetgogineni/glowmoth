using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// World backdrop: a vertical gradient (tiny 2x64 texture stretched over the view) plus drifting,
    /// twinkling motes. Recoloured per world; costs one extra draw call for the gradient.
    /// </summary>
    public sealed class Background : MonoBehaviour
    {
        const int GradientHeight = 64;
        const int MoteCount = 28;

        Texture2D gradientTex;
        SpriteRenderer gradient;
        readonly Transform[] motes = new Transform[MoteCount];
        readonly SpriteRenderer[] moteSr = new SpriteRenderer[MoteCount];
        readonly Vector2[] motePos = new Vector2[MoteCount];
        readonly float[] moteSpeed = new float[MoteCount];
        readonly float[] motePhase = new float[MoteCount];
        readonly Color32[] gradientPixels = new Color32[2 * GradientHeight];
        Color moteColor = Color.white;
        float halfW = 5f, halfH = 9f, time;
        int world = -1;

        public static Background Create(Transform parent)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Background>();
            bg.Init();
            return bg;
        }

        void Init()
        {
            gradientTex = new Texture2D(2, GradientHeight, TextureFormat.RGBA32, false)
            {
                name = "BackgroundGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            // pixelsPerUnit = 2 -> sprite is 1 x 32 units; scaled to the view in Fit().
            var sprite = Sprite.Create(gradientTex, new Rect(0, 0, 2, GradientHeight), new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
            gradient = Make.Sprite("Gradient", transform, sprite, -100);

            for (int i = 0; i < MoteCount; i++)
            {
                moteSr[i] = Make.Sprite("Mote", transform, SpriteFactory.Glow, -90);
                motes[i] = moteSr[i].transform;
                motePos[i] = new Vector2(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH));
                moteSpeed[i] = Random.Range(0.08f, 0.35f);
                motePhase[i] = Random.Range(0f, 10f);
                float s = Random.Range(0.12f, 0.4f);
                motes[i].localScale = new Vector3(s, s, 1f);
            }
            SetWorld(0);
        }

        public void SetWorld(int index)
        {
            if (index == world) return;
            world = index;
            WorldPalette p = Palettes.Worlds[index];
            for (int y = 0; y < GradientHeight; y++)
            {
                Color32 c = Color.Lerp(p.bgBottom, p.bgTop, y / (GradientHeight - 1f));
                gradientPixels[y * 2] = c;
                gradientPixels[y * 2 + 1] = c;
            }
            gradientTex.SetPixels32(gradientPixels);
            gradientTex.Apply(false, false);
            moteColor = p.mote;
        }

        public void Fit(Camera cam)
        {
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
            gradient.transform.localScale = new Vector3(halfW * 2f + 0.5f, (halfH * 2f + 0.5f) / 32f, 1f);
        }

        void Update()
        {
            // Unscaled so the backdrop keeps breathing while paused.
            float dt = Time.unscaledDeltaTime;
            time += dt;
            for (int i = 0; i < MoteCount; i++)
            {
                Vector2 p = motePos[i];
                p.y += moteSpeed[i] * dt;
                p.x += Mathf.Sin(time * 0.3f + motePhase[i]) * 0.05f * dt;
                if (p.y > halfH + 0.5f)
                {
                    p.y = -halfH - 0.5f;
                    p.x = Random.Range(-halfW, halfW);
                }
                motePos[i] = p;
                motes[i].localPosition = new Vector3(p.x, p.y, 0f);
                Color c = moteColor;
                c.a = 0.18f + 0.22f * (0.5f + 0.5f * Mathf.Sin(time * 1.7f + motePhase[i] * 3f));
                moteSr[i].color = c;
            }
        }
    }
}
