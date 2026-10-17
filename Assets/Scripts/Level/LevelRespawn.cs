using UnityEngine;
using Lightbug.CharacterControllerPro.Core;

namespace NasCon.Level
{
    /// <summary>
    /// Handles both halves of respawning, because they are the same concern:
    ///
    ///   FallZone   - a large trigger under the course. When the player drops into it they are
    ///                teleported back to the last checkpoint.
    ///   Checkpoint - a pad that becomes the respawn point (and lights up) when the player touches it.
    ///
    /// Uses CharacterActor.Teleport(), which raises CCP's OnTeleport event - NormalMovement listens to
    /// that and resets its own state, so the player arrives cleanly rather than mid-air with stale
    /// velocity.
    /// </summary>
    public class LevelRespawn : MonoBehaviour
    {
        public enum Mode { FallZone, Checkpoint }

        [SerializeField] Mode mode = Mode.FallZone;

        [Tooltip("Where the player starts. Assign on the start pad so a fall before the first checkpoint still works.")]
        [SerializeField] Transform startPoint;

        [Tooltip("Claim this respawn point as soon as play begins (use on the start pad).")]
        [SerializeField] bool claimOnStart = false;

        [Header("Claimed look (checkpoints only)")]
        [SerializeField] Color claimedEdgeColor = new Color(1f, 0.75f, 0.28f, 1f);
        [SerializeField] Color claimedCornerColor = new Color(1f, 0.92f, 0.6f, 1f);
        [SerializeField] float claimedGlowIntensity = 9f;

        static Vector3 respawnPosition;
        static Quaternion respawnRotation;
        static bool hasRespawn;

        Renderer padRenderer;
        MaterialPropertyBlock propertyBlock;
        bool claimed;

        void Awake()
        {
            if (mode == Mode.Checkpoint)
            {
                padRenderer = GetComponent<Renderer>();
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        void Start()
        {
            if (claimOnStart && startPoint != null)
            {
                respawnPosition = startPoint.position;
                respawnRotation = startPoint.rotation;
                hasRespawn = true;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            CharacterActor actor = other.GetComponentInParent<CharacterActor>();
            if (actor == null)
                return;     // only the player matters

            if (mode == Mode.Checkpoint)
            {
                respawnPosition = transform.position + Vector3.up * 0.6f;
                respawnRotation = transform.rotation;
                hasRespawn = true;
                Claim();
                return;
            }

            if (!hasRespawn)
                return;

            actor.Teleport(respawnPosition, respawnRotation);
        }

        void Claim()
        {
            if (claimed || padRenderer == null)
                return;

            claimed = true;

            padRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EdgeColor", claimedEdgeColor);
            propertyBlock.SetColor("_CornerColor", claimedCornerColor);
            propertyBlock.SetFloat("_GlowIntensity", claimedGlowIntensity);
            padRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
