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
    /// Pole hanging and swinging.
    ///
    /// The character hangs from the bar using a single hang pose (loop it in the Animator). The swing
    /// itself is a pendulum driven by code:
    ///   W / S      pump the swing forward / backward (hold to build momentum)
    ///   Jump       let go and launch toward the next pole in the swing direction, then grab it
    ///
    /// If there is no pole in range, jump falls back to a plain pendulum release.
    ///
    /// The capsule swings along the arc, and the visible model is tilted so its up axis always points
    /// at the bar. That keeps the hands on the bar at any swing angle.
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

        [Tooltip("Angular acceleration (rad/s^2) added while W or S is held. Higher = faster pumping.")]
        [SerializeField] float pumpAcceleration = 7f;

        [Tooltip("Energy bled per second while no key is held, so the hang settles back to rest.")]
        [SerializeField] float idleDamping = 1.0f;

        [Tooltip("Energy bled per second while W or S is held.")]
        [SerializeField] float damping = 0.35f;

        [Tooltip("Angular speed cap, in rad/s.")]
        [SerializeField] float maxOmega = 6f;

        [Tooltip("Swing limit either side of straight down, in degrees.")]
        [Range(30f, 110f)]
        [SerializeField] float maxAngleDegrees = 80f;

        [Tooltip("Safety release if the player never lets go. 0 = hang forever.")]
        [SerializeField] float maxHangTime = 0f;

        [Header("Visual")]
        [Tooltip("Child object holding the character model (the one with the mesh / Animator). It is " +
                 "tilted so the hands stay on the bar. It must be a direct child of the Player root, " +
                 "with its pivot at the feet.")]
        [SerializeField] Transform visual;

        [Tooltip("How much of the rope angle the body follows. 1 = hands stay exactly on the bar.")]
        [Range(0f, 1f)]
        [SerializeField] float tiltAmount = 1f;

        [Header("Jump To Next Pole")]
        [Tooltip("How far to look for the next pole, measured horizontally from this one.")]
        [SerializeField] float nextPoleRange = 9f;

        [Tooltip("Half-angle of the search cone around the swing direction, in degrees.")]
        [Range(5f, 180f)]
        [SerializeField] float nextPoleAngle = 70f;

        [Tooltip("Target horizontal speed of the jump. Flight time is distance / this.")]
        [SerializeField] float jumpFlightSpeed = 8f;

        [Tooltip("Shortest and longest flight time, in seconds.")]
        [SerializeField] float minFlightTime = 0.35f;
        [SerializeField] float maxFlightTime = 1.1f;

        [Tooltip("Gravity used to compute the jump arc. Set it to match the gravity PlayerNormalMovement " +
                 "applies in the air, or the arc will land short or long of the next bar.")]
        [SerializeField] float launchGravity = 25f;

        [Header("Fallback Release (no pole in range)")]
        [Tooltip("How much of the pendulum speed is kept on release.")]
        [SerializeField] float releaseBoost = 1.2f;

        [Tooltip("Upward kick added on release.")]
        [SerializeField] float releaseLift = 4f;

        [Header("Animator")]
        [SerializeField] string swingParameter = "Swing";
        [SerializeField] string swingEndParameter = "SwingEnd";

        // the bar we just let go of - refused until we land, so a release does not snap straight back
        static SwingPole s_blocked;
        static float cooldownUntil;

        static readonly Collider[] Buffer = new Collider[16];

        SwingPole pole;
        Vector3 planeAlong;
        float angle;
        float omega;
        float elapsed;
        bool jumpHeldPrev;
        bool released;

        // the visual's local rotation before any tilt, restored when the swing ends
        Quaternion visualRest = Quaternion.identity;
        bool visualCached;

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

            // the bar we just left must not catch us again
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
            omega = Mathf.Clamp(omega, -maxOmega, maxOmega);

            // Start with jump already counted as held, so a jump button still down from the leap that
            // got us here is not read as a fresh press.
            jumpHeldPrev = CharacterActions.jump.value;
            CharacterActor.alwaysNotGrounded = true;

            if (visual != null)
            {
                visualRest = visual.localRotation;
                visualCached = true;
            }

            if (IsAnimatorValid())
            {
                // clear a stale SwingEnd so the hang is not cut short
                CharacterActor.Animator.ResetTrigger(swingEndParameter);
                CharacterActor.Animator.SetTrigger(swingParameter);
            }
        }

        public override void UpdateBehaviour(float dt)
        {
            if (pole == null) return;

            elapsed += dt;

            // W / S pump the swing. Forward is the swing direction chosen on entry.
            float input = CharacterActions.movement.value.y;
            bool pumping = Mathf.Abs(input) > 0.1f;

            // pendulum: alpha = -(g / L) * sin(theta)
            float g = Mathf.Abs(Physics.gravity.y);
            omega += -(g / ropeLength) * Mathf.Sin(angle) * dt;

            if (pumping)
                omega += input * pumpAcceleration * dt;

            float bleed = pumping ? damping : idleDamping;
            omega *= Mathf.Clamp01(1f - bleed * dt);
            omega = Mathf.Clamp(omega, -maxOmega, maxOmega);
            angle += omega * dt;

            float limit = maxAngleDegrees * Mathf.Deg2Rad;
            if (angle > limit) { angle = limit; omega = Mathf.Min(omega, 0f); }
            if (angle < -limit) { angle = -limit; omega = Mathf.Max(omega, 0f); }

            Vector3 offset = (Vector3.down * Mathf.Cos(angle) + planeAlong * Mathf.Sin(angle)) * ropeLength;
            Vector3 wanted = pole.Point + offset;

            // Drive with velocity rather than Teleport, so CCP's collision resolution stays clean.
            Vector3 delta = wanted - CharacterActor.transform.position;
            CharacterActor.Velocity = Vector3.ClampMagnitude(delta / Mathf.Max(dt, 0.0001f), maxTrackSpeed);

            // face along the swing so the hang pose looks right on the bar
            CharacterActor.transform.rotation = Quaternion.Slerp(
                CharacterActor.transform.rotation,
                Quaternion.LookRotation(planeAlong),
                10f * dt);

            ApplyTilt();
        }

        /// <summary>
        /// Tilts the visible model so its up axis points from the character toward the bar. The capsule
        /// swings along the arc but stays upright; without this the hands slide away from the bar as the
        /// swing angle grows.
        /// </summary>
        void ApplyTilt()
        {
            if (visual == null || !visualCached) return;

            // direction from the character to the bar, inside the swing plane
            Vector3 towardBar = Vector3.up * Mathf.Cos(angle) - planeAlong * Mathf.Sin(angle);
            Quaternion lean = Quaternion.FromToRotation(Vector3.up, towardBar);
            lean = Quaternion.Slerp(Quaternion.identity, lean, tiltAmount);

            // apply the lean in world space on top of the actor's rotation
            Quaternion root = CharacterActor.transform.rotation;
            visual.localRotation = Quaternion.Inverse(root) * lean * root * visualRest;
        }

        void ResetTilt()
        {
            if (visual != null && visualCached)
                visual.localRotation = visualRest;
        }

        public override void CheckExitTransition()
        {
            bool jumpHeld = CharacterActions.jump.value;
            bool jumpPressed = jumpHeld && !jumpHeldPrev;
            jumpHeldPrev = jumpHeld;   // must be refreshed every frame or jumpPressed never fires again

            bool timedOut = maxHangTime > 0f && elapsed >= maxHangTime;

            if (released || pole == null || jumpPressed || timedOut)
            {
                if (!released && pole != null) Release();
                CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
            }
        }

        // ---------------------------------------------------------------- jump / release

        /// <summary>
        /// The nearest other pole inside a cone around the swing direction. The direction follows the
        /// held key if there is one, otherwise the way the pendulum is currently moving.
        /// </summary>
        SwingPole FindNextPole()
        {
            float input = CharacterActions.movement.value.y;
            Vector3 dir;
            if (Mathf.Abs(input) > 0.3f)
                dir = input > 0f ? planeAlong : -planeAlong;
            else
                dir = omega >= 0f ? planeAlong : -planeAlong;

            SwingPole best = null;
            float bestDist = float.MaxValue;

            foreach (var candidate in UnityEngine.Object.FindObjectsByType<SwingPole>(FindObjectsSortMode.None))
            {
                if (candidate == null || candidate == pole) continue;

                Vector3 to = candidate.Point - pole.Point;
                to.y = 0f;
                float flat = to.magnitude;
                if (flat < 1f || flat > nextPoleRange) continue;

                if (Vector3.Angle(dir, to / flat) > nextPoleAngle) continue;

                if (flat < bestDist) { bestDist = flat; best = candidate; }
            }

            return best;
        }

        void Release()
        {
            released = true;

            // refuse this bar until we land, otherwise the release instantly re-grabs
            s_blocked = pole;
            cooldownUntil = Time.time + cooldown;

            SwingPole next = FindNextPole();

            if (next != null)
            {
                // ballistic launch to the position the character will hang at on the next bar
                Vector3 hangPoint = next.Point + Vector3.down * ropeLength;
                Vector3 d = hangPoint - CharacterActor.transform.position;
                Vector3 flat = new Vector3(d.x, 0f, d.z);

                float flight = Mathf.Clamp(flat.magnitude / Mathf.Max(0.1f, jumpFlightSpeed),
                                           minFlightTime, maxFlightTime);

                Vector3 velocity = flat / flight;
                velocity.y = (d.y + 0.5f * launchGravity * flight * flight) / flight;
                CharacterActor.Velocity = velocity;
            }
            else
            {
                // nothing to aim at: plain pendulum release
                Vector3 tangent = -Vector3.down * Mathf.Sin(angle) + planeAlong * Mathf.Cos(angle);
                float speed = omega * ropeLength;

                Vector3 velocity = tangent * speed * releaseBoost;
                velocity.y += releaseLift;
                CharacterActor.Velocity = velocity;
            }

            if (IsAnimatorValid()) CharacterActor.Animator.SetTrigger(swingEndParameter);
        }

        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            CharacterActor.alwaysNotGrounded = false;

            ResetTilt();

            if (IsAnimatorValid())
                CharacterActor.Animator.ResetTrigger(swingParameter);

            pole = null;
            angle = 0f;
            omega = 0f;
            elapsed = 0f;
        }

        public override string GetInfo()
        {
            return "Pole hang: W/S pump the swing, Jump launches to the next pole in range.";
        }
    }
}