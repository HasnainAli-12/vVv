using UnityEngine;
using UnityEngine.SceneManagement;

namespace NasCon.UI
{
    /// <summary>
    /// Shown when the course is cleared.
    ///
    /// The button and the auto-return both exist on purpose: a WebGL build has no reliable way out of
    /// a dead-end scene, and a player who walks away from the keyboard should still end up back at the
    /// menu rather than staring at a static screen.
    /// </summary>
    public class WinScreen : MonoBehaviour
    {
        [Header("Navigation")]
        [Tooltip("Scene the button and the auto-return both go to.")]
        [SerializeField] string nextScene = "MainMenu";

        [Tooltip("Seconds before returning automatically. 0 disables it.")]
        [SerializeField] float autoReturnAfter = 20f;

        float fireAt = -1f;

        void Start()
        {
            // a win screen is a menu, so the cursor has to be usable
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (autoReturnAfter > 0f) fireAt = Time.unscaledTime + autoReturnAfter;
        }

        void Update()
        {
            if (fireAt < 0f) return;
            if (Time.unscaledTime < fireAt) return;

            fireAt = -1f;
            Back();
        }

        /// <summary>Wired to the on-screen button, and used by the auto-return.</summary>
        public void Back()
        {
            Time.timeScale = 1f;     // a death or a freeze earlier in the run could have left this at 0
            SceneManager.LoadScene(nextScene);
        }
    }
}
