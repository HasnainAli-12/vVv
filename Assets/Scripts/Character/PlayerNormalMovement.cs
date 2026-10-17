using UnityEngine;
using Lightbug.CharacterControllerPro.Demo;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// NormalMovement with a roll and a sprint slide layered on top.
    ///
    /// This must be used INSTEAD of CCP's NormalMovement. The bundled NormalMovement only enqueues
    /// its own demo states (Dash, JetPack, WallSlide, ...), none of which exist on this character, so
    /// dash input would do nothing. CCP resolves states by class name, and both extra states exit by
    /// enqueuing PlayerNormalMovement by name - so the names are coupled.
    /// </summary>
    public class PlayerNormalMovement : NormalMovement
    {
        /// <summary>
        /// Which combo PlayerAttackState should open with. Set when the attack is enqueued.
        /// </summary>
        public bool PendingComboIsKick { get; private set; }

        bool lastAttack;
        bool lastKick;
        bool lastDash;
        bool lastCrouch;

        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            base.EnterBehaviour(dt, fromState);

            // Snapshot on entry so a button already held from a previous state is not read as a fresh press.
            lastAttack = CharacterActions.attack.value;
            lastKick = CharacterActions.kick.value;
            lastDash = CharacterActions.dash.value;
            lastCrouch = CharacterActions.crouch.value;
        }

        public override void CheckExitTransition()
        {
            // CCP's BoolAction.Started is a LATCHING flag (`Started |= !prev && value`) and CharacterBrain
            // never calls Reset() per frame, so .Started stays true forever after the first press. Track
            // our own rising edges off .value, which IS refreshed every frame.
            bool attackHeld = CharacterActions.attack.value;
            bool kickHeld = CharacterActions.kick.value;
            bool dashHeld = CharacterActions.dash.value;
            bool crouchHeld = CharacterActions.crouch.value;

            bool attackPressed = attackHeld && !lastAttack;
            bool kickPressed = kickHeld && !lastKick;
            bool dashPressed = dashHeld && !lastDash;
            bool crouchPressed = crouchHeld && !lastCrouch;

            lastAttack = attackHeld;
            lastKick = kickHeld;
            lastDash = dashHeld;
            lastCrouch = crouchHeld;

            // attacks get first refusal so melee always feels responsive
            if (attackPressed || kickPressed)
            {
                PendingComboIsKick = kickPressed && !attackPressed;
                CharacterStateController.EnqueueTransition<PlayerAttackState>();
                return;
            }

            if (CharacterActor.IsGrounded && dashPressed)
            {
                CharacterStateController.EnqueueTransition<PlayerRollState>();
                return;
            }

            if (crouchPressed)
            {
                // PlayerSlideState.CheckEnterTransition rejects this unless we are grounded and
                // genuinely sprinting, so walking and pressing crouch does nothing.
                CharacterStateController.EnqueueTransition<PlayerSlideState>();
            }

            // Ledge climb / vault. Offered while moving; the state probes for a standable top in front
            // and rejects the transition if the ledge is outside its height band, so this does not
            // duplicate the detection.
            if (CharacterActor.PlanarVelocity.magnitude > 0.5f)
                CharacterStateController.EnqueueTransition<PlayerClimbState>();

            // Pole swing. Offered while airborne; the state looks for a bar within reach and rejects
            // the transition if there is none. Checked before the wall run so a bar in front of a wall
            // wins, which is what makes pole lines read as a distinct route.
            if (!CharacterActor.IsGrounded && CharacterActor.PlanarVelocity.magnitude >= 2.5f)
                CharacterStateController.EnqueueTransition<PlayerSwingState>();

            // Wall run. Offered whenever we are airborne and quick - the state itself probes for an
            // actual wall and rejects the transition if there is nothing to run on, so this does not
            // need to duplicate the detection.
            if (!CharacterActor.IsGrounded && CharacterActor.PlanarVelocity.magnitude >= 3f)
                CharacterStateController.EnqueueTransition<PlayerWallRunState>();

            base.CheckExitTransition();
        }
    }
}
