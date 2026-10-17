using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NasCon.FX
{
    /// <summary>
    /// Switches between the three pipeline assets and applies the matching quality knobs.
    ///
    /// Exposes a single int so it can be wired straight to a Dropdown or three Buttons. Persists the
    /// choice in PlayerPrefs, which works on WebGL (IndexedDB-backed).
    /// </summary>
    public class QualityTierController : MonoBehaviour
    {
        public const string PrefsKey = "EmberAscent.VideoTier";

        [Header("Pipeline assets, low to high")]
        [SerializeField] UniversalRenderPipelineAsset low;
        [SerializeField] UniversalRenderPipelineAsset medium;
        [SerializeField] UniversalRenderPipelineAsset high;

        [Header("Optional")]
        [Tooltip("Volume holding film grain, so it can be switched off on the lowest tier.")]
        [SerializeField] Volume grainVolume;

        [Tooltip("Applied on Start from PlayerPrefs.")]
        [SerializeField] bool loadSavedTier = true;

        public int CurrentTier { get; private set; } = 1;

        void Start()
        {
            if (loadSavedTier)
                ApplyTier(PlayerPrefs.GetInt(PrefsKey, 1));
            else
                ApplyTier(1);
        }

        /// <summary>0 = low, 1 = medium, 2 = high. Clamped, so a Dropdown index can be passed through.</summary>
        public void ApplyTier(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            CurrentTier = tier;

            var asset = tier == 0 ? low : tier == 1 ? medium : high;

            // defaultRenderPipeline applies project-wide; setting it here survives scene loads
            if (asset != null) GraphicsSettings.defaultRenderPipeline = asset;

            QualitySettings.shadowDistance = tier == 0 ? 25f : tier == 1 ? 35f : 45f;
            QualitySettings.lodBias = tier == 0 ? 0.6f : tier == 1 ? 1f : 1.3f;
            QualitySettings.globalTextureMipmapLimit = tier == 0 ? 1 : 0;   // 1 = half resolution
            QualitySettings.antiAliasing = 0;                          // AA lives on the camera, not here

            if (grainVolume != null) grainVolume.weight = tier == 0 ? 0f : 1f;

            PlayerPrefs.SetInt(PrefsKey, tier);
            PlayerPrefs.Save();
        }

        /// <summary>Drops a tier if the frame rate is unhealthy. Call once a few seconds into a scene.</summary>
        public void AutoTune(float averageMilliseconds)
        {
            if (averageMilliseconds > 30f && CurrentTier > 0) ApplyTier(CurrentTier - 1);
            else if (averageMilliseconds > 22f && CurrentTier > 0) ApplyTier(CurrentTier - 1);
        }
    }
}
