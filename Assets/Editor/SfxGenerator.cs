using System.IO;
using UnityEngine;
using UnityEditor;

namespace NasCon.Level.EditorTools
{
    /// <summary>
    /// Synthesises the placeholder SFX set as real 16-bit PCM .wav assets.
    ///
    /// There is no SFX library in this project, so these are generated rather than sourced: they are
    /// shaped noise bursts and thumps, which cover impacts, footsteps, whooshes and slides well.
    /// A human "grunt" cannot be synthesised convincingly - sfx_jump is an exertion-style breath, not
    /// a voice. Drop real clips into Assets/Audio/SFX with the same filenames to replace them.
    ///
    /// Re-run via Tools > Ember Ascent > Generate SFX.
    /// </summary>
    public static class SfxGenerator
    {
        const int Rate = 44100;
        const string Folder = "Assets/Audio/SFX";

        [MenuItem("Tools/Ember Ascent/Generate SFX")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Audio", "SFX");

            for (int i = 0; i < 4; i++)
                WriteWav("sfx_footstep_" + (i + 1), Footstep(1200 + i * 311));

            WriteWav("sfx_jump", Jump());
            WriteWav("sfx_land", Land());
            WriteWav("sfx_swing", Swing());
            WriteWav("sfx_hit", Hit());
            WriteWav("sfx_slide", Slide());

            AssetDatabase.Refresh();
            Debug.Log("[SfxGenerator] Wrote SFX set to " + Folder);
        }

        // ============================================================ sounds

        static float[] Footstep(int seed)
        {
            int n = Samples(0.11f);
            float[] a = Noise(n, seed);
            HighPass(a, 260f);
            LowPass(a, 2400f);
            Envelope(a, 0.004f, 1f, 3.2f);

            // small low thump under the scuff
            Add(a, Thump(n, 95f, 0.05f, 0.45f));
            return Normalize(a, 0.62f);
        }

        static float[] Jump(int unused = 0)
        {
            int n = Samples(0.30f);

            // exertion breath: band-limited noise that swells then falls
            float[] a = Noise(n, 4711);
            HighPass(a, 420f);
            LowPass(a, 2600f);
            Envelope(a, 0.02f, 0.28f, 2.4f);

            // body weight behind it
            Add(a, Thump(n, 82f, 0.02f, 0.30f));
            return Normalize(a, 0.70f);
        }

        static float[] Land()
        {
            int n = Samples(0.36f);

            float[] a = Noise(n, 9091);
            LowPass(a, 1100f);
            Envelope(a, 0.002f, 1f, 4.0f);

            Add(a, Thump(n, 58f, 0.0f, 0.55f));
            Add(a, Thump(n, 96f, 0.015f, 0.28f));
            return Normalize(a, 0.82f);
        }

        static float[] Swing()
        {
            int n = Samples(0.24f);
            float[] a = Noise(n, 2027);

            // sweeping band-pass = the whoosh. One-pole cutoff moves across the clip.
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float cutoff = Mathf.Lerp(3600f, 700f, t * t);
                float dt = 1f / Rate;
                float rc = 1f / (2f * Mathf.PI * cutoff);
                float alpha = dt / (rc + dt);
                prev += alpha * (a[i] - prev);
                a[i] = prev;
            }
            HighPass(a, 220f);
            Envelope(a, 0.05f, 0.35f, 2.6f);
            return Normalize(a, 0.58f);
        }

        static float[] Hit()
        {
            int n = Samples(0.22f);
            float[] a = Noise(n, 31337);
            HighPass(a, 500f);
            LowPass(a, 5200f);
            Envelope(a, 0.001f, 1f, 6.0f);

            Add(a, Thump(n, 70f, 0f, 0.5f));
            return Normalize(a, 0.85f);
        }

        static float[] Slide()
        {
            int n = Samples(0.9f);
            float[] a = Noise(n, 5150);
            HighPass(a, 300f);
            LowPass(a, 1500f);

            // slow swell in, held, quick out - reads as a continuous scrape
            Envelope(a, 0.12f, 0.85f, 1.6f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                a[i] *= 0.85f + 0.15f * Mathf.Sin(t * 38f);
            }
            return Normalize(a, 0.5f);
        }

        // ============================================================ dsp

        static int Samples(float seconds) { return Mathf.Max(1, Mathf.RoundToInt(seconds * Rate)); }

        static float[] Noise(int n, int seed)
        {
            var rng = new System.Random(seed);
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            return a;
        }

        static void LowPass(float[] a, float cutoff)
        {
            float dt = 1f / Rate;
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = dt / (rc + dt);
            float prev = 0f;
            for (int i = 0; i < a.Length; i++) { prev += alpha * (a[i] - prev); a[i] = prev; }
        }

        static void HighPass(float[] a, float cutoff)
        {
            float dt = 1f / Rate;
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = rc / (rc + dt);
            float prevIn = 0f;
            float prevOut = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float x = a[i];
                prevOut = alpha * (prevOut + x - prevIn);
                prevIn = x;
                a[i] = prevOut;
            }
        }

        static void Envelope(float[] a, float attack, float sustainLevel, float decayRate)
        {
            int attackN = Mathf.Max(1, Mathf.RoundToInt(attack * Rate));
            for (int i = 0; i < a.Length; i++)
            {
                float env;
                if (i < attackN) env = i / (float)attackN;
                else
                {
                    float t = (i - attackN) / (float)Rate;
                    env = sustainLevel * Mathf.Exp(-decayRate * t);
                }
                a[i] *= env;
            }
        }

        static float[] Thump(int n, float freq, float delay, float amplitude)
        {
            var a = new float[n];
            int start = Mathf.RoundToInt(delay * Rate);
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)Rate;
                // pitch drops as it decays, which is what makes a hit read as weighty
                float f = freq * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(t * 12f));
                a[i] = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-14f * t) * amplitude;
            }
            return a;
        }

        static void Add(float[] target, float[] source)
        {
            int n = Mathf.Min(target.Length, source.Length);
            for (int i = 0; i < n; i++) target[i] += source[i];
        }

        static float[] Normalize(float[] a, float peak)
        {
            float max = 0f;
            for (int i = 0; i < a.Length; i++) max = Mathf.Max(max, Mathf.Abs(a[i]));
            if (max < 1e-5f) return a;
            float k = peak / max;
            for (int i = 0; i < a.Length; i++) a[i] = Mathf.Clamp(a[i] * k, -1f, 1f);
            return a;
        }

        // ============================================================ wav out

        static void WriteWav(string name, float[] samples)
        {
            string path = Folder + "/" + name + ".wav";
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataSize = samples.Length * 2;

                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataSize);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);              // PCM
                w.Write((short)1);              // mono
                w.Write(Rate);
                w.Write(Rate * 2);              // byte rate
                w.Write((short)2);              // block align
                w.Write((short)16);             // bits per sample
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                    w.Write((short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f));
            }
        }
    }
}
