using UnityEngine;
using UnityEngine.InputSystem;
using Lightbug.CharacterControllerPro.Core;

namespace NasCon.Game
{
    /// <summary>
    /// Minimal third-person orbit camera driven by the Look action of InputSystem_Actions.
    /// Its Transform doubles as Character Controller Pro's "external reference", which is what
    /// makes character movement camera-relative.
    /// </summary>
    public class ThirdPersonOrbitCamera : MonoBehaviour
    {
        [SerializeField] Transform target = null;
        [SerializeField] InputActionAsset actions = null;
        [SerializeField] string actionMapName = "Player";

        [Header("Orbit")]
        [SerializeField] float distance = 4.5f;
        [SerializeField] float pivotHeight = 1.3f;
        [SerializeField] float minPitch = -35f;
        [SerializeField] float maxPitch = 65f;
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField] float followSmoothing = 14f;
        [SerializeField] float startYaw = 0f;
        [SerializeField] float startPitch = 12f;

        [Header("Cursor")]
        [SerializeField] bool lockCursor = true;
        [SerializeField] Key toggleCursorKey = Key.Escape;

        [Header("Speed FOV")]
        [Tooltip("Degrees of field of view added when moving at full sprint speed.")]
        [SerializeField] float sprintFovBoost = 10f;

        [Tooltip("Below this planar speed there is no FOV boost. Keep it above the walk speed so walking stays neutral.")]
        [SerializeField] float minSpeedForFov = 4f;

        [Tooltip("Planar speed at which the full boost is applied. Should match the character's boost (sprint) speed.")]
        [SerializeField] float fullFovSpeed = 8f;

        [Tooltip("How quickly the FOV catches up to the target value.")]
        [SerializeField] float fovSmoothing = 6f;

        [Header("Wall run")]
        [Tooltip("Extra FOV while wall running, on top of any speed FOV. Sells the acceleration.")]
        [SerializeField] float wallRunFovBoost = 10f;

        [Tooltip("How quickly the camera rolls towards the wall-run tilt. Snapping would break the illusion.")]
        [SerializeField] float rollRate = 6f;

        [Header("Collision")]
        [Tooltip("Layers the camera should not pass through.")]
        [SerializeField] LayerMask collisionMask = ~0;

        [Tooltip("Radius of the camera probe. Roughly the camera's near-plane footprint.")]
        [SerializeField] float collisionRadius = 0.25f;

        [Tooltip("Gap kept between the camera and whatever it hits.")]
        [SerializeField] float collisionPadding = 0.05f;

        [Tooltip("The camera never comes closer than this to the pivot.")]
        [SerializeField] float minDistance = 0.6f;

        [Tooltip("How fast the camera eases back out once the obstruction clears.")]
        [SerializeField] float extendSmoothing = 6f;

        InputActionMap map;
        InputAction lookAction;
        Camera cam;
        CharacterActor actor;
        float baseFov;
        float currentDistance;
        Vector3 smoothedPivot;
        bool pivotReady;
        float yaw;
        float pitch;
        float currentRoll;
        static readonly RaycastHit[] hitBuffer = new RaycastHit[8];

        void Awake()
        {
            yaw = startYaw;
            pitch = startPitch;

            cam = GetComponent<Camera>();
            baseFov = cam != null ? cam.fieldOfView : 60f;

            if (actions == null)
            {
                Debug.LogError("[ThirdPersonOrbitCamera] No InputActionAsset assigned.");
                return;
            }

            map = actions.FindActionMap(actionMapName, false);
            if (map == null)
            {
                Debug.LogError("[ThirdPersonOrbitCamera] Action map '" + actionMapName + "' not found.");
                return;
            }

            lookAction = map.FindAction("Look", false);
            if (lookAction == null)
                Debug.LogWarning("[ThirdPersonOrbitCamera] 'Look' action not found.");

            if (target == null)
            {
                Debug.LogWarning("[ThirdPersonOrbitCamera] No target assigned.");
            }
            else
            {
                actor = target.GetComponentInParent<CharacterActor>();
                if (actor == null)
                    Debug.LogWarning("[ThirdPersonOrbitCamera] No CharacterActor found on the target; speed FOV is disabled.");
            }
        }

        void OnEnable()
        {
            if (map != null && !map.enabled)
                map.Enable();

            if (lockCursor)
                SetCursorLocked(true);
        }

        void OnDisable()
        {
            if (map != null && map.enabled)
                map.Disable();
        }

