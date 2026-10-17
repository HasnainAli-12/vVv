using UnityEngine;
using UnityEngine.UI;

namespace NasCon.Game
{
    /// <summary>
    /// The in-scene start menu and cinematic open.
    ///
    /// Runs as an explicit state machine - Menu, Starting, Standing, Playing - so it is always clear
    /// which part of the sequence is live and what is allowed to move.
    ///
    /// Deliberately does NOT use Time.timeScale. Freezing time that way would also stop the UI fades
    /// and the camera blend, which are the entire point of the sequence. Everything is frozen
    /// individually instead.
    ///
    /// Extends IntroSequence rather than replacing it: that script already owns the Intro trigger and
    /// the CharacterBrain block, both of which are tested and working.
    /// </summary>
    public class IntroSequenceController : MonoBehaviour
    {
        public enum Phase { Menu, Starting, Standing, Playing }

        [Header("Phase")]
        [SerializeField] Phase phase = Phase.Menu;

        [Tooltip("After the first run, a scene reload skips straight to gameplay.")]
        [SerializeField] bool skipOnReload = true;

        [Header("Menu UI")]
        [Tooltip("CanvasGroup on the menu canvas. Faded out on Start.")]
        [SerializeField] CanvasGroup menuGroup;

        [Tooltip("CanvasGroup on the gameplay HUD. Hidden in the menu, faded in near the end.")]
        [SerializeField] CanvasGroup hudGroup;

        [Tooltip("Full-screen black image for fades. Optional.")]
        [SerializeField] Image fadeImage;

        [SerializeField] Button startButton;

        [Header("Camera")]
        [Tooltip("The camera moved during the sequence. Leave empty for Camera.main.")]
        [SerializeField] Camera gameCamera;

        [Tooltip("Hero shot pose. A plain transform, not a second camera.")]
        [SerializeField] Transform menuCamPose;

        [Tooltip("Gentle sway of the hero shot, in degrees. 0 disables it.")]
        [SerializeField] float menuSwayDegrees = 0.6f;
        [SerializeField] float menuSwaySeconds = 7f;

        [Tooltip("Your gameplay follow camera's transform. The blend lands here.")]
        [SerializeField] Transform gameplayCamera;

        [Header("Timing (unscaled seconds)")]
        [SerializeField] float fadeOutTime = 0.65f;
        [SerializeField] float delayBeforeStand = 0.15f;
        [SerializeField] float cameraBlendTime = 3.0f;

        [Tooltip("Fraction of the blend at which the player gains control.")]
        [Range(0.1f, 1f)] [SerializeField] float controlAtFraction = 0.85f;

        [SerializeField] float hudFadeTime = 0.5f;

        [Header("Player")]
        [SerializeField] IntroSequence intro;
        [SerializeField] Animator playerAnimator;
        [SerializeField] GameObject playerRoot;

        [Tooltip("Animator state name for the intro pose. Must match PC_Locomotion.")]
        [SerializeField] string introStateName = "Intro";

        /// <summary>Cached so the menu pose call never allocates a string per frame.</summary>
        string IntroStateName { get { return introStateName; } }

        Lightbug.CharacterControllerPro.Implementation.CharacterBrain playerBrain;

        [Header("Audio")]
        [Tooltip("Started from the Start click - browsers block audio before a user gesture.")]
        [SerializeField] AudioSource music;

        [Header("Freeze")]
        [SerializeField] bool freezeEnemies = true;
        [SerializeField] bool freezeParticles = true;

        // survives the scene reload that Retry performs, which is what skips the menu on respawn
        static bool hasPlayedIntro;

        readonly System.Collections.Generic.List<Behaviour> frozenBrains = new System.Collections.Generic.List<Behaviour>();
        readonly System.Collections.Generic.List<Animator> frozenAnimators = new System.Collections.Generic.List<Animator>();
        readonly System.Collections.Generic.List<ParticleSystem> frozenParticles = new System.Collections.Generic.List<ParticleSystem>();

        Vector3 camStartPos;
        Quaternion camStartRot;
        Vector3 gameTargetPos;
        Quaternion gameTargetRot;
        float phaseTime;
        bool controlGiven;
        bool timerStarted;

        public Phase CurrentPhase { get { return phase; } }

        void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
            if (intro == null) intro = FindFirstObjectByType<IntroSequence>();
            if (playerAnimator == null && playerRoot != null)
                playerAnimator = playerRoot.GetComponentInChildren<Animator>();

            if (playerBrain == null && playerRoot != null)
                playerBrain = playerRoot.GetComponentInChildren<Lightbug.CharacterControllerPro.Implementation.CharacterBrain>();
        }

