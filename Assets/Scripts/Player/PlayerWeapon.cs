using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NasCon.Game
{
    /// <summary>
    /// Draws and sheathes the sword on X, and tells the combat states which combo to run.
    ///
    /// Two copies of the sword exist: the original on the back, and a runtime clone parented to the
    /// hand bone. Drawing and sheathing only swaps which one is visible - NOTHING is ever reparented,
    /// so the transforms are baked once and can never drift. This replaced the old reparenting
    /// approach, where the same object moved between two parents and the grip shifted with the
    /// animator's pose every time.
    /// </summary>
    public class PlayerWeapon : MonoBehaviour
    {
        [Header("Sword on the back")]
        [Tooltip("The existing sword on the spine. Found by name if left empty.")]
        [SerializeField] Transform sword;
        [SerializeField] string swordName = "SwordOnBack";

        [Header("Sword in the hand")]
        [Tooltip("Local offset for the hand copy. Tuned with F10 and baked.")]
        [SerializeField] Vector3 handLocalPosition = new Vector3(-0.687f, 0.157f, 0.034f);
        [SerializeField] Vector3 handLocalEuler = new Vector3(42.889f, 106.499f, 29.252f);
        [SerializeField] Vector3 handLocalScale = Vector3.one;

        [Header("Timing")]
        [Tooltip("Fraction of the draw clip at which the swords swap. The hand is at the shoulder around 70%.")]
        [SerializeField, Range(0f, 1f)] float handoverAt = 0.7f;

        [Header("Animator")]
        [SerializeField] string drawTrigger = "Draw";
        [SerializeField] string sheathTrigger = "Sheath";
        [SerializeField] float drawFallback = 0.63f;
        [SerializeField] float sheathFallback = 1.10f;

        [Header("Input")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] Key toggleKey = Key.X;
#else
        [SerializeField] KeyCode toggleKey = KeyCode.X;
#endif

        public bool IsEquipped { get; private set; }
        public bool IsTransitioning { get; private set; }

        Animator animator;
        Transform handParent;
        GameObject handCopy;
        float transitionEnd;
        bool pendingEquip;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();

            if (sword == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == swordName) { sword = t; break; }
            }

            if (animator != null)
                handParent = animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (sword != null && handParent != null)
            {
                // the hand copy: a clone, parented once, never moved again
                handCopy = Instantiate(sword.gameObject, handParent);
                handCopy.name = "SwordInHand";
                handCopy.transform.localPosition = handLocalPosition;
                handCopy.transform.localRotation = Quaternion.Euler(handLocalEuler);
                handCopy.transform.localScale = handLocalScale;
                handCopy.SetActive(false);

                // Blade trail. Added in code rather than on the prefab so it cannot be lost when the
                // sword asset is replaced, and because it rides the hand copy it is hidden with the
                // sword while sheathed - no extra bookkeeping.
                TrailRenderer trail = handCopy.AddComponent<TrailRenderer>();
                trail.time = 0.18f;
                trail.minVertexDistance = 0.06f;
                trail.numCapVertices = 2;
                trail.startWidth = 0.08f;
                trail.endWidth = 0f;
                trail.alignment = LineAlignment.View;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.material = TrailMaterial();
                trail.startColor = new Color(1f, 0.78f, 0.45f, 0.9f);
                trail.endColor = new Color(1f, 0.42f, 0.12f, 0f);
            }

            ApplyVisibility();
        }

        // One shared trail material for every instance of the player. Sprites/Default is in Unity's
        // always-included shader set, so it survives the build without any asset referencing it.
        static Material trailMaterial;

        static Material TrailMaterial()
        {
            if (trailMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                trailMaterial = s == null ? null : new Material(s);
            }
            return trailMaterial;
        }

        void Update()
        {
            if (IsTransitioning)
            {
                if (Time.time >= transitionEnd)
                {
                    IsTransitioning = false;
                    IsEquipped = pendingEquip;
                    ApplyVisibility();
                }
                return;
            }

            CaptureRequested();

            if (TogglePressed()) Begin(!IsEquipped);
        }

        /// <summary>
        /// Holds the hand grip every frame. With a single baked parent the offset cannot drift, but
        /// enforcing it means tuning the fields moves the sword live.
        /// </summary>
        void LateUpdate()
        {
            if (handCopy == null || handParent == null) return;

            handCopy.transform.localPosition = handLocalPosition;
            handCopy.transform.localRotation = Quaternion.Euler(handLocalEuler);
            handCopy.transform.localScale = handLocalScale;
        }

        public void Begin(bool equip)
        {
            if (IsTransitioning || sword == null) return;

            // plays at the start of the transition, not the handover - the sound of a draw belongs to
            // the motion that begins it
            Sfx.Play(equip ? "sword_draw" : "sword_sheat",
                     transform.position + Vector3.up * 1.2f, 0.75f);

            IsTransitioning = true;
            pendingEquip = equip;

            if (animator != null && !string.IsNullOrEmpty(equip ? drawTrigger : sheathTrigger))
                animator.SetTrigger(equip ? drawTrigger : sheathTrigger);

            float length = equip ? drawFallback : sheathFallback;
            transitionEnd = Time.time + length * handoverAt;

            // the swap happens at the handover moment, when the animation hides it
            Invoke("SwapVisibility", length * handoverAt);
        }

        void SwapVisibility()
        {
            IsEquipped = pendingEquip;
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            if (sword != null) sword.gameObject.SetActive(!IsEquipped);
            if (handCopy != null) handCopy.SetActive(IsEquipped);
        }

        /// <summary>
        /// F10 reads the hand copy's CURRENT local transform, applies it live, and logs the three
        /// lines to paste back for baking into the prefab.
        /// </summary>
        void CaptureRequested()
        {
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame) pressed = true;
#else
            if (Input.GetKeyDown(KeyCode.F10)) pressed = true;
#endif
            if (!pressed || handCopy == null) return;

            // F10 has to beat LateUpdate, so read from the copy, not from the fields
            Vector3 pos = handCopy.transform.localPosition;
            Vector3 euler = handCopy.transform.localEulerAngles;
            Vector3 scl = handCopy.transform.localScale;

            handLocalPosition = pos;
            handLocalEuler = euler;
            handLocalScale = scl;

            Debug.Log("[PlayerWeapon] CAPTURE - bake these onto the prefab:\n" +
                      pos.ToString("F4") + "\n" +
                      euler.ToString("F3") + "\n" +
                      scl.ToString("F2"));
        }

        public void ForceEquip()
        {
            CancelInvoke();
            IsTransitioning = false;
            IsEquipped = true;
            ApplyVisibility();
        }

        public void ForceSheathe()
        {
            CancelInvoke();
            IsTransitioning = false;
            IsEquipped = false;
            ApplyVisibility();
        }

        bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(toggleKey);
#endif
        }
    }
}
