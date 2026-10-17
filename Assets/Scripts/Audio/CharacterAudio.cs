using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Audio
{
    /// <summary>
    /// Drives character SFX from what the character is actually doing, rather than from raw input -
    /// so the sounds stay in sync with the animation and with CCP's own state.
    ///
    /// Transition-based: it watches IsGrounded and vertical speed for jump/land, accumulates travelled
    /// distance for footsteps (stride shortens as you speed up), and reads the input edges for swing,
    /// dash and slide. Reading CharacterActions.value rather than .Started is deliberate - Started
    /// latches in CCP and never resets per frame.
    /// </summary>
    [RequireComponent(typeof(CharacterActor))]
    public class CharacterAudio : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] AudioClip[] footsteps;
        [SerializeField] AudioClip jump;
        [SerializeField] AudioClip land;
        [SerializeField] AudioClip swing;
        [SerializeField] AudioClip slide;

        [Tooltip("Played by PlayHit(). Combat does not call this yet - it is here for when it does.")]
        [SerializeField] AudioClip hit;

        [Header("Mix")]
        [Range(0f, 1f)] [SerializeField] float volume = 0.65f;
        [Range(0f, 1f)] [SerializeField] float spatialBlend = 0.35f;
        [SerializeField] float minDistance = 2f;
        [SerializeField] float maxDistance = 18f;

        [Header("Feel")]
        [Tooltip("Distance travelled per footstep when walking.")]
        [SerializeField] float walkStride = 2.3f;

        [Tooltip("Distance travelled per footstep when at full sprint.")]
        [SerializeField] float sprintStride = 1.7f;

        [Tooltip("Downward speed on landing above which the land clip plays instead of a footstep.")]
        [SerializeField] float landVelocityThreshold = 4.5f;

        CharacterActor actor;
        CharacterBrain brain;
        AudioSource source;

        bool wasGrounded;
        float previousVerticalSpeed;
        float strideAccumulator;
        bool attackPrevious;
        bool dashPrevious;
        bool crouchPrevious;
        float swingCooldown;
        float slideCooldown;

        void Awake()
        {
            actor = GetComponent<CharacterActor>();
            brain = GetComponentInChildren<CharacterBrain>();

            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;

            wasGrounded = true;
        }

        void Update()
        {
            if (actor == null) return;

            float dt = Time.deltaTime;
            bool grounded = actor.IsGrounded;
            float verticalSpeed = actor.LocalVelocity.y;
            float speed = actor.PlanarVelocity.magnitude;

            if (grounded && !wasGrounded)
            {
                if (previousVerticalSpeed < -landVelocityThreshold)
                {
                    Play(land, 1f, Random.Range(0.94f, 1.06f));
                }
                else
                {
                    strideAccumulator = 0f;
                    PlayFootstep();
                }
            }
            else if (!grounded && wasGrounded)
            {
                // only a genuine launch, not walking off a ledge
                if (verticalSpeed > 2f) Play(jump, 1f, Random.Range(0.95f, 1.06f));
            }

            if (grounded && speed > 0.7f)
            {
                strideAccumulator += speed * dt;
                float stride = Mathf.Lerp(walkStride, sprintStride, Mathf.InverseLerp(3.2f, 8f, speed));
                if (strideAccumulator >= stride)
                {
                    strideAccumulator = 0f;
                    PlayFootstep();
                }
            }
            else if (!grounded)
            {
                strideAccumulator = 0f;
            }

            if (brain != null)
            {
                CharacterActions actions = brain.CharacterActions;
                if (swingCooldown > 0f) swingCooldown -= dt;
                if (slideCooldown > 0f) slideCooldown -= dt;

                if (actions.attack.value && !attackPrevious && swingCooldown <= 0f)
                {
                    Play(swing, 0.9f, Random.Range(0.95f, 1.08f));
                    swingCooldown = 0.12f;
                }
                attackPrevious = actions.attack.value;

                if (actions.dash.value && !dashPrevious)
                    Play(swing, 0.7f, 0.82f);       // roll: lower, softer whoosh
                dashPrevious = actions.dash.value;

                if (actions.crouch.value && !crouchPrevious && speed > 4f && slideCooldown <= 0f)
                {
                    Play(slide, 0.9f, Random.Range(0.96f, 1.05f));
                    slideCooldown = 0.9f;
                }
                crouchPrevious = actions.crouch.value;
            }

            previousVerticalSpeed = verticalSpeed;
            wasGrounded = grounded;
        }

        /// <summary>For combat to call on impact.</summary>
        public void PlayHit(float strength = 1f)
        {
            Play(hit, Mathf.Clamp01(strength), Random.Range(0.94f, 1.06f));
        }

        void PlayFootstep()
        {
            if (footsteps == null || footsteps.Length == 0) return;
            Play(footsteps[Random.Range(0, footsteps.Length)], 0.8f, Random.Range(0.92f, 1.08f));
        }

        void Play(AudioClip clip, float volumeScale, float pitch)
        {
            if (clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, Mathf.Clamp01(volume * volumeScale));
        }
    }
}
