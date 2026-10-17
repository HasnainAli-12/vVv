using System;
using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Anything that can be hit. The player's attack windows and enemy attacks both resolve
    /// through this, so it has to stay free of any knowledge about who is hitting whom.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 sourcePosition, GameObject instigator);
    }

    /// <summary>
    /// Hit points for the player and for enemies.
    ///
    /// Deliberately dumb: it tracks a number, raises events, and gets out of the way. Hit reactions,
    /// death animations and AI state changes are the listeners' job - anything that needs to know
    /// *what kind* of thing took damage lives on that thing, not here.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;

        [Tooltip("Seconds of immunity after taking a hit. Stops a multi-collider attack counting twice.")]
        [SerializeField] float invulnerabilityTime = 0.15f;

        [Tooltip("Disable the object this many seconds after dying. 0 leaves it alone.")]
        [SerializeField] float disableDelayOnDeath;

        [Tooltip("Destroy the object this many seconds after dying. 0 leaves it alone.")]
        [SerializeField] float destroyDelayOnDeath;

        [SerializeField] bool logDamage;

        [Header("Identity")]
        [Tooltip("Which side this belongs to. Attacks skip their own side, so nothing can damage " +
                 "itself, its own projectiles, or anything else on its team.")]
        [SerializeField] Team team = Team.Enemy;

        float current;
        float invulnerableUntil;

        /// <summary>
        /// Who a character fights for. Deliberately not a layer: a layer cannot answer "is this mine",
        /// and the damage resolver needs to at every hit.
        /// </summary>
        public enum Team { Player, Enemy }

        public Team Side { get { return team; } }

        public float Current { get { return current; } }
        public float Max { get { return maxHealth; } }
        public float Normalized { get { return maxHealth <= 0f ? 0f : Mathf.Clamp01(current / maxHealth); } }
        public bool IsAlive { get { return current > 0f; } }

        /// <summary>Fired on every hit that lands, with the damage actually applied.</summary>
        public event Action<Health, float, Vector3, GameObject> Damaged;

        /// <summary>Fired once, when health first reaches zero.</summary>
        public event Action<Health, GameObject> Died;

        void Awake()
        {
            current = maxHealth;
        }

        /// <summary>Spawn-time setup, so an enemy prefab can carry a per-type health value.</summary>
        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, value);
            if (refill) current = maxHealth;
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            current = Mathf.Min(maxHealth, current + amount);
        }

        /// <summary>
        /// Forces the current value without raising events. This is what a rewind uses: the health
        /// bar polls Normalized every frame, and firing Damaged on the way *back* up would trigger
        /// hit feedback for healing.
        /// Does not resurrect - a dead character stays dead.
        /// </summary>
        public void Restore(float value)
        {
            if (!IsAlive) return;
            current = Mathf.Clamp(value, 0f, maxHealth);
        }

        public bool TryConsumeInvulnerability()
        {
            if (Time.time < invulnerableUntil) return false;
            invulnerableUntil = Time.time + invulnerabilityTime;
            return true;
        }

        public void TakeDamage(float amount, Vector3 sourcePosition, GameObject instigator)
        {
            if (!IsAlive || amount <= 0f) return;

            // a single attack sweeps several colliders; without this it applies once per collider
            if (Time.time < invulnerableUntil) return;
            invulnerableUntil = Time.time + invulnerabilityTime;

            float applied = Mathf.Min(amount, current);
            current -= applied;

            if (logDamage)
                Debug.Log("[" + name + "] took " + applied.ToString("0.#") +
                          " (" + current.ToString("0.#") + "/" + maxHealth.ToString("0.#") + ") from " +
                          (instigator == null ? "unknown" : instigator.name), this);

            if (Damaged != null) Damaged(this, applied, sourcePosition, instigator);

            if (current <= 0f)
            {
                current = 0f;
                if (Died != null) Died(this, instigator);

                if (disableDelayOnDeath > 0f) Invoke("DisableSelf", disableDelayOnDeath);
                if (destroyDelayOnDeath > 0f) Destroy(gameObject, destroyDelayOnDeath);
            }
        }

        void DisableSelf()
        {
            gameObject.SetActive(false);
        }
    }
}
