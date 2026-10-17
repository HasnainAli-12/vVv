using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// A decaying shake magnitude that the orbit camera reads.
    ///
    /// Same pattern as PlayerWallRunState.TargetRoll: the camera already polls statics for roll and
    /// FOV, so this needs no camera API and nothing else has to know it exists. The camera ticks the
    /// decay from its own LateUpdate.
    /// </summary>
    public static class CameraShake
    {
        static float magnitude;
        static float decayPerSecond = 3.5f;

        /// <summary>Current shake strength, 0..1. Read by ThirdPersonOrbitCamera.</summary>
        public static float Magnitude { get { return magnitude; } }

        public static void Add(float amount)
        {
            if (amount <= 0f) return;
            magnitude = Mathf.Min(1f, magnitude + amount);
        }

        public static void Clear()
        {
            magnitude = 0f;
        }

        /// <summary>Called by the camera each frame. Unscaled so shaking still decays while hit-stopped.</summary>
        public static void Tick(float deltaTime)
        {
            if (magnitude <= 0f) return;
            magnitude = Mathf.MoveTowards(magnitude, 0f, decayPerSecond * deltaTime);
        }
    }
}
