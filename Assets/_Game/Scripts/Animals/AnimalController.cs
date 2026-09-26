using System;
using LittleFarmStory.Inventory;
using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// One farm animal, and the owner of its own gameplay state.
    ///
    /// All species-specific behaviour comes from <see cref="AnimalDefinition"/> data, so this
    /// class contains no chicken or cow logic. Time is pushed in by the owning
    /// <see cref="AnimalHabitat"/> - the animal has no Update of its own, exactly as
    /// <c>FarmPlot</c> has none and is driven by <c>FarmGrid</c>.
    ///
    /// Needs, production and movement live in plain C# classes rather than extra components:
    /// composition without paying for seven MonoBehaviours per animal.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimalController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private AnimalDefinition definition;
        [Tooltip("Habitat this animal registers with. REQUIRED: the habitat also drives this " +
                 "animal's simulation, so without one the animal never gets hungry or produces.")]
        [SerializeField] private AnimalHabitat habitat;
        [Tooltip("Stable per-instance id for save data. Generated at build time.")]
        [SerializeField] private string instanceId = "";

        [Header("Presentation")]
        [Tooltip("Optional. Drives the walk cycle and the eating dip.")]
        [SerializeField] private AnimalIdleAnimator animator;

        private AnimalNeeds needs;
        private AnimalProduction production;
        private AnimalMovement movement;

        private float activityTimer;
        private float eatTimer;
        private bool registered;

        public AnimalDefinition Definition => definition;

        public AnimalHabitat Habitat => habitat;

        public string InstanceId => instanceId;

        public string DisplayName => definition != null ? definition.DisplayName : "Animal";

        public AnimalActivity Activity { get; private set; } = AnimalActivity.Idle;

        public AnimalNeeds Needs => needs;

        public AnimalProduction Production => production;

        public bool IsHungry => needs != null && needs.IsHungry;

        public bool HasProductReady => production != null && production.IsReady;

        /// <summary>Raised whenever hunger, happiness, activity or production phase changes meaningfully.</summary>
        public event Action<AnimalController> StateChanged;

        // ============================================================ lifecycle

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();

            if (habitat != null && !registered)
            {
                registered = habitat.Register(this);
            }
        }

        private void OnDisable()
        {
            if (habitat != null && registered)
            {
                habitat.Unregister(this);
                registered = false;
            }
        }

        private void EnsureRuntimeState()
        {
            if (definition == null)
            {
                Debug.LogWarning(
                    "AnimalController on '" + name + "' has no AnimalDefinition; it will stand still and reject interaction.",
                    this);
                return;
            }

            if (needs == null)
            {
                // Seed hunger directly rather than fast-forwarding time: ticking a large delta
                // would also apply the whole hunger period's happiness decay before the player
                // has had any chance to feed the animal. A staggered start also stops a flock
                // from going hungry on the same frame, and means roughly half the animals are
                // ready to be fed the moment the scene loads.
                needs = new AnimalNeeds(definition);
                needs.Restore(UnityEngine.Random.value, 55f);
            }

            production ??= new AnimalProduction(definition);
            movement ??= new AnimalMovement(transform, definition);

            if (activityTimer <= 0f)
            {
                activityTimer = definition.IdleDwell * (0.4f + UnityEngine.Random.value);
            }

            if (string.IsNullOrEmpty(instanceId))
            {
                // Fallback only: the scene builder assigns explicit, stable ids.
                instanceId = definition.AnimalId + "_" + name;
            }
        }

        // ============================================================ ticking

        /// <summary>
        /// Per-frame work: movement smoothing and the animator drive. Called by the habitat,
        /// never by a per-animal Update.
        /// </summary>
        internal void FrameTick(float deltaSeconds)
        {
            if (definition == null || movement == null)
            {
                return;
            }

            if (movement.Tick(deltaSeconds) && Activity == AnimalActivity.Wandering)
            {
                SetActivity(AnimalActivity.Idle);
                activityTimer = IdleDwellForCurrentMood();
            }

            if (animator != null)
            {
                animator.SetLocomotion(movement.NormalisedSpeed);
            }
        }

        /// <summary>
        /// Coarse simulation: hunger, happiness, production and the activity decision.
        /// Runs on the habitat's simulation interval with accumulated - not sampled - time,
        /// and keeps running while the animal is off screen.
        /// </summary>
        internal void SimulationTick(float deltaSeconds)
        {
            if (definition == null)
            {
                return;
            }

            bool changed = false;
            AnimalMood moodBefore = needs.Mood;

            needs.Tick(deltaSeconds);

            if (needs.Mood != moodBefore)
            {
                changed = true;
            }

            if (production.Tick(deltaSeconds))
            {
                changed = true;

                AnimalDebug.Log(this, DisplayName + " production READY",
                    "Item: " + definition.ProductItemId,
                    "Quantity: " + (definition.ProductAmount + (needs.IsHappy ? definition.HappyBonusAmount : 0)),
                    "Phase: " + production.Phase);
            }

            if (Activity == AnimalActivity.Eating)
            {
                eatTimer -= deltaSeconds;
                if (eatTimer <= 0f)
                {
                    SetActivity(AnimalActivity.Idle);
                    activityTimer = IdleDwellForCurrentMood();
                    changed = true;
                }
            }
            else
            {
                UpdateWandering(deltaSeconds);
            }

            if (changed)
            {
                StateChanged?.Invoke(this);
            }
        }

        private void UpdateWandering(float deltaSeconds)
        {
            if (Activity != AnimalActivity.Idle)
            {
                return;
            }

            activityTimer -= deltaSeconds;
            if (activityTimer > 0f)
            {
                return;
            }

            activityTimer = IdleDwellForCurrentMood();

            if (habitat == null || UnityEngine.Random.value > definition.WanderChance)
            {
                return;
            }

            // A rejected sample simply means the animal stands still and tries again after the
            // next dwell. No retry loop, no pathfinding fallback.
            if (habitat.TrySampleDestination(this, out Vector3 point))
            {
                movement.SetDestination(point);
                SetActivity(AnimalActivity.Wandering);
            }
        }

        /// <summary>A hungry animal is lethargic, which reads without needing a "Hungry" activity.</summary>
        private float IdleDwellForCurrentMood()
        {
            float hunger = needs != null ? needs.Hunger : 0f;
            return definition.IdleDwell * (0.5f + UnityEngine.Random.value) * (1f + hunger);
        }

        private void SetActivity(AnimalActivity value)
        {
            if (Activity == value)
            {
                return;
            }

            Activity = value;

            if (value != AnimalActivity.Wandering)
            {
                movement?.Stop();
            }
        }

        // ============================================================ actions

        /// <summary>
        /// Consumes feed from the inventory and resets hunger. All-or-nothing: a result other
        /// than <see cref="AnimalActionResult.Success"/> leaves the inventory and the animal
        /// exactly as they were.
        /// </summary>
        public AnimalActionResult TryFeed(PlayerInventory inventory)
        {
            if (definition == null)
            {
                return AnimalActionResult.NoDefinition;
            }

            if (inventory == null)
            {
                return AnimalActionResult.NoInventory;
            }

            if (!needs.IsHungry)
            {
                return AnimalActionResult.NotHungry;
            }

            // Remove is all-or-nothing, so a failure here cannot leave a partial spend.
            if (!inventory.Remove(definition.FeedItemId, definition.FeedAmount))
            {
                return AnimalActionResult.NotEnoughFeed;
            }

            needs.OnFed();

            // One cycle per feeding: TryBegin refuses while a cycle runs or a product waits.
            bool started = production.TryBegin();

            AnimalDebug.Log(this, DisplayName + (started ? " production started" : " fed, but production did NOT start"),
                "Item: " + definition.ProductItemId,
                "Duration: " + definition.ProductionSeconds + "s (habitat speed multiplier applies)",
                "Phase: " + production.Phase,
                started ? null : "Reason: a cycle was already running or a product is waiting");

            eatTimer = definition.EatDuration;
            SetActivity(AnimalActivity.Eating);
            animator?.TriggerDip();

            StateChanged?.Invoke(this);
            return AnimalActionResult.Success;
        }

        /// <summary>
        /// Grants a ready product. Rejects every phase but <see cref="ProductionPhase.Ready"/>,
        /// so a second press can never pay out twice.
        /// </summary>
        public AnimalActionResult TryCollect(PlayerInventory inventory, out int amount)
        {
            amount = 0;

            if (definition == null)
            {
                return AnimalActionResult.NoDefinition;
            }

            if (inventory == null)
            {
                return AnimalActionResult.NoInventory;
            }

            if (!production.IsReady)
            {
                return AnimalActionResult.InvalidState;
            }

            // Collect clears the phase before anything is granted, mirroring FarmPlot.TryHarvest.
            int rolled = production.Collect(needs.IsHappy);
            if (rolled <= 0)
            {
                return AnimalActionResult.InvalidState;
            }

            int before = inventory.GetQuantity(definition.ProductItemId);
            inventory.Add(definition.ProductItemId, rolled);
            needs.AddHappiness(4f);
            amount = rolled;

            AnimalDebug.Log(this, definition.ProductItemId + " collected from " + DisplayName,
                "Inventory before: " + before,
                "Inventory after: " + inventory.GetQuantity(definition.ProductItemId),
                "Phase: " + production.Phase);

            StateChanged?.Invoke(this);
            return AnimalActionResult.Success;
        }

        /// <summary>
        /// Development helper: makes the animal hungry immediately so a feed can be tested
        /// without waiting out a hunger timer.
        ///
        /// This sets a need. It does NOT feed the animal, grant produce or bypass the player's
        /// interaction path - the feed itself still has to go through the USE button.
        /// </summary>
        public void DebugMakeHungry()
        {
            if (definition == null || needs == null)
            {
                return;
            }

            needs.Restore(1f, needs.Happiness);

            AnimalDebug.Log(this, DisplayName + " forced hungry for testing",
                "Hunger: " + needs.Hunger.ToString("0.00") + " (threshold " + definition.HungerThreshold + ")",
                "Feed item: " + definition.FeedItemId + " x" + definition.FeedAmount);

            StateChanged?.Invoke(this);
        }

        // ============================================================ save preparation

        /// <summary>Capture for the future save system. Phase 5 does not persist anything.</summary>
        public AnimalSnapshot CaptureSnapshot()
        {
            Vector3 position = transform.position;

            return new AnimalSnapshot
            {
                InstanceId = instanceId,
                AnimalId = definition != null ? definition.AnimalId : string.Empty,
                HabitatId = habitat != null ? habitat.HabitatId : string.Empty,
                Hunger = needs != null ? needs.Hunger : 0f,
                Happiness = needs != null ? needs.Happiness : 0f,
                ProductionPhase = production != null ? production.Phase : ProductionPhase.Dormant,
                ProductionElapsed = production != null ? production.Elapsed : 0f,
                PositionX = position.x,
                PositionZ = position.z,
                Yaw = transform.eulerAngles.y
            };
        }

        /// <summary>
        /// Restores a captured state directly, bypassing the action guards.
        /// Only for the save system - gameplay must go through the Try* methods.
        /// </summary>
        public void RestoreSnapshot(AnimalSnapshot snapshot)
        {
            EnsureRuntimeState();

            if (needs == null || production == null)
            {
                return;
            }

            instanceId = snapshot.InstanceId;
            needs.Restore(snapshot.Hunger, snapshot.Happiness);
            production.Restore(snapshot.ProductionPhase, snapshot.ProductionElapsed);

            transform.position = new Vector3(snapshot.PositionX, transform.position.y, snapshot.PositionZ);
            transform.rotation = Quaternion.Euler(0f, snapshot.Yaw, 0f);

            SetActivity(AnimalActivity.Idle);
            StateChanged?.Invoke(this);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only wiring used by the prototype builder tool.</summary>
        public void EditorConfigure(
            AnimalDefinition animalDefinition, AnimalHabitat owningHabitat,
            AnimalIdleAnimator idleAnimator, string id)
        {
            definition = animalDefinition;
            habitat = owningHabitat;
            animator = idleAnimator;
            instanceId = id;
        }
#endif
    }
}
