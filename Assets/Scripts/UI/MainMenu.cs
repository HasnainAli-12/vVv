using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NasCon.UI
{
    /// <summary>
    /// Main menu behaviour: start the run, quit, and show the best completion time.
    ///
    /// The best time is written by LevelGoal when the player reaches the beacon, and stored in
    /// PlayerPrefs so it survives between sessions.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Scenes")]
        [SerializeField] string gameplayScene = "NightParkour";

        [Header("Wiring")]
        [SerializeField] Button startButton;
        [SerializeField] Button quitButton;
        [SerializeField] Text bestTimeLabel;

        public const string BestTimeKey = "EmberAscent.BestTime";

        void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(StartRun);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            RefreshBestTime();
        }

        public void StartRun()
        {
            if (string.IsNullOrEmpty(gameplayScene))
            {
                Debug.LogWarning("[MainMenu] No gameplay scene set.");
                return;
            }
            SceneManager.LoadScene(gameplayScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void RefreshBestTime()
        {
            if (bestTimeLabel == null) return;

            if (!PlayerPrefs.HasKey(BestTimeKey))
            {
                bestTimeLabel.text = "--:--.--";
                return;
            }

            float best = PlayerPrefs.GetFloat(BestTimeKey);
            int minutes = Mathf.FloorToInt(best / 60f);
            float seconds = best - minutes * 60f;
            bestTimeLabel.text = string.Format("{0:00}:{1:00.00}", minutes, seconds);
        }
    }
}
