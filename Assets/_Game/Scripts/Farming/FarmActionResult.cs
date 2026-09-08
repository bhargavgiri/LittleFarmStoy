namespace LittleFarmStory.Farming
{
    /// <summary>
    /// Outcome of a farming action. Returned instead of a bare bool so the caller can give
    /// the player an accurate reason ("No Wheat Seeds") without inspecting plot internals.
    /// </summary>
    public enum FarmActionResult
    {
        Success = 0,

        /// <summary>The plot is not in a state where this action is legal.</summary>
        InvalidState = 1,

        /// <summary>No crop is assigned to this plot, so there is nothing to sow.</summary>
        NoCrop = 2,

        /// <summary>The interactor has no inventory component.</summary>
        NoInventory = 3,

        /// <summary>The player does not have the required seed.</summary>
        NotEnoughSeeds = 4
    }
}
