using UnityEngine;
using UnityEngine.UI;

namespace NasCon.UI
{
    /// <summary>
    /// HUD for the rewind ability.
    ///
    /// Three elements: an icon that stays hidden until the orb is taken, a cooldown fill, and a
    /// history fill showing how much rewind is banked. Everything is a fill on an Image, so there is
    /// no text formatting and therefore no per-frame string allocation.
    /// </summary>
    public class RewindUI : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Leave empty to find the player's ability on Start.")]
        [SerializeField] NasCon.Game.TimeRewindAbility ability;

        [Header("Elements")]
        [Tooltip("Root shown only once the ability is unlocked.")]
        [SerializeField] GameObject root;

        [Tooltip("Fill 0..1. Represents available history, 0 = empty.")]
        [SerializeField] Image historyFill;

        [Tooltip("Fill 0..1. Represents cooldown remaining, 0 = ready.")]
        [SerializeField] Image cooldownFill;

        [Header("Colours")]
        [SerializeField] Color readyColor = new Color(0.25f, 1f, 0.75f);
        [SerializeField] Color rewindingColor = new Color(0.55f, 1f, 1f);
        [SerializeField] Color coolingColor = new Color(0.9f, 0.45f, 0.2f);

        [Header("Smoothing")]
        [Tooltip("How quickly the bars follow the real values, per second.")]
        [SerializeField] float barSpeed = 6f;

        float shownHistory;
        float shownCooldown;

        void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        void Start()
        {
            if (ability == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) ability = player.GetComponent<NasCon.Game.TimeRewindAbility>();
            }

            if (ability == null)
            {
                enabled = false;
                return;
            }

            ability.Unlocked += OnUnlocked;

            // if the ability was already unlocked before this UI came up, catch up
            if (ability.IsUnlocked) OnUnlocked();
        }

        void OnDestroy()
        {
            if (ability != null) ability.Unlocked -= OnUnlocked;
        }

        void OnUnlocked()
        {
            if (root != null) root.SetActive(true);
        }

        void Update()
        {
            if (ability == null) return;

            float targetHistory = ability.RecordSeconds > 0f
                ? Mathf.Clamp01(ability.StoredSeconds / ability.RecordSeconds)
                : 0f;

            float targetCooldown = ability.CooldownNormalized;

            float k = 1f - Mathf.Exp(-barSpeed * Time.unscaledDeltaTime);
            shownHistory = Mathf.Lerp(shownHistory, targetHistory, k);
            shownCooldown = Mathf.Lerp(shownCooldown, targetCooldown, k);

            if (historyFill != null)
            {
                historyFill.fillAmount = shownHistory;
                historyFill.color = ability.IsRewinding ? rewindingColor
                                  : ability.CanRewind ? readyColor
                                  : coolingColor;
            }

            if (cooldownFill != null)
                cooldownFill.fillAmount = shownCooldown;
        }
    }
}
