using LittleFarmStory.Interaction;
using UnityEngine;

namespace LittleFarmStory.World
{
    /// <summary>
    /// Generic named point of interest: the market, the coop, the barn, a production pad.
    /// Phase 1 uses it purely to prove the interaction loop and to label areas in the HUD.
    /// Later phases replace or subclass it with the real market / animal / machine behaviours.
    /// </summary>
    [DisallowMultipleComponent]
    public class FarmLandmark : InteractableBase
    {
        public enum LandmarkKind
        {
            Generic = 0,
            Field = 1,
            AnimalPen = 2,
            Production = 3,
            Market = 4,
            Home = 5
        }

        [Header("Landmark")]
        [SerializeField] private LandmarkKind kind = LandmarkKind.Generic;
        [Tooltip("Stable id for save data and orders.")]
        [SerializeField] private string landmarkId = "landmark";

        public LandmarkKind Kind => kind;

        public string LandmarkId => landmarkId;

        protected override void OnInteract(GameObject interactor)
        {
            Debug.Log("Interacted with " + InteractionLabel + " [" + kind + "] - system arrives in a later phase.", this);
        }

#if UNITY_EDITOR
        public void EditorConfigure(string id, string label, LandmarkKind landmarkKind)
        {
            landmarkId = id;
            kind = landmarkKind;
            SetLabel(label);
        }
#endif
    }
}
