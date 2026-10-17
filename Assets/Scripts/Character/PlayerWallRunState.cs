using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// Wall run + wall jump.
    ///
    /// Entered from PlayerNormalMovement when the character is airborne, moving fast enough, and a
    /// vertical wall is detected beside them. While running the character is locked to the wall plane,
    /// gravity is reduced, and the run direction is projected along the wall so entering at an angle
    /// does not kill momentum.
    ///
    /// Jumping mid-run performs a wall jump: the character is launched away from the wall and upward,
    /// which is how the wall-run-only gaps in the level are crossed.
    ///
    /// The side matters because the animation clips are side-specific - a left-wall clip with the wall
    /// on the right reads as the character running through the wall.
    /// </summary>
    public class PlayerWallRunState : CharacterState
    {
        [Header("Detection")]
        [Tooltip("Reach of the sideways probe from the character's centre. Larger = easier to grab.")]
        [SerializeField] float probeDistance = 1.8f;

        [Tooltip("Centre height of the probe band, measured from the character's feet.")]
        [SerializeField] float probeHeight = 1.15f;

        [SerializeField] LayerMask wallMask = ~0;

        [Header("Entry")]
        [Tooltip("Minimum planar speed to be allowed to start a wall run.")]
        [SerializeField] float minEntrySpeed = 3.5f;

        [Header("Run")]
        [Tooltip("Speed the run blends towards along the wall.")]
        [SerializeField] float runSpeed = 8.5f;

        [Tooltip("How quickly the entry speed blends into the run speed.")]
        [SerializeField] float speedBlend = 2.5f;

        [Tooltip("Fraction of normal gravity while on the wall. Lower = longer, flatter run.")]
        [Range(0f, 1f)] [SerializeField] float gravityScale = 0.22f;

        [Tooltip("Upward speed on entering, so the run lifts before it sags. 0 gives a flat run.")]
        [SerializeField] float entryLift = 3.2f;

        [Tooltip("How fast the entry lift decays into reduced gravity.")]
        [SerializeField] float liftDecay = 3.2f;

        [Tooltip("Maximum wall run length in seconds before the character drops off.")]
        [SerializeField] float maxDuration = 2.2f;

        [Tooltip("Gentle push into the wall to hold contact. A big value reads as slamming into it.")]
        [SerializeField] float stickForce = 2.2f;

        [Tooltip("Seconds the wall may be missing before the run ends - stops one missed probe dropping you.")]
        [SerializeField] float wallLostGrace = 0.22f;

        [Tooltip("How quickly the character turns to face the run direction.")]
        [SerializeField] float turnRate = 9f;

        [Tooltip("Short anti-chain gate. Re-grabbing the SAME wall is prevented by the wall block, so this only needs to stop instant ping-ponging between neighbouring walls.")]
        [SerializeField] float cooldown = 0.3f;

        [Tooltip("Camera roll towards the wall, in degrees. The single biggest contributor to how a wall run feels.")]
        [SerializeField] float cameraRoll = 15f;

        [Tooltip("Camera roll lerp speed.")]
        [SerializeField] float cameraRollRate = 6f;

        [Header("Wall jump")]
        [Tooltip("Upward launch. Also sets airtime, so it contributes to jump distance.")]
        [SerializeField] float jumpUpSpeed = 8.5f;

        [Tooltip("Push away from the wall.")]
        [SerializeField] float jumpAwaySpeed = 4f;

        [Tooltip("Forward acceleration ADDED on top of the speed carried into the jump.")]
        [SerializeField] float jumpForwardSpeed = 6f;

        [Tooltip("Ceiling on the resulting forward speed. This is the biggest lever on wall-jump distance.")]
        [SerializeField] float maxForwardSpeed = 13f;

        [Header("Audio")]
        [Tooltip("Looped while on the wall. Placeholder - swap for a dedicated wall-run scrape when you have one.")]
        [SerializeField] string wallRunClip = "slide";
        [SerializeField, Range(0f, 1f)] float wallRunVolume = 0.35f;

        AudioSource wallRunSource;

        [Header("Animator")]
        [SerializeField] string runLeftParameter = "WallRunL";
        [SerializeField] string runRightParameter = "WallRunR";
        [SerializeField] string jumpLeftParameter = "WallJumpL";
        [SerializeField] string jumpRightParameter = "WallJumpR";

        [SerializeField] string groundedParameter = "Grounded";
        [SerializeField] string verticalSpeedParameter = "VerticalSpeed";
        [SerializeField] string planarSpeedParameter = "PlanarSpeed";

        static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        float elapsed;
        float lift;
        float speed;
        float wallLostTime;
        bool jumped;
        bool jumpHeldPrev;
        Vector3 runDirection;
        Vector3 wallNormal;
        bool wallOnRight;
        float entrySpeed;

        /// <summary>
        /// Read by the camera for the wall-run tilt and FOV kick. Static because the camera holds no
        /// reference to the character's state machine, and only one player exists.
        /// </summary>
        public static bool IsActive { get; private set; }

        /// <summary>Signed camera roll in degrees. Positive dips the right side of the view, which is
        /// what reads as leaning into a wall on your right.</summary>
        public static float TargetRoll { get; private set; }

        static float cooldownUntil;

        // set on a wall jump; cleared only by touching the ground
        static bool jumpBlocked;
        static Vector3 jumpBlockNormal;

        // ---------------------------------------------------------------- static probe

        /// <summary>
        /// Looks for a vertical wall beside the character. Returns false if the only thing nearby is a
        /// floor, ceiling or the character's own colliders.
        /// </summary>
        public static bool Probe(CharacterActor actor, float distance, float height, LayerMask mask,
                                 out Vector3 normal, out bool onRight)
        {
            normal = Vector3.zero;
            onRight = false;
            if (actor == null) return false;

            Vector3 feet = actor.transform.position;
            Vector3 right = actor.transform.right;
            Vector3 forward = actor.transform.forward;
            float nearest = float.MaxValue;
            bool found = false;

            // Three heights and a fan of directions. A single chest-height sideways ray misses walls
            // whenever you are high or low beside them, which is what made grabbing feel unreliable.
            for (int band = -1; band <= 1; band++)
            {
                Vector3 origin = feet + Vector3.up * (height + band * 0.55f);

                // Full circle, not just the right-hand side. The original fan was centred on the
                // character's right vector and spanned only +/-80 degrees, so it literally never
                // looked to the left - walls on the left were undetectable, which is why grabbing
                // felt like it only worked some of the time.
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * 30f;
                    Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * right;

                    int count = Physics.RaycastNonAlloc(origin, dir, HitBuffer, distance, mask, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < count; h++)
                    {
                        RaycastHit hit = HitBuffer[h];
                        if (hit.collider == null) continue;
                        if (hit.collider.transform.root == actor.transform.root) continue;      // our own body
                        if (Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) > 0.5f) continue;     // floor or ceiling
                        if (hit.distance >= nearest) continue;

                        // a surface we are facing head-on is a wall in FRONT, not beside - running that
                        // is a different move. Kept loose so angled approaches still count.
                        if (Mathf.Abs(Vector3.Dot(hit.normal, forward)) > 0.85f) continue;

                        nearest = hit.distance;
                        normal = hit.normal;
                        onRight = Vector3.Dot(dir, right) > 0f;
                        found = true;
                    }
                }
            }

            return found;
        }

        public static bool Probe(CharacterActor actor, float distance, float height, LayerMask mask)
        {
            Vector3 n; bool r;
            return Probe(actor, distance, height, mask, out n, out r);
        }

        // ---------------------------------------------------------------- state

        public override bool CheckEnterTransition(CharacterState fromState)
        {
            if (Time.time < cooldownUntil) return false;      // anti-chaining
            if (CharacterActor.IsGrounded) return false;

            Vector3 n; bool right;
            if (!Probe(CharacterActor, probeDistance, probeHeight, wallMask, out n, out right)) return false;

            // The wall we just launched off is off limits until we land. A cooldown only delays the
            // re-grab - this prevents it outright, no matter how long we are in the air.
            if (jumpBlocked && Vector3.Dot(n, jumpBlockNormal) > 0.8f) return false;

            return CharacterActor.PlanarVelocity.magnitude >= minEntrySpeed;
        }

        /// Update runs whether or not this state is current, which is what makes it the right place to
        /// clear the wall-jump block - CheckEnterTransition only ever runs while airborne.
        void Update()
        {
            if (!jumpBlocked) return;
            if (CharacterActor != null && CharacterActor.IsGrounded) jumpBlocked = false;
        }

        public override void EnterBehaviour(float dt, CharacterState fromState)
        {
            elapsed = 0f;
            wallLostTime = 0f;

            // If the player is still holding jump from the leap that got them here, that must not
            // register as a fresh press or the run would wall-jump on its first frame.
            jumpHeldPrev = CharacterActions.jump.value;

            Vector3 n; bool right;
            Probe(CharacterActor, probeDistance, probeHeight, wallMask, out n, out right);
            wallNormal = n.sqrMagnitude > 0.001f ? n.normalized : -CharacterActor.transform.right;
            wallOnRight = right;

            // keep whatever momentum we arrived with, projected along the wall - enter at an angle and
            // the run bends rather than stalling
            Vector3 planar = CharacterActor.PlanarVelocity;
            entrySpeed = Mathf.Max(minEntrySpeed, planar.magnitude);
            speed = entrySpeed;
            Vector3 along = Vector3.ProjectOnPlane(planar, wallNormal);
            if (along.sqrMagnitude < 0.01f)
            {
                // arrived almost head-on: fall back to whichever way along the wall keeps the wall on side
                along = Vector3.Cross(Vector3.up, wallNormal);
                if (wallOnRight) along = -along;
            }
            runDirection = along.normalized;

            // Re-derive the side from the run direction so the clip matches: the wall normal points
            // away from the wall, so if it lies to the character's left the wall itself is on the right.
            Vector3 rightOfRun = Vector3.Cross(Vector3.up, runDirection);
            wallOnRight = Vector3.Dot(wallNormal, rightOfRun) < 0f;

            lift = entryLift;
            CharacterActor.alwaysNotGrounded = true;

            if (IsAnimatorValid())
            {
                // Clear any leftover end trigger first. If a previous run ended while the animator was
                // already elsewhere, WallRunEnd was never consumed - it would then fire the instant
                // this run starts and cut it after a frame. Same for the wall-jump triggers: they
                // latch until consumed, and a stale one plays the one-arm swing clip out of nowhere.
                CharacterActor.Animator.ResetTrigger("WallRunEnd");
                CharacterActor.Animator.ResetTrigger("WallJumpL");
                CharacterActor.Animator.ResetTrigger("WallJumpR");
                CharacterActor.Animator.SetTrigger(wallOnRight ? runRightParameter : runLeftParameter);
                CharacterActor.Animator.SetBool(groundedParameter, false);
            }

            // Hand the camera its tilt. Rolling towards the wall so it dips is what sells the run.
            IsActive = true;
            TargetRoll = wallOnRight ? cameraRoll : -cameraRoll;

            // Scrape loop for the duration of the run. Started here rather than in Update so it plays
            // once, not every frame, and stopped in ExitBehaviour so it cannot outlive the state.
            wallRunSource = Sfx.PlayLoop(wallRunClip, CharacterActor.transform, wallRunVolume);
        }

        public override void UpdateBehaviour(float dt)
        {
            elapsed += dt;
            lift = Mathf.Max(0f, lift - liftDecay * dt);
            speed = Mathf.Lerp(speed, runSpeed, Mathf.Clamp01(speedBlend * dt));

            Vector3 velocity = runDirection * speed - wallNormal * stickForce;

            // Vertical is entirely ours: NormalMovement is what applies gravity, and while this state
            // is current it is not running.
            //
            // Physics.gravity.y is NEGATIVE. Subtracting it (as this originally did) ADDS upward
            // acceleration - the character floated up the wall and popped off the top instead of
            // running along it. Subtract the magnitude.
            float vertical = CharacterActor.LocalVelocity.y;
            if (lift > 0f)
                vertical = Mathf.Max(vertical, lift);
            else
                vertical -= Mathf.Abs(Physics.gravity.y) * gravityScale * dt;

            velocity.y = vertical;
            CharacterActor.Velocity = velocity;

            // the scrape was never wanted - the run reads better without dust spraying off the wall
            // face. Replaced by a looped scrape sound, started in EnterBehaviour.

            // face the run, so the side-specific clip lines up with the wall
            Quaternion want = Quaternion.LookRotation(runDirection, Vector3.up);
            CharacterActor.transform.rotation = Quaternion.Slerp(CharacterActor.transform.rotation, want,
                                                                  Mathf.Clamp01(turnRate * dt));
        }

        public override void CheckExitTransition()
        {
            // Track our own rising edge on .value. CCP's BoolAction.Started LATCHES true forever
            // (it is only cleared on OnEnable/Disable), so reading it here meant every wall run
            // immediately "jumped" off - or, if the button happened to be up, reported a held jump
            // as a fresh press.
            bool jumpHeld = CharacterActions.jump.value;
            bool jumpPressed = jumpHeld && !jumpHeldPrev;
            jumpHeldPrev = jumpHeld;

            if (jumpPressed)
            {
                WallJump();
                return;
            }

            if (CharacterActor.IsGrounded || elapsed >= maxDuration)
            {
                Debug.Log("[WallRun] ended after " + elapsed.ToString("F2") + "s - reason: " +
                          (CharacterActor.IsGrounded ? "touched ground" : "max duration"));
                CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
                return;
            }

            // Grace period rather than an instant drop: the probe can miss for a frame at a corner or
            // a break in the wall, and ending the run on that reads as broken.
            bool wallThere = Probe(CharacterActor, probeDistance * 1.25f, probeHeight, wallMask);
            wallLostTime = wallThere ? 0f : wallLostTime + Time.fixedDeltaTime;

            if (wallLostTime > wallLostGrace)
            {
                Debug.Log("[WallRun] ended after " + elapsed.ToString("F2") + "s - reason: wall lost for " +
                          wallLostTime.ToString("F2") + "s   (moving away, or ran past the end of the wall)");
                CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
            }
        }

        void WallJump()
        {
            CharacterActor.alwaysNotGrounded = false;

            // this wall is off limits until we land, however long the jump takes
            jumpBlocked = true;
            jumpBlockNormal = wallNormal;

            // Carry whatever speed we already had along the run, then ADD the jump's forward
            // acceleration on top. Setting the velocity straight to jumpForwardSpeed (as this first
            // did) threw away the momentum built up getting to the wall.
            float carried = Mathf.Max(0f, Vector3.Dot(CharacterActor.PlanarVelocity, runDirection));
            float forward = Mathf.Min(carried + jumpForwardSpeed, maxForwardSpeed);

            // wallNormal points out of the wall, so this is the "away" nudge - deliberately small
            Vector3 velocity = wallNormal * jumpAwaySpeed + runDirection * forward;
            velocity.y = jumpUpSpeed;
            CharacterActor.Velocity = velocity;

            if (IsAnimatorValid())
                CharacterActor.Animator.SetTrigger(wallOnRight ? jumpRightParameter : jumpLeftParameter);

            jumped = true;
            CharacterStateController.EnqueueTransition<PlayerNormalMovement>();
        }

        public override void ExitBehaviour(float dt, CharacterState toState)
        {
            CharacterActor.alwaysNotGrounded = false;
            lift = 0f;
            wallLostTime = 0f;

            // Tell the animator to leave the run loop - but NOT when we wall jumped, because that
            // already fired its own transition and a second trigger would race it.
            //
            // This used to be handled by an exit-time transition on the animator (0.98). The loop clip
            // is only 0.4 s long, so it fired every loop and dropped the animation while the character
            // kept wall running.
            if (!jumped && IsAnimatorValid())
            {
                CharacterActor.Animator.SetTrigger("WallRunEnd");
                // never let a wall-jump trigger survive the run - see EnterBehaviour
                CharacterActor.Animator.ResetTrigger("WallJumpL");
                CharacterActor.Animator.ResetTrigger("WallJumpR");
            }

            jumped = false;

            // release the camera and start the cooldown, so runs cannot be chained back to back
            IsActive = false;
            TargetRoll = 0f;
            cooldownUntil = Time.time + cooldown;

            // the loop must not outlive the state - skipping this leaves it playing forever, since
            // the source is pooled and nothing else owns it
            if (wallRunSource != null) { wallRunSource.Stop(); wallRunSource = null; }
        }


        public override string GetInfo()
        {
            return "Wall run: locked to the wall plane with reduced gravity. Jump mid-run to wall jump.";
        }
    }
}