        void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (toggleCursorKey != Key.None &&
                Keyboard.current != null &&
                Keyboard.current[toggleCursorKey].wasPressedThisFrame)
            {
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
            }
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            // Time.timeScale is zero while the pause menu is up. Update and LateUpdate still run at
            // zero timescale, so without this the mouse keeps swinging the camera behind the menu.
            if (lookAction != null && Time.timeScale > 0f)
            {
                Vector2 look = lookAction.ReadValue<Vector2>();
                yaw += look.x * sensitivity;
                pitch = Mathf.Clamp(pitch - look.y * sensitivity, minPitch, maxPitch);
            }

            // Wall run tilts the view towards the wall. Lerped rather than snapped - snapping here
            // reads as a glitch rather than as leaning.
            float targetRoll = PlayerWallRunState.IsActive ? PlayerWallRunState.TargetRoll : 0f;
            float rollT = rollRate > 0f ? 1f - Mathf.Exp(-rollRate * Time.deltaTime) : 1f;
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, rollT);

            // Hit shake. Additive and decaying, ticked on unscaled time so it keeps settling while
            // hit-stop has time nearly frozen. Purely a camera offset - nothing gameplay-facing moves.
            CameraShake.Tick(Time.unscaledDeltaTime);
            float shake = CameraShake.Magnitude;
            const float shakeAngle = 2.5f;                 // degrees at full strength
            float shakePitch = shake * shakeAngle * Mathf.Sin(Time.unscaledTime * 47f);
            float shakeYaw = shake * shakeAngle * Mathf.Cos(Time.unscaledTime * 39f);
            float shakeRoll = shake * shakeAngle * Mathf.Sin(Time.unscaledTime * 53f);

            Quaternion rotation = Quaternion.Euler(pitch + shakePitch, yaw + shakeYaw, currentRoll + shakeRoll);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;

            if (!pivotReady)
            {
                smoothedPivot = pivot;
                currentDistance = distance;
                pivotReady = true;
            }

            // smooth the pivot so the camera keeps its soft follow, then resolve distance separately
            // so collision can snap in instantly while easing back out.
            float follow = followSmoothing > 0f ? 1f - Mathf.Exp(-followSmoothing * Time.deltaTime) : 1f;
            smoothedPivot = Vector3.Lerp(smoothedPivot, pivot, follow);

            Vector3 direction = rotation * new Vector3(0f, 0f, -1f);
            float wanted = ResolveDistance(smoothedPivot, direction);

            if (wanted < currentDistance)
            {
                currentDistance = wanted;   // never let geometry clip: snap in immediately
            }
            else
            {
                float extend = extendSmoothing > 0f ? 1f - Mathf.Exp(-extendSmoothing * Time.deltaTime) : 1f;
                currentDistance = Mathf.Lerp(currentDistance, wanted, extend);
            }

            transform.position = smoothedPivot + direction * currentDistance;
            transform.rotation = rotation;

            UpdateFov();
        }

        /// <summary>
        /// Distance the camera may sit from the pivot without passing through geometry.
        /// The character's own colliders are ignored.
        /// </summary>
        float ResolveDistance(Vector3 pivot, Vector3 direction)
        {
            if (collisionRadius <= 0f)
                return distance;

            int count = Physics.SphereCastNonAlloc(
                pivot, collisionRadius, direction, hitBuffer, distance, collisionMask, QueryTriggerInteraction.Ignore);

            float nearest = distance;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hitBuffer[i];

                // distance <= 0 means the cast started inside that collider
                if (hit.collider == null || hit.distance <= 0f)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (target != null && (hitTransform == target.root || hitTransform.IsChildOf(target.root)))
                    continue;

                if (hit.distance < nearest)
                    nearest = hit.distance;
            }

            return Mathf.Max(minDistance, nearest - collisionPadding);
        }

        /// <summary>
        /// Widens the field of view with planar speed so sprinting reads as fast.
        /// Driven by actual velocity rather than the sprint button, so it ramps with acceleration,
        /// decays as you slow down, and gives no zoom when holding sprint while standing still.
        /// PlanarVelocity is used so falling does not spike the FOV.
        /// </summary>
        void UpdateFov()
        {
            if (cam == null)
                return;

            float targetFov = baseFov;

            if (actor != null && fullFovSpeed > minSpeedForFov)
            {
                float speed01 = Mathf.InverseLerp(minSpeedForFov, fullFovSpeed, actor.PlanarVelocity.magnitude);
                targetFov = baseFov + sprintFovBoost * speed01;
            }

            // wall run adds on top of the speed boost, so the two compound during a sprint into a run
            if (PlayerWallRunState.IsActive)
                targetFov += wallRunFovBoost;

            float t = fovSmoothing > 0f ? 1f - Mathf.Exp(-fovSmoothing * Time.deltaTime) : 1f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, t);
        }
    }
}
