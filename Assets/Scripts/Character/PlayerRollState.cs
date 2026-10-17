using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// Roll / dodge state. Enqueued by PlayerNormalMovement when the Dash action starts while grounded.
    /// Fires the "Roll" trigger on the animator and applies a forward velocity that decays over the clip.
    /// </summary>
    public class PlayerRollState : CharacterState
    {
        [Tooltip("Animator trigger parameter name. Must exist on the animator controller.")]
        [SerializeField] string rollParameter = "Roll";

        [Min(0f)]
        [SerializeField] float initialVelocity = 7f;

        [Tooltip("Should match the Roll state's exit time in the animator controller.")]
        [Min(0f)]
        [SerializeField] float duration = 0.9f;

        [SerializeField] AnimationCurve movementCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);

        float cursor;
        Vector3 direction;
        bool finished;

        public override bool CheckEnterTransition(CharacterState fromState)
        {
            return CharacterActor.IsGrounded;
        }

        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            direction = CharacterActor.Forward;
            cursor = 0f;
            finished = false;

            if (IsAnimatorValid())
                CharacterActor.Animator.SetTrigger(rollParameter);
        }

        public override void UpdateBehaviour(float dt)
        {
            float t = duration > 0f ? Mathf.Clamp01(cursor / duration) : 1f;

            Vector3 rollVelocity = direction * (initialVelocity * movementCurve.Evaluate(t));

            // Keep whatever vertical velocity the actor already has so gravity / ground snapping still applies.
            rollVelocity.y = CharacterActor.Velocity.y;

            CharacterActor.Velocity = rollVelocity;

            cursor += dt;

            if (cursor >= duration)
                finished = true;
        }

        public override void CheckExitTransition()
        {
            if (finished)
                CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
        }

        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            finished = false;
            cursor = 0f;
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
            return "Roll/dodge: a burst of forward velocity with a decaying curve, driven by the Dash action.";
        }
    }
}
