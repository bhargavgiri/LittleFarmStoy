using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleFarmStory.Farming
{
    /// <summary>
    /// A rectangular block of farm plots (one field). Plots are spawned from a prefab at
    /// runtime so the scene file stays small and field size can be data driven later
    /// (farm expansion, save/load, procedural layouts).
    ///
    /// The grid also drives growth for the plots it owns. That keeps the whole farm down to
    /// one Update per field instead of one per plot, while each plot still owns its own state.
    /// </summary>
    [DisallowMultipleComponent]
    public class FarmGrid : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Stable id used by save data and orders. Never rename once shipped.")]
        [SerializeField] private string fieldId = "field_wheat";
        [SerializeField] private CropDefinition assignedCrop;

        [Header("Layout")]
        [SerializeField] private FarmPlot plotPrefab;
        [SerializeField] private Vector2Int size = new Vector2Int(5, 4);
        [SerializeField] private float cellSize = 2.2f;
        [Tooltip("Parent for spawned plots. Falls back to this transform.")]
        [SerializeField] private Transform plotParent;

        [Header("Growth")]
        [Tooltip("Shared farming tuning. Optional: growth runs at authored speed when unset.")]
        [SerializeField] private FarmingSettings farmingSettings;

        [Header("Lifecycle")]
        [SerializeField] private bool buildOnAwake = true;

        private FarmPlot[] plots;

        /// <summary>Only the plots currently mid-growth. Everything else costs nothing per frame.</summary>
        private readonly List<FarmPlot> growingPlots = new List<FarmPlot>();

        private float tickAccumulator;

        private const float DefaultTickInterval = 0.2f;

        /// <summary>Read live rather than cached, so the dev multiplier can be tuned during Play.</summary>
        private float TickInterval =>
            farmingSettings != null ? farmingSettings.GrowthTickInterval : DefaultTickInterval;

        private float SpeedMultiplier =>
            farmingSettings != null ? farmingSettings.GrowthSpeedMultiplier : 1f;

        public string FieldId => fieldId;

        public CropDefinition AssignedCrop => assignedCrop;

        public Vector2Int Size => size;

        public float CellSize => cellSize;

        public int PlotCount => plots != null ? plots.Length : 0;

        public int GrowingPlotCount => growingPlots.Count;

        /// <summary>Raised once the field has finished spawning its plots.</summary>
        public event Action<FarmGrid> Built;

        private void Awake()
        {
            if (buildOnAwake)
            {
                Build();
            }
        }

        private void Update()
        {
            // Nothing is growing in this field: no work at all.
            if (growingPlots.Count == 0)
            {
                return;
            }

            tickAccumulator += Time.deltaTime;
            if (tickAccumulator < TickInterval)
            {
                return;
            }

            // Accumulated, not sampled: growth stays accurate whatever the tick interval is.
            float scaledDelta = tickAccumulator * SpeedMultiplier;
            tickAccumulator = 0f;

            for (int i = growingPlots.Count - 1; i >= 0; i--)
            {
                FarmPlot plot = growingPlots[i];

                if (plot == null || !plot.AdvanceGrowth(scaledDelta))
                {
                    growingPlots.RemoveAt(i);
                }
            }
        }

        public void Build()
        {
            if (plotPrefab == null)
            {
                Debug.LogWarning("FarmGrid '" + fieldId + "' has no plot prefab assigned; nothing was built.", this);
                return;
            }

            if (size.x <= 0 || size.y <= 0)
            {
                Debug.LogWarning("FarmGrid '" + fieldId + "' has a non-positive size; nothing was built.", this);
                return;
            }

            Transform parent = plotParent != null ? plotParent : transform;
            plots = new FarmPlot[size.x * size.y];
            growingPlots.Clear();

            for (int z = 0; z < size.y; z++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    GridCoord coord = new GridCoord(x, z);
                    FarmPlot plot = Instantiate(plotPrefab, parent);
                    plot.transform.localPosition = CoordToLocal(coord);
                    plot.transform.localRotation = Quaternion.identity;

                    // Initialise sets the crop, resets state and writes the contextual label.
                    plot.Initialise(this, coord, assignedCrop);

                    plots[Index(coord)] = plot;
                }
            }

            Built?.Invoke(this);
        }

        /// <summary>Called by a plot when it starts growing, so this field begins ticking it.</summary>
        internal void NotifyGrowthStarted(FarmPlot plot)
        {
            if (plot == null || growingPlots.Contains(plot))
            {
                return;
            }

            growingPlots.Add(plot);
        }

        public bool TryGetPlot(GridCoord coord, out FarmPlot plot)
        {
            plot = null;

            if (plots == null || !Contains(coord))
            {
                return false;
            }

            plot = plots[Index(coord)];
            return plot != null;
        }

        public void ForEachPlot(Action<FarmPlot> action)
        {
            if (plots == null || action == null)
            {
                return;
            }

            for (int i = 0; i < plots.Length; i++)
            {
                if (plots[i] != null)
                {
                    action(plots[i]);
                }
            }
        }

        public IReadOnlyList<FarmPlot> Plots => plots ?? Array.Empty<FarmPlot>();

        public bool Contains(GridCoord coord)
        {
            return coord.X >= 0 && coord.X < size.x && coord.Z >= 0 && coord.Z < size.y;
        }

        public Vector3 CoordToLocal(GridCoord coord)
        {
            float offsetX = (size.x - 1) * 0.5f;
            float offsetZ = (size.y - 1) * 0.5f;
            return new Vector3((coord.X - offsetX) * cellSize, 0f, (coord.Z - offsetZ) * cellSize);
        }

        public Vector3 CoordToWorld(GridCoord coord)
        {
            return transform.TransformPoint(CoordToLocal(coord));
        }

        public GridCoord WorldToCoord(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float offsetX = (size.x - 1) * 0.5f;
            float offsetZ = (size.y - 1) * 0.5f;

            int x = Mathf.RoundToInt(local.x / cellSize + offsetX);
            int z = Mathf.RoundToInt(local.z / cellSize + offsetZ);

            GridCoord coord = new GridCoord(x, z);
            return Contains(coord) ? coord : GridCoord.Invalid;
        }

        /// <summary>Footprint of the field in world units, used for soil pads and camera bounds.</summary>
        public Vector2 WorldFootprint => new Vector2(size.x * cellSize, size.y * cellSize);

        private int Index(GridCoord coord)
        {
            return coord.Z * size.x + coord.X;
        }

#if UNITY_EDITOR
        public void EditorConfigure(string id, CropDefinition crop, Vector2Int gridSize, float cell, FarmPlot prefab)
        {
            fieldId = id;
            assignedCrop = crop;
            size = gridSize;
            cellSize = cell;
            plotPrefab = prefab;
        }

        public void EditorSetFarmingSettings(FarmingSettings settings)
        {
            farmingSettings = settings;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.4f, 0.6f);
            Vector2 footprint = WorldFootprint;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(footprint.x, 0.1f, footprint.y));
        }
#endif
    }
}
