namespace LittleFarmStory.Farming
{
    /// <summary>
    /// Lifecycle of a single farm plot. Phase 1 only ever uses Empty and Tilled;
    /// the remaining states exist so the growth system can be dropped in without
    /// touching plot or grid code.
    /// </summary>
    public enum PlotState
    {
        Empty = 0,
        Tilled = 1,
        Planted = 2,
        Growing = 3,
        ReadyToHarvest = 4
    }
}
