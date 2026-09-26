namespace LittleFarmStory.Animals
{
    /// <summary>Species. Also decides which habitat will accept an animal.</summary>
    public enum AnimalType
    {
        Chicken = 0,
        Cow = 1
    }

    /// <summary>
    /// What the animal is physically doing. Deliberately separate from hunger and from
    /// production: a hungry animal still wanders, and a producing animal still stands about.
    /// Folding those into one enum would need a state per combination.
    /// </summary>
    public enum AnimalActivity
    {
        Idle = 0,
        Wandering = 1,
        Eating = 2
    }

    /// <summary>
    /// Where the animal is in its production cycle. The guards on this enum are what make
    /// double production and double collection impossible.
    /// </summary>
    public enum ProductionPhase
    {
        /// <summary>Nothing in progress. Only a feeding can start a cycle.</summary>
        Dormant = 0,
        Producing = 1,
        Ready = 2
    }

    /// <summary>Derived from hunger and happiness for labels and feedback. Never stored.</summary>
    public enum AnimalMood
    {
        Hungry = 0,
        Content = 1,
        Happy = 2
    }

    /// <summary>Outcome of a feed or collect attempt, mirroring <c>FarmActionResult</c>.</summary>
    public enum AnimalActionResult
    {
        Success = 0,

        /// <summary>The animal is not in a state where this action means anything.</summary>
        InvalidState = 1,

        /// <summary>No <c>AnimalDefinition</c> assigned.</summary>
        NoDefinition = 2,

        /// <summary>The interactor has no <c>PlayerInventory</c>.</summary>
        NoInventory = 3,

        /// <summary>The player does not carry enough feed.</summary>
        NotEnoughFeed = 4,

        /// <summary>The animal is already full.</summary>
        NotHungry = 5
    }
}
