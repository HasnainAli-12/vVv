using UnityEngine;
using Lightbug.CharacterControllerPro.Core;

namespace NasCon.Level
{
    /// <summary>
    /// Finish trigger. Fires once when the player reaches it, and pushes the beacon hotter so the
    /// goal reads as satisfied.
    /// </summary>
    public class LevelGoal : MonoBehaviour
    {
        [Tooltip("Renderer whose glow is boosted on completion. Usually the beacon.")]
        [SerializeField] Renderer beaconRenderer;

        [SerializeField] Color completeEdgeColor = new Color(1f, 0.85f, 0.45f, 1f);
        [SerializeField] float completeGlowIntensity = 14f;

        bool completed;

        /// <summary>Fired once, when the player reaches the goal. Read-only hook for the HUD's timer.</summary>
        public event System.Action Completed;

        void OnTriggerEnter(Collider other)
        {
            if (completed)
                return;

            CharacterActor actor = other.GetComponentInParent<CharacterActor>();
            if (actor == null)
                return;

            completed = true;

            if (Completed != null)
                Completed();

            NasCon.Game.Sfx.Play2D("level_complete", 0.9f);

            // record the run: time since the scene loaded, kept in PlayerPrefs if it beats the best
            float elapsed = Time.timeSinceLevelLoad;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            float seconds = elapsed - minutes * 60f;
            string formatted = string.Format("{0:00}:{1:00.00}", minutes, seconds);

            // the key lives on MainMenu because that is what displays it
            float best = PlayerPrefs.GetFloat(NasCon.UI.MainMenu.BestTimeKey, float.MaxValue);
            bool isBest = elapsed < best;
            if (isBest)
            {
                PlayerPrefs.SetFloat(NasCon.UI.MainMenu.BestTimeKey, elapsed);
                PlayerPrefs.Save();
            }

            Debug.Log("[LevelGoal] Ember Ascent complete in " + formatted + (isBest ? "  (new best)" : ""));

            if (beaconRenderer == null)
                return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            beaconRenderer.GetPropertyBlock(block);
            block.SetColor("_EdgeColor", completeEdgeColor);
            block.SetColor("_CornerColor", completeEdgeColor);
            block.SetFloat("_GlowIntensity", completeGlowIntensity);
            beaconRenderer.SetPropertyBlock(block);
        }
    }
}
