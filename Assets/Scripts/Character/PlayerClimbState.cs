using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// Ledge climb / vault.
    ///
    /// Detects a vertical face in front with a standable top inside a height band, then lifts the
    /// character onto it. Used for the railings and containers along the course, and for any ledge
    /// that is too tall to simply jump.
    ///
    /// CCP's capsule cannot be pushed up a vertical face - its collision stops it dead - so the rise
    /// is driven with Teleport rather than velocity. The climb animation covers the move, which is
    /// also how most games do a mantle.
    /// </summary>
    public class PlayerClimbState : CharacterState
    {
        [Header("Detection")]
        [Tooltip("How far in front to look for a face to climb.")]
        [SerializeField] float reach = 1.3f;

        [Tooltip("Lowest ledge, measured from the feet, that counts as a climb. Below this you should just step or jump.")]
        [SerializeField] float minLedgeHeight = 0.65f;

        [Tooltip("Highest ledge that can be climbed.")]
        [SerializeField] float maxLedgeHeight = 2.4f;

        [SerializeField] LayerMask ledgeMask = ~0;

        [Header("Climb")]
        [Tooltip("Seconds the lift takes. Should roughly match the animator state's effective length.")]
        [SerializeField] float climbTime = 0.95f;

        [Tooltip("How far onto the ledge to land, so the capsule clears the lip.")]
        [SerializeField] float landingInset = 0.4f;

        [Tooltip("Seconds after a climb before another may start. Without this the state re-enters the moment it exits - you climb the same ledge over and over and ratchet into the sky.")]
        [SerializeField] float cooldown = 0.6f;

        static float cooldownUntil;

        [Header("Animator")]
        [SerializeField] string climbParameter = "Climb";

        Vector3 startPosition;
        Vector3 targetPosition;
        float progress;
        bool done;

        // ---------------------------------------------------------------- detection

        /// <summary>
        /// Finds a climbable ledge in front of the character. Requires a vertical face, a standable
        /// top above it, and that top to sit inside the height band.
        /// </summary>
        public static bool ProbeLedge(CharacterActor actor, float reach, float minHeight, float maxHeight,
                                      float inset, LayerMask mask, out Vector3 target)
        {
            target = Vector3.zero;
            if (actor == null) return false;

            Vector3 feet = actor.transform.position;
            Vector3 forward = actor.transform.forward;

            RaycastHit face;
            if (!Physics.Raycast(feet + Vector3.up * 0.6f, forward, out face, reach, mask, QueryTriggerInteraction.Ignore))
                return false;

            if (Mathf.Abs(Vector3.Dot(face.normal, Vector3.up)) > 0.35f) return false;   // not a vertical face

            // Never climb a character. An enemy's capsule is ~2 m tall with a flat top, so it passes
            // every geometric test below and the player would mantle straight up the enemy's body.
            if (face.collider.GetComponentInParent<Health>() != null) return false;

            // Cast down from a point INSIDE the ledge to find its top face. This was subtracting
            // forward, which put the probe back on the player's side of the wall - so it hit the floor
            // the player was already standing on instead of the ledge.
            Vector3 above = face.point + Vector3.up * (maxHeight + 0.6f) + forward * 0.3f;
            RaycastHit top;
            if (!Physics.Raycast(above, Vector3.down, out top, maxHeight + 1.2f, mask, QueryTriggerInteraction.Ignore))
                return false;

            if (Mathf.Abs(Vector3.Dot(top.normal, Vector3.up)) < 0.7f) return false;      // top is not flat

            float relative = top.point.y - feet.y;
            if (relative < minHeight || relative > maxHeight) return false;

            target = top.point + forward * inset + Vector3.up * 0.05f;
            return true;
        }

        // ---------------------------------------------------------------- state

        public override bool CheckEnterTransition(CharacterState fromState)
        {
            if (Time.time < cooldownUntil) return false;

            Vector3 t;
            if (!ProbeLedge(CharacterActor, reach, minLedgeHeight, maxLedgeHeight, landingInset, ledgeMask, out t))
                return false;

            // only when we are actually trying to move into it, so idle players do not climb walls
            return CharacterActor.PlanarVelocity.magnitude > 0.5f;
        }

        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            Vector3 t;
            if (!ProbeLedge(CharacterActor, reach, minLedgeHeight, maxLedgeHeight, landingInset, ledgeMask, out t))
            {
                done = true;
                return;
            }

            startPosition = CharacterActor.transform.position;
            targetPosition = t;
            progress = 0f;
            done = false;

            CharacterActor.alwaysNotGrounded = true;

            if (IsAnimatorValid())
                CharacterActor.Animator.SetTrigger(climbParameter);
        }

        public override void UpdateBehaviour(float dt)
        {
            progress += dt;
            float k = climbTime > 0f ? Mathf.Clamp01(progress / climbTime) : 1f;

            // ease out, so the last part of the climb settles onto the ledge rather than snapping
            float eased = Mathf.SmoothStep(0f, 1f, k);
            CharacterActor.Teleport(Vector3.Lerp(startPosition, targetPosition, eased));

            if (k >= 1f) done = true;
        }

        public override void CheckExitTransition()
        {
            if (done) CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
        }

        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            CharacterActor.alwaysNotGrounded = false;
            progress = 0f;
            done = false;

            // stop the state immediately re-entering on the ledge we just climbed
            cooldownUntil = Time.time + cooldown;
        }

        public override string GetInfo()
        {
            return "Ledge climb: detects a standable top in front and lifts the character onto it.";
        }
    }
}
