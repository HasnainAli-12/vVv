using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// One sample of the player at an instant.
    ///
    /// A struct so the ring buffer is one contiguous block of value types - no boxing, no heap
    /// objects, no garbage collector pressure. It is roughly 48 bytes, so 150 samples is 7 KB total.
    /// </summary>
    public struct PlayerSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;

        /// <summary>Animator state hash, so the pose can be restored rather than snapped to idle.</summary>
        public int animatorStateHash;

        /// <summary>Normalised time within that state, 0..1.</summary>
        public float animatorNormalizedTime;

        /// <summary>Which traversal state the player was in. See SnapshotFlags.</summary>
        public byte flags;

        /// <summary>
        /// Health at this moment. Recording it is what makes a rewind undo damage taken *after* the
        /// point you return to, while damage taken before it stays applied.
        /// </summary>
        public float health;

        /// <summary>Seconds since scene load when this was taken. Used for the history bar.</summary>
        public float timestamp;

        // ------------------------------------------------------------ flags

        public bool IsGrounded { get { return (flags & (byte)SnapshotFlags.Grounded) != 0; } }
        public bool IsWallRunning { get { return (flags & (byte)SnapshotFlags.WallRunning) != 0; } }
        public bool IsSliding { get { return (flags & (byte)SnapshotFlags.Sliding) != 0; } }

        public void SetFlag(SnapshotFlags flag, bool on)
        {
            if (on) flags |= (byte)flag;
            else flags &= (byte)~flag;
        }
    }

    /// <summary>
    /// Movement modes worth restoring. Not every state in the controller needs its own bit - these
    /// are the ones where the pose and the physics disagree if you come out of a rewind wrong.
    /// </summary>
    public enum SnapshotFlags : byte
    {
        None = 0,
        Grounded = 1 << 0,
        WallRunning = 1 << 1,
        Sliding = 1 << 2,
        Climbing = 1 << 3,
        Swinging = 1 << 4,
        Crouched = 1 << 5
    }
}
