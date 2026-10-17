using UnityEngine;
using UnityEngine.AI;

namespace NasCon.Game
{
    /// <summary>
    /// Shadow enemy AI.
    ///
    /// Pathing and movement are both the NavMeshAgent's job. That is deliberate: the agent only
    /// walks the baked surface, so it physically cannot path itself off a ledge or into a pit, and
    /// there is no custom edge logic to get wrong. `autoBraking` plus a stopping distance means it
    /// stops short of the player rather than shoving into them.
    ///
    /// Behaviour is a small explicit state machine. It is intentionally not a behaviour tree -
    /// there are six states and they fit on one screen.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyBrain : MonoBehaviour
    {
        public enum State { Idle, Chase, Melee, Throw, Stagger, Dead }

        [Header("Ranges")]
        [SerializeField] float aggroRange = 24f;
        [Tooltip("Close enough to swing.")]
        [SerializeField] float meleeRange = 2.4f;
        [Tooltip("Beyond this it closes on foot instead of throwing.")]
        [SerializeField] float throwRange = 15f;
        [Tooltip("Below this it will not throw - stops it lobbing fire point blank.")]
        [SerializeField] float throwMinRange = 5f;

        [Header("Combat")]
        [SerializeField] float meleeDamage = 14f;
        [SerializeField] float meleeImpactDelay = 0.45f;
        [SerializeField] float meleeHitRadius = 1.5f;
        [SerializeField] float meleeCooldown = 1.4f;
        [SerializeField] float throwCooldown = 3.2f;
        [Tooltip("Chance a throw is chosen instead of closing to melee, per opportunity.")]
        [SerializeField, Range(0f, 1f)] float throwChance = 0.6f;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 2.6f;
        [SerializeField] float chaseSpeed = 4.4f;
        [SerializeField] float turnRate = 480f;
        [Tooltip("Buys a little grace if the agent ends up slightly off the NavMesh.")]
        [SerializeField] float repathInterval = 0.25f;

        [Header("Reactions")]
        [SerializeField] float staggerDuration = 0.55f;
        [SerializeField] float staggerKnockback = 4f;
        [SerializeField] float hitFlashTime = 0.12f;

        [Header("Projectile")]
        [SerializeField] GameObject fireProjectilePrefab;
        [Tooltip("Where the fireball is born. Falls back to chest height if unset.")]
        [SerializeField] Transform throwOrigin;
        [SerializeField] float throwReleaseDelay = 0.45f;

        [Header("Animation")]
        [SerializeField] string speedParameter = "Speed";
        [SerializeField] string attackParameter = "Attack";
        [SerializeField] string throwParameter = "Throw";
        [SerializeField] string hurtParameter = "Hurt";
        [SerializeField] string deadParameter = "Dead";

        [Header("Targeting")]
        [Tooltip("Leave empty to find the object tagged Player on start.")]
        [SerializeField] Transform target;
        [SerializeField] string playerTag = "Player";

        NavMeshAgent agent;
        Health health;
        Animator animator;

        State state = State.Idle;
        float stateEntered;
        float nextMeleeAllowed;
        float nextThrowAllowed;
        float nextRepath;
        bool meleeDamageFired;
        bool throwReleased;

        Renderer[] renderers;
        MaterialPropertyBlock flashBlock;
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public State Current { get { return state; } }
        public bool IsAlive { get { return health != null && health.IsAlive; } }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<Renderer>();
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void Start()
        {
            if (target == null)
            {
                var found = GameObject.FindGameObjectWithTag(playerTag);
                if (found != null) target = found.transform;
            }

            if (agent != null)
            {
                agent.speed = chaseSpeed;
                agent.angularSpeed = turnRate;
                agent.stoppingDistance = meleeRange * 0.8f;
                agent.autoBraking = true;
                agent.updateRotation = false;   // we turn manually, so it never snaps
                agent.updateUpAxis = false;
            }

            EnterState(State.Idle);
        }

        void Update()
        {
            if (state == State.Dead || target == null || !health.IsAlive) return;

            float dt = Time.deltaTime;
            float distance = Vector3.Distance(transform.position, target.position);

            switch (state)
            {
                case State.Idle: TickIdle(distance); break;
                case State.Chase: TickChase(dt, distance); break;
                case State.Melee: TickMelee(dt); break;
                case State.Throw: TickThrow(dt); break;
                case State.Stagger: TickStagger(); break;
            }

            UpdateAnimatorSpeed();
        }

        // ---------------------------------------------------------------- states

        void TickIdle(float distance)
        {
            if (distance <= aggroRange) EnterState(State.Chase);
        }

        void TickChase(float dt, float distance)
        {
            if (distance > aggroRange * 1.4f) { EnterState(State.Idle); return; }

            // in melee range: swing if it is off cooldown, otherwise hold position
            if (distance <= meleeRange)
            {
                if (agent != null) agent.isStopped = true;
                FaceTarget(dt);

                if (Time.time >= nextMeleeAllowed) EnterState(State.Melee);
                return;
            }

            // mid range with a throw available: lob fire instead of closing
            if (distance <= throwRange && distance >= throwMinRange && Time.time >= nextThrowAllowed)
            {
                if (agent != null) agent.isStopped = true;
                FaceTarget(dt);

                if (Random.value < throwChance) EnterState(State.Throw);
                else { nextThrowAllowed = Time.time + 0.6f; agent.isStopped = false; }
                return;
            }

            if (agent == null || !agent.isOnNavMesh) return;

            agent.isStopped = false;
            agent.speed = distance > meleeRange * 3f ? chaseSpeed : walkSpeed;

            // repathing every frame is wasteful and makes the agent jitter between corners
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                agent.SetDestination(target.position);
            }

            FaceTarget(dt);
        }

