using UnityEngine;
using UnityEngine.SceneManagement;

namespace NasCon.Game
{
    /// <summary>
    /// Wins the run when the last enemy in the level dies.
    ///
    /// Counts Health components on the Enemy team rather than EnemyBrain, so an enemy still counts
    /// while it is staggering or mid-death-animation. The run should end when the fight is over, not
    /// the frame the killing blow lands.
    ///
    /// Two guards matter here. It waits for at least one enemy to have existed, or a scene with no
    /// enemies would win instantly on load. And it polls on a timer rather than every frame -
    /// FindObjectsByType is a full scene walk and does not belong in Update.
    /// </summary>
    public class WinCondition : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Seconds between the last kill and the win screen, so the death animation finishes.")]
        [SerializeField] float delay = 1.5f;

        [Tooltip("How often the enemy count is checked, in seconds.")]
        [SerializeField] float checkInterval = 0.5f;

        [Header("Scene")]
        [Tooltip("Scene loaded when the level is cleared.")]
        [SerializeField] string winScene = "Win";

        bool won;
        bool seenAnEnemy;
        float nextCheck;
        float fireAt = -1f;

        void Start()
        {
            // stagger the first check so this does not run on the same frame as everything else
            nextCheck = Time.unscaledTime + checkInterval;
        }

        void Update()
        {
            if (won) return;

            if (fireAt > 0f)
            {
                if (Time.unscaledTime < fireAt) return;
                won = true;
                SceneManager.LoadScene(winScene);
                return;
            }

            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;

            if (!AllDefeated()) return;

            fireAt = Time.unscaledTime + delay;
            Sfx.Play2D("level_complete", 0.9f);
        }

        bool AllDefeated()
        {
            int alive = 0;
            int total = 0;

            foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h == null || h.Side != Health.Team.Enemy) continue;
                total++;
                if (h.IsAlive) alive++;
            }

            // a scene with no enemies at all is not a win - that is an unfinished level, and firing
            // here would load the win screen the moment the scene started
            if (total == 0) return false;

            seenAnEnemy = true;
            return seenAnEnemy && alive == 0;
        }
    }
}
