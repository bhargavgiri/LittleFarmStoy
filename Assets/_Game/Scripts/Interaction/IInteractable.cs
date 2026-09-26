using UnityEngine;

namespace LittleFarmStory.Interaction
{
    /// <summary>
    /// Anything the player can walk up to and act on: a plot, a coop, the market, a machine.
    /// Kept deliberately small so later phases can add behaviour without changing the contract.
    /// </summary>
    public interface IInteractable
    {
        Transform Transform { get; }

        /// <summary>Short text for the HUD prompt, e.g. "Wheat Field".</summary>
        string InteractionLabel { get; }

        /// <summary>
        /// Tie-break when several interactables are in range. Higher wins; distance decides
        /// only within the same priority. Without this a decorative area trigger sitting a few
        /// centimetres nearer than a chicken silently swallows the interact button.
        /// </summary>
        int InteractionPriority { get; }

        bool CanInteract(GameObject interactor);

        void Interact(GameObject interactor);
    }
}
