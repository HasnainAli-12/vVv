using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Plays the impact sounds that come from Health events.
    ///
    /// Deliberately one component rather than edits to five scripts. Health already raises Damaged and
    /// Died, and every character in the game has a Health, so subscribing here covers the player, the
    /// enemies, the hurt samples and the death sound without touching a single existing file.
    ///
    /// Re-scans periodically so enemies spawned after the scene loads are picked up too - they are
    /// created at runtime, so a one-shot listing at Start would miss every one of them.
    /// </summary>
    public class SfxDirector : MonoBehaviour
    {
        [Header("Levels")]
        [SerializeField, Range(0f, 1f)] float volume = 0.9f;
        [Tooltip("Volume of a light hit. Heavier hits scale up toward 1.")]
        [SerializeField, Range(0f, 1f)] float lightHitVolume = 0.45f;
        [Tooltip("Damage at or above which a hit plays at full volume.")]
        [SerializeField] float loudDamage = 30f;

        [Header("Timing")]
        [Tooltip("Minimum seconds between two damage sounds from the same object.")]
        [SerializeField] float repeatGuard = 0.06f;

        [Header("Enemy")]
        [Tooltip("Scales the enemy hurt sound. Enemies take hits far more often than the player, " +
                 "so they need to sit further back in the mix or combat becomes a wall of noise.")]
        [SerializeField, Range(0f, 1f)] float enemyHurtScale = 0.5f;

        [Header("Rescan")]
        [Tooltip("Seconds between sweeps for enemies spawned at runtime.")]
        [SerializeField] float rescanInterval = 2f;

        readonly System.Collections.Generic.List<Health> watched = new System.Collections.Generic.List<Health>();
        readonly System.Collections.Generic.Dictionary<Health, float> lastPlayed =
            new System.Collections.Generic.Dictionary<Health, float>();

        float nextScan;
        bool alternateSample;

        void Start()
        {
            Scan();
        }

        [Header("Jump")]
        [Tooltip("Watched here because the jump itself lives inside CCP's state machine, not in our code.")]
        [SerializeField] string jumpClip = "female_jump";
        [SerializeField, Range(0f, 1f)] float jumpVolume = 0.7f;

        Lightbug.CharacterControllerPro.Core.CharacterActor playerActor;
        bool wasGrounded = true;

        void Update()
        {
            WatchJump();

            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + rescanInterval;
            Scan();
        }

        /// <summary>
        /// Plays the jump sound when the character leaves the ground going UP.
        ///
        /// A character that walks off a ledge also leaves the ground, so the vertical speed is what
        /// separates a jump from a fall - otherwise every drop in a parkour game fires a jump sound.
        /// </summary>
        void WatchJump()
        {
            if (playerActor == null)
            {
                playerActor = FindFirstObjectByType<Lightbug.CharacterControllerPro.Core.CharacterActor>();
                if (playerActor == null) return;
                wasGrounded = playerActor.IsGrounded;
                return;
            }

            bool grounded = playerActor.IsGrounded;

            // Skipped while wall running.
            //
            // Entering a wall run ALSO leaves the ground with upward velocity - that is the state's
            // entry lift - so this fired a jump sound on every single wall grab. It is not the wall
            // run making a noise, it is the jump sound being triggered by it.
            if (wasGrounded && !grounded && playerActor.Velocity.y > 1f && !PlayerWallRunState.IsActive)
                Sfx.Play(jumpClip, playerActor.transform.position, jumpVolume);

            wasGrounded = grounded;
        }

        /// <summary>Subscribes to any Health not already being watched.</summary>
        void Scan()
        {
            var found = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                Health h = found[i];
                if (h == null || watched.Contains(h)) continue;

                h.Damaged += OnDamaged;
                h.Died += OnDied;
                watched.Add(h);
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < watched.Count; i++)
            {
                if (watched[i] == null) continue;
                watched[i].Damaged -= OnDamaged;
                watched[i].Died -= OnDied;
            }
            watched.Clear();
            lastPlayed.Clear();
        }

        void OnDamaged(Health source, float amount, Vector3 position, GameObject instigator)
        {
            if (source == null) return;

            // a rigged character has dozens of colliders and a hit can land on several in one frame.
            // Health has its own invulnerability window, but guard here too so a burst cannot stack.
            float last;
            if (lastPlayed.TryGetValue(source, out last) && Time.unscaledTime - last < repeatGuard) return;
            lastPlayed[source] = Time.unscaledTime;

            Vector3 at = source.transform.position + Vector3.up * 1.1f;
            float level = Mathf.Lerp(lightHitVolume, 1f, Mathf.Clamp01(amount / Mathf.Max(1f, loudDamage))) * volume;

            if (source.Side == Health.Team.Player)
            {
                // two samples, alternating so repeated hits do not sound identical
                alternateSample = !alternateSample;
                Sfx.Play(alternateSample ? "player hurt 1" : "player hurt 2", at, level);
            }
            else
            {
                Sfx.Play("enemy_hurt", at, level * enemyHurtScale);
                // a light impact tick under the vocal, which reads as the hit landing. Reduced with
                // the vocal so the pair stays in proportion instead of the tick becoming the loud part.
                Sfx.Play("hit", at, level * enemyHurtScale * 0.6f);
            }
        }

        void OnDied(Health source, GameObject instigator)
        {
            if (source == null) return;
            if (source.Side == Health.Team.Player) return;   // the death screen has its own moment

            Vector3 at = source.transform.position + Vector3.up * 0.9f;
            Sfx.Play("enemy_death", at, volume);
        }
    }
}
