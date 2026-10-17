using UnityEngine;
using UnityEngine.InputSystem;
using Lightbug.CharacterControllerPro.Implementation;

namespace NasCon.Game
{
    /// <summary>
    /// Bridges the new Input System (InputSystem_Actions) into Character Controller Pro.
    ///
    /// CCP's built-in UnityInputHandler calls the legacy Input.GetAxis/GetButton API, which is
    /// unavailable because this project runs with activeInputHandler = 1 (Input System Package only).
    /// Assign this component to CharacterBrain > Input Handler Settings > Input Handler, and set
    /// Human Input Type to "Custom".
    ///
    /// The string names handled here are hard-coded by CharacterActions.SetValues and cannot change.
    /// </summary>
    public class CCPInputSystemHandler : InputHandler
    {
        const string NameMovement = "Movement";
        const string NameJump = "Jump";
        const string NameRun = "Run";
        const string NameCrouch = "Crouch";
        const string NameInteract = "Interact";
        const string NameDash = "Dash";
        const string NamePitch = "Pitch";
        const string NameAttack = "Attack";
        const string NameKick = "Kick";

        [SerializeField] InputActionAsset actions = null;
        [SerializeField] string actionMapName = "Player";

        InputActionMap map;
        InputAction moveAction;
        InputAction jumpAction;
        InputAction runAction;
        InputAction crouchAction;
        InputAction interactAction;
        InputAction dashAction;
        InputAction lookAction;
        InputAction attackAction;
        InputAction kickAction;

        bool resolved;
        bool failed;

        void Resolve()
        {
            if (resolved || failed)
                return;

            if (actions == null)
            {
                failed = true;
                Debug.LogError("[CCPInputSystemHandler] No InputActionAsset assigned. Assign InputSystem_Actions in the Inspector.");
                return;
            }

            map = actions.FindActionMap(actionMapName, false);
            if (map == null)
            {
                failed = true;
                Debug.LogError("[CCPInputSystemHandler] Action map '" + actionMapName + "' not found in " + actions.name + ".");
                return;
            }

            moveAction = map.FindAction("Move", false);
            jumpAction = map.FindAction("Jump", false);
            runAction = map.FindAction("Sprint", false);
            crouchAction = map.FindAction("Crouch", false);
            interactAction = map.FindAction("Interact", false);
            dashAction = map.FindAction("Dash", false);
            lookAction = map.FindAction("Look", false);
            attackAction = map.FindAction("Attack", false);
            kickAction = map.FindAction("Kick", false);

            string missing = "";
            if (moveAction == null) missing += "Move ";
            if (jumpAction == null) missing += "Jump ";
            if (runAction == null) missing += "Sprint ";
            if (crouchAction == null) missing += "Crouch ";
            if (interactAction == null) missing += "Interact ";
            if (dashAction == null) missing += "Dash ";
            if (lookAction == null) missing += "Look ";
            if (attackAction == null) missing += "Attack ";
            if (kickAction == null) missing += "Kick ";

            if (missing.Length > 0)
                Debug.LogWarning("[CCPInputSystemHandler] Missing actions in map '" + actionMapName + "': " + missing);

            resolved = true;
        }

        void OnEnable()
        {
            Resolve();
            if (map != null && !map.enabled)
                map.Enable();
        }

        void OnDisable()
        {
            if (map != null && map.enabled)
                map.Disable();
        }

        bool Pressed(InputAction action)
        {
            return action != null && action.IsPressed();
        }

        public override bool GetBool(string actionName)
        {
            Resolve();

            switch (actionName)
            {
                case NameJump: return Pressed(jumpAction);
                case NameRun: return Pressed(runAction);
                case NameCrouch: return Pressed(crouchAction);
                case NameInteract: return Pressed(interactAction);
                case NameDash: return Pressed(dashAction);
                case NameAttack: return Pressed(attackAction);
                case NameKick: return Pressed(kickAction);
                default: return false;
            }
        }

        public override float GetFloat(string actionName)
        {
            Resolve();

            if (actionName == NamePitch && lookAction != null)
                return lookAction.ReadValue<Vector2>().y;

            return 0f;
        }

        public override Vector2 GetVector2(string actionName)
        {
            Resolve();

            if (actionName == NameMovement && moveAction != null)
                return moveAction.ReadValue<Vector2>();

            return Vector2.zero;
        }
    }
}
