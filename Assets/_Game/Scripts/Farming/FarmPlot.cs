using System;
using LittleFarmStory.Core;
using LittleFarmStory.Interaction;
using LittleFarmStory.Inventory;
using UnityEngine;

namespace LittleFarmStory.Farming
{
    /// <summary>
    /// One tile of soil, and the owner of its own farming lifecycle.
    /// All crop-specific behaviour comes from <see cref="CropDefinition"/> data, so this class
    /// contains no wheat/tomato/corn logic. Growth time is pushed in by the owning
    /// <see cref="FarmGrid"/> - the plot has no Update of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public class FarmPlot : InteractableBase
    {
        [Header("Plot")]
        [SerializeField] private PlotState state = PlotState.Empty;
        [SerializeField] private CropDefinition crop;

        [Header("Presentation")]
        [Tooltip("Renderer tinted to show soil state.")]
        [SerializeField] private Renderer soilRenderer;
        [Tooltip("Where crop stage visuals are spawned.")]
        [SerializeField] private Transform cropAnchor;
        [Tooltip("How far the soil is lightened when untilled.")]
        [Range(0f, 1f)] [SerializeField] private float untilledLighten = 0.55f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color FallbackSoil = new Color(0.42f, 0.29f, 0.19f);
        private static readonly Color DrySoil = new Color(0.62f, 0.5f, 0.34f);

        private MaterialPropertyBlock propertyBlock;

        // --- runtime farming state (explicit and snapshot-able, see FarmPlotSnapshot)
        private int stageIndex;
        private float growthElapsed;
        private long plantedUtcTicks;

        // --- visuals
        private GameObject currentVisual;
        private int currentVisualStage = -1;

        // --- cached per-interactor services, resolved once instead of every button press
        private GameObject cachedInteractor;
        private PlayerInventory cachedInventory;
        private ActionFeedbackChannel cachedFeedback;

        public GridCoord Coord { get; private set; }

        public FarmGrid Grid { get; private set; }

        public PlotState State => state;

        public CropDefinition Crop => crop;

        public Transform CropAnchor => cropAnchor != null ? cropAnchor : transform;

        /// <summary>Current visual stage, 0 = freshly sown, <see cref="CropDefinition.GrowthStages"/> = mature.</summary>
        public int StageIndex => stageIndex;

        /// <summary>Growth progress 0..1 across the whole crop. 0 when nothing is sown.</summary>
        public float GrowthProgress
        {
            get
            {
                if (crop == null || !IsGrowing)
                {
                    return state == PlotState.ReadyToHarvest ? 1f : 0f;
                }

                return Mathf.Clamp01(growthElapsed / crop.TotalGrowthSeconds);
            }
        }

        public bool IsGrowing => state == PlotState.Planted || state == PlotState.Growing;

        public bool CanBeTilled => state == PlotState.Empty;

        public bool CanBePlanted => state == PlotState.Tilled && crop != null;

        public bool CanBeHarvested => state == PlotState.ReadyToHarvest && crop != null;

        /// <summary>Raised whenever the plot state changes.</summary>
        public event Action<FarmPlot> StateChanged;

        // ============================================================ setup

        /// <summary>Called by <see cref="FarmGrid"/> right after the plot is spawned.</summary>
        public void Initialise(FarmGrid grid, GridCoord coord, CropDefinition assignedCrop)
        {
            Grid = grid;
            Coord = coord;
            crop = assignedCrop;

            name = "Plot_" + coord.X + "_" + coord.Z;

            ResetGrowthState();
            state = PlotState.Empty;
            RefreshPresentation();
        }

        public void SetCrop(CropDefinition newCrop)
        {
            if (crop == newCrop)
            {
                return;
            }

            // Changing the crop clears whatever was in the ground; it is not a harvest.
            crop = newCrop;
            ClearCropInternal();
            RefreshPresentation();
            StateChanged?.Invoke(this);
        }

        // ============================================================ actions

        /// <summary>Empty -> Tilled. Rejects every other state.</summary>
        public bool TryTill()
        {
            if (state != PlotState.Empty)
            {
                return false;
            }

            SetState(PlotState.Tilled);
            return true;
        }

        /// <summary>Tilled -> Planted, consuming exactly one seed. Rejects every other state.</summary>
        public FarmActionResult TryPlant(PlayerInventory inventory)
        {
            if (state != PlotState.Tilled)
            {
                return FarmActionResult.InvalidState;
            }

            if (crop == null)
            {
                return FarmActionResult.NoCrop;
            }

            if (inventory == null)
            {
                return FarmActionResult.NoInventory;
            }

            // Remove is all-or-nothing, so a failure here cannot leave a partial spend.
            if (!inventory.Remove(crop.SeedItemId, 1))
            {
                return FarmActionResult.NotEnoughSeeds;
            }

            stageIndex = 0;
            growthElapsed = 0f;
            plantedUtcTicks = DateTime.UtcNow.Ticks;

            SetState(PlotState.Planted);
            ApplyStageVisual();

            if (Grid != null)
            {
                Grid.NotifyGrowthStarted(this);
            }

            return FarmActionResult.Success;
        }

        /// <summary>
        /// Ready -> Empty, granting the crop yield. Rejects every other state, so a second
        /// press on the same plot can never pay out twice.
        /// </summary>
        public FarmActionResult TryHarvest(PlayerInventory inventory, out int harvestedAmount)
        {
            harvestedAmount = 0;

            if (state != PlotState.ReadyToHarvest)
            {
                return FarmActionResult.InvalidState;
            }

            if (crop == null)
            {
                return FarmActionResult.NoCrop;
            }

            if (inventory == null)
            {
                return FarmActionResult.NoInventory;
            }

            int amount = crop.RollYield();
            string itemId = crop.HarvestItemId;

            // Consume the plot first: after this line the state guard above rejects re-entry.
            ClearCropInternal();
            RefreshPresentation();
            StateChanged?.Invoke(this);

            inventory.Add(itemId, amount);
            harvestedAmount = amount;
            return FarmActionResult.Success;
        }

        // ============================================================ growth

        /// <summary>
        /// Advances growth by the supplied (already speed-scaled) seconds.
        /// Called by the owning grid, never per-frame per-plot.
        /// Returns true while this plot still needs ticking.
        /// </summary>
        internal bool AdvanceGrowth(float deltaSeconds)
        {
            if (crop == null || !IsGrowing || deltaSeconds <= 0f)
            {
                return IsGrowing;
            }

            growthElapsed += deltaSeconds;

            int targetStage = Mathf.Clamp(
                Mathf.FloorToInt(growthElapsed / crop.SecondsPerStage), 0, crop.GrowthStages);

            if (targetStage != stageIndex)
            {
                stageIndex = targetStage;
                ApplyStageVisual();
            }

            PlotState next;
            if (stageIndex >= crop.GrowthStages)
            {
                next = PlotState.ReadyToHarvest;
            }
            else if (stageIndex > 0)
            {
                next = PlotState.Growing;
            }
            else
            {
                next = PlotState.Planted;
            }

            if (next != state)
            {
                SetState(next);
            }

            return IsGrowing;
        }

        // ============================================================ save preparation

        /// <summary>Capture for the future save system. Phase 2 does not persist anything.</summary>
        public FarmPlotSnapshot CaptureSnapshot()
        {
            return new FarmPlotSnapshot
            {
                CoordX = Coord.X,
                CoordZ = Coord.Z,
                State = state,
                CropId = crop != null ? crop.CropId : string.Empty,
                StageIndex = stageIndex,
                GrowthElapsed = growthElapsed,
                PlantedUtcTicks = plantedUtcTicks
            };
        }

        /// <summary>
        /// Restores a captured state directly, bypassing the transition guards.
        /// Only for the save system - gameplay must go through the Try* methods.
        /// </summary>
        public void RestoreSnapshot(FarmPlotSnapshot snapshot, CropDefinition resolvedCrop)
        {
            crop = resolvedCrop;
            state = snapshot.State;
            stageIndex = snapshot.StageIndex;
            growthElapsed = snapshot.GrowthElapsed;
            plantedUtcTicks = snapshot.PlantedUtcTicks;

            currentVisualStage = -1;
            ApplyStageVisual();
            RefreshPresentation();

            if (IsGrowing && Grid != null)
            {
                Grid.NotifyGrowthStarted(this);
            }

            StateChanged?.Invoke(this);
        }

        // ============================================================ interaction

        protected override void OnInteract(GameObject interactor)
        {
            ResolveInteractorServices(interactor);

            switch (state)
            {
                case PlotState.Empty:
                    if (TryTill())
                    {
                        Post("Tilled!");
                    }

                    break;

                case PlotState.Tilled:
                    HandlePlant();
                    break;

                case PlotState.Planted:
                case PlotState.Growing:
                    Post(CropName + " is growing...");
                    break;

                case PlotState.ReadyToHarvest:
                    HandleHarvest();
                    break;
            }
        }

        private void HandlePlant()
        {
            FarmActionResult result = TryPlant(cachedInventory);

            switch (result)
            {
                case FarmActionResult.Success:
                    Post("Planted " + CropName + "!");
                    break;

                case FarmActionResult.NotEnoughSeeds:
                    Post("No " + CropName + " Seeds");
                    break;

                case FarmActionResult.NoCrop:
                    Post("Nothing to plant here");
                    break;

                case FarmActionResult.NoInventory:
                    Debug.LogWarning("FarmPlot: the interactor has no PlayerInventory component.", this);
                    break;
            }
        }

        private void HandleHarvest()
        {
            FarmActionResult result = TryHarvest(cachedInventory, out int amount);

            if (result == FarmActionResult.Success)
            {
                Post("+" + amount + " " + CropName);
            }
            else if (result == FarmActionResult.NoInventory)
            {
                Debug.LogWarning("FarmPlot: the interactor has no PlayerInventory component.", this);
            }
        }

        private void ResolveInteractorServices(GameObject interactor)
        {
            if (interactor == null || ReferenceEquals(interactor, cachedInteractor))
            {
                return;
            }

            cachedInteractor = interactor;
            cachedInventory = interactor.GetComponent<PlayerInventory>();
            cachedFeedback = interactor.GetComponent<ActionFeedbackChannel>();
        }

        private void Post(string message)
        {
            if (cachedFeedback != null)
            {
                cachedFeedback.Post(message);
            }
        }

        private string CropName => crop != null ? crop.DisplayName : "Crop";

        // ============================================================ internals

        private void SetState(PlotState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
            RefreshPresentation();
            StateChanged?.Invoke(this);
        }

        private void ClearCropInternal()
        {
            DestroyVisual();
            ResetGrowthState();
            state = PlotState.Empty;
        }

        private void ResetGrowthState()
        {
            stageIndex = 0;
            growthElapsed = 0f;
            plantedUtcTicks = 0L;
        }

        private void RefreshPresentation()
        {
            ApplySoilTint();
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            string cropName = CropName.ToUpperInvariant();

            switch (state)
            {
                case PlotState.Empty:
                    SetLabel("TILL");
                    break;

                case PlotState.Tilled:
                    SetLabel("PLANT " + cropName);
                    break;

                case PlotState.Planted:
                case PlotState.Growing:
                    SetLabel(cropName + " GROWING");
                    break;

                case PlotState.ReadyToHarvest:
                    SetLabel("HARVEST");
                    break;
            }
        }

        private void ApplySoilTint()
        {
            if (soilRenderer == null)
            {
                return;
            }

            Color color = crop != null ? crop.SoilColor : FallbackSoil;

            if (state == PlotState.Empty)
            {
                // Untilled soil reads lighter and drier.
                color = Color.Lerp(color, DrySoil, untilledLighten);
            }

            propertyBlock ??= new MaterialPropertyBlock();
            soilRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            soilRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyStageVisual()
        {
            if (currentVisualStage == stageIndex && currentVisual != null)
            {
                return;
            }

            DestroyVisual();

            bool shouldShowCrop = IsGrowing || state == PlotState.ReadyToHarvest;
            if (crop == null || !shouldShowCrop)
            {
                return;
            }

            GameObject prefab = crop.GetStagePrefab(stageIndex);
            if (prefab == null)
            {
                return;
            }

            currentVisual = Instantiate(prefab, CropAnchor);
            currentVisual.transform.localPosition = Vector3.zero;

            // Deterministic per-plot yaw so a field of identical prefabs does not look stamped.
            float yaw = (Coord.X * 53 + Coord.Z * 97) % 360;
            currentVisual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            currentVisualStage = stageIndex;
        }

        private void DestroyVisual()
        {
            if (currentVisual != null)
            {
                Destroy(currentVisual);
            }

            currentVisual = null;
            currentVisualStage = -1;
        }

        private void OnDestroy()
        {
            currentVisual = null;
        }
    }
}
