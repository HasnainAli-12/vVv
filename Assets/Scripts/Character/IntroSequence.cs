using System;
using System.Collections;
using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// Plays a one-shot intro animation at the start of the scene, holds the player still while it
    /// runs, then hands control over.
    ///
    /// Input is blocked by disabling the CharacterBrain rather than freezing time, because the intro
    /// animation has to actually play. CharacterBrain is what samples the input handler each frame,
    /// so with it off the character receives nothing and simply stands there.
    ///
    /// The animation itself is a state in PC_Locomotion reached by the "Intro" trigger; that state
    /// has an exit-time transition back to Locomotion, so nothing here has to stop it.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [Header("Animation")]
        [Tooltip("Trigger name in PC_Locomotion.")]
        [SerializeField] string trigger = "Intro";

        [Tooltip("How long to hold before releasing control. 0 uses the clip's own length.")]
        [SerializeField] float holdDuration;

        [Tooltip("Extra pause after the clip finishes before the player can move.")]
        [SerializeField] float settleAfter = 0.25f;

        [Header("Behaviour")]
        [Tooltip("Freeze player input for the duration.")]
        [SerializeField] bool blockInput = true;

        [Tooltip("Allow any jump press to cut the intro short. Recommended - nobody wants to be stuck watching.")]
        [SerializeField] bool allowSkip = true;

        [Tooltip("Earliest the intro can be skipped, in seconds.")]
        [SerializeField] float skipAfter = 0.3f;

        [Tooltip("Start immediately on scene load. Turn OFF when a menu drives the sequence.")]
        [SerializeField] bool autoPlay = true;

        [Header("Wiring (found automatically if empty)")]
        [SerializeField] Animator animator;
        [SerializeField] CharacterBrain brain;

        /// <summary>True while the intro is playing. Useful for holding a camera or a HUD element.</summary>
        public static bool IsPlaying { get; private set; }

        /// <summary>Fired when control is handed back to the player.</summary>
        public event Action Finished;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (brain == null) brain = GetComponentInChildren<CharacterBrain>();
        }

        void Start()
        {
            if (autoPlay) StartCoroutine(Run());
        }

        /// <summary>
        /// Starts the intro on demand, for when a menu owns the sequence rather than the scene load.
        ///
        /// Safe if already playing: the menu can call this without having to know.
        /// </summary>
        public void Begin()
        {
            if (IsPlaying) return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsPlaying = true;

            if (blockInput && brain != null) brain.enabled = false;

            if (animator != null && !string.IsNullOrEmpty(trigger))
                animator.SetTrigger(trigger);

            // let the animator actually enter the Intro state before measuring it, otherwise
            // ClipLength reads whatever state was current a frame ago
            yield return null;

            float duration = holdDuration > 0f ? holdDuration : ClipLength();
            float elapsed = 0f;

            while (elapsed < duration + settleAfter)
            {
                // unscaled so a paused game does not leave the intro hanging forever
                elapsed += Time.unscaledDeltaTime;

                if (allowSkip && elapsed >= skipAfter && SkipPressed())
                {
                    // put the animator back on its feet immediately rather than waiting out the clip
                    if (animator != null) animator.ResetTrigger(trigger);
                    break;
                }

                yield return null;
            }

            if (blockInput && brain != null) brain.enabled = true;

            IsPlaying = false;
            if (Finished != null) Finished();
        }

        float ClipLength()
        {
            if (animator == null) return 1f;

            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips != null && clips.Length > 0 && clips[0].clip != null)
                return clips[0].clip.length;

            return 1f;
        }

        bool SkipPressed()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                return true;

            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            if (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame))
                return true;

            return false;
        }

        void OnDisable()
        {
            // never leave the player unable to move if this is torn down mid-intro
            if (blockInput && brain != null) brain.enabled = true;
            IsPlaying = false;
        }
    }
}
