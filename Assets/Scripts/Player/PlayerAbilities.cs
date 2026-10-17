using System;
using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Player-side counters the HUD reads.
    ///
    /// Deliberately just a data holder: the rewind mechanic itself does not exist yet, so this
    /// tracks a charge count and raises an event when it moves. The green gem pickup and the
    /// rewind implementation will both drive this later without the HUD needing to change.
    /// </summary>
    public class PlayerAbilities : MonoBehaviour
    {
        [Header("Time rewind")]
        [Tooltip("Charges currently held. Spent by the rewind once it exists.")]
        [SerializeField] int rewindCharges = 3;

        [Tooltip("Maximum charges the player can hold. Pickups clamp to this.")]
        [SerializeField] int rewindChargesMax = 5;

        /// <summary>Fired whenever the charge count changes, with the new value.</summary>
        public event Action<int> RewindChanged;

        public int RewindCharges { get { return rewindCharges; } }
        public int RewindChargesMax { get { return rewindChargesMax; } }
        public bool HasRewind { get { return rewindCharges > 0; } }

        void Awake()
        {
            rewindCharges = Mathf.Clamp(rewindCharges, 0, rewindChargesMax);
        }

        public void AddRewindCharge(int amount = 1)
        {
            if (amount <= 0) return;
            SetRewindCharges(rewindCharges + amount);
        }

        /// <summary>
        /// Spends a charge. Returns false if none were available, so a future rewind can bail out
        /// rather than rewinding with nothing to pay for it.
        /// </summary>
        public bool TrySpendRewind()
        {
            if (rewindCharges <= 0) return false;
            SetRewindCharges(rewindCharges - 1);
            return true;
        }

        public void SetRewindCharges(int value)
        {
            int clamped = Mathf.Clamp(value, 0, rewindChargesMax);
            if (clamped == rewindCharges) return;

            rewindCharges = clamped;
            if (RewindChanged != null) RewindChanged(rewindCharges);
        }
    }
}
