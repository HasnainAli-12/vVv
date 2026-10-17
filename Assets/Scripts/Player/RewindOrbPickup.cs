using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// The green orb. Walking into it unlocks time rewind.
    ///
    /// Deliberately a plain trigger with no rigidbody: the player is a kinematic CCP capsule, and a
    /// trigger on a static collider is the cheapest thing that reliably fires OnTriggerEnter.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RewindOrbPickup : MonoBehaviour
    {
        [Header("Unlock")]
        [Tooltip("Also adds a charge to PlayerAbilities, so the HUD counter stays in step.")]
        [SerializeField] bool alsoCreditAbilityCharge = true;

        [Header("Presentation")]
        [SerializeField] Transform visual;
        [SerializeField] float spinDegreesPerSecond = 45f;
        [SerializeField] float bobHeight = 0.18f;
        [SerializeField] float bobSpeed = 1.6f;
        [SerializeField] AudioClip pickupSfx;
        [SerializeField, Range(0f, 1f)] float pickupVolume = 0.7f;
        [SerializeField] GameObject pickupVfx;
        [SerializeField] float pickupVfxLifetime = 2f;

        [Header("Respawn")]
        [Tooltip("Seconds before the orb reappears. 0 keeps it collected forever.")]
        [SerializeField] float respawnSeconds;

        Vector3 visualStart;
        Collider trigger;
        bool taken;

        void Awake()
        {
            trigger = GetComponent<Collider>();
            trigger.isTrigger = true;

            if (visual == null) visual = transform;
            visualStart = visual.localPosition;
        }

        void Update()
        {
            if (taken) return;

            // cheap and allocation-free: two transform writes per frame
            visual.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            visual.localPosition = visualStart + new Vector3(0f, bob, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;

            var ability = other.GetComponentInParent<TimeRewindAbility>();
            if (ability == null) return;         // something other than the player

            taken = true;

            ability.Unlock();

            if (alsoCreditAbilityCharge)
            {
                var charges = other.GetComponentInParent<PlayerAbilities>();
                if (charges != null) charges.AddRewindCharge(1);
                Sfx.Play("time_orb", transform.position, 0.8f);
            }

            if (pickupSfx != null)
                AudioSource.PlayClipAtPoint(pickupSfx, transform.position, pickupVolume);

            if (pickupVfx != null)
            {
                var vfx = Instantiate(pickupVfx, transform.position, Quaternion.identity);
                Destroy(vfx, pickupVfxLifetime);
            }

            SetVisible(false);
            trigger.enabled = false;

            if (respawnSeconds > 0f) Invoke("Respawn", respawnSeconds);
        }

        void Respawn()
        {
            taken = false;
            SetVisible(true);
            trigger.enabled = true;
        }

        void SetVisible(bool on)
        {
            // walk the ROOT, not `visual` - the halo is a sibling of the visual child, so hiding only
            // the visual left the halo floating there after pickup
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = on;

            // and deactivate outright. If it respawns we come back through SetVisible(true) from a
            // live object, but with no respawn delay the orb should simply be gone.
            if (!on && respawnSeconds <= 0f) gameObject.SetActive(false);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}
