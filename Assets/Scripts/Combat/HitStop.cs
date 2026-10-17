using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// A brief freeze on impact — the single cheapest thing that makes a hit feel like it connected.
    ///
    /// Static, with a hidden driver object, because it has to survive being triggered from anywhere
    /// (an attack state, a projectile, a feedback listener) without anyone owning an instance.
    ///
    /// It deliberately does not fight the pause menu: triggers are ignored while time is already
    /// stopped, and a restore never writes a non-zero scale back over a paused game.
    /// </summary>
    public static class HitStop
    {
        static float restoreAt;
        static float previousScale = 1f;
        static bool active;

        public static bool IsActive { get { return active; } }

        /// <summary>
        /// Dip time for `duration` seconds of real time. Overlapping calls extend rather than stack,
        /// so a combo cannot compound into a freeze.
        /// </summary>
        public static void Trigger(float duration, float scale = 0.05f)
        {
            if (duration <= 0f) return;
            if (Time.timeScale <= 0f) return;      // paused - do not touch it

            if (!active)
            {
                previousScale = Time.timeScale;
                Time.timeScale = Mathf.Clamp(scale, 0.01f, 1f);
                active = true;
            }

            restoreAt = Mathf.Max(restoreAt, Time.unscaledTime + duration);
        }

        /// <summary>Cancels an in-flight hit-stop without touching the clock. Used when something else takes control of time.</summary>
        public static void Cancel()
        {
            active = false;
            restoreAt = 0f;
            previousScale = 1f;
        }

        internal static void Tick()
        {
            if (!active) return;

            // if something else stopped time (the pause menu), abandon rather than fighting it
            if (Time.timeScale <= 0f) { Cancel(); return; }

            if (Time.unscaledTime >= restoreAt)
            {
                Time.timeScale = previousScale;
                Cancel();
            }
        }
    }
}
