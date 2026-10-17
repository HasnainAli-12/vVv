using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// Sprint slide. Enqueued by PlayerNormalMovement when the Crouch action is pressed while
    /// moving at or above sprint speed.
    ///
    /// It shrinks the collision capsule so the character physically fits under low obstacles,
    /// carries the sprint momentum with a decaying curve, then hands back to PlayerNormalMovement.
    ///
    /// The capsule is deliberately NOT restored on exit. NormalMovement calls StandUp() every frame,
    /// which goes through CharacterActor.CheckAndInterpolateHeight(): that grows the capsule back
    /// smoothly and simply refuses while something is overhead. So sliding under an object and
    /// popping back up on the far side is handled by CCP's own collision checks.
    /// </summary>
    public class PlayerSlideState : CharacterState
    {
        [Header("Animator")]
        [SerializeField] string slideParameter = "Slide";
        [SerializeField] string slideEndParameter = "SlideEnd";

        [Header("Entry")]
        [Tooltip("Minimum planar speed needed to start a slide. Sprint speed is 8, walk is 3.2.")]
        [Min(0f)]
        [SerializeField] float minEntrySpeed = 6f;

        [Header("Slide")]
        [Tooltip("Capsule height during the slide, as a fraction of the character's default height.")]
        [Range(0.2f, 1f)]
        [SerializeField] float slideHeightRatio = 0.5f;

        [Min(0.05f)]
        [SerializeField] float slideDuration = 0.9f;

        [Tooltip("Slide speed across the slide duration. 1 = entry speed, 0 = stopped.")]
        [SerializeField] AnimationCurve speedOverTime = AnimationCurve.Linear(0f, 1f, 1f, 0.12f);

        [Tooltip("Grace period before the slide can be cancelled by losing speed or leaving the ground.")]
        [SerializeField] float minSlideTime = 0.15f;

        float elapsed;
        Vector3 slideDirection;
        float entrySpeed;
        bool endAnnounced;

        public override bool CheckEnterTransition(CharacterState fromState)
        {
            // must be grounded and genuinely sprinting
            return CharacterActor.IsGrounded &&
                   CharacterActor.PlanarVelocity.magnitude >= minEntrySpeed;
        }

        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            // once per slide, at the entry - not looped while sliding
            Sfx.Play("slide", CharacterActor.transform.position, 0.7f);

            elapsed = 0f;
            endAnnounced = false;

            Vector3 planar = CharacterActor.PlanarVelocity;
            slideDirection = planar.sqrMagnitude > 0.0001f ? planar.normalized : CharacterActor.Forward;
            entrySpeed = Mathf.Max(planar.magnitude, minEntrySpeed);

            Vector2 size = CharacterActor.DefaultBodySize;
            CharacterActor.SetBodySize(new Vector2(size.x, size.y * slideHeightRatio));

            if (IsAnimatorValid())
                CharacterActor.Animator.SetTrigger(slideParameter);
        }

        public override void UpdateBehaviour(float dt)
        {
            elapsed += dt;

            float t = slideDuration > 0f ? Mathf.Clamp01(elapsed / slideDuration) : 1f;
            float speed = entrySpeed * speedOverTime.Evaluate(t);

            Vector3 velocity = slideDirection * speed;

            // keep whatever vertical velocity the actor has so gravity and ground snapping still apply
            velocity.y = CharacterActor.Velocity.y;

            CharacterActor.Velocity = velocity;
        }

        public override void CheckExitTransition()
        {
            bool done = elapsed >= slideDuration;

            if (!done && elapsed >= minSlideTime)
            {
                // ran off an edge, or hit something that stopped the slide dead
                if (!CharacterActor.IsGrounded || CharacterActor.PlanarVelocity.sqrMagnitude < 0.09f)
                    done = true;
            }

            if (!done)
                return;

            AnnounceEnd();
            CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
        }

        void AnnounceEnd()
        {
            if (endAnnounced)
                return;

            endAnnounced = true;

            if (IsAnimatorValid())
                CharacterActor.Animator.SetTrigger(slideEndParameter);
        }

        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            AnnounceEnd();
        }

        /// <summary>
        /// Keeps the locomotion animator parameters fresh. They are written by NormalMovement's
        /// Pre/PostCharacterSimulation, which only run while NormalMovement is the current state - but the
        /// animator blends back to Locomotion on its own exit-time transition while this state is still
        /// current, so without this Locomotion would briefly show a stale pose.
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

        public override string GetInfo()
        {
            return "Sprint slide: shrinks the capsule so the character fits under low obstacles, " +
                   "carries sprint momentum with a decaying curve.";
        }
    }
}