        void Start()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartPressed);

            if (skipOnReload && hasPlayedIntro) { EnterPlayingInstant(); return; }

            EnterMenu();
        }

        // ---------------------------------------------------------------- phases

        void EnterMenu()
        {
            phase = Phase.Menu;
            phaseTime = 0f;

            if (menuGroup != null) { menuGroup.alpha = 1f; menuGroup.blocksRaycasts = true; menuGroup.interactable = true; }
            if (hudGroup != null) { hudGroup.alpha = 0f; hudGroup.blocksRaycasts = false; hudGroup.interactable = false; }
            if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            CacheFreezeTargets();

            // Block input for the WHOLE menu, not just the stand-up.
            //
            // IntroSequence does this too, but only once Begin() runs - and that happens when the menu
            // closes. Until then the CharacterBrain was live, so Space both started the game and fired
            // a jump. Nothing about the menu should reach the character.
            if (playerBrain != null) playerBrain.enabled = false;

            // Put the character INTO the intro pose before freezing. Freezing alone holds whatever
            // state the animator happened to be in on load - which is Locomotion, standing upright -
            // so the menu showed an idle rather than a stance.
            //
            // Update(0f) forces the animator to sample the new state on this frame instead of next,
            // so the very first rendered frame of the menu already shows the pose.
            if (playerAnimator != null)
            {
                playerAnimator.Play(IntroStateName, 0, 0f);
                playerAnimator.Update(0f);
            }

            FreezeAll(true);

            // Snapshot where gameplay wants the camera BEFORE this controller takes it over.
            //
            // Blending toward gameplayCamera's LIVE transform does not work here: gameplayCamera IS
            // the camera this script is writing, so reading it each frame just reads back our own
            // previous write and the camera converges on itself instead of moving. Capture it once,
            // while the follow rig still owns it.
            if (gameplayCamera != null)
            {
                gameTargetPos = gameplayCamera.position;
                gameTargetRot = gameplayCamera.rotation;
            }

            if (gameCamera != null && menuCamPose != null)
            {
                gameCamera.transform.position = menuCamPose.position;
                gameCamera.transform.rotation = menuCamPose.rotation;
            }
        }

        void OnStartPressed()
        {
            // every press after the first lands here and is ignored, which is what makes
            // spam-clicking Start safe
            if (phase != Phase.Menu) return;

            if (startButton != null) startButton.interactable = false;

            if (music != null && !music.isPlaying) music.Play();

            phase = Phase.Starting;
            phaseTime = 0f;
        }

        void EnterStanding()
        {
            phase = Phase.Standing;
            phaseTime = 0f;

            if (gameCamera != null)
            {
                camStartPos = gameCamera.transform.position;
                camStartRot = gameCamera.transform.rotation;
            }

            FreezeAll(false);

            if (playerAnimator != null) playerAnimator.speed = 1f;

            // Deliberately NOT calling intro.Begin().
            //
            // The character is already IN the Intro state, frozen at frame 0. Begin() sets the Intro
            // trigger, which makes the animator enter that same state a second time - that was the
            // double stand-up. Unfreezing above IS the hand-off.
            //
            // IntroSequence is disabled rather than left enabled so it cannot re-block the brain or
            // fire the trigger behind this controller's back. It stays on the object, harmless.
            if (intro != null) intro.enabled = false;

            if (playerBrain != null) playerBrain.enabled = true;
        }

        void EnterPlaying()
        {
            phase = Phase.Playing;
            hasPlayedIntro = true;

            if (startButton != null) startButton.interactable = false;

            // The HUD has to accept clicks once gameplay starts.
            //
            // blocksRaycasts = false on a CanvasGroup kills raycasts for EVERY child, and the death
            // panel's Retry and Menu buttons live on that canvas - which is why the death screen was
            // unclickable. Individual non-interactive HUD images already set raycastTarget = false,
            // so nothing here needs to be shielded from the mouse.
            if (hudGroup != null) { hudGroup.blocksRaycasts = true; hudGroup.interactable = true; }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void EnterPlayingInstant()
        {
            phase = Phase.Playing;

            if (menuGroup != null) { menuGroup.alpha = 0f; menuGroup.blocksRaycasts = false; menuGroup.interactable = false; }
            if (hudGroup != null) { hudGroup.alpha = 1f; hudGroup.blocksRaycasts = true; hudGroup.interactable = true; }
            if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, 0f);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // no camera work: the gameplay camera is already where it belongs on a reload
        }

        // ---------------------------------------------------------------- loop

        /// <summary>
        /// LateUpdate, not Update, so the camera write lands AFTER the gameplay follow rig has written
        /// its own transform. Running in Update meant the follow rig overwrote the hero pose every
        /// frame and the menu camera did nothing.
        /// </summary>
        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            phaseTime += dt;

            switch (phase)
            {
                case Phase.Menu: MenuTick(); break;
                case Phase.Starting: StartingTick(); break;
                case Phase.Standing: StandingTick(); break;
                case Phase.Playing: break;
            }
        }

        void MenuTick()
        {
            if (gameCamera != null && menuCamPose != null)
            {
                float sway = Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / Mathf.Max(0.01f, menuSwaySeconds))) * menuSwayDegrees;
                gameCamera.transform.position = menuCamPose.position;
                gameCamera.transform.rotation = menuCamPose.rotation * Quaternion.Euler(0f, 0f, sway);
            }

            if (StartKeyPressed()) OnStartPressed();
        }

        void StartingTick()
        {
            if (menuGroup != null)
            {
                float t = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, fadeOutTime));
                menuGroup.alpha = 1f - t;
                if (t >= 1f) { menuGroup.blocksRaycasts = false; menuGroup.interactable = false; }
            }

            if (phaseTime < fadeOutTime + delayBeforeStand) return;
            EnterStanding();
        }

        void StandingTick()
        {
            float total = Mathf.Max(0.01f, cameraBlendTime);
            float t = Mathf.Clamp01((phaseTime - fadeOutTime - delayBeforeStand) / total);

            // smoothstep: eases in and out, so neither end snaps
            float eased = t * t * (3f - 2f * t);

            if (gameCamera != null)
            {
                // blend toward the pose captured in EnterMenu, not the live one - see the note there
                gameCamera.transform.position = Vector3.Lerp(camStartPos, gameTargetPos, eased);
                gameCamera.transform.rotation = Quaternion.Slerp(camStartRot, gameTargetRot, eased);
            }

            if (hudGroup != null)
            {
                float hudWindow = Mathf.Max(0.01f, hudFadeTime / total);
                float hudStart = Mathf.Max(0f, controlAtFraction - hudWindow);
                hudGroup.alpha = Mathf.Clamp01((t - hudStart) / hudWindow);
            }

            if (!controlGiven && t >= controlAtFraction)
            {
                controlGiven = true;
                GiveControl();
            }

            if (t >= 1f) EnterPlaying();
        }

        void GiveControl()
        {
            // IntroSequence restores the CharacterBrain on its own when the clip finishes, so this
            // does not fight it - it only starts the clock and reveals the HUD.
            if (timerStarted) return;
            timerStarted = true;

            if (hudGroup == null) return;
            var hud = hudGroup.GetComponent<NasCon.UI.PlayerHUD>();
            if (hud != null) hud.StartRunTimer();
        }

        // ---------------------------------------------------------------- freeze

        void CacheFreezeTargets()
        {
            frozenBrains.Clear();
            frozenAnimators.Clear();
            frozenParticles.Clear();

            if (freezeEnemies)
            {
                foreach (var b in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
                    if (b != null) frozenBrains.Add(b);

                foreach (var a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    if (a == null || a == playerAnimator) continue;
                    if (playerRoot != null && a.transform.root == playerRoot.transform.root) continue;
                    frozenAnimators.Add(a);
                }
            }

            if (freezeParticles)
            {
                foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    if (ps != null) frozenParticles.Add(ps);
            }
        }

        /// <summary>
        /// Freezes or thaws everything except the player's animator, which is handled separately so
        /// the kneel pose can be held independently of the enemies.
        /// </summary>
        void FreezeAll(bool freeze)
        {
            for (int i = 0; i < frozenBrains.Count; i++)
                if (frozenBrains[i] != null) frozenBrains[i].enabled = !freeze;

            for (int i = 0; i < frozenAnimators.Count; i++)
                if (frozenAnimators[i] != null) frozenAnimators[i].speed = freeze ? 0f : 1f;

            for (int i = 0; i < frozenParticles.Count; i++)
            {
                var ps = frozenParticles[i];
                if (ps == null) continue;
                if (freeze) ps.Pause(); else ps.Play();
            }

            if (playerAnimator != null) playerAnimator.speed = freeze ? 0f : 1f;
        }

        // ---------------------------------------------------------------- input

        bool StartKeyPressed()
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame ||
                              k.spaceKey.wasPressedThisFrame)) return true;

            var g = UnityEngine.InputSystem.Gamepad.current;
            if (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame)) return true;

            return false;
        }

        void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(OnStartPressed);

            // never leave the world frozen if this is torn down mid-sequence
            FreezeAll(false);
        }
    }
}
