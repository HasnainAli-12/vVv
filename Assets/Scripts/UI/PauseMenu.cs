using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NasCon.UI
{
    /// <summary>
    /// Pause menu. Escape (or gamepad Start) freezes the game, releases the cursor and shows the
    /// panel; Escape again resumes.
    ///
    /// Input is read straight from the Input System device rather than through an action, because
    /// the project's .inputactions asset has no Pause action and adding one means editing that JSON
    /// and reimporting - not worth it for one key.
    ///
    /// Time.timeScale = 0 is a supported pause in this project: CCP's CharacterBrain early-returns
    /// when timeScale is zero, so character input stops cleanly. Anything animated while paused must
    /// use unscaled time.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("The menu root. Hidden on start, enabled while paused.")]
        [SerializeField] GameObject panel;

        [Header("Buttons")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button menuButton;
        [SerializeField] Button quitButton;

        [Header("Scenes")]
        [SerializeField] string gameplayScene = "Freerun";
        [SerializeField] string menuScene = "MainMenu";

        [Header("Cursor")]
        [Tooltip("Restore the locked cursor when resuming. Leave off if a cutscene or menu wants it visible.")]
        [SerializeField] bool lockCursorOnResume = true;

        public bool IsPaused { get; private set; }

        void Awake()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        void Start()
        {
            SetPaused(false);
        }

        void Update()
        {
            if (TogglePressed())
                SetPaused(!IsPaused);
        }

        /// <summary>Escape on keyboard, Start on gamepad. Read directly - no input action needed.</summary>
        bool TogglePressed()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;

            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            if (gamepad != null && gamepad.startButton.wasPressedThisFrame) return true;

            return false;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;

            Time.timeScale = paused ? 0f : 1f;

            if (panel != null) panel.SetActive(paused);

            Cursor.lockState = paused || !lockCursorOnResume ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused || !lockCursorOnResume;

            if (paused)
            {
                // so gamepad and keyboard navigation start on something sensible
                if (EventSystem.current != null && resumeButton != null)
                    EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplayScene);
        }

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuScene);
        }

        public void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDisable()
        {
            // never leave the game frozen if this object is torn down
            if (IsPaused) Time.timeScale = 1f;
        }
    }
}
