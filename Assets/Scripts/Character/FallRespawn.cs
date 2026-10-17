using UnityEngine;
using Lightbug.CharacterControllerPro.Core;

namespace NasCon.Game
{
    /// <summary>
    /// Catches the player when they fall off the course.
    ///
    /// Deliberately has no checkpoint objects to place: the last spot the player stood on safely IS
    /// the checkpoint, so this covers every gap in the level without any authoring. That matters here
    /// because the course descends across 200 m and hand-placing checkpoints would always miss one.
    ///
    /// The safe position is only sampled while GROUNDED and moving at a sane speed, so falling past a
    /// ledge does not record the ledge edge as somewhere safe to come back to.
    /// </summary>
    public class FallRespawn : MonoBehaviour
    {
        [Header("Threshold")]
        [Tooltip("Fall below this world Y and you are put back. Must sit under the lowest walkable surface.")]
        [SerializeField] float killHeight = -5f;

        [Header("Timing")]
        [Tooltip("Seconds between safe-position samples while grounded.")]
        [SerializeField] float sampleInterval = 0.25f;

        [Tooltip("Seconds before a second respawn can happen, so one fall cannot loop.")]
        [SerializeField] float respawnCooldown = 1f;

        [Tooltip("How far above the recorded spot to place the player, so they do not spawn in the floor.")]
        [SerializeField] float liftOnRespawn = 0.35f;

        [Header("Feedback")]
        [Tooltip("Plays twice - a puff where you fell, and one where you arrive.")]
        [SerializeField] string respawnVfx = "vfx_checkpoint";
        [SerializeField] string respawnSound = "checkpint 2";

        CharacterActor actor;
        Vector3 safePosition;
        bool hasSafe;
        float nextSample;
        float nextRespawnAllowed;

        /// <summary>How many times the player has fallen. Useful for a HUD counter later.</summary>
        public int RespawnCount { get; private set; }

        void Awake()
        {
            actor = GetComponent<CharacterActor>();

            if (actor != null)
            {
                safePosition = actor.transform.position;
                hasSafe = true;
            }
        }

        void Update()
        {
            if (actor == null) return;

            float now = Time.unscaledTime;

            if (actor.IsGrounded && now >= nextSample)
            {
                nextSample = now + sampleInterval;

                // Do not record a spot while moving fast. Sliding or sprinting off an edge is still
                // "grounded" on the frame it happens, and recording that would respawn the player right
                // back at the edge they just fell from.
                if (actor.PlanarVelocity.sqrMagnitude < 400f)
                {
                    safePosition = actor.transform.position;
                    hasSafe = true;
                }
            }

            if (actor.transform.position.y >= killHeight) return;
            if (now < nextRespawnAllowed) return;

            Respawn();
        }

        /// <summary>
        /// Puts the player back on the last safe spot. Public so a checkpoint trigger could call it.
        /// </summary>
        public void Respawn()
        {
            if (actor == null || !hasSafe) return;

            nextRespawnAllowed = Time.unscaledTime + respawnCooldown;
            RespawnCount++;

            Vector3 fellAt = actor.transform.position;
            Vector3 target = safePosition + Vector3.up * liftOnRespawn;

            // puff where they left the course, so the fall is acknowledged rather than just undone
            FXV.Spawn(respawnVfx, fellAt + Vector3.up * 0.9f, Quaternion.identity, 1.4f);

            // Teleport rather than setting position: CCP owns the character's transform and would
            // otherwise resolve the move as a slide back through the geometry.
            actor.Teleport(target, actor.transform.rotation);
            actor.Velocity = Vector3.zero;

            FXV.Spawn(respawnVfx, target + Vector3.up * 0.9f, Quaternion.identity, 1.4f);
            if (!string.IsNullOrEmpty(respawnSound)) Sfx.Play(respawnSound, target, 0.8f);
        }
    }
}
