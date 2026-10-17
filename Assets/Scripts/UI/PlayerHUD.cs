using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;
using NasCon.Game;
using NasCon.Level;

namespace NasCon.UI
{
    /// <summary>
    /// Gameplay HUD: health, run timer, traversal state, speed, rewind charges, damage vignette
    /// and the death screen.
    ///
    /// Everything is bound by reference where possible and found once on Awake where not - the HUD
    /// never polls the scene graph per frame. The only per-frame reads are the ones that genuinely
    /// change continuously (health fill, speed, timer, vignette fade).
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Player (leave empty to find on start)")]
        [SerializeField] Health playerHealth;
        [SerializeField] PlayerAbilities abilities;
        [SerializeField] CharacterStateController stateController;
        [SerializeField] CharacterActor actor;

        // cached so a rewind can suppress the damage vignette without a per-frame lookup
        TimeRewindAbility rewindAbility;

        [Header("Health")]
        [Tooltip("Main fill. Driven by Health.Normalized.")]
        [SerializeField] Image healthFill;
        [Tooltip("Pale bar behind the fill that lags on damage, so a hit is readable.")]
        [SerializeField] Image healthDelayed;
        [SerializeField] Color healthHigh = new Color(0.55f, 0.85f, 0.35f);
        [SerializeField] Color healthMid = new Color(0.95f, 0.72f, 0.20f);
        [SerializeField] Color healthLow = new Color(0.90f, 0.25f, 0.20f);
        [SerializeField] float healthMidThreshold = 0.6f;
        [SerializeField] float healthLowThreshold = 0.3f;
        [Tooltip("How fast the delayed bar catches up, in fraction per second.")]
        [SerializeField] float delayedBarSpeed = 0.7f;

        [Header("Rewind")]
        [SerializeField] Text rewindLabel;
        [SerializeField] Image rewindSlot;
        [SerializeField] Color rewindReady = new Color(0.95f, 0.72f, 0.20f);
        [SerializeField] Color rewindEmpty = new Color(0.35f, 0.32f, 0.30f);

        [Header("Timer")]
        [SerializeField] Text timerLabel;
        [SerializeField] Text bestLabel;
        [SerializeField] Color timerNormal = Color.white;
        [SerializeField] Color timerFinished = new Color(0.55f, 0.85f, 0.35f);

        [Header("Traversal state + speed")]
        [SerializeField] Text stateLabel;
        [SerializeField] Image speedFill;
        [SerializeField] Color speedWalk = new Color(0.65f, 0.68f, 0.72f);
        [SerializeField] Color speedSprint = new Color(0.95f, 0.72f, 0.20f);
        [Tooltip("Speed at or above which the bar reads as sprinting.")]
        [SerializeField] float sprintThreshold = 5.2f;
        [SerializeField] float speedBarMax = 8f;

        [Header("Damage vignette")]
        [SerializeField] Image vignette;
        [SerializeField] Color vignetteColor = new Color(0.75f, 0.05f, 0.05f);
        [SerializeField] float vignetteFlash = 0.55f;
        [SerializeField] float vignetteFade = 1.6f;
        [Tooltip("Below this health fraction the vignette pulses continuously.")]
        [SerializeField] float lowHealthPulse = 0.3f;

        [Header("Death screen")]
        [SerializeField] GameObject deathPanel;
        [SerializeField] Button retryButton;
        [SerializeField] Button deathMenuButton;
        [SerializeField] string menuScene = "MainMenu";

        [Header("Scene")]
        [Tooltip("Reloaded by Retry and by the pause menu's Restart.")]
        [SerializeField] string gameplayScene = "Freerun";

        float displayedHealth = 1f;
        float laggingHealth = 1f;
        float vignetteAlpha;
        float runStartTime;
        float finishedTime = -1f;
        bool dragging;

