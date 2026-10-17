using System.Collections;
using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Plays the reaction to being hit: hit-stop, camera shake, a spark, a flash, and a sound.
    ///
    /// Feedback is deliberately asymmetric. An enemy's reaction is what sells a hit, so it always
    /// fires in full. The player's own damage is better felt through the HUD vignette and the shake
    /// than through constant screen noise, so each channel is rolled against a chance instead. Those
    /// chances are inspector values, not buried constants.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HitFeedback : MonoBehaviour
    {
        [Header("Player feedback chances")]
        [Tooltip("Enemies always get the full reaction. The player rolls each channel against these.")]
        [Range(0f, 1f)] [SerializeField] float hitStopChance = 0.3f;
        [Range(0f, 1f)] [SerializeField] float sparkChance = 0.3f;
        [Range(0f, 1f)] [SerializeField] float flashChance = 0.35f;
        [Tooltip("Player shake is always applied, but at this fraction of the enemy amount.")]
        [Range(0f, 1f)] [SerializeField] float playerShakeScale = 0.5f;

        [Header("Hit-stop")]
        [SerializeField] float hitStopLight = 0.04f;
        [SerializeField] float hitStopHeavy = 0.08f;
        [Tooltip("Damage at or above this counts as a heavy hit and freezes for longer.")]
        [SerializeField] float heavyDamage = 16f;
        [Range(0.01f, 1f)] [SerializeField] float hitStopScale = 0.05f;

        [Header("Shake")]
        [SerializeField] float shakeAmount = 0.35f;

        [Header("Flash")]
        [SerializeField] Color flashColor = new Color(2.5f, 2.5f, 2.5f, 1f);
        [SerializeField] float flashTime = 0.12f;

        [Header("Spark")]
        [Tooltip("Material for the impact spark. An emissive one reads best.")]
        [SerializeField] Material sparkMaterial;
        [SerializeField] float sparkSize = 0.35f;
        [SerializeField] float sparkLife = 0.18f;
        [SerializeField] Color sparkTint = new Color(4f, 1.6f, 0.4f, 1f);

        [Header("Sound")]
        [SerializeField] AudioClip hitClip;
        [Range(0f, 1f)] [SerializeField] float volume = 0.7f;

        Health health;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Coroutine flashRoutine;
        bool flashing;

        // One shared material for every spark. Creating a Material per hit leaked one per hit,
        // which is what made the editor slow down after a fight.
        Material sparkInstance;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();

            if (sparkMaterial != null)
            {
                sparkInstance = new Material(sparkMaterial);
                sparkInstance.EnableKeyword("_EMISSION");
                sparkInstance.SetColor(EmissionId, sparkTint);
            }
        }

        void OnDestroy()
        {
            if (sparkInstance != null) Destroy(sparkInstance);
        }

        void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        /// <summary>
        /// Drives the hit-stop clock. Any HitFeedback in the scene will do it - the tick is idempotent,
        /// so several enemies ticking the same static is harmless.
        /// </summary>
        void Update()
        {
            HitStop.Tick();
        }

        void OnDamaged(Health source, float amount, Vector3 from, GameObject instigator)
        {
            // enemies always get the full read; the player rolls for it
            bool full = health == null || health.Side == Health.Team.Enemy;

            if (full || Random.value < hitStopChance)
                HitStop.Trigger(amount >= heavyDamage ? hitStopHeavy : hitStopLight, hitStopScale);

            CameraShake.Add(full ? shakeAmount : shakeAmount * playerShakeScale);

            if (full || Random.value < sparkChance) SpawnSpark(from, amount);

            if (full || Random.value < flashChance) Flash();

            if (hitClip != null)
                AudioSource.PlayClipAtPoint(hitClip, transform.position + Vector3.up * 1.2f, volume);
        }

        // ---------------------------------------------------------------- spark

        void SpawnSpark(Vector3 from, float amount)
        {
            if (sparkInstance == null) return;

            // a small emissive sphere that pops out, expands and is destroyed. No pooling needed at
            // this scale. The built-in primitive mesh is shared by Unity, so only the GameObject and
            // renderer are allocated here.
            Vector3 point = transform.position + Vector3.up * 1.1f;

            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) point += away.normalized * 0.2f;

            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "~HitSpark";
            Destroy(spark.GetComponent<Collider>());
            spark.transform.position = point;

            // heavier hits get a bigger spark; the shared material stays untouched
            float scale = sparkSize * Mathf.Clamp(amount / Mathf.Max(1f, heavyDamage), 0.7f, 1.8f);
            spark.transform.localScale = Vector3.one * scale;

            var renderer = spark.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = sparkInstance;

            StartCoroutine(Pop(spark, Mathf.Max(0.05f, sparkLife)));
        }

        IEnumerator Pop(GameObject spark, float life)
        {
            float t = 0f;
            Vector3 startScale = spark.transform.localScale;

            while (t < life && spark != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / life);

                // expand fast then collapse, so it reads as an impact rather than a growing blob
                float scale = Mathf.Lerp(startScale.x, startScale.x * 2.2f, k) * (1f - k * k);
                spark.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);

                yield return null;
            }

            if (spark != null) Destroy(spark);
        }

        // ---------------------------------------------------------------- flash

        void Flash()
        {
            if (renderers == null || renderers.Length == 0) return;

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            flashing = true;
            ApplyEmission(flashColor);

            float t = 0f;
            while (t < flashTime)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyEmission(Color.black);
            flashing = false;
            flashRoutine = null;
        }

        void ApplyEmission(Color color)
        {
            if (block == null) block = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
