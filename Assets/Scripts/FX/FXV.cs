using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// One-shot particle spawner.
    ///
    /// Prefabs are loaded by name from Resources/Vfx. That folder matters: Unity strips a prefab
    /// from a build when nothing references it, and these live in an imported pack that nothing
    /// else points at, so without the copy they simply would not exist in the WebGL build.
    ///
    /// Loading by name also means no inspector wiring in five different places, which is the only
    /// reason this is a static helper rather than a serialized reference on each caller.
    ///
    /// Instances are destroyed once their longest particle system has finished, so a busy fight
    /// does not accumulate clones.
    /// </summary>
    public static class FXV
    {
        static readonly System.Collections.Generic.Dictionary<string, GameObject> cache =
            new System.Collections.Generic.Dictionary<string, GameObject>();
        static readonly System.Collections.Generic.Dictionary<string, float> lifetimes =
            new System.Collections.Generic.Dictionary<string, float>();

        /// <summary>
        /// Spawns the named effect. Returns null (once, with a warning) if the prefab is missing,
        /// so a typo degrades to "no effect" instead of a null-reference crash mid-combat.
        /// </summary>
        public static GameObject Spawn(string name, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            GameObject prefab = Load(name);
            if (prefab == null) return null;

            GameObject go = Object.Instantiate(prefab, position, rotation);
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;

            float life = Lifetime(name, go);
            Object.Destroy(go, life);
            return go;
        }

        public static GameObject Spawn(string name, Vector3 position) =>
            Spawn(name, position, Quaternion.identity, 1f);

        /// <summary>Spawns attached to a transform, so a moving effect follows its owner.</summary>
        public static GameObject SpawnAttached(string name, Transform parent, Vector3 localPosition, float scale = 1f)
        {
            GameObject prefab = Load(name);
            if (prefab == null) return null;

            GameObject go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = prefab.transform.localScale * scale;

            float life = Lifetime(name, go);
            Object.Destroy(go, life);
            return go;
        }

        static GameObject Load(string name)
        {
            GameObject cached;
            if (cache.TryGetValue(name, out cached))
                return cached;   // null is cached too - a missing name warns once, not every hit

            GameObject prefab = Resources.Load<GameObject>("Vfx/" + name);
            cache[name] = prefab;

            if (prefab == null)
                Debug.LogWarning("[FXV] no effect named '" + name + "' in Resources/Vfx");
            return prefab;
        }

        /// <summary>How long the effect lasts, measured once per prefab and cached.</summary>
        static float Lifetime(string name, GameObject instance)
        {
            float known;
            if (lifetimes.TryGetValue(name, out known)) return known;

            float longest = 0f;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                float total = main.duration + main.startLifetime.constantMax;
                if (total > longest) longest = total;
            }

            if (longest <= 0.01f) longest = 2f;
            longest += 0.25f;   // let the last particles fade rather than cutting them off
            lifetimes[name] = longest;
            return longest;
        }

        /// <summary>Drops the cached prefabs. Only needed if the assets change while playing.</summary>
        public static void ClearCache()
        {
            cache.Clear();
            lifetimes.Clear();
        }
    }
}
