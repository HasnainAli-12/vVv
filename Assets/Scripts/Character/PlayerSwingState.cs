using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// Marks a horizontal bar as swingable. Put on the pole object itself.
    /// </summary>
    public class SwingPole : MonoBehaviour
    {
        /// <summary>World position of the bar's centre line.</summary>
        public Vector3 Point { get { return transform.position; } }

        /// <summary>Horizontal axis the bar runs along. The swing plane is perpendicular to it.
        /// The course builds its bars long in local Z, so forward is the bar's length.</summary>
        public Vector3 Axis
        {
            get
            {
                Vector3 a = transform.forward;
                a.y = 0f;
                return a.sqrMagnitude < 0.0001f ? Vector3.right : a.normalized;
            }
        }
    }

    /// <summary>
    /// Pole swinging.
    ///
    /// The pack has no swing animation, so this uses Wall_Climb_Hold as the hang pose and the wall-run
    /// fall clip on release - hanging from a bar reads close enough to hanging from a ledge.
    ///
    /// The swing is a real pendulum: angle integrated from gravity over the rope length, with the
    /// player's entry speed converted into angular velocity, so momentum carries through the chain of
    /// poles rather than resetting at each one. Releasing converts the angular velocity back into
    /// tangential velocity plus a small upward kick.
    /// </summary>
    public class PlayerSwingState : CharacterState
    {
        [Header("Detection")]
        [Tooltip("How far from the character's centre to look for a bar.")]
        [SerializeField] float reach = 1.9f;

        [Tooltip("Minimum speed to grab a bar - stops you latching on while barely moving.")]
        [SerializeField] float minEntrySpeed = 2.5f;

        [Header("Swing")]
        [Tooltip("Distance from the bar down to the character's origin while hanging. Must equal the " +
                 "clip's hand height above the origin, or the hands float above or below the bar.")]
        [SerializeField] float ropeLength = 1.95f;

        [Tooltip("Seconds after release before another bar may be grabbed.")]
        [SerializeField] float cooldown = 0.25f;

        [Tooltip("Cap on the correction speed used to track the pendulum.")]
        [SerializeField] float maxTrackSpeed = 28f;

        // the bar we just let go of - refused until we land, so a release does not snap straight back
        static SwingPole s_blocked;
        static float cooldownUntil;

        [Tooltip("Undamped pendulums swing forever; this bleeds a little energy per second.")]
        [SerializeField] float damping = 0.35f;

        [Tooltip("Angular damping is capped here so long chains still build momentum.")]
        [SerializeField] float maxOmega = 6f;

        [Tooltip("How much of the pendulum speed is kept on release.")]
        [SerializeField] float releaseBoost = 1.2f;

        [Tooltip("Upward kick added on release, so leaving a bar arcs up rather than dropping.")]
        [SerializeField] float releaseLift = 4f;

        [Tooltip("Safety release if the player never lets go.")]
        [SerializeField] float maxDuration = 4f;

        [Header("Animator")]
        [SerializeField] string swingParameter = "Swing";
        [SerializeField] string swingEndParameter = "SwingEnd";

        static readonly Collider[] Buffer = new Collider[16];

        SwingPole pole;
        Vector3 planeAlong;
        float angle;
        float omega;
        float elapsed;
        bool jumpHeldPrev;
        bool released;

        // ---------------------------------------------------------------- probe

        /// <summary>Finds the nearest swingable bar within reach.</summary>
        public static SwingPole FindPole(CharacterActor actor, float reach)
        {
            if (actor == null) return null;

            int count = Physics.OverlapSphereNonAlloc(actor.transform.position + Vector3.up * 1.2f,
                                                      reach, Buffer, ~0, QueryTriggerInteraction.Collide);
            SwingPole best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (Buffer[i] == null) continue;
                var p = Buffer[i].GetComponentInParent<SwingPole>();
                if (p == null) continue;

                float d = Vector3.Distance(actor.transform.position, p.Point);
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        // ---------------------------------------------------------------- state

        public override bool CheckEnterTransition(CharacterState fromState)
        {
            // landing clears the re-grab block, so a released bar is only refused while airborne
            if (CharacterActor.IsGrounded)
            {
                s_blocked = null;
                cooldownUntil = 0f;
                return false;
            }

            if (Time.time < cooldownUntil) return false;
            if (CharacterActor.PlanarVelocity.magnitude < minEntrySpeed) return false;

            SwingPole p = FindPole(CharacterActor, reach);
            if (p == null) return false;

            // the bar we just left must not catch us again, or releasing does nothing and you can
            // never reach the next one
            if (s_blocked != null && p == s_blocked) return false;

            return true;
        }

        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            pole = FindPole(CharacterActor, reach);
            if (pole == null) { released = true; return; }

            elapsed = 0f;
            released = false;
            angle = 0f;

            // swing plane runs perpendicular to the bar, chosen to match how we arrived
            Vector3 cross = Vector3.Cross(Vector3.up, pole.Axis);
            if (cross.sqrMagnitude < 0.0001f) cross = Vector3.forward;
            cross.Normalize();

            Vector3 planar = CharacterActor.PlanarVelocity;
            planeAlong = Vector3.Dot(planar, cross) >= 0f ? cross : -cross;

            // entry speed becomes angular velocity: v = omega * r
            omega = Vector3.Dot(planar, planeAlong) / Mathf.Max(0.1f, ropeLength);

            jumpHeldPrev = CharacterActions.jump.value;
            CharacterActor.alwaysNotGrounded = true;

            if (IsAnimatorValid()) CharacterActor.Animator.SetTrigger(swingParameter);
        }

        public override void UpdateBehaviour(float dt)
        {
            if (pole == null) return;

            elapsed += dt;

            // pendulum: alpha = -(g / L) * sin(theta)
            float g = Mathf.Abs(Physics.gravity.y);
            omega += -(g / ropeLength) * Mathf.Sin(angle) * dt;
            omega *= Mathf.Clamp01(1f - damping * dt);
            omega = Mathf.Clamp(omega, -maxOmega, maxOmega);
            angle += omega * dt;

            // keep the swing within sane limits
            angle = Mathf.Clamp(angle, -Mathf.PI * 0.55f, Mathf.PI * 0.55f);

            Vector3 offset = (Vector3.down * Mathf.Cos(angle) + planeAlong * Mathf.Sin(angle)) * ropeLength;
            Vector3 wanted = pole.Point + offset;

            // Drive with velocity rather than Teleport. Teleporting every frame fights CCP's own
            // collision resolution and reads as jitter; a velocity lets the capsule resolve cleanly.
            Vector3 delta = wanted - CharacterActor.transform.position;
            CharacterActor.Velocity = Vector3.ClampMagnitude(delta / Mathf.Max(dt, 0.0001f), maxTrackSpeed);
        }

        public override void CheckExitTransition()
        {
            bool jumpHeld = CharacterActions.jump.value;
            bool jumpPressed = jumpHeld && !jumpHeldPrev;

            if (released || pole == null || jumpPressed || elapsed >= maxDuration)
            {
                if (!released && pole != null) Release();
                CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
            }
        }

        void Release()
        {
            released = true;

            // refuse this bar until we land, otherwise the release instantly re-grabs and you can
            // never get to the next pole in the chain
            s_blocked = pole;
            cooldownUntil = Time.time + cooldown;

            // tangent at the current angle: d/dt of the offset
            Vector3 tangent = -Vector3.down * Mathf.Sin(angle) + planeAlong * Mathf.Cos(angle);
            float speed = omega * ropeLength;

            Vector3 velocity = tangent * speed * releaseBoost;
            velocity.y += releaseLift;
            CharacterActor.Velocity = velocity;

            if (IsAnimatorValid()) CharacterActor.Animator.SetTrigger(swingEndParameter);
        }

        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            CharacterActor.alwaysNotGrounded = false;
            pole = null;
            angle = 0f;
            omega = 0f;
            elapsed = 0f;
        }

        public override string GetInfo()
        {
            return "Pole swing: pendulum integration with momentum carried through on release.";
        }
    }
}
