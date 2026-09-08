using System;

namespace LittleFarmStory.Farming
{
    /// <summary>
    /// Everything needed to rebuild a plot's farming state later.
    /// Phase 2 does not persist anything - this type exists so the plot is forced to keep
    /// its state explicit and serializable, and so the save phase has a ready-made shape
    /// to write out. Nothing here references a Unity object.
    /// </summary>
    [Serializable]
    public struct FarmPlotSnapshot
    {
        public int CoordX;
        public int CoordZ;
        public PlotState State;

        /// <summary>Crop id from <see cref="CropDefinition.CropId"/>, empty when nothing is sown.</summary>
        public string CropId;

        public int StageIndex;

        /// <summary>
        /// Growth seconds accumulated so far, measured against the crop's authored pacing.
        /// A development speed multiplier is applied before this is accumulated, so the value
        /// is always directly comparable to <see cref="CropDefinition.TotalGrowthSeconds"/>.
        /// </summary>
        public float GrowthElapsed;

        /// <summary>UTC ticks at the moment of planting. 0 when nothing is sown.</summary>
        public long PlantedUtcTicks;

        public GridCoord Coord => new GridCoord(CoordX, CoordZ);
    }
}
