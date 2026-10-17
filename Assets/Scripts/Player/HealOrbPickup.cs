using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Heal pickup. Restores a fixed amount of health and disappears.
    ///
    /// Deliberately mirrors RewindOrbPickup - trigger sphere, collect on enter, play effect and sound,
    /// hide the object rather than only its renderer. Keeping the two identical means a level designer
    /// can place either without learning a second set of rules.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class HealOrbPickup : MonoBehaviour
    {
        [Header("Heal")]
        [Tooltip("Health restored on pickup.")]
        [SerializeField] float healAmount = 30f;

        [Tooltip("Seconds before the orb reappears. 0 keeps it collected forever.")]
        [SerializeField] float respawnSeconds = 0f;

        [Header("Feedback")]
        [SerializeField] string pickupSound = "heal";
        [SerializeField] string pickupVfx = "vfx_heal_custom";
        [SerializeField] float pickupVfxLifetime = 2f;

        [Header("Visual")]
        [Tooltip("Spins for a bit of life. 0 leaves it still.")]
        [SerializeField] float spinSpeed = 60f;
        [SerializeField] float bobHeight = 0.15f;
        [SerializeField] float bobSpeed = 1.6f;

        Vector3 startPosition;
        bool collected;

        void Awake()
        {
            startPosition = transform.position;

            var col = GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (collected) return;

            // only the player heals. The team filter comes from Health, not layers.
            var health = other.GetComponentInParent<Health>();
            if (health == null || health.Side != Health.Team.Player) return;
            if (!health.IsAlive) return;

            health.Heal(healAmount);

            if (!string.IsNullOrEmpty(pickupVfx)) FXV.Spawn(pickupVfx, transform.position, Quaternion.identity, 1f);
            if (!string.IsNullOrEmpty(pickupSound)) Sfx.Play(pickupSound, transform.position, 0.8f);

            if (respawnSeconds <= 0f)
            {
                collected = true;
                // deactivate the whole object: hiding only the renderer leaves the trigger and the
                // halo child behind, which is how the rewind orb used to stay visible after pickup
                gameObject.SetActive(false);
            }
            else
            {
                collected = true;
                Invoke("Respawn", respawnSeconds);
            }
        }

        void Respawn()
        {
            collected = false;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (collected) return;

            if (spinSpeed != 0f) transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

            if (bobHeight > 0f)
            {
                Vector3 p = startPosition;
                p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = p;
            }
        }
    }
}
