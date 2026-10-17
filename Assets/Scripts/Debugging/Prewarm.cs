using System.Collections;
using UnityEngine;

namespace NasCon.Debugging
{
    /// <summary>
    /// Compiles shader variants before gameplay instead of during it.
    ///
    /// The probe showed the build starts at 3 fps and climbs to 164 over ten seconds - the classic
    /// signature of lazy shader compilation. Every effect compiles its variant the first time it is
    /// drawn, which stalls a frame. This draws each material once, off-screen, during load, so the
    /// stall happens before the player sees anything.
    /// </summary>
    public class Prewarm : MonoBehaviour
    {
        [Tooltip("Materials that first appear during play - combat sparks, projectiles, fire VFX.")]
        [SerializeField] Material[] materials;

        [Tooltip("Prefabs spawned at runtime; their materials are collected automatically.")]
        [SerializeField] GameObject[] prefabs;

        [Tooltip("How many frames to hold the warm-up geometry on screen.")]
        [SerializeField] int warmupFrames = 3;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        IEnumerator Start()
        {
            // a mesh drawn below the level, so nothing is ever visible
            Vector3 hidden = new Vector3(0f, -5000f, 0f);
            Mesh quad = null;
            var list = new System.Collections.Generic.List<Material>(32);

            if (materials != null)
                foreach (var m in materials) if (m != null) list.Add(m);

            // prefabs carry their own materials; collect those too rather than listing them twice
            if (prefabs != null)
            {
                foreach (var prefab in prefabs)
                {
                    if (prefab == null) continue;
                    foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in r.sharedMaterials)
                            if (m != null && !list.Contains(m)) list.Add(m);
                }
            }

            if (list.Count == 0) yield break;

            // draw each material once
            for (int i = 0; i < list.Count; i++)
                Graphics.DrawMesh(Quad(), hidden + Vector3.right * i, Quaternion.identity, list[i], 0);

            // hold for a few frames so the compiles actually complete rather than being queued
            for (int f = 0; f < Mathf.Max(1, warmupFrames); f++)
            {
                for (int i = 0; i < list.Count; i++)
                    Graphics.DrawMesh(Quad(), hidden + Vector3.right * i, Quaternion.identity, list[i], 0);
                yield return null;
            }

            Debug.Log("[Prewarm] warmed " + list.Count + " materials");
        }

        static Mesh quad;

        static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh();
            quad.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            quad.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
            quad.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateNormals();
            return quad;
        }
    }
}