        public string GameplayScene { get { return gameplayScene; } }

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (deathMenuButton != null) deathMenuButton.onClick.AddListener(GoToMenu);
            if (deathPanel != null) deathPanel.SetActive(false);
            if (vignette != null) vignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
            if (bestLabel != null) bestLabel.text = "BEST " + FormatTime(PlayerPrefs.GetFloat(MainMenu.BestTimeKey, 0f), PlayerPrefs.HasKey(MainMenu.BestTimeKey));
        }

        void Start()
        {
            ResolvePlayer();

            if (playerHealth != null)
            {
                playerHealth.Damaged += OnDamaged;
                playerHealth.Died += OnDied;
            }

            if (abilities != null)
            {
                abilities.RewindChanged += OnRewindChanged;
                OnRewindChanged(abilities.RewindCharges);
            }

            if (stateController != null)
                stateController.OnStateChange += OnStateChanged;

            // a respawn teleport is the only public signal LevelRespawn emits
            if (actor != null) actor.OnTeleport += OnTeleported;

            foreach (var goal in UnityEngine.Object.FindObjectsByType<LevelGoal>(FindObjectsSortMode.None))
                goal.Completed += OnLevelCompleted;

            runStartTime = Time.time;
            RefreshSpeedPalette(0f);
        }

        void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnDamaged;
                playerHealth.Died -= OnDied;
            }
            if (abilities != null) abilities.RewindChanged -= OnRewindChanged;
            if (stateController != null) stateController.OnStateChange -= OnStateChanged;
            if (actor != null) actor.OnTeleport -= OnTeleported;
        }

        void ResolvePlayer()
        {
            if (playerHealth != null && stateController != null && actor != null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            if (playerHealth == null) playerHealth = player.GetComponent<Health>();
            if (abilities == null) abilities = player.GetComponent<PlayerAbilities>();
            if (actor == null) actor = player.GetComponent<CharacterActor>();
            if (stateController == null) stateController = player.GetComponentInChildren<CharacterStateController>();
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            TickHealth();
            TickSpeed();
            TickState();
            TickTimer();
            TickVignette();
        }

        void TickHealth()
        {
            if (playerHealth == null) return;

            // polled rather than event-driven: Health.Heal() raises nothing, so an event-only bar
            // would drift the moment a fire gem grants regen
            displayedHealth = Mathf.MoveTowards(displayedHealth, playerHealth.Normalized, 1.5f * Time.deltaTime);

            if (laggingHealth < displayedHealth) laggingHealth = displayedHealth;
            else if (!dragging) laggingHealth = Mathf.MoveTowards(laggingHealth, displayedHealth, delayedBarSpeed * Time.deltaTime);

            dragging = false;

            if (healthFill != null)
            {
                healthFill.fillAmount = displayedHealth;
                healthFill.color = displayedHealth > healthMidThreshold ? healthHigh
                                  : displayedHealth > healthLowThreshold ? healthMid
                                  : healthLow;
            }

            if (healthDelayed != null)
            {
                healthDelayed.fillAmount = Mathf.Max(laggingHealth, displayedHealth);
            }
        }

        void TickSpeed()
        {
            if (actor == null || speedFill == null) return;

            float speed = actor.PlanarVelocity.magnitude;
            float max = speedBarMax > 0.1f ? speedBarMax : 8f;
            speedFill.fillAmount = Mathf.Clamp01(speed / max);
            RefreshSpeedPalette(speed);
        }

        void RefreshSpeedPalette(float speed)
        {
            if (speedFill != null)
                speedFill.color = speed >= sprintThreshold ? speedSprint : speedWalk;
        }

        void TickState()
        {
            // the state label is event driven (OnStateChanged); nothing to poll
        }

        void TickTimer()
        {
            if (timerLabel == null) return;

            float elapsed = finishedTime >= 0f ? finishedTime : Time.time - runStartTime;
            timerLabel.text = FormatTime(elapsed, true);
        }

        void TickVignette()
        {
            if (vignette == null) return;

            // The rewind deliberately puts health back below the low-health threshold, which lit the
            // damage vignette red over the whole effect. While rewinding, fade it out and let the
            // volume profile own the screen colour.
            if (rewindAbility == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) rewindAbility = player.GetComponent<TimeRewindAbility>();
            }

            if (rewindAbility != null && rewindAbility.IsRewinding)
            {
                vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, 8f * Time.unscaledDeltaTime);
                vignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteAlpha);
                return;
            }

            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, vignetteFade * Time.unscaledDeltaTime);

            // continuous pulse while critically low
            if (playerHealth != null && playerHealth.IsAlive && playerHealth.Normalized < lowHealthPulse)
            {
                float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f)) * 0.22f;
                vignetteAlpha = Mathf.Max(vignetteAlpha, pulse);
            }

            vignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteAlpha);
        }

        // ---------------------------------------------------------------- events

        void OnDamaged(Health source, float amount, Vector3 from, GameObject instigator)
        {
            dragging = true;                                   // hold the lagging bar this frame
            vignetteAlpha = Mathf.Min(1f, vignetteAlpha + vignetteFlash);
        }

        void OnDied(Health source, GameObject instigator)
        {
            vignetteAlpha = 1f;
            if (deathPanel != null) deathPanel.SetActive(true);
        }

        void OnRewindChanged(int charges)
        {
            if (rewindLabel != null) rewindLabel.text = charges.ToString();
            if (rewindSlot != null) rewindSlot.color = charges > 0 ? rewindReady : rewindEmpty;
        }

        void OnStateChanged(CharacterState from, CharacterState to)
        {
            if (stateLabel == null || to == null) return;

            string type = to.GetType().Name;
            string display;

            switch (type)
            {
                case "PlayerWallRunState": display = "WALL RUN"; break;
                case "PlayerClimbState": display = "CLIMB"; break;
                case "PlayerSwingState": display = "SWING"; break;
                case "PlayerSlideState": display = "SLIDE"; break;
                case "PlayerRollState": display = "ROLL"; break;
                case "PlayerAttackState": display = "ATTACK"; break;
                default: display = ""; break;
            }

            stateLabel.text = display;
        }

        void OnTeleported(Vector3 position, Quaternion rotation)
        {
            // Deliberately does nothing.
            //
            // This used to flash the damage vignette on any teleport, on the assumption that a teleport
            // means a respawn. But the character is repositioned with Teleport by the LEDGE CLIMB and by
            // the POLE SWING too - so every mantle and every swing exit flashed red as if the player had
            // been hit. A reposition is not damage.
            //
            // If a respawn system is added later and it should be felt, trigger the vignette from that
            // code directly rather than from the teleport event.
        }

        void OnLevelCompleted()
        {
            if (finishedTime >= 0f) return;
            finishedTime = Time.time - runStartTime;
            if (timerLabel != null) timerLabel.color = timerFinished;
            if (bestLabel != null) bestLabel.text = "BEST " + FormatTime(PlayerPrefs.GetFloat(MainMenu.BestTimeKey, 0f), true);
        }

        // ---------------------------------------------------------------- buttons

        /// <summary>
        /// Starts the run clock.
        ///
        /// Called by the intro sequence at the moment control is handed over, so the timer measures
        /// the run rather than however long the player sat on the start menu.
        /// </summary>
        public void StartRunTimer()
        {
            runStartTime = Time.time;
            finishedTime = -1f;
            if (timerLabel != null) timerLabel.color = timerNormal;
        }

        public void Retry()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplayScene);
        }

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuScene);
        }

        // ---------------------------------------------------------------- helpers

        public static string FormatTime(float seconds, bool valid)
        {
            if (!valid || seconds <= 0f) return "--:--.--";
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return string.Format("{0:00}:{1:00.00}", minutes, rest);
        }
    }
}
