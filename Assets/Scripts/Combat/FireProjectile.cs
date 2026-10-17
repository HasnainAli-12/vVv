using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// The fireball an enemy throws.
    ///
    /// Moved by hand rather than by a Rigidbody: it is a small, fast, short-lived object, and a
    /// swept raycast between frames means it cannot tunnel through a thin railing the way a
    /// discrete rigidbody can. Everything is done in world space so the prefab needs no setup.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class FireProjectile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] float speed = 14f;
        [SerializeField] float gravity = 4f;          // a light arc reads better than a laser
        [SerializeField] float lifetime = 6f;
        [SerializeField] float turnRate = 90f;        // homing, degrees per second. 0 = straight

        [Header("Impact")]
        [SerializeField] float damage = 12f;
        [SerializeField] float impactRadius = 1.4f;
        [SerializeField] float knockback = 4f;
        [SerializeField] LayerMask hitMask = ~0;

        [Header("Presentation")]
        [SerializeField] GameObject impactVfx;
        [SerializeField] float impactVfxLifetime = 3f;
        [SerializeField] AudioClip impactSfx;
        [SerializeField] float impactSfxVolume = 0.6f;

        Transform target;
        Vector3 velocity;
        float age;
        bool spent;

        // who threw this. The resolver skips it, so an orb can never damage its own caster.
        Health.Team attackerTeam = Health.Team.Enemy;

        readonly Collider[] overlap = new Collider[8];

        void Start()
        {
            Destroy(gameObject, lifetime + 0.5f);
        }

        public void Launch(Transform homingTarget)
        {
            Launch(homingTarget, Health.Team.Enemy);
        }

        /// <summary>Launch with an owning side, read from the caster's Health.</summary>
        public void Launch(Transform homingTarget, Health.Team team)
        {
            attackerTeam = team;
            target = homingTarget;

            Vector3 direction = transform.forward;
            if (target != null)
            {
                Vector3 toTarget = (target.position + Vector3.up * 1f) - transform.position;
                if (toTarget.sqrMagnitude > 0.01f) direction = toTarget.normalized;
            }

            velocity = direction * speed;
        }

        void Update()
        {
            if (spent) return;

            float dt = Time.deltaTime;
            age += dt;
            if (age >= lifetime) { Impact(transform.position, null); return; }

            // gentle homing, so the throw stays dodgeable rather than being a guaranteed hit
            if (target != null && turnRate > 0f)
            {
                Vector3 desired = ((target.position + Vector3.up * 1f) - transform.position).normalized * speed;
                velocity = Vector3.RotateTowards(velocity, desired, turnRate * Mathf.Deg2Rad * dt, 0f);
            }

            velocity += Vector3.down * gravity * dt;

            Vector3 step = velocity * dt;
            float distance = step.magnitude;

            // sweep, so a fast projectile cannot skip past thin geometry between frames
            RaycastHit hit;
            if (distance > 0f && Physics.SphereCast(transform.position, 0.25f, step / distance, out hit,
                                                    distance, hitMask, QueryTriggerInteraction.Ignore))
            {
                Impact(hit.point, hit.collider);
                return;
            }

            transform.position += step;
            if (velocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(velocity);
        }

        void Impact(Vector3 position, Collider other)
        {
            if (spent) return;
            spent = true;

            // splash: fire should hurt through a near miss, and it makes the throw worth dodging.
            // Routed through the shared resolver so the team filter applies - this is what stops an
            // orb from damaging the enemy that threw it.
            var damaged = new System.Collections.Generic.List<Health>(4);
            HitResolver.Sphere(position, impactRadius, damage, attackerTeam, gameObject, position, damaged);

            for (int i = 0; i < damaged.Count; i++)
            {
                Health h = damaged[i];
                if (h == null) continue;

                Vector3 away = h.transform.position - position;
                away.y = 0f;
                var col = h.GetComponentInChildren<Collider>();
                if (col != null)
                    ApplyKnockback(col, away.sqrMagnitude > 0.01f ? away.normalized : transform.forward);
            }

            if (impactVfx != null)
            {
                var vfx = Instantiate(impactVfx, position, Quaternion.identity);
                Destroy(vfx, impactVfxLifetime);
            }

            if (impactSfx != null)
                AudioSource.PlayClipAtPoint(impactSfx, position, impactSfxVolume);

            Destroy(gameObject);
        }

        void ApplyKnockback(Collider other, Vector3 direction)
        {
            if (knockback <= 0f) return;

            var body = other.attachedRigidbody;
            if (body != null && !body.isKinematic)
                body.AddForce(direction * knockback, ForceMode.VelocityChange);
        }
    }
}
