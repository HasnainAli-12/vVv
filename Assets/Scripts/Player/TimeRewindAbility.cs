using System;
using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;
using NasCon.FX;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NasCon.Game
{
    /// <summary>
    /// Time rewind.
    ///
    /// Records the player into a fixed ring buffer, and on hold plays that history backwards at a
    /// chosen speed. The buffer is one preallocated struct array: writing wraps around and overwrites
    /// the oldest sample, so there is never an allocation, never a shift, and never a growing list.
    ///
    /// The player is a Character Controller Pro actor, so movement is applied with
    /// CharacterActor.Teleport rather than by writing a transform or a rigidbody. That is the
    /// supported path and it raises OnTeleport, which lets the state machine reset cleanly instead of
    /// carrying stale velocity into whatever happens next.
    /// </summary>
    [RequireComponent(typeof(CharacterActor))]
    public class TimeRewindAbility : MonoBehaviour
    {
        [Header("Recording")]
        [Tooltip("How much history to keep, in seconds.")]
        [SerializeField, Min(0.5f)] float recordSeconds = 5f;

        [Tooltip("Samples per second. Buffer size is recordSeconds * sampleRate, computed once.")]
        [SerializeField, Min(5f)] float sampleRate = 30f;

        [Tooltip("Only the player is recorded. Keep this off unless you extend the system to other objects.")]
        [SerializeField] bool recordAnimator = true;

        [Header("Rewind")]
        [Tooltip("Playback speed relative to real time. Below 1 is slow-motion reverse.")]
        [SerializeField, Min(0.1f)] float rewindSpeed = 0.55f;

        [Tooltip("World time scale while rewinding. The playback uses unscaled time, so this slows the " +
                 "world down without slowing the rewind itself.")]
        [SerializeField, Range(0.05f, 1f)] float slowMotionScale = 0.35f;

        [Tooltip("Seconds before the ability can be used again after a rewind ends.")]
        [SerializeField, Min(0f)] float cooldown = 10f;

        [Tooltip("If set, rewinding is refused while this component reports the player is dead.")]
        [SerializeField] Health health;

        [Header("Input")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("Key held to rewind.")]
        [SerializeField] Key rewindKey = Key.R;
#else
        [Tooltip("Key held to rewind.")]
        [SerializeField] KeyCode rewindKey = KeyCode.R;
#endif

        [Header("Audio")]
        [SerializeField] AudioSource rewindSource;
        [SerializeField] AudioClip rewindLoop;

        // ---------------------------------------------------------------- ring buffer state

        PlayerSnapshot[] buffer;
        int capacity;
        int head;            // next slot to write
        int count;           // valid samples, caps at capacity
        float recordAccumulator;

        // ---------------------------------------------------------------- rewind state

        bool unlocked;
        bool isRewinding;
        float cursor;        // samples back from the newest, fractional
        float cooldownRemaining;

        CharacterActor actor;
        CharacterBrain brain;
        CharacterStateController controller;
        Animator animator;
        Rigidbody body;      // only used if there is no CharacterActor, kept for portability

        bool hadBrain, hadController, hadAnimator;

        // ---------------------------------------------------------------- public API for the HUD

        public bool IsUnlocked { get { return unlocked; } }
        public bool IsRewinding { get { return isRewinding; } }
        public bool CanRewind
        {
            get { return unlocked && !isRewinding && cooldownRemaining <= 0f && count > 1; }
        }

        /// <summary>0 = ready, 1 = just used. For the cooldown bar.</summary>
        public float CooldownNormalized
        {
            get { return cooldown <= 0f ? 0f : Mathf.Clamp01(cooldownRemaining / cooldown); }
        }

        /// <summary>Seconds of history actually stored right now.</summary>
        public float StoredSeconds
        {
            get { return sampleRate <= 0f ? 0f : count / sampleRate; }
        }

        public float RecordSeconds { get { return recordSeconds; } }

        public event Action Unlocked;
        public event Action RewindStarted;
        public event Action RewindEnded;

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            actor = GetComponent<CharacterActor>();
            brain = GetComponentInChildren<CharacterBrain>();
            controller = GetComponentInChildren<CharacterStateController>();
            animator = GetComponentInChildren<Animator>();
            body = GetComponent<Rigidbody>();
            if (health == null) health = GetComponent<Health>();

            // capacity is fixed for the lifetime of the component - resizing would allocate
            capacity = Mathf.Max(2, Mathf.CeilToInt(recordSeconds * sampleRate));
            buffer = new PlayerSnapshot[capacity];

            head = 0;
            count = 0;
        }

        void OnEnable()
        {
            if (health != null) health.Died += OnDied;
            if (actor != null) actor.OnTeleport += OnTeleport;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
            if (actor != null) actor.OnTeleport -= OnTeleport;
            if (isRewinding) EndRewind();
        }

        public void Unlock()
        {
            if (unlocked) return;
            unlocked = true;
            if (Unlocked != null) Unlocked();
        }

        // ---------------------------------------------------------------- frame loop

        bool rewindHeldPrev;

        void Update()
        {
            if (cooldownRemaining > 0f)
                cooldownRemaining -= Time.unscaledDeltaTime;

            bool held = RawRewindHeld();
            bool pressed = held && !rewindHeldPrev;
            rewindHeldPrev = held;

            if (!isRewinding)
            {
                // single press, not a hold: one tap plays the whole buffer and stops on its own.
                // Holding was the thing that let it run indefinitely and made the second use
                // unpredictable.
                if (pressed && CanRewind) BeginRewind();
                else RecordTick(Time.deltaTime);
            }
            else
            {
                RewindTick(Time.unscaledDeltaTime);
            }
        }

        // ---------------------------------------------------------------- recording

        void RecordTick(float dt)
        {
            if (dt <= 0f) return;

            recordAccumulator += dt;

            float interval = 1f / sampleRate;

            // one sample per frame at most: catching up in a loop would record the same position
            // several times and waste buffer space on duplicates
            if (recordAccumulator < interval) return;

            recordAccumulator -= interval;
            if (recordAccumulator > interval) recordAccumulator = 0f;   // dropped frames: resync

            PlayerSnapshot s = default;

            s.position = actor != null ? actor.transform.position : transform.position;
            s.rotation = actor != null ? actor.transform.rotation : transform.rotation;
            s.velocity = actor != null ? actor.Velocity : (body != null ? body.linearVelocity : Vector3.zero);

            s.flags = 0;
            if (actor != null && actor.IsGrounded) s.SetFlag(SnapshotFlags.Grounded, true);
            s.SetFlag(SnapshotFlags.WallRunning, PlayerWallRunState.IsActive);

            if (controller != null && controller.CurrentState != null)
            {
                string state = controller.CurrentState.GetType().Name;
                s.SetFlag(SnapshotFlags.Sliding, state == "PlayerSlideState");
                s.SetFlag(SnapshotFlags.Climbing, state == "PlayerClimbState");
                s.SetFlag(SnapshotFlags.Swinging, state == "PlayerSwingState");
            }

            if (recordAnimator && animator != null && animator.isActiveAndEnabled)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                s.animatorStateHash = info.fullPathHash;
                s.animatorNormalizedTime = info.normalizedTime;
            }

            s.timestamp = Time.timeSinceLevelLoad;
            if (health != null) s.health = health.Current;

            buffer[head] = s;
            head = head + 1;
            if (head >= capacity) head = 0;
            if (count < capacity) count++;
        }

        // index of the sample N back from the newest, 0 = newest
        int IndexBack(int back)
        {
            int newest = head - 1;
            if (newest < 0) newest += capacity;
            int i = newest - back;
            while (i < 0) i += capacity;
            return i;
        }

        // ---------------------------------------------------------------- rewind

        void BeginRewind()
        {
            if (count < 2) return;

            isRewinding = true;
            cursor = 0f;

            // freeze everything that would fight the playback
            if (brain != null) { hadBrain = brain.enabled; brain.enabled = false; }
            if (controller != null) { hadController = controller.enabled; controller.enabled = false; }

            if (body != null && !body.isKinematic)
            {
                body.isKinematic = true;
                body.linearVelocity = Vector3.zero;
            }

            if (actor != null) actor.alwaysNotGrounded = true;

            // Enemies keep acting through a rewind unless stopped: they chase, punch and throw while
            // you are reversing, and you cannot react. Freeze their brains and the orbs in flight.
            FreezeEnemies(true);

            // slow the world while the playback runs on unscaled time, which reads as slow motion
            if (Time.timeScale > 0f) Time.timeScale = slowMotionScale;

            if (rewindSource != null && rewindLoop != null)
            {
                rewindSource.clip = rewindLoop;
                rewindSource.loop = true;
                rewindSource.Play();
            }

            if (RewindVolumeBlend.Instance != null) RewindVolumeBlend.Instance.StartRewindVisuals();

            if (RewindStarted != null) RewindStarted();
        }

        void RewindTick(float unscaledDt)
        {
            if (count < 2) { EndRewind(); return; }

            // move back through the history at rewindSpeed, in sample units
            cursor += unscaledDt * sampleRate * rewindSpeed;

            float maxCursor = count - 1;

            if (cursor >= maxCursor)
            {
                // history exhausted: snap to the oldest and finish
                ApplyAt(maxCursor);
                EndRewind();
                return;
            }

            ApplyAt(cursor);
        }

        /// <summary>Applies an interpolated pose at a fractional offset back from the newest sample.</summary>
        void ApplyAt(float back)
        {
            int i0 = Mathf.FloorToInt(back);
            int i1 = Mathf.Min(i0 + 1, count - 1);
            float t = back - i0;

            PlayerSnapshot a = buffer[IndexBack(i0)];
            PlayerSnapshot b = buffer[IndexBack(i1)];

            Vector3 position = Vector3.LerpUnclamped(a.position, b.position, t);
            Quaternion rotation = Quaternion.SlerpUnclamped(a.rotation, b.rotation, t);

            if (actor != null)
            {
                actor.Teleport(position, rotation);
            }
            else
            {
                transform.position = position;
                transform.rotation = rotation;
                if (body != null) body.Move(position, rotation);
            }

            if (recordAnimator && animator != null && a.animatorStateHash != 0)
                animator.Play(a.animatorStateHash, 0, a.animatorNormalizedTime);

            // health rolls back with position. Damage taken after the point being rewound to is
            // undone; damage taken before it stays applied, because that is what was recorded.
            if (health != null) health.Restore(a.health);

            // keep the clock showing reversed motion without touching Time.timeScale
            if (Time.timeScale > 0f) { /* no-op: playback uses unscaled dt by design */ }
        }

        void EndRewind()
        {
            if (!isRewinding) return;
            isRewinding = false;

            // only hand time back if we still own it - the pause menu may have taken it
            if (Time.timeScale > 0f) Time.timeScale = 1f;

            if (actor != null) actor.alwaysNotGrounded = false;

            FreezeEnemies(false);

            if (body != null && body.isKinematic)
                body.isKinematic = false;

            // re-enable unconditionally: whatever the prior flags were, the player must end a rewind
            // able to move. Leaving input off would be a soft-lock.
            if (brain != null) brain.enabled = true;
            if (controller != null) controller.enabled = true;

            // hand back the velocity of whatever we last stood on, so exiting a rewind mid-air
            // carries momentum instead of dropping dead
            if (count > 0)
            {
                PlayerSnapshot last = buffer[IndexBack(Mathf.Clamp(Mathf.FloorToInt(cursor), 0, count - 1))];
                if (actor != null) actor.Velocity = last.velocity;
                else if (body != null) body.linearVelocity = last.velocity;
            }

            if (rewindSource != null) rewindSource.Stop();

            if (RewindVolumeBlend.Instance != null) RewindVolumeBlend.Instance.EndRewindVisuals();

            // fresh history after every use - the old one is no longer contiguous with the player
            head = 0;
            count = 0;
            recordAccumulator = 0f;
            cursor = 0f;

            cooldownRemaining = cooldown;

            if (RewindEnded != null) RewindEnded();
        }

        /// <summary>
        /// Stops and restarts everything that could hurt the player while they are reversing.
        /// Brains disable chase/attack decisions; projectiles disable their own flight so thrown
        /// orbs hang in the air instead of landing mid-rewind.
        /// </summary>
        void FreezeEnemies(bool frozen)
        {
            var brains = UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < brains.Length; i++)
                if (brains[i] != null) brains[i].enabled = !frozen;

            var projectiles = UnityEngine.Object.FindObjectsByType<FireProjectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
                if (projectiles[i] != null) projectiles[i].enabled = !frozen;
        }

        // ---------------------------------------------------------------- edge cases

        void OnDied(Health source, GameObject instigator)
        {
            if (!isRewinding) return;
            EndRewind();
        }

        /// <summary>
        /// A respawn teleports the player, which would otherwise look like a jump in the recording.
        /// Note that our own rewind teleports too - the guard is what stops it recursing.
        /// </summary>
        void OnTeleport(Vector3 position, Quaternion rotation)
        {
            if (isRewinding) return;

            head = 0;
            count = 0;
            recordAccumulator = 0f;
        }

        // ---------------------------------------------------------------- input

        bool RawRewindHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[rewindKey].isPressed;
#else
            return Input.GetKey(rewindKey);
#endif
        }

        // ---------------------------------------------------------------- gizmos

        void OnDrawGizmosSelected()
        {
            if (buffer == null || count == 0) return;

            Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.5f);
            for (int i = 0; i < count - 1; i++)
                Gizmos.DrawLine(buffer[IndexBack(i)].position, buffer[IndexBack(i + 1)].position);
        }
    }
}
