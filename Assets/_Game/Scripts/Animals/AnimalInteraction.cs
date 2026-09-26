using LittleFarmStory.Core;
using LittleFarmStory.Interaction;
using LittleFarmStory.Inventory;
using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// Makes an animal usable through the EXISTING interaction system: it is an
    /// <see cref="InteractableBase"/> like a farm plot, found by the same
    /// <c>InteractionController</c> proximity scan and driven by the same interact button.
    /// No second input path, no bespoke UI.
    ///
    /// One press does the most useful thing available: collect a waiting product, otherwise
    /// feed a hungry animal, otherwise report how the animal is doing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnimalController))]
    public class AnimalInteraction : InteractableBase
    {
        [SerializeField] private AnimalController animal;

        // Cached per-interactor services, resolved once instead of on every button press.
        private bool announcedReady;

        private GameObject cachedInteractor;
        private PlayerInventory cachedInventory;
        private ActionFeedbackChannel cachedFeedback;

        private void Awake()
        {
            if (animal == null)
            {
                animal = GetComponent<AnimalController>();
            }
        }

        private void OnEnable()
        {
            if (animal != null)
            {
                animal.StateChanged += OnAnimalStateChanged;
            }

            RefreshLabel();
        }

        private void OnDisable()
        {
            if (animal != null)
            {
                animal.StateChanged -= OnAnimalStateChanged;
            }
        }

        private void OnAnimalStateChanged(AnimalController changed)
        {
            // Announce the product the moment it becomes ready, but only to a player who has
            // already interacted with this animal - that is the only feedback channel we hold.
            if (animal != null && animal.HasProductReady && !announcedReady)
            {
                announcedReady = true;
                Post(ProductName + " Ready!");
            }
            else if (animal != null && !animal.HasProductReady)
            {
                announcedReady = false;
            }

            RefreshLabel();
        }

        // ============================================================ interaction

        protected override void OnInteract(GameObject interactor)
        {
            if (animal == null)
            {
                return;
            }

            ResolveInteractorServices(interactor);

            bool collecting = animal.HasProductReady;
            AnimalDefinition definition = animal.Definition;

            AnimalDebug.Log(this, animal.DisplayName + " interaction requested",
                "Action: " + (collecting ? "Collect" : "Feed"),
                "Definition: " + (definition != null ? definition.DisplayName : "MISSING"),
                "Activity: " + animal.Activity,
                "Hunger: " + (animal.Needs != null ? animal.Needs.Hunger.ToString("0.00") : "n/a") +
                    (animal.IsHungry ? " (hungry)" : " (not hungry)"),
                "Happiness: " + (animal.Needs != null ? animal.Needs.Happiness.ToString("0") : "n/a"),
                "Production: " + (animal.Production != null ? animal.Production.Phase.ToString() : "n/a") +
                    " " + (animal.Production != null ? (animal.Production.Progress * 100f).ToString("0") + "%" : ""),
                "Feed item: " + (definition != null ? definition.FeedItemId + " x" + definition.FeedAmount : "n/a"),
                "Feed held: " + (cachedInventory != null && definition != null
                    ? cachedInventory.GetQuantity(definition.FeedItemId).ToString() : "NO INVENTORY"),
                "Inventory component: " + (cachedInventory != null ? "found" : "MISSING on the interactor"));

            // Collection wins: a player standing over a ready egg means to pick it up.
            if (collecting)
            {
                HandleCollect();
            }
            else
            {
                HandleFeed();
            }

            RefreshLabel();
        }

        private void HandleCollect()
        {
            AnimalActionResult result = animal.TryCollect(cachedInventory, out int amount);

            AnimalDebug.Log(this, animal.DisplayName + " collect result",
                "Result: " + (result == AnimalActionResult.Success ? "SUCCESS" : "FAILURE"),
                "Reason: " + result,
                "Amount: " + amount);

            switch (result)
            {
                case AnimalActionResult.Success:
                    Post(ProductName + " Collected +" + amount);
                    break;

                case AnimalActionResult.NoInventory:
                    Debug.LogWarning("AnimalInteraction: the interactor has no PlayerInventory component.", this);
                    break;
            }
        }

        private void HandleFeed()
        {
            AnimalActionResult result = animal.TryFeed(cachedInventory);

            AnimalDebug.Log(this, animal.DisplayName + " feed result",
                "Result: " + (result == AnimalActionResult.Success ? "SUCCESS" : "FAILURE"),
                "Reason: " + result);

            switch (result)
            {
                case AnimalActionResult.Success:
                    Post(animal.DisplayName + " Fed");
                    break;

                case AnimalActionResult.NotEnoughFeed:
                    Post("Need " + FeedAmount + " " + FeedName + " to feed the " + animal.DisplayName);
                    break;

                case AnimalActionResult.NotHungry:
                    Post(animal.DisplayName + StatusSuffix);
                    break;

                case AnimalActionResult.NoInventory:
                    Debug.LogWarning("AnimalInteraction: the interactor has no PlayerInventory component.", this);
                    break;
            }
        }

        // ============================================================ label

        /// <summary>
        /// Keeps the HUD prompt truthful while the player stands still - the same contextual
        /// labelling a farm plot does as it ripens.
        /// </summary>
        private void RefreshLabel()
        {
            if (animal == null)
            {
                return;
            }

            string label;

            if (animal.HasProductReady)
            {
                label = "Collect " + ProductName;
            }
            else if (animal.IsHungry)
            {
                label = "Feed " + animal.DisplayName;
            }
            else
            {
                label = animal.DisplayName;
            }

            SetLabel(label);
        }

        private string ProductName
        {
            get
            {
                AnimalDefinition definition = animal != null ? animal.Definition : null;
                return definition != null ? Titlecase(definition.ProductItemId) : "Product";
            }
        }

        private string FeedName
        {
            get
            {
                AnimalDefinition definition = animal != null ? animal.Definition : null;
                return definition != null ? Titlecase(definition.FeedItemId) : "Feed";
            }
        }

        private int FeedAmount
        {
            get
            {
                AnimalDefinition definition = animal != null ? animal.Definition : null;
                return definition != null ? definition.FeedAmount : 1;
            }
        }

        private string StatusSuffix
        {
            get
            {
                if (animal.Needs == null)
                {
                    return " is fine";
                }

                if (animal.Production != null && animal.Production.Phase == ProductionPhase.Producing)
                {
                    return " is busy - " + Mathf.RoundToInt(animal.Production.Progress * 100f) + "%";
                }

                return animal.Needs.Mood == AnimalMood.Happy ? " is happy!" : " is not hungry yet";
            }
        }

        /// <summary>Item ids are lowercase by convention; the HUD shows them capitalised.</summary>
        private static string Titlecase(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(itemId[0]) + itemId.Substring(1);
        }

        private void ResolveInteractorServices(GameObject interactor)
        {
            if (interactor == null || ReferenceEquals(interactor, cachedInteractor))
            {
                return;
            }

            cachedInteractor = interactor;
            cachedInventory = interactor.GetComponent<PlayerInventory>();
            cachedFeedback = interactor.GetComponent<ActionFeedbackChannel>();
        }

        private void Post(string message)
        {
            if (cachedFeedback != null)
            {
                cachedFeedback.Post(message);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only wiring used by the prototype builder tool.</summary>
        public void EditorConfigure(AnimalController owner)
        {
            animal = owner;
        }
#endif
    }
}
