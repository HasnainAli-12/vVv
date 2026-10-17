using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Central sound player.
    ///
    /// Clips are loaded by name from Resources/Audio. That folder matters: Unity strips an asset from a
    /// build when nothing references it, and these are loose files nothing points at, so without the
    /// copy they would simply not exist in the WebGL build.
    ///
    /// Uses a small pool of AudioSources rather than AudioSource.PlayClipAtPoint. That helper creates
    /// and destroys a GameObject per sound, which is garbage churn on a platform where a GC pause is a
    /// visible stutter - and with combat sounds firing several times a second it adds up fast.
    /// </summary>
    public static class Sfx
    {
        const int PoolSize = 12;

        static readonly System.Collections.Generic.Dictionary<string, AudioClip> cache =
            new System.Collections.Generic.Dictionary<string, AudioClip>();

        static AudioSource[] pool;
        static GameObject holder;
        static int next;
        static float lastPitchVary;

        /// <summary>
        /// Plays a clip at a position in the world. Pitch is varied slightly by default so repeated
        /// sounds - footsteps, punches - do not machine-gun.
        /// </summary>
        public static void Play(string name, Vector3 position, float volume = 1f, float vary = 0.08f)
        {
            AudioClip clip = Load(name);
            if (clip == null) return;

            AudioSource src = Take();
            if (src == null) return;

            src.transform.position = position;
            src.spatialBlend = 1f;          // 3D
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-vary, vary);
            src.loop = false;
            src.Play();
        }

        /// <summary>Plays a clip flat, for UI and interface sounds that have no world position.</summary>
        public static void Play2D(string name, float volume = 1f, float vary = 0f)
        {
            AudioClip clip = Load(name);
            if (clip == null) return;

            AudioSource src = Take();
            if (src == null) return;

            src.spatialBlend = 0f;          // 2D
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f + (vary > 0f ? Random.Range(-vary, vary) : 0f);
            src.loop = false;
            src.Play();
        }

        /// <summary>Starts a looping clip and returns the source, so the caller can stop it.</summary>
        public static AudioSource PlayLoop(string name, Transform follow, float volume = 1f)
        {
            AudioClip clip = Load(name);
            if (clip == null) return null;

            AudioSource src = Take();
            if (src == null) return null;

            if (follow != null) src.transform.position = follow.position;
            src.spatialBlend = 1f;
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f;
            src.loop = true;
            src.Play();
            return src;
        }

        static AudioSource Take()
        {
            if (pool == null) Build();

            // round-robin. Stealing the oldest is fine for short sounds; nothing here is a music bed.
            for (int i = 0; i < PoolSize; i++)
            {
                int idx = (next + i) % PoolSize;
                if (!pool[idx].isPlaying)
                {
                    next = (idx + 1) % PoolSize;
                    return pool[idx];
                }
            }

            AudioSource fallback = pool[next];
            next = (next + 1) % PoolSize;
            return fallback;
        }

        static void Build()
        {
            holder = new GameObject("~SfxPool");
            Object.DontDestroyOnLoad(holder);

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                pool[i] = holder.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 1f;
                pool[i].rolloffMode = AudioRolloffMode.Linear;
                pool[i].minDistance = 3f;
                pool[i].maxDistance = 45f;
                pool[i].dopplerLevel = 0f;      // doppler on a fast parkour game sounds like warping
            }
        }

        static AudioClip Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            AudioClip cached;
            if (cache.TryGetValue(name, out cached)) return cached;   // null cached too, so a typo warns once

            AudioClip clip = Resources.Load<AudioClip>("Audio/" + name);
            cache[name] = clip;

            if (clip == null)
                Debug.LogWarning("[Sfx] no clip named '" + name + "' in Resources/Audio");

            return clip;
        }

        /// <summary>Drops cached clips. Only needed if the audio assets change while playing.</summary>
        public static void ClearCache()
        {
            cache.Clear();
            pool = null;
            holder = null;
            next = 0;
            lastPitchVary = 0f;
        }
    }
}