        void TickMelee(float dt)
        {
            if (agent != null) agent.isStopped = true;
            FaceTarget(dt);

            float elapsed = Time.time - stateEntered;

            if (!meleeDamageFired && elapsed >= meleeImpactDelay)
            {
                meleeDamageFired = true;
                DealMeleeDamage();
            }

            // the clip is the authority on when the swing is done; give it a floor so a short
            // clip cannot machine-gun, and a ceiling so a missing clip cannot soft-lock
            float clipLength = CurrentClipLength(1.2f);
            if (elapsed >= Mathf.Max(meleeImpactDelay + 0.15f, clipLength * 0.85f))
                EnterState(State.Chase);
        }

        void TickThrow(float dt)
        {
            if (agent != null) agent.isStopped = true;
            FaceTarget(dt);

            float elapsed = Time.time - stateEntered;

            if (!throwReleased && elapsed >= throwReleaseDelay)
            {
                throwReleased = true;
                ReleaseFireball();
            }

            float clipLength = CurrentClipLength(1.0f);
            if (elapsed >= Mathf.Max(throwReleaseDelay + 0.2f, clipLength * 0.85f))
                EnterState(State.Chase);
        }

        void TickStagger()
        {
            if (agent != null) agent.isStopped = true;
            if (Time.time - stateEntered >= staggerDuration) EnterState(State.Chase);
        }

        void EnterState(State next)
        {
            state = next;
            stateEntered = Time.time;

            switch (next)
            {
                case State.Melee:
                    meleeDamageFired = false;
                    nextMeleeAllowed = Time.time + meleeCooldown;
                    SetTrigger(attackParameter);
                    break;

                case State.Throw:
                    throwReleased = false;
                    nextThrowAllowed = Time.time + throwCooldown;
                    SetTrigger(throwParameter);
                    break;

                case State.Stagger:
                    SetTrigger(hurtParameter);
                    break;

                case State.Dead:
                    // isStopped throws unless the agent is enabled AND on a NavMesh, so check both.
                    // An enemy killed while off-mesh (spawned in the air, warped by a hit) would
                    // otherwise throw on death.
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                    if (agent != null) agent.enabled = false;
                    SetTrigger(deadParameter);
                    break;
            }
        }

