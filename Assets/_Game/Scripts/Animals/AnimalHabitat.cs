using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// A pen: the coop run, the cow pasture. It is the registry, the capacity rule, the
    /// movement bounds and the simulation driver for the animals inside it.
    ///
    /// This is deliberately the same relationship <c>FarmGrid</c> has with <c>FarmPlot</c>:
    /// one container owns a set of actors and runs a SINGLE Update for all of them, while each
    /// actor still owns its own state. Two habitats in the farm scene means two Update calls
    /// for every animal on the farm.
    ///
    /// It also replaces the scene-wide "AnimalManager" a farm game usually grows: capacity,
    /// bounds and membership already belong to the pen, so no singleton and no scene search
    /// is needed to find them.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimalHabitat : MonoBehaviour
    {
        /// <summary>An axis-aligned local-XZ rectangle animals will not walk into.</summary>
        [Serializable]
        public struct ExclusionZone
        {
            [Tooltip("Centre in habitat local space (XZ).")]
            public Vector2 Centre;

            [Tooltip("Half extents in habitat local space (XZ).")]
            public Vector2 HalfExtents;
        }

        [Header("Identity")]
        [Tooltip("Stable id used by save data. Never rename once shipped.")]
        [SerializeField] private string habitatId = "habitat_coop";
        [SerializeField] private AnimalType acceptedType = AnimalType.Chicken;

        [Header("Capacity")]
        [Tooltip("Total capacity weight this habitat can hold.")]
        [Min(1)] [SerializeField] private int capacity = 6;

        [Header("Wander area (habitat local space)")]
        [SerializeField] private Vector2 wanderCentre = Vector2.zero;
        [Tooltip("Half extents of the walkable rectangle.")]
        [SerializeField] private Vector2 wanderHalfExtents = new Vector2(5f, 3.5f);
        [Tooltip("Rectangles inside the pen that animals must not enter - the coop, the barn.")]
        [SerializeField] private ExclusionZone[] exclusions;

        [Header("Spacing")]
        [Tooltip("Animals will not choose a destination this close to another animal.")]
        [Min(0f)] [SerializeField] private float minSeparation = 0.9f;
        [Tooltip("Animals will not choose a destination this close to the player.")]
        [Min(0f)] [SerializeField] private float playerClearance = 1.6f;
        [Tooltip("Optional. Used only to keep animals from crowding the player.")]
        [SerializeField] private Transform playerTransform;

        [Header("Simulation")]
        [Tooltip("Seconds between hunger/production evaluations. Timing stays exact regardless: " +
                 "elapsed time is accumulated, not sampled.")]
        [Range(0.1f, 2f)] [SerializeField] private float simulationInterval = 0.5f;

        [Tooltip("Multiplies hunger and production speed. 1 = authored pace, 5 = five times " +
                 "faster. Set to 1 before shipping, or leave 'Editor Only' ticked. Mirrors " +
                 "FarmingSettings.developmentGrowthMultiplier, which does the same for crops.")]
        [Min(0.01f)] [SerializeField] private float developmentSpeedMultiplier = 1f;

        [Tooltip("When ticked the multiplier applies in the Editor only, so a device build " +
                 "always runs at the authored speed.")]
        [SerializeField] private bool developmentEditorOnly = true;

        [Tooltip("Development only. Traces the whole feed -> produce -> collect chain to the " +
                 "Console for every animal. Turn off once the loop is proven.")]
        [SerializeField] private bool logAnimalDiagnostics;

        private readonly List<AnimalController> animals = new List<AnimalController>();

        private float simulationAccumulator;

        /// <summary>Samples tried before an animal gives up on finding a spot this dwell.</summary>
        private const int DestinationSamples = 8;

        public string HabitatId => habitatId;

        public AnimalType AcceptedType => acceptedType;

        public int Capacity => capacity;

        public int AnimalCount => animals.Count;

        /// <summary>Sum of the capacity weights of the animals currently registered.</summary>
        public int UsedCapacity { get; private set; }

        public IReadOnlyList<AnimalController> Animals => animals;

        /// <summary>Effective simulation speed for the current runtime. Read live, so it can be tuned during Play.</summary>
        public float SpeedMultiplier
        {
            get
            {
                if (developmentEditorOnly && !Application.isEditor)
                {
                    return 1f;
                }

                return Mathf.Max(0.01f, developmentSpeedMultiplier);
            }
        }

        /// <summary>Raised when an animal joins or leaves, so a future HUD can show occupancy.</summary>
        public event Action<AnimalHabitat> OccupancyChanged;

        // ============================================================ registration

        /// <summary>True when one more animal of this species would still fit.</summary>
        public bool HasRoomFor(AnimalDefinition definition)
        {
            if (definition == null || definition.HabitatType != acceptedType)
            {
                return false;
            }

            return UsedCapacity + definition.CapacityWeight <= capacity;
        }

        /// <summary>
        /// Adds an animal. Returns false - and explains why - when the species is wrong or the
        /// pen is full, so a future purchase flow can validate before it spawns anything.
        /// </summary>
        public bool Register(AnimalController animal)
        {
            if (animal == null || animals.Contains(animal))
            {
                return false;
            }

            AnimalDefinition definition = animal.Definition;

            if (definition == null)
            {
                Debug.LogWarning(
                    "AnimalHabitat '" + habitatId + "' refused '" + animal.name + "': it has no AnimalDefinition.", this);
                return false;
            }

            if (definition.HabitatType != acceptedType)
            {
                Debug.LogWarning(
                    "AnimalHabitat '" + habitatId + "' accepts " + acceptedType + " but '" +
                    animal.name + "' is a " + definition.HabitatType + ".", this);
                return false;
            }

            if (UsedCapacity + definition.CapacityWeight > capacity)
            {
                Debug.LogWarning(
                    "AnimalHabitat '" + habitatId + "' is full (" + UsedCapacity + "/" + capacity +
                    "); '" + animal.name + "' was not registered.", this);
                return false;
            }

            animals.Add(animal);
            UsedCapacity += definition.CapacityWeight;
            OccupancyChanged?.Invoke(this);

            AnimalDebug.Log(this, "Registered " + animal.name + " with habitat " + habitatId,
                "Capacity: " + UsedCapacity + "/" + capacity,
                "Simulation: " + simulationInterval + "s interval x" + SpeedMultiplier + " speed");

            return true;
        }

        public void Unregister(AnimalController animal)
        {
            if (animal == null || !animals.Remove(animal))
            {
                return;
            }

            if (animal.Definition != null)
            {
                UsedCapacity = Mathf.Max(0, UsedCapacity - animal.Definition.CapacityWeight);
            }

            OccupancyChanged?.Invoke(this);
        }

        private void Awake()
        {
            if (logAnimalDiagnostics)
            {
                AnimalDebug.Enabled = true;
            }
        }

        // ============================================================ ticking

        private void Update()
        {
            int count = animals.Count;
            if (count == 0)
            {
                return;
            }

            float delta = Time.deltaTime;

            // Per-frame pass: movement and the animator drive only.
            for (int i = count - 1; i >= 0; i--)
            {
                AnimalController animal = animals[i];

                if (animal == null)
                {
                    animals.RemoveAt(i);
                    continue;
                }

                animal.FrameTick(delta);
            }

            simulationAccumulator += delta;
            if (simulationAccumulator < simulationInterval)
            {
                return;
            }

            // Accumulated, not sampled: hunger and production stay exact whatever the interval is,
            // and they keep running while the animal is off screen.
            float simulationDelta = simulationAccumulator * SpeedMultiplier;
            simulationAccumulator = 0f;

            for (int i = animals.Count - 1; i >= 0; i--)
            {
                AnimalController animal = animals[i];

                if (animal == null)
                {
                    animals.RemoveAt(i);
                    continue;
                }

                animal.SimulationTick(simulationDelta);
            }
        }

        // ============================================================ movement bounds

        /// <summary>
        /// Picks a legal wander destination for one animal, or returns false.
        /// Legality is decided here, once per walk, so the movement code never has to test
        /// anything per frame.
        /// </summary>
        public bool TrySampleDestination(AnimalController asker, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            if (asker == null || wanderHalfExtents.x <= 0f || wanderHalfExtents.y <= 0f)
            {
                return false;
            }

            float y = asker.transform.position.y;

            for (int attempt = 0; attempt < DestinationSamples; attempt++)
            {
                Vector2 local = wanderCentre + new Vector2(
                    UnityEngine.Random.Range(-wanderHalfExtents.x, wanderHalfExtents.x),
                    UnityEngine.Random.Range(-wanderHalfExtents.y, wanderHalfExtents.y));

                if (IsExcluded(local))
                {
                    continue;
                }

                Vector3 candidate = transform.TransformPoint(new Vector3(local.x, 0f, local.y));
                candidate.y = y;

                if (IsCrowded(candidate, asker) || IsNearPlayer(candidate))
                {
                    continue;
                }

                worldPoint = candidate;
                return true;
            }

            return false;
        }

        /// <summary>True when a world point is inside the walkable rectangle and outside every exclusion.</summary>
        public bool IsWalkable(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector2 flat = new Vector2(local.x, local.z);

            if (Mathf.Abs(flat.x - wanderCentre.x) > wanderHalfExtents.x ||
                Mathf.Abs(flat.y - wanderCentre.y) > wanderHalfExtents.y)
            {
                return false;
            }

            return !IsExcluded(flat);
        }

        private bool IsExcluded(Vector2 local)
        {
            if (exclusions == null)
            {
                return false;
            }

            for (int i = 0; i < exclusions.Length; i++)
            {
                ExclusionZone zone = exclusions[i];

                if (Mathf.Abs(local.x - zone.Centre.x) <= zone.HalfExtents.x &&
                    Mathf.Abs(local.y - zone.Centre.y) <= zone.HalfExtents.y)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCrowded(Vector3 candidate, AnimalController asker)
        {
            if (minSeparation <= 0f)
            {
                return false;
            }

            float threshold = minSeparation * minSeparation;

            for (int i = 0; i < animals.Count; i++)
            {
                AnimalController other = animals[i];

                if (other == null || ReferenceEquals(other, asker))
                {
                    continue;
                }

                if ((other.transform.position - candidate).sqrMagnitude < threshold)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsNearPlayer(Vector3 candidate)
        {
            if (playerTransform == null || playerClearance <= 0f)
            {
                return false;
            }

            return (playerTransform.position - candidate).sqrMagnitude < playerClearance * playerClearance;
        }

        public void SetPlayer(Transform player)
        {
            playerTransform = player;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigure(
            string id, AnimalType type, int maxCapacity,
            Vector2 centre, Vector2 halfExtents, ExclusionZone[] blocked)
        {
            habitatId = id;
            acceptedType = type;
            capacity = maxCapacity;
            wanderCentre = centre;
            wanderHalfExtents = halfExtents;
            exclusions = blocked;
        }

        /// <summary>Editor-only development pacing, mirroring FarmingSettings for crops.</summary>
        public void EditorConfigureDevelopmentSpeed(float multiplier, bool editorOnly, bool diagnostics)
        {
            developmentSpeedMultiplier = multiplier;
            developmentEditorOnly = editorOnly;
            logAnimalDiagnostics = diagnostics;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.7f);
            Gizmos.DrawWireCube(
                new Vector3(wanderCentre.x, 0.1f, wanderCentre.y),
                new Vector3(wanderHalfExtents.x * 2f, 0.1f, wanderHalfExtents.y * 2f));

            if (exclusions == null)
            {
                return;
            }

            Gizmos.color = new Color(0.95f, 0.35f, 0.3f, 0.7f);
            for (int i = 0; i < exclusions.Length; i++)
            {
                ExclusionZone zone = exclusions[i];
                Gizmos.DrawWireCube(
                    new Vector3(zone.Centre.x, 0.1f, zone.Centre.y),
                    new Vector3(zone.HalfExtents.x * 2f, 0.1f, zone.HalfExtents.y * 2f));
            }
        }
#endif
    }
}
