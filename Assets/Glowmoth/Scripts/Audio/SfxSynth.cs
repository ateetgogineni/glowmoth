using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// Synthesises every sound at startup (sine tones, sweeps, filtered noise and a looping ambient track),
    /// so the build contains no audio files. Mono 22 kHz keeps memory tiny (~0.8 MB for everything).
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 22050;

        public static AudioClip Tap, Click, Pop, Collect, Win, Portal, Die, Fizzle, Music;

        public static void Build()
        {
            if (Tap != null) return;
            Tap = Tone("tap", 0.16f, 880f, 620f, 18f, 0.5f);
            Click = Tone("click", 0.06f, 1400f, 1150f, 60f, 0.3f);
            Pop = Tone("pop", 0.12f, 420f, 880f, 26f, 0.45f);
            Collect = Notes("collect", new[] { 1046.5f, 1568f }, 0.08f, 0.32f, 0.45f);
            Win = Notes("win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 0.75f, 0.45f);
            Portal = Sweep("portal", 0.42f, 260f, 1300f, 0.4f, 0f);
            Die = Sweep("die", 0.55f, 520f, 70f, 0.5f, 0.35f);
            Fizzle = Noise("fizzle", 0.18f, 0.3f);
            Music = Ambient("music", 8f);
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Single sine "blip" gliding exponentially from f0 to f1, with a fast attack and exponential decay.</summary>
        static AudioClip Tone(string name, float seconds, float f0, float f1, float decay, float volume)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float u = t / seconds;
                float f = f0 * Mathf.Pow(f1 / f0, u);
                phase += 2.0 * Mathf.PI * f / Rate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * decay);
                float s = Mathf.Sin((float)phase) + 0.25f * Mathf.Sin((float)(phase * 2.0));
                d[i] = s * env * volume * 0.8f;
            }
            return MakeClip(name, d);
        }

        /// <summary>A short sequence of bell-like notes, each ringing out under the next.</summary>
        static AudioClip Notes(string name, float[] freqs, float step, float ring, float volume)
        {
            float seconds = step * (freqs.Length - 1) + ring;
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = (int)(k * step * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * (6f / ring));
                    float w = 2f * Mathf.PI * freqs[k] * t;
                    d[i] += (Mathf.Sin(w) + 0.3f * Mathf.Sin(w * 2f) + 0.1f * Mathf.Sin(w * 3f)) * env;
                }
            }
            Normalize(d, volume);
            return MakeClip(name, d);
        }

        /// <summary>Pitch sweep with a bell envelope; optional noise mixed in for rougher sounds.</summary>
        static AudioClip Sweep(string name, float seconds, float f0, float f1, float volume, float noise)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            double phase = 0;
            float lp = 0f;
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float u = t / seconds;
                float f = f0 * Mathf.Pow(f1 / f0, u) * (1f + 0.03f * Mathf.Sin(t * 60f));
                phase += 2.0 * Mathf.PI * f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.15f));
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.2f;
                d[i] = (Mathf.Sin((float)phase) * (1f - noise) + lp * noise * 2f) * env;
            }
            Normalize(d, volume);
            return MakeClip(name, d);
        }

        /// <summary>Low-passed noise burst (a soft "fft" for invalid taps).</summary>
        static AudioClip Noise(string name, float seconds, float volume)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            var rng = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.12f;
                d[i] = lp * Mathf.Exp(-t * 18f) * Mathf.Min(1f, t / 0.005f);
            }
            Normalize(d, volume);
            return MakeClip(name, d);
        }

        /// <summary>
        /// Seamless 8-second ambient loop: a soft pad cycling Am-F-C-G with a gentle pentatonic pluck melody.
        /// Pluck tails wrap around the loop point so there is no click when it repeats.
        /// </summary>
        static AudioClip Ambient(string name, float seconds)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];

            float[][] chords =
            {
                new[] { 220.00f, 261.63f, 329.63f }, // Am
                new[] { 174.61f, 220.00f, 261.63f }, // F
                new[] { 196.00f, 261.63f, 329.63f }, // C (voiced)
                new[] { 196.00f, 246.94f, 293.66f }, // G
            };
            float chordLen = seconds / chords.Length;

            // Pad: each chord fades in/out with a raised-cosine window that overlaps its neighbours.
            for (int c = 0; c < chords.Length; c++)
            {
                float centre = (c + 0.5f) * chordLen;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate;
                    float dist = Mathf.Abs(t - centre);
                    dist = Mathf.Min(dist, seconds - dist); // circular distance -> seamless loop
                    float w = dist < chordLen ? 0.5f + 0.5f * Mathf.Cos(Mathf.PI * dist / chordLen) : 0f;
                    if (w <= 0f) continue;
                    float s = 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        float f = chords[c][k];
                        s += Mathf.Sin(2f * Mathf.PI * f * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 2f * t + 0.5f);
                    }
                    d[i] += s * w * 0.12f;
                }
            }

            // Pluck melody (A minor pentatonic), one note every half second.
            float[] melody = { 440f, 523.25f, 659.25f, 587.33f, 523.25f, 440f, 392f, 440f,
                               349.23f, 440f, 523.25f, 440f, 392f, 493.88f, 587.33f, 493.88f };
            float stepLen = seconds / melody.Length;
            for (int m = 0; m < melody.Length; m++)
            {
                float start = m * stepLen;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate - start;
                    if (t < 0f) t += seconds; // wrap tails across the loop point
                    if (t > 2.5f) continue;
                    float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 2.2f);
                    d[i] += Mathf.Sin(2f * Mathf.PI * melody[m] * t) * env * 0.16f;
                }
            }

            Normalize(d, 0.5f);
            return MakeClip(name, d);
        }

        static void Normalize(float[] d, float target)
        {
            float peak = 0.0001f;
            for (int i = 0; i < d.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            float k = target / peak;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }
    }
}
