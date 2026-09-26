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
        /// <summary>
        /// Shared priority bands, so the numbers mean the same thing everywhere.
        /// Gameplay actors outrank scenery; decoration must never win.
        /// </summary>
        public static class Priority
        {
            public const int Decoration = 0;
            public const int Area = 5;
            public const int Production = 10;

            // Gameplay actors both outrank scenery. Animals sit highest because they are the
            // ones that stand inside a habitat's own area trigger and were being swallowed by
            // it; plots never share ground with an animal, so this ordering costs farming
            // nothing.
            public const int Plot = 25;
            public const int Animal = 30;
        }

        [SerializeField] private string interactionLabel = "Interact";
        [SerializeField] private bool interactable = true;
        [Tooltip("Higher wins when several interactables are in range; distance breaks ties.")]
        [SerializeField] private int interactionPriority = Priority.Area;

        public Transform Transform => transform;

        public string InteractionLabel => interactionLabel;

        public int InteractionPriority => interactionPriority;

        public void SetPriority(int value)
        {
            interactionPriority = value;
        }

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
