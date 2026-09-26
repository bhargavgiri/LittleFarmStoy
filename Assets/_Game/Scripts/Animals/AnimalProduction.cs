using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// The production cycle for one animal: Dormant -> Producing -> Ready -> Dormant.
    ///
    /// Production is purely logical - nothing is instantiated, nothing is spawned, no egg
    /// GameObject exists. The phase guards below are the whole reason double production and
    /// double collection cannot happen, and they mirror the state guards in <c>FarmPlot</c>.
    /// </summary>
    public class AnimalProduction
    {
        private readonly AnimalDefinition definition;

        public ProductionPhase Phase { get; private set; } = ProductionPhase.Dormant;

        /// <summary>Seconds accumulated into the current cycle.</summary>
        public float Elapsed { get; private set; }

        public AnimalProduction(AnimalDefinition definition)
        {
            this.definition = definition;
        }

        public bool IsReady => Phase == ProductionPhase.Ready;

        /// <summary>Progress 0..1 through the current cycle. 1 when ready, 0 when dormant.</summary>
        public float Progress
        {
            get
            {
                if (Phase == ProductionPhase.Ready)
                {
                    return 1f;
                }

                if (Phase != ProductionPhase.Producing || definition == null)
                {
                    return 0f;
                }

                return Mathf.Clamp01(Elapsed / definition.ProductionSeconds);
            }
        }

        /// <summary>
        /// Starts a cycle. Returns false when one is already running or a product is waiting,
        /// which is what limits production to one cycle per feeding.
        /// </summary>
        public bool TryBegin()
        {
            if (definition == null || Phase != ProductionPhase.Dormant)
            {
                return false;
            }

            Elapsed = 0f;
            Phase = ProductionPhase.Producing;
            return true;
        }

        /// <summary>
        /// Advances the cycle. Returns true on the tick the product becomes ready, so the
        /// caller can post feedback exactly once.
        /// </summary>
        public bool Tick(float deltaSeconds)
        {
            if (definition == null || Phase != ProductionPhase.Producing || deltaSeconds <= 0f)
            {
                return false;
            }

            Elapsed += deltaSeconds;

            if (Elapsed < definition.ProductionSeconds)
            {
                return false;
            }

            Elapsed = definition.ProductionSeconds;
            Phase = ProductionPhase.Ready;
            return true;
        }

        /// <summary>
        /// Consumes a ready product and returns how many units it was worth.
        /// Returns 0 and changes nothing unless the phase is <see cref="ProductionPhase.Ready"/>,
        /// so a second collection can never pay out.
        /// </summary>
        public int Collect(bool happy)
        {
            if (definition == null || Phase != ProductionPhase.Ready)
            {
                return 0;
            }

            // Reset first: after this line the guard above rejects re-entry.
            Phase = ProductionPhase.Dormant;
            Elapsed = 0f;

            return definition.ProductAmount + (happy ? definition.HappyBonusAmount : 0);
        }

        /// <summary>Restore path for the future save system.</summary>
        public void Restore(ProductionPhase phase, float elapsed)
        {
            Phase = phase;
            Elapsed = Mathf.Max(0f, elapsed);
        }
    }
}
