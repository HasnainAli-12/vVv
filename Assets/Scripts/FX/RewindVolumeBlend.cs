using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NasCon.FX
{
    /// <summary>
    /// Blends the two Global Volumes by lerping their weights - one for the normal fire-lit night,
    /// one for rewind. Two volumes with two profiles, never a profile swap: swapping `volume.profile`
    /// mid-effect resets the weight and the inspector values snap.
    ///
    /// Runs on unscaled time, because the rewind itself may be slowing the clock.
    /// </summary>
    public class RewindVolumeBlend : MonoBehaviour
    {
        [SerializeField] Volume normalVolume;
        [SerializeField] Volume rewindVolume;

        [Tooltip("Seconds to cross-fade. 0.3 matches the wind-up of the rewind ability.")]
        [SerializeField] float blendDuration = 0.3f;

        [Header("Ember reversal")]
        [Tooltip("Ember particle systems that should fall upwards while time is reversed.")]
        [SerializeField] ParticleSystem[] emberSystems;

        public static RewindVolumeBlend Instance { get; private set; }

        public bool IsRewinding { get; private set; }

        /// <summary>Fired when the blend settles, with the new state.</summary>
        public event Action<bool> RewindVisualsChanged;

        Coroutine blendRoutine;

        void Awake()
        {
            Instance = this;

            if (normalVolume != null) normalVolume.weight = 1f;
            if (rewindVolume != null) rewindVolume.weight = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- public API

        public void StartRewindVisuals() { Apply(true); }
        public void EndRewindVisuals() { Apply(false); }

        public void SetRewinding(bool rewinding) { Apply(rewinding); }

        // ---------------------------------------------------------------- internals

        void Apply(bool rewinding)
        {
            if (IsRewinding == rewinding) return;
            IsRewinding = rewinding;

            SetEmbersReversed(rewinding);

            if (blendRoutine != null) StopCoroutine(blendRoutine);
            blendRoutine = StartCoroutine(Blend(rewinding ? 1f : 0f));
        }

        IEnumerator Blend(float target)
        {
            float startNormal = normalVolume != null ? normalVolume.weight : 0f;
            float startRewind = rewindVolume != null ? rewindVolume.weight : 0f;
            float endNormal = 1f - target;
            float endRewind = target;

            float elapsed = 0f;
            while (elapsed < blendDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / blendDuration);
                k = k * k * (3f - 2f * k);                 // smoothstep

                if (normalVolume != null) normalVolume.weight = Mathf.Lerp(startNormal, endNormal, k);
                if (rewindVolume != null) rewindVolume.weight = Mathf.Lerp(startRewind, endRewind, k);

                yield return null;
            }

            if (normalVolume != null) normalVolume.weight = endNormal;
            if (rewindVolume != null) rewindVolume.weight = endRewind;

            blendRoutine = null;
            if (RewindVisualsChanged != null) RewindVisualsChanged(IsRewinding);
        }

        /// <summary>
        /// Negative simulationSpeed is clamped by Unity, so the motion is reversed through the two
        /// values that actually drive it: launch speed and gravity.
        /// </summary>
        void SetEmbersReversed(bool rewinding)
        {
            if (emberSystems == null) return;

            for (int i = 0; i < emberSystems.Length; i++)
            {
                var ps = emberSystems[i];
                if (ps == null) continue;

                var main = ps.main;
                float speed = Mathf.Abs(main.startSpeedMultiplier);
                float gravity = Mathf.Abs(main.gravityModifierMultiplier);

                main.startSpeedMultiplier = rewinding ? -speed : speed;
                main.gravityModifierMultiplier = rewinding ? -gravity : gravity;
            }
        }
    }
}
