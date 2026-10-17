using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NasCon.UI
{
    /// <summary>
    /// The square frame showing whatever is currently equipped.
    ///
    /// Driven by SetItem, not by polling - whoever changes the item calls SetItem once. Polling a
    /// sprite reference every frame just to notice it did not change is the classic way to make a
    /// HUD cost more than the game.
    ///
    /// Settling on a sprite rebuilds that Image's mesh once, so the pop animation is scaled on a
    /// child rather than the Image itself: changing a RectTransform's scale does not rebuild the
    /// canvas batch, but changing the sprite does.
    /// </summary>
    public class ActiveSlotUI : MonoBehaviour
    {
        [Header("Elements")]
        [Tooltip("The icon image. Swapping its sprite rebuilds the canvas once - that is intended.")]
        [SerializeField] Image icon;

        [Tooltip("Scales during the pop. Must NOT be the icon itself, or the mesh rebuilds every frame.")]
        [SerializeField] RectTransform popTarget;

        [Tooltip("Frame behind the icon. Tinted to the equipped state.")]
        [SerializeField] Image frame;

        [Tooltip("Small key hint, e.g. \"X\". Hidden when nothing is equipped.")]
        [SerializeField] TMP_Text keyHint;

        [Tooltip("Shown when nothing is equipped. The icon and hint hide while this is active.")]
        [SerializeField] GameObject emptyState;

        [Header("Colours")]
        [SerializeField] Color equippedTint = new Color(0.95f, 0.72f, 0.20f, 1f);   // fire amber
        [SerializeField] Color emptyTint = new Color(0.35f, 0.32f, 0.30f, 0.85f);   // dead slate

        [Header("Pop animation")]
        [Tooltip("Peak scale of the pop when the icon changes.")]
        [SerializeField] float popScale = 1.18f;

        [Tooltip("Length of the pop, in seconds.")]
        [SerializeField] float popDuration = 0.18f;

        Sprite currentSprite;
        string currentHint;
        bool isEmpty = true;
        float popTime = -1f;

        void Awake()
        {
            // nothing here is clickable; leaving raycast on makes the HUD eat clicks for nothing
            if (icon != null) icon.raycastTarget = false;
            if (frame != null) frame.raycastTarget = false;
            if (popTarget == null && icon != null) popTarget = icon.rectTransform;
        }

        void OnEnable()
        {
            // a pop started before the object was disabled would otherwise snap the scale on re-enable
            if (popTime >= 0f && popTarget != null) popTarget.localScale = Vector3.one;
            popTime = -1f;
        }

        /// <summary>
        /// Shows an item. Passing the same sprite and hint again is a no-op, so calling this from a
        /// periodic check costs nothing.
        /// </summary>
        public void SetItem(Sprite sprite, string keyHintText)
        {
            if (!isEmpty && sprite == currentSprite && keyHintText == currentHint) return;

            currentSprite = sprite;
            currentHint = keyHintText;
            isEmpty = sprite == null;

            if (icon != null)
            {
                icon.enabled = !isEmpty;
                if (!isEmpty) icon.sprite = sprite;
            }

            if (keyHint != null)
            {
                keyHint.gameObject.SetActive(!isEmpty && !string.IsNullOrEmpty(keyHintText));
                if (!isEmpty && !string.IsNullOrEmpty(keyHintText)) keyHint.SetText(keyHintText);
            }

            if (emptyState != null) emptyState.SetActive(isEmpty);
            if (frame != null) frame.color = isEmpty ? emptyTint : equippedTint;

            // clearing should not celebrate
            if (!isEmpty) popTime = 0f;
        }

        /// <summary>Empties the slot - nothing equipped.</summary>
        public void Clear()
        {
            SetItem(null, null);
        }

        void Update()
        {
            if (popTime < 0f || popTarget == null) return;

            popTime += Time.unscaledDeltaTime;
            float t = popTime / Mathf.Max(0.01f, popDuration);

            if (t >= 1f)
            {
                popTarget.localScale = Vector3.one;
                popTime = -1f;
                return;
            }

            // 1 -> popScale -> 1, eased out. Unscaled so pausing does not freeze it mid-pop.
            float bump = Mathf.Sin(t * Mathf.PI);
            popTarget.localScale = Vector3.one * (1f + (popScale - 1f) * bump);
        }
    }
}
