using LittleFarmStory.Interaction;
using LittleFarmStory.UI;
using UnityEngine;

namespace LittleFarmStory.Economy
{
    /// <summary>
    /// The market's world-side entrance.
    ///
    /// This is the same physical landmark <c>FarmPrototypeBuilder</c> has always placed for
    /// "Farmers Market" - same trigger, same position - now wired to actually do something
    /// instead of logging "arrives in a later phase".
    ///
    /// It is its own <see cref="InteractableBase"/> rather than a special case inside the
    /// generic <c>FarmLandmark</c>: every other landmark (Home, Production, Field) is still
    /// exactly that generic, UI-and-economy-ignorant placeholder, and stays that way. Only the
    /// market gained real behaviour, so only the market gets its own component.
    /// </summary>
    [DisallowMultipleComponent]
    public class MarketInteractable : InteractableBase
    {
        [Tooltip("Stable id for save data. Never rename once shipped.")]
        [SerializeField] private string landmarkId = "market";

        [Tooltip("Opened on interact. Wired by the scene builder once the HUD - and its shop " +
                 "panel - exist.")]
        [SerializeField] private ShopPanel shopPanel;

        public string LandmarkId => landmarkId;

        protected override void OnInteract(GameObject interactor)
        {
            if (shopPanel == null)
            {
                Debug.LogError("MarketInteractable '" + name + "' has no ShopPanel assigned; " +
                               "the shop cannot open.", this);
                return;
            }

            shopPanel.Open();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(string id, string label)
        {
            landmarkId = id;
            SetLabel(label);
        }

        /// <summary>Editor-only wiring, set once the HUD's shop panel exists.</summary>
        public void EditorSetShopPanel(ShopPanel panel)
        {
            shopPanel = panel;
        }
#endif
    }
}