        // ---------------------------------------------------------------- actions

        void DealMeleeDamage()
        {
            Vector3 origin = transform.position + Vector3.up * 1.0f + transform.forward * (meleeRange * 0.5f);

            // shared resolver: team filter, one hit per character, no self-damage
            HitResolver.Sphere(origin, meleeHitRadius, meleeDamage, health.Side, gameObject, transform.position);
        }

        void ReleaseFireball()
        {
            if (fireProjectilePrefab == null) return;

            Vector3 origin = throwOrigin != null
                ? throwOrigin.position
                : transform.position + Vector3.up * 1.3f + transform.forward * 0.4f;

            var projectile = Instantiate(fireProjectilePrefab, origin, transform.rotation);
            var fire = projectile.GetComponent<FireProjectile>();
            // carrying the team forward is what stops the orb hurting the enemy that threw it
            if (fire != null) fire.Launch(target, health.Side);
        }

        void FaceTarget(float dt)
        {
            if (target == null) return;

            Vector3 to = target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(to), turnRate * dt);
        }

        void UpdateAnimatorSpeed()
        {
            if (animator == null || agent == null) return;

            // normalised against chase speed so the blend tree's 0..1 range maps to idle..run
            float speed = agent.velocity.magnitude / Mathf.Max(0.1f, chaseSpeed);
            animator.SetFloat(speedParameter, speed, 0.1f, Time.deltaTime);
        }

        float CurrentClipLength(float fallback)
        {
            if (animator == null) return fallback;
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips == null || clips.Length == 0 || clips[0].clip == null) return fallback;
            return clips[0].clip.length;
        }

        void SetTrigger(string parameter)
        {
            if (animator != null && !string.IsNullOrEmpty(parameter))
                animator.SetTrigger(parameter);
        }

        // ---------------------------------------------------------------- reactions

        void OnDamaged(Health source, float amount, Vector3 from, GameObject instigator)
        {
            if (state == State.Dead) return;

            // knock back the agent rather than the transform: the NavMesh still constrains it,
            // so a hit can shove it around without ever putting it off the surface
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f && agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Move(away.normalized * (staggerKnockback * 0.1f));

            AddHitFlash();

            // do not interrupt a swing that has already landed - it reads as the enemy ignoring you
            if (state == State.Melee && meleeDamageFired) return;
            EnterState(State.Stagger);
        }

        void OnDied(Health source, GameObject instigator)
        {
            EnterState(State.Dead);

            // Death always happens on a frame where Hurt also fires: Health raises Damaged before
            // Died, so OnDamaged has just queued the flinch, and the AnyState evaluations run in
            // order with Hurt ahead of Dead. The flinch would win and the death trigger would sit
            // unconsumed. Clear the others and drive the death state directly so it cannot be lost.
            if (animator != null)
            {
                animator.ResetTrigger(attackParameter);
                animator.ResetTrigger(throwParameter);
                animator.ResetTrigger(hurtParameter);
                animator.Play("Dead", 0, 0f);
            }

            // the agent is stopped and disabled by EnterState(State.Dead) above - do not touch it
            // again here, because isStopped throws once the agent is disabled.

            var colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
        }

        void AddHitFlash()
        {
            if (renderers == null || renderers.Length == 0) return;
            if (flashBlock == null) flashBlock = new MaterialPropertyBlock();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(flashBlock);
                flashBlock.SetColor(EmissionColorId, Color.white * 2.5f);
                r.SetPropertyBlock(flashBlock);
            }

            CancelInvoke("ClearHitFlash");
            Invoke("ClearHitFlash", hitFlashTime);
        }

        void ClearHitFlash()
        {
            if (renderers == null) return;
            if (flashBlock == null) flashBlock = new MaterialPropertyBlock();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(flashBlock);
                flashBlock.SetColor(EmissionColorId, Color.black);
                r.SetPropertyBlock(flashBlock);
            }
        }

        // ---------------------------------------------------------------- gizmos

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, meleeRange);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, throwRange);
        }
    }
}
