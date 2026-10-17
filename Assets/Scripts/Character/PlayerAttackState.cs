using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// One hit in a combo: the clip the animator will play, the trigger that starts it, when the
    /// next hit may interrupt it, and how fast to play it.
    /// </summary>
    [System.Serializable]
    public class ComboStep
    {
        [Tooltip("The clip the matching animator state plays. Only used for its length.")]
        public AnimationClip clip;

        [Tooltip("Animator trigger that starts this hit. Must match the controller.")]
        public string trigger = "";

        [Tooltip("Fraction of the clip after which the next hit may cancel this one. " +
                 "Set it just after the clip's impact frame so a chain never cuts the hit.")]
        [Range(0f, 1.2f)]
        public float cancelFraction = 0.5f;

        [Tooltip("Playback speed. Must match the animator state's Speed or the timing will drift.")]
        [Min(0.1f)]
        public float playbackSpeed = 1f;

        [Header("Damage")]
        [Tooltip("Fraction of the clip at which the hit lands. Fires once, when elapsed crosses it. " +
                 "Set it at the clip's real impact frame or the damage reads as out of sync with the swing.")]
        [Range(0f, 1f)]
        public float hitFraction = 0.4f;

        [Tooltip("Damage dealt by this hit.")]
        [Min(0f)]
        public float damage = 12f;

        [Tooltip("Radius of the damage sweep, centred in front of the chest.")]
        [Min(0.1f)]
        public float hitRadius = 1.1f;

        [Tooltip("How far in front of the character the sweep is centred.")]
        [Min(0f)]
        public float hitReach = 1.3f;

        [Tooltip("Height above the character's feet at which the sweep sits.")]
        [Min(0f)]
        public float hitHeight = 1.1f;

        public float Duration
        {
            get { return clip == null ? 0.5f : clip.length / Mathf.Max(0.1f, playbackSpeed); }
        }
    }

    /// <summary>
    /// Combo-driven melee. Entered from PlayerNormalMovement when Attack (LMB) or Kick (RMB) is pressed.
    ///
    /// LMB walks the punch combo, RMB walks the kick combo. Within a hit, pressing the same button after
    /// that hit's cancel fraction interrupts it into the next hit. After a hit ends there is a short
    /// grace window to still chain; miss it and the combo resets to hit one.
    ///
    /// The clips are "attack while running" animations with long recoveries (impact around a third of
    /// the way in), so the cancel windows are what make the combo feel fast rather than the clip length.
    /// </summary>
    public class PlayerAttackState : CharacterState
    {
        [Header("Animator")]
        [Tooltip("Trigger that hands the animator back to Locomotion. Each attack state exits on this, so " +
                 "Locomotion is never displayed while the attack still owns the locomotion parameters.")]
        [SerializeField] string comboEndParameter = "ComboEnd";

        [Header("Combos")]
        [SerializeField] ComboStep[] punchCombo = new ComboStep[3];
        [SerializeField] ComboStep[] kickCombo = new ComboStep[3];

        [Tooltip("How long after a hit ends you can still press to continue the combo.")]
        [Min(0f)]
        [SerializeField] float comboResetTime = 0.35f;

        [Header("Aiming")]
        [Tooltip("Rotate the attack toward the nearest enemy within this range. Without it, swinging " +
                 "while running past a target sends the hit into empty air.")]
        [Min(0f)]
        [SerializeField] float softAimRange = 3.5f;

        [Tooltip("Half-angle of the aim cone, in degrees. Wider snaps more aggressively.")]
        [Range(0f, 180f)]
        [SerializeField] float softAimAngle = 60f;

        [Header("Movement")]
        [Tooltip("Fraction of the entry speed kept during an attack.")]
        [Range(0f, 1f)]
        [SerializeField] float speedRetention = 0.2f;

        [Tooltip("Forward push applied at the start of each hit.")]
        [Min(0f)]
        [SerializeField] float lungeSpeed = 1.5f;

        ComboStep[] activeCombo;
        int index;
        float elapsed;
        float stepDuration;
        float initialLunge;
        Vector3 attackDirection;
        bool finished;
        bool endAnnounced;
        bool lastAttackHeld;
        bool lastKickHeld;

        // one damage sweep per combo step, latched so a step cannot hit twice
        bool hitFired;

        Health selfHealth;

        /// <summary>The attacker's own Health, for the team filter. Cached; the component is on the actor root.</summary>
        Health SelfHealth
        {
            get
            {
                if (selfHealth == null && CharacterActor != null)
                    selfHealth = CharacterActor.GetComponent<Health>();
                return selfHealth;
            }
        }

        public override bool CheckEnterTransition(CharacterState fromState)
        {
            return CharacterActor.IsGrounded;
        }

        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            // PlayerNormalMovement saw the actual rising edge and recorded which button caused it.
            // It cannot be inferred here from CharacterActions.attack.Started, because that flag latches
            // true forever after the first press (CharacterBrain never resets it per frame).
            PlayerNormalMovement normal = CharacterStateController.GetState<PlayerNormalMovement>() as PlayerNormalMovement;
            bool kick = normal != null
                ? normal.PendingComboIsKick
                : CharacterActions.kick.value && !CharacterActions.attack.value;

            activeCombo = kick ? kickCombo : punchCombo;
            if (activeCombo == null || activeCombo.Length == 0)
                activeCombo = punchCombo;

            lastAttackHeld = CharacterActions.attack.value;
            lastKickHeld = CharacterActions.kick.value;

            index = 0;
            finished = false;
            endAnnounced = false;
            attackDirection = CharacterActor.Forward;

            Vector3 planar = CharacterActor.PlanarVelocity;
            if (planar.sqrMagnitude > 0.0001f)
                attackDirection = planar.normalized;

            ApplySoftAim();

            BeginStep(0);
        }

        /// <summary>
        /// Snaps the attack direction onto the nearest hostile inside a forward cone.
        ///
        /// The direction is otherwise taken from movement, which is right when you are charging
        /// straight at something and wrong the moment you run past it - you punch where you are
        /// going rather than where the target is, and the hit misses. This nudges both the facing
        /// and the hitbox together, so the character turns into the swing.
        /// </summary>
        void ApplySoftAim()
        {
            if (softAimRange <= 0f) return;

            Health mine = SelfHealth;
            Health.Team side = mine != null ? mine.Side : Health.Team.Player;

            Vector3 feet = CharacterActor.transform.position;
            Transform best = null;
            float bestDistance = float.MaxValue;

            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (candidate == null || !candidate.IsAlive || candidate.Side == side) continue;

                Vector3 to = candidate.transform.position - feet;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) continue;
                if (to.sqrMagnitude > softAimRange * softAimRange) continue;

                if (Vector3.Angle(attackDirection, to.normalized) > softAimAngle) continue;

                float d = to.sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = candidate.transform; }
            }

            if (best == null) return;

            Vector3 aim = best.position - feet;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.0001f) return;

            attackDirection = aim.normalized;
            CharacterActor.transform.rotation = Quaternion.LookRotation(attackDirection);
        }

        void BeginStep(int i)
        {
            index = i;
            elapsed = 0f;
            hitFired = false;

            ComboStep step = activeCombo[i];
            stepDuration = step.Duration;
            initialLunge = Mathf.Max(lungeSpeed, CharacterActor.PlanarVelocity.magnitude * speedRetention);

            if (IsAnimatorValid() && !string.IsNullOrEmpty(step.trigger))
                CharacterActor.Animator.SetTrigger(step.trigger);
        }

        public override void UpdateBehaviour(float dt)
        {
            elapsed += dt;

            // detect our own rising edges so this works regardless of CCP's Started semantics
            bool attackHeld = CharacterActions.attack.value;
            bool kickHeld = CharacterActions.kick.value;
            bool attackPressed = attackHeld && !lastAttackHeld;
            bool kickPressed = kickHeld && !lastKickHeld;
            lastAttackHeld = attackHeld;
            lastKickHeld = kickHeld;

            bool isPunchCombo = activeCombo == punchCombo;
            bool advancePressed = isPunchCombo ? attackPressed : kickPressed;

            ComboStep step = activeCombo[index];
            float fraction = stepDuration > 0f ? elapsed / stepDuration : 1f;

            // the damage window: fires once, on the frame the clip reaches its impact fraction
            if (!hitFired && fraction >= step.hitFraction)
            {
                hitFired = true;
                DealHit(step);
            }

            if (advancePressed && fraction >= step.cancelFraction && index < activeCombo.Length - 1)
            {
                BeginStep(index + 1);
            }
            else if (fraction >= 1f && elapsed >= stepDuration + comboResetTime)
            {
                Finish();
            }

            // keep the lunge but bleed it off linearly across the hit, preserving gravity
            float lungeNow = fraction >= 1f ? 0f : initialLunge * (1f - Mathf.Clamp01(fraction));

            Vector3 velocity = attackDirection * lungeNow;
            velocity.y = CharacterActor.Velocity.y;
            CharacterActor.Velocity = velocity;
        }

        /// <summary>
        /// Fires the sweep for one combo step. Centred at chest height in front of the character,
        /// along the same frozen direction the lunge uses, so the hitbox follows the animation.
        /// </summary>
        void DealHit(ComboStep step)
        {
            if (step == null || step.damage <= 0f) return;

            Health mine = SelfHealth;
            Health.Team side = mine != null ? mine.Side : Health.Team.Player;

            Vector3 origin = CharacterActor.transform.position
                             + Vector3.up * step.hitHeight
                             + attackDirection * step.hitReach;

            HitResolver.Sphere(origin, step.hitRadius, step.damage, side,
                               CharacterActor.gameObject, origin);
        }

        /// <summary>
        /// The locomotion animator parameters (PlanarSpeed, Grounded, HorizontalAxis...) are written by
        /// NormalMovement's Pre/PostCharacterSimulation, which only run while NormalMovement is the CURRENT
        /// state. During an attack it is not, so those parameters freeze at whatever the character was doing
        /// beforehand - and because the attack blends back to Locomotion on its own exit-time transition
        /// before this state ends, Locomotion would show that stale pose (e.g. the sprint it was mid-way
        /// through) for the whole grace window. Keep them fresh by delegating to NormalMovement.
        /// </summary>
        public override void PreCharacterSimulation(float dt)
        {
            PlayerNormalMovement normal = CharacterStateController.GetState<PlayerNormalMovement>() as PlayerNormalMovement;
            if (normal != null)
                normal.PreCharacterSimulation(dt);
        }

        public override void PostCharacterSimulation(float dt)
        {
            PlayerNormalMovement normal = CharacterStateController.GetState<PlayerNormalMovement>() as PlayerNormalMovement;
            if (normal != null)
                normal.PostCharacterSimulation(dt);
        }

        void Finish()
        {
            // Hand the animator back explicitly. The attack states deliberately have no exit-time
            // transition: if the animator were allowed to leave early it would sit in Locomotion while
            // this state still owned the locomotion parameters, and could show a stale pose.
            finished = true;
            AnnounceEnd();
        }

        void AnnounceEnd()
        {
            if (endAnnounced)
                return;

            endAnnounced = true;

            if (IsAnimatorValid() && !string.IsNullOrEmpty(comboEndParameter))
                CharacterActor.Animator.SetTrigger(comboEndParameter);
        }

        public override void CheckExitTransition()
        {
            if (!finished)
                return;

            AnnounceEnd();
            CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
        }

        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            AnnounceEnd();
        }

        public override string GetInfo()
        {
            return "Combo melee: LMB = punch combo, RMB = kick combo. Press again after a hit's cancel " +
                   "fraction to chain; miss the grace window and the combo restarts from hit one.";
        }
    }
}
