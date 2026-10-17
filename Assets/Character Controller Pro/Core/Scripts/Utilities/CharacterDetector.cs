using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.Utilities;

namespace Lightbug.CharacterControllerPro.Demo
{

    public abstract class CharacterDetector : MonoBehaviour
    {
        protected Dictionary<Transform, CharacterActor> characters = new Dictionary<Transform, CharacterActor>();
        protected List<EntityId> onEnterDirtyTransforms = new List<EntityId>();
        protected List<EntityId> onStayDirtyTransforms = new List<EntityId>();
        protected List<EntityId> onExitDirtyTransforms = new List<EntityId>();

        protected virtual void ProcessEnterAction(CharacterActor characterActor) { }
        protected virtual void ProcessStayAction(CharacterActor characterActor) { }
        protected virtual void ProcessExitAction(CharacterActor characterActor) { }

        /// <summary>
        /// Gets the number of characters (CharacterActor) currently inside this trigger.
        /// </summary>
        public int CharactersNumber { get; private set; }

        void FixedUpdate()
        {
            if (onEnterDirtyTransforms.Count != 0)
                onEnterDirtyTransforms.Clear();

            if (onStayDirtyTransforms.Count != 0)
                onStayDirtyTransforms.Clear();

            if (onExitDirtyTransforms.Count != 0)
                onExitDirtyTransforms.Clear();
        }

        void ProcessAction(Transform transform, List<EntityId> characterActorsIDList, System.Action<CharacterActor> Action)
        {
            if (!enabled)
                return;

            CharacterActor characterActor = characters.GetOrRegisterValue(transform);

            if (characterActor == null)
                return;

            // We don't want to trigger the logic more than once due to multiple colliders interacting with the trigger.
            EntityId characterActorID = characterActor.GetEntityId();
            if (characterActorsIDList.Contains(characterActorID))
                return;

            // The actor is not registered, add the ID to the list.
            characterActorsIDList.Add(characterActorID);

            CharactersNumber++;

            Action?.Invoke(characterActor);
        }

        void OnTriggerEnter(Collider collider)
        {
            var characterRigidbody = collider.attachedRigidbody;
            if (characterRigidbody == null)
                return;

            ProcessAction(characterRigidbody.transform, onEnterDirtyTransforms, ProcessEnterAction);
        }

        void OnTriggerEnter2D(Collider2D collider)
        {
            var characterRigidbody = collider.attachedRigidbody;
            if (characterRigidbody == null)
                return;

            ProcessAction(characterRigidbody.transform, onEnterDirtyTransforms, ProcessEnterAction);
        }

        void OnTriggerStay(Collider collider)
        {
            var characterRigidbody = collider.attachedRigidbody;
            if (characterRigidbody == null)
                return;

            ProcessAction(characterRigidbody.transform, onStayDirtyTransforms, ProcessStayAction);
        }

        void OnTriggerStay2D(Collider2D collider)
        {
            var characterRigidbody = collider.attachedRigidbody;
            if (characterRigidbody == null)
                return;

            ProcessAction(characterRigidbody.transform, onStayDirtyTransforms, ProcessStayAction);
        }

        void OnTriggerExit(Collider collider)
        {
            var characterRigidbody = collider.attachedRigidbody;
            if (characterRigidbody == null)
                return;

            ProcessAction(characterRigidbody.transform, onExitDirtyTransforms, ProcessExitAction);
        }

        void OnTriggerExit2D(Collider2D collider)
        {
            var characterRigidbody = collider.attachedRigidbody;
            if (characterRigidbody == null)
                return;

            ProcessAction(characterRigidbody.transform, onExitDirtyTransforms, ProcessExitAction);
        }
    }

}
