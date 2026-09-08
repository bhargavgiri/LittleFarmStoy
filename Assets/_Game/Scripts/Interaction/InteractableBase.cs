using System;
using UnityEngine;

namespace LittleFarmStory.Interaction
{
    /// <summary>
    /// Convenience base for scene interactables. Concrete systems (farming, animals, market)
    /// override <see cref="OnInteract"/> rather than reimplementing the interface.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactionLabel = "Interact";
        [SerializeField] private bool interactable = true;

        public Transform Transform => transform;

        public string InteractionLabel => interactionLabel;

        /// <summary>Raised after a successful interaction. Interactor is the acting GameObject.</summary>
        public event Action<GameObject> Interacted;

        /// <summary>
        /// Raised when the label changes. Lets the HUD keep a prompt accurate while the player
        /// stands still - e.g. a plot ripening from "WHEAT GROWING" to "HARVEST".
        /// </summary>
        public event Action<IInteractable> LabelChanged;

        public void SetLabel(string value)
        {
            if (interactionLabel == value)
            {
                return;
            }

            interactionLabel = value;
            LabelChanged?.Invoke(this);
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
        }

        public virtual bool CanInteract(GameObject interactor)
        {
            return interactable && isActiveAndEnabled;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            OnInteract(interactor);
            Interacted?.Invoke(interactor);
        }

        protected abstract void OnInteract(GameObject interactor);
    }
}
