using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// Designer-authored configuration for one animal species.
    /// <see cref="AnimalController"/> runs one generic lifecycle against this data, so there is
    /// no chicken-specific or cow-specific branch anywhere in the runtime code. Adding a species
    /// means authoring an asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Animal_",
        menuName = "Little Farm Story/Animal Definition",
        order = 10)]
    public class AnimalDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by save data and orders. Never rename this once shipped.")]
        [SerializeField] private string animalId = "chicken";
        [SerializeField] private string displayName = "Chicken";
        [SerializeField] private AnimalType animalType = AnimalType.Chicken;
        [Tooltip("Visual prefab. Used by future purchase/spawn code; the scene builder places its own instances.")]
        [SerializeField] private GameObject prefab;

        [Header("Economy")]
        [Min(0)] [SerializeField] private int purchaseCost = 100;

        [Tooltip("Coins one unit of this animal's produce sells for. 0 means the produce has no " +
                 "price yet and must not be offered for sale - egg and milk pricing is a later " +
                 "phase. The slot exists now so adding it needs no restructuring.")]
        [Min(0)] [SerializeField] private int produceSellValue;

        [Header("Feeding")]
        [Tooltip("Inventory id consumed when feeding.")]
        [SerializeField] private string feedItemId = "wheat";
        [Min(1)] [SerializeField] private int feedAmount = 1;
        [Tooltip("Seconds from fully fed to fully hungry.")]
        [Min(1f)] [SerializeField] private float hungerDuration = 60f;
        [Tooltip("Hunger fraction at which the animal will accept food.")]
        [Range(0.05f, 1f)] [SerializeField] private float hungerThreshold = 0.5f;
        [Tooltip("Seconds the eating pose is held after a feed.")]
        [Min(0.1f)] [SerializeField] private float eatDuration = 2.5f;

        [Header("Production")]
        [Tooltip("Inventory id granted when collecting.")]
        [SerializeField] private string productItemId = "egg";
        [Min(1)] [SerializeField] private int productAmount = 1;
        [Tooltip("Extra product granted when happiness is at or above the happy threshold.")]
        [Min(0)] [SerializeField] private int happyBonusAmount = 1;
        [Tooltip("Seconds from being fed to the product being ready.")]
        [Min(1f)] [SerializeField] private float productionSeconds = 30f;

        [Header("Happiness")]
        [Tooltip("Happiness granted by one feeding, on a 0-100 scale.")]
        [Range(0f, 100f)] [SerializeField] private float feedHappiness = 22f;
        [Tooltip("Happiness lost per second while the animal is hungry.")]
        [Min(0f)] [SerializeField] private float happinessDecayPerSecond = 0.6f;
        [Range(0f, 100f)] [SerializeField] private float happyThreshold = 70f;

        [Header("Movement")]
        [Min(0f)] [SerializeField] private float moveSpeed = 0.55f;
        [Min(1f)] [SerializeField] private float turnSpeed = 220f;
        [Tooltip("Average seconds spent standing still between walks.")]
        [Min(0.1f)] [SerializeField] private float idleDwell = 3.5f;
        [Tooltip("Chance of choosing to walk when an idle dwell expires.")]
        [Range(0f, 1f)] [SerializeField] private float wanderChance = 0.7f;

        [Header("Interaction")]
        [Tooltip("Radius of the trigger the player must reach to interact.")]
        [Min(0.1f)] [SerializeField] private float interactionRadius = 0.8f;

        [Header("Habitat")]
        [SerializeField] private AnimalType habitatType = AnimalType.Chicken;
        [Tooltip("How much habitat capacity one of these consumes.")]
        [Min(1)] [SerializeField] private int capacityWeight = 1;

        public string AnimalId => animalId;

        public string DisplayName => displayName;

        public AnimalType Type => animalType;

        public GameObject Prefab => prefab;

        public int PurchaseCost => purchaseCost;

        /// <summary>Coins per unit of produce, or 0 when it is not priced yet.</summary>
        public int ProduceSellValue => produceSellValue;

        /// <summary>False while the produce has no price, which is the case for this phase.</summary>
        public bool HasProduceSellValue => produceSellValue > 0;

        public string FeedItemId => feedItemId;

        public int FeedAmount => Mathf.Max(1, feedAmount);

        public float HungerDuration => Mathf.Max(1f, hungerDuration);

        public float HungerThreshold => Mathf.Clamp(hungerThreshold, 0.05f, 1f);

        public float EatDuration => Mathf.Max(0.1f, eatDuration);

        public string ProductItemId => productItemId;

        public int ProductAmount => Mathf.Max(1, productAmount);

        public int HappyBonusAmount => Mathf.Max(0, happyBonusAmount);

        public float ProductionSeconds => Mathf.Max(1f, productionSeconds);

        public float FeedHappiness => feedHappiness;

        public float HappinessDecayPerSecond => Mathf.Max(0f, happinessDecayPerSecond);

        public float HappyThreshold => happyThreshold;

        public float MoveSpeed => Mathf.Max(0f, moveSpeed);

        public float TurnSpeed => Mathf.Max(1f, turnSpeed);

        public float IdleDwell => Mathf.Max(0.1f, idleDwell);

        public float WanderChance => Mathf.Clamp01(wanderChance);

        public float InteractionRadius => Mathf.Max(0.1f, interactionRadius);

        public AnimalType HabitatType => habitatType;

        public int CapacityWeight => Mathf.Max(1, capacityWeight);

#if UNITY_EDITOR
        /// <summary>Editor-only authoring helper used by the prototype builder tool.</summary>
        public void EditorConfigureIdentity(
            string id, string display, AnimalType type, GameObject visualPrefab, int cost)
        {
            animalId = id;
            displayName = display;
            animalType = type;
            habitatType = type;
            prefab = visualPrefab;
            purchaseCost = cost;
        }

        /// <summary>Editor-only authoring helper for the feeding and production loop.</summary>
        public void EditorConfigureLoop(
            string feedId, int feedQuantity, float hungerSeconds,
            string productId, int productQuantity, float produceSeconds)
        {
            feedItemId = feedId;
            feedAmount = feedQuantity;
            hungerDuration = hungerSeconds;
            productItemId = productId;
            productAmount = productQuantity;
            productionSeconds = produceSeconds;
        }

        /// <summary>Editor-only authoring helper for movement and interaction tuning.</summary>
        public void EditorConfigureMovement(
            float speed, float turn, float dwell, float chance, float radius, int weight)
        {
            moveSpeed = speed;
            turnSpeed = turn;
            idleDwell = dwell;
            wanderChance = chance;
            interactionRadius = radius;
            capacityWeight = weight;
        }
#endif
    }
}
