using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// Hunger and happiness for one animal.
    ///
    /// A plain class rather than a MonoBehaviour: it needs no transform, no Unity lifecycle and
    /// no inspector of its own, and on a farm of twenty animals that is twenty fewer components
    /// to serialize and tick. <see cref="AnimalController"/> owns one and pushes time into it.
    ///
    /// Time arrives in coarse chunks from the habitat's simulation tick, never per frame.
    /// </summary>
    public class AnimalNeeds
    {
        private readonly AnimalDefinition definition;

        /// <summary>0 = just fed, 1 = fully hungry.</summary>
        public float Hunger { get; private set; }

        /// <summary>0..100.</summary>
        public float Happiness { get; private set; }

        public AnimalNeeds(AnimalDefinition definition, float startingHappiness = 50f)
        {
            this.definition = definition;
            Happiness = Mathf.Clamp(startingHappiness, 0f, 100f);
        }

        public bool IsHungry => definition != null && Hunger >= definition.HungerThreshold;

        public bool IsHappy => definition != null && Happiness >= definition.HappyThreshold;

        public AnimalMood Mood
        {
            get
            {
                if (IsHungry)
                {
                    return AnimalMood.Hungry;
                }

                return IsHappy ? AnimalMood.Happy : AnimalMood.Content;
            }
        }

        /// <summary>Advances hunger, and decays happiness only while the animal is actually hungry.</summary>
        public void Tick(float deltaSeconds)
        {
            if (definition == null || deltaSeconds <= 0f)
            {
                return;
            }

            Hunger = Mathf.Clamp01(Hunger + deltaSeconds / definition.HungerDuration);

            if (IsHungry)
            {
                Happiness = Mathf.Clamp(
                    Happiness - definition.HappinessDecayPerSecond * deltaSeconds, 0f, 100f);
            }
        }

        /// <summary>Called after a successful feed. Resets hunger and lifts happiness.</summary>
        public void OnFed()
        {
            if (definition == null)
            {
                return;
            }

            Hunger = 0f;
            Happiness = Mathf.Clamp(Happiness + definition.FeedHappiness, 0f, 100f);
        }

        /// <summary>Small reward for any successful interaction, e.g. collecting produce.</summary>
        public void AddHappiness(float amount)
        {
            Happiness = Mathf.Clamp(Happiness + amount, 0f, 100f);
        }

        /// <summary>Restore path for the future save system. Bypasses no guards - there are none.</summary>
        public void Restore(float hunger, float happiness)
        {
            Hunger = Mathf.Clamp01(hunger);
            Happiness = Mathf.Clamp(happiness, 0f, 100f);
        }
    }
}
