using System;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// Everything a save system needs to restore one animal, and nothing else.
    /// Phase 5 does not persist anything; this exists so the save phase is a serializer,
    /// not a refactor. Mirrors <c>FarmPlotSnapshot</c>.
    /// </summary>
    [Serializable]
    public struct AnimalSnapshot
    {
        /// <summary>Instance id, unique within a save.</summary>
        public string InstanceId;

        /// <summary>Species id from the <see cref="AnimalDefinition"/>.</summary>
        public string AnimalId;

        /// <summary>Habitat this animal is registered to.</summary>
        public string HabitatId;

        public float Hunger;
        public float Happiness;
        public ProductionPhase ProductionPhase;
        public float ProductionElapsed;

        public float PositionX;
        public float PositionZ;
        public float Yaw;
    }
}
