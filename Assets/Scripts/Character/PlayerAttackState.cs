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

            BeginStep(0);
        }

        void BeginStep(int i)
        {
            index = i;
            elapsed = 0f;

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
