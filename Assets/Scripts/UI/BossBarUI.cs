using NasCon.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NasCon.UI
{
    /// <summary>
    /// Boss health bar: hidden until Show() is called with a boss, then fades in.
    ///
    /// Pushed entirely by Health's events - Damaged and Died - so there is no polling and no
    /// per-frame comparison against a value that almost never changes. The script subscribes and
    /// unsubscribes cleanly, which matters because a dead boss leaves the bar subscribed otherwise.
    /// </summary>
    public class BossBarUI : MonoBehaviour
    {
        [Header("Elements")]
        [Tooltip("Root CanvasGroup. Faded in and out; starts hidden.")]
        [SerializeField] CanvasGroup group;

        [SerializeField] Image fill;
        [Tooltip("Pale segment behind the fill that drains slowly, so a hit is readable.")]
        [SerializeField] Image delayed;

        [SerializeField] TMP_Text nameLabel;

        [Header("Colours")]
        [SerializeField] Color bossFill = new Color(0.78f, 0.16f, 0.14f, 1f);
        [SerializeField] Color bossDelayed = new Color(0.95f, 0.85f, 0.55f, 0.75f);

        [Header("Timing")]
        [SerializeField] float fadeIn = 0.35f;
        [SerializeField] float fadeOut = 0.5f;
        [Tooltip("How fast the delayed bar catches up, in fraction per second.")]
        [SerializeField] float delayedSpeed = 0.35f;
        [Tooltip("Seconds the bar lingers after the boss dies before fading out.")]
        [SerializeField] float holdOnDeath = 2.5f;

        Health boss;
        float shown;          // current fill, 0..1
        float shownDelayed;   // trailing fill, 0..1
        float targetAlpha;
        float deathHoldUntil = -1f;
        bool subscribed;

        void Awake()
        {
            if (fill != null) { fill.raycastTarget = false; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; }
            if (delayed != null) { delayed.raycastTarget = false; delayed.type = Image.Type.Filled; delayed.fillMethod = Image.FillMethod.Horizontal; }

            SetVisible(false, true);
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Binds to a boss and shows the bar. Name is passed in rather than read from the object so
        /// the display name can differ from the GameObject name.
        /// </summary>
        public void Show(Health bossHealth, string displayName)
        {
            Unsubscribe();

            boss = bossHealth;
            if (boss == null) { Hide(); return; }

            nameLabel?.SetText(displayName);

            shown = boss.Normalized;
            shownDelayed = shown;
            deathHoldUntil = -1f;

            boss.Damaged += OnBossDamaged;
            boss.Died += OnBossDied;
            subscribed = true;

            SetVisible(true, false);
            Apply();
        }

        public void Hide()
        {
            Unsubscribe();
            boss = null;
            SetVisible(false, false);
        }

        void Unsubscribe()
        {
            if (!subscribed || boss == null) { subscribed = false; return; }
            boss.Damaged -= OnBossDamaged;
            boss.Died -= OnBossDied;
            subscribed = false;
        }

        void OnBossDamaged(Health source, float amount, Vector3 position, GameObject instigator)
        {
            // the fill is read in Update from Normalized; nothing to do here but let the trail lag
        }

        void OnBossDied(Health source, GameObject instigator)
        {
            deathHoldUntil = Time.unscaledTime + holdOnDeath;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (group != null)
            {
                float a = Mathf.MoveTowards(group.alpha, targetAlpha, dt / Mathf.Max(0.01f, targetAlpha > group.alpha ? fadeIn : fadeOut));
                if (!Mathf.Approximately(a, group.alpha)) group.alpha = a;
            }

            if (boss != null)
            {
                shown = boss.Normalized;

                // the trailing bar only ever drains toward the real value, never above it
                if (shownDelayed > shown) shownDelayed = Mathf.MoveTowards(shownDelayed, shown, delayedSpeed * dt);
                else shownDelayed = shown;
            }

            Apply();

            if (deathHoldUntil > 0f && Time.unscaledTime >= deathHoldUntil)
            {
                deathHoldUntil = -1f;
                SetVisible(false, false);
            }
        }

        void Apply()
        {
            if (fill != null) fill.fillAmount = shown;
            if (delayed != null) delayed.fillAmount = shownDelayed;
        }

        void SetVisible(bool visible, bool instant)
        {
            targetAlpha = visible ? 1f : 0f;

            if (group != null)
            {
                group.alpha = instant ? targetAlpha : group.alpha;
                group.blocksRaycasts = false;   // never intercept anything
                group.interactable = false;
            }

            if (gameObject.activeSelf != visible && instant)
                gameObject.SetActive(visible);
            else if (visible)
                gameObject.SetActive(true);
        }
    }
}
