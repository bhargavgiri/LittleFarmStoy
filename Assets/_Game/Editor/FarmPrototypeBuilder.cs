using System.Collections.Generic;
using LittleFarmStory.Animals;
using LittleFarmStory.CameraSystem;
using LittleFarmStory.Core;
using LittleFarmStory.Economy;
using LittleFarmStory.Farming;
using LittleFarmStory.Input;
using LittleFarmStory.Interaction;
using LittleFarmStory.Inventory;
using LittleFarmStory.Persistence;
using LittleFarmStory.Player;
using LittleFarmStory.UI;
using LittleFarmStory.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// One-click generator for the whole farm: scene, materials, meshes, prefabs, layout,
    /// player, camera, lighting and HUD. Re-runnable and fully deterministic - the scene is
    /// a build product, so nothing is lost by regenerating it.
    /// </summary>
    public static class FarmPrototypeBuilder
    {
        public const string ScenePath = ProtoAssets.ScenesFolder + "/Farm_Prototype.unity";
        private const string PlotPrefabPath = ProtoAssets.PrefabsFolder + "/FarmPlot.prefab";
        private const string InteractableLayerName = "Interactable";

        // Authored growth pacing. With 3 growth stages these give 18s / 27s / 36s full cycles,
        // which keeps the prototype testable without touching the dev multiplier.
        // ---- development pacing. Both are EDITOR ONLY: a device build always runs at the
        // authored speed, so these can be left dialled up without affecting the real game.
        // Set either to 1 to feel the shipping pace.
        private const float DevelopmentGrowthMultiplier = 4f;
        private const float DevelopmentAnimalMultiplier = 5f;

        // Console tracing for the animal chain and for interaction focus. On while the animal
        // loops are being proven; set both to false once the automated test passes.
        private const bool AnimalDiagnostics = true;
        private const bool InteractionDiagnostics = true;

        /// <summary>Logs every transaction and every refusal while the economy is being proven.</summary>
        private const bool EconomyDiagnostics = true;

        /// <summary>Logs every save and load while persistence is being proven.</summary>
        private const bool SaveDiagnostics = true;

        // ---- economy. The shop catalogue and the crop assets are both authored from these,
        // so a price exists in exactly one place.
        private const int StartingCoins = 100;
        private const int WheatSeedPrice = 2;
        private const int WheatSellPrice = 4;

        private const float WheatSecondsPerStage = 6f;
        private const float TomatoSecondsPerStage = 9f;
        private const float CornSecondsPerStage = 12f;

        // ---- zone anchors, shared by the layout and the decoration pass
        private static readonly Vector3 WheatCentre = new Vector3(-16.5f, 0f, 17.5f);
        private static readonly Vector3 TomatoCentre = new Vector3(-16.5f, 0f, 6.5f);
        private static readonly Vector3 CornCentre = new Vector3(16.5f, 0f, 17.5f);
        private static readonly Vector3 ProductionCentre = new Vector3(16.5f, 0f, 6.5f);
        private static readonly Vector3 ChickenCentre = new Vector3(-16.5f, 0f, -7.5f);
        private static readonly Vector3 CowCentre = new Vector3(-16.5f, 0f, -20f);
        private static readonly Vector3 MarketCentre = new Vector3(16.5f, 0f, -8f);
        private static readonly Vector3 HomeCentre = new Vector3(16.5f, 0f, -20f);
        private static readonly Vector3 PondCentre = new Vector3(-26.8f, 0f, 27f);

        [MenuItem("Little Farm Story/Run Full Setup (Player Settings + Scene)", false, 0)]
        public static void RunFullSetup()
        {
            AndroidPlayerSetup.Apply();
            UrpQualitySetup.Apply();
            BuildPrototypeScene();
        }

        [MenuItem("Little Farm Story/Rebuild Farm Scene", false, 20)]
        public static void BuildPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("Little Farm Story: scene build cancelled by the user.");
                return;
            }

            ProtoAssets.EnsureFolders();
            UrpQualitySetup.Apply();

            // Gate the whole build on TMP BEFORE the current scene is replaced. Discovering a
            // missing font halfway through would leave a half-built farm and a blank HUD.
            TextMeshProSetup.EnsureImported();

            if (TextMeshProSetup.ResolveDefaultFont() == null)
            {
                Debug.LogError(
                    "Little Farm Story: scene build cancelled - no TextMeshPro font asset. " +
                    "Import Window > TextMeshPro > Import TMP Essential Resources, then rebuild. " +
                    "Nothing was changed.");
                return;
            }

            int interactableLayer = ProtoAssets.EnsureLayer(InteractableLayerName);

            // ORDER MATTERS. EditorSceneManager.NewScene unloads assets nothing in the new
            // scene references yet, which turns a freshly created ScriptableObject reference
            // into Unity's "fake null": the managed wrapper compares == null even though the
            // asset is still on disk. Authoring the crop assets before this line is exactly
            // what silently disabled crop visuals and the HUD counters for three phases -
            // ConfigureCropGrowth and WireInventoryCounters both hit their null guards and
            // returned, while SerializedObject assignment still resolved the underlying
            // instance id, so the scene looked correctly wired.
            //
            // Everything asset-related now happens AFTER the scene exists.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The palette creates material assets, so it is subject to the same unload hazard
            // and is built after the scene exists too.
            ProtoPalette p = ProtoPalette.Create();

            // Seed cost and sell value are kept in step with the shop catalogue below, so the
            // crop asset and the shop can never quote two different prices for the same item.
            CropDefinition wheat = EnsureCrop("Crop_Wheat", "wheat", "Wheat",
                ProtoPalette.WheatAccent, ProtoPalette.Hex("6B4A2F"), WheatSeedPrice, WheatSellPrice);
            CropDefinition tomato = EnsureCrop("Crop_Tomato", "tomato", "Tomato",
                ProtoPalette.TomatoAccent, ProtoPalette.Hex("63432B"), 9, 22);
            CropDefinition corn = EnsureCrop("Crop_Corn", "corn", "Corn",
                ProtoPalette.CornAccent, ProtoPalette.Hex("6E4C30"), 14, 34);

            FarmingSettings farmingSettings = EnsureFarmingSettings();

            ConfigureCropGrowth(wheat, p, WheatSecondsPerStage, 1, 3);
            ConfigureCropGrowth(tomato, p, TomatoSecondsPerStage, 1, 2);
            ConfigureCropGrowth(corn, p, CornSecondsPerStage, 1, 2);

            // Icons are generated before anything that uses them - the plot prefab and the
            // animal prefabs both hang a world-space badge built from this set.
            UiIconLibrary.BuildAll();

            PropLibrary.Props props = PropLibrary.BuildAll(p);
            PropLibraryExtra.Extras extras = PropLibraryExtra.BuildAll(p);
            GameObject farmerPrefab = CharacterBuilder.BuildFarmerPrefab(p);
            GameObject chickenPrefabA = CharacterBuilder.BuildChickenPrefab(p, "Chicken_A", p.White);
            GameObject chickenPrefabB = CharacterBuilder.BuildChickenPrefab(p, "Chicken_B", p.ChickenBrown);
            GameObject cowPrefab = CharacterBuilder.BuildCowPrefab(p);

            // Animal definitions are authored AFTER the prefabs exist, because a definition holds
            // a prefab reference. The habitat and the instance id are wired per instance instead.
            AnimalDefinition chickenDefinition = EnsureChickenDefinition(chickenPrefabA);
            AnimalDefinition cowDefinition = EnsureCowDefinition(cowPrefab);

            EconomySettings economySettings = EnsureEconomySettings();
            ShopDefinition shopDefinition = EnsureShopDefinition(wheat);

            AssetDatabase.SaveAssets();
            VerifyCropVisuals(wheat, tomato, corn);

            FarmPlot plotPrefab = BuildPlotPrefab(p, interactableLayer);

            ConfigureLighting(p);

            GameObject systemsRoot = new GameObject("--- SYSTEMS ---");
            GameObject worldRoot = new GameObject("--- WORLD ---");
            GameObject actorsRoot = new GameObject("--- ACTORS ---");
            GameObject uiRoot = new GameObject("--- UI ---");

            systemsRoot.AddComponent<GameBootstrap>();
            BuildEventSystem(systemsRoot.transform);

            BuildWorld(worldRoot.transform, p, plotPrefab, wheat, tomato, corn,
                interactableLayer, farmingSettings, props, extras,
                chickenPrefabA, chickenPrefabB, cowPrefab,
                chickenDefinition, cowDefinition);

            GameObject player = BuildPlayer(
                actorsRoot.transform, p, farmerPrefab, economySettings, shopDefinition);

            // Habitats keep animals from crowding the player. Resolved once here, at build time.
            AnimalHabitat[] habitats = worldRoot.GetComponentsInChildren<AnimalHabitat>(true);
            for (int i = 0; i < habitats.Length; i++)
            {
                habitats[i].SetPlayer(player.transform);
            }
            FarmCameraController cameraController = BuildCamera(actorsRoot.transform, player.transform);

            EconomyManager economyManager = player.GetComponent<EconomyManager>();
            PlayerController playerControllerForShop = player.GetComponent<PlayerController>();
            InteractionController interactionControllerForShop = player.GetComponent<InteractionController>();

            HudBuilder.Result hud = HudBuilder.Build(
                uiRoot.transform, BuildResourceTable(wheat, corn, chickenDefinition, cowDefinition),
                shopDefinition, economyManager, playerControllerForShop, interactionControllerForShop);

            if (hud == null)
            {
                Debug.LogError("Little Farm Story: the HUD could not be built; aborting the scene build.");
                return;
            }

            // The market's physical entrance already exists (built with BuildWorld, above); it
            // can only be pointed at the shop panel now that the panel exists.
            MarketInteractable market = worldRoot.GetComponentInChildren<MarketInteractable>(true);
            if (market != null)
            {
                market.EditorSetShopPanel(hud.ShopPanel);
            }
            else
            {
                Debug.LogError("Little Farm Story: no MarketInteractable found in the world; " +
                               "the shop can never be reached.");
            }

            WireEverything(player, cameraController, hud, interactableLayer, wheat);

            BuildSaveManager(systemsRoot.transform, player, cameraController, worldRoot);

            bool playable = VerifyBuild(
                player, worldRoot, hud.Hud, wheat, tomato, corn, chickenDefinition, cowDefinition,
                shopDefinition, economySettings);

            ProtoAssets.MarkStatic(worldRoot);

            // ...but not the animals. They walk now, and a batching-static renderer is baked
            // into a combined mesh at its authored transform - moving it afterwards does not
            // work. This also corrects the Phase 4B animals, whose idle motion had the same
            // conflict.
            AnimalController[] livestock = worldRoot.GetComponentsInChildren<AnimalController>(true);
            for (int i = 0; i < livestock.Length; i++)
            {
                ProtoAssets.ClearStatic(livestock[i].gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("Little Farm Story: failed to save the farm scene to " + ScenePath);
                return;
            }

            RegisterSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (playable)
            {
                Debug.Log("Little Farm Story: farm scene built at " + ScenePath +
                          " and verified PLAYABLE. Press Play to test.");
            }
            else
            {
                Debug.LogError("Little Farm Story: farm scene was saved to " + ScenePath +
                               " but verification FAILED. Do not treat this build as playable.");
            }
        }

        /// <summary>
        /// Adds the save system beside <c>GameBootstrap</c> and hands it every system that owns
        /// savable state. Fields and habitats are resolved from the built world rather than
        /// listed by hand, so a field added to the farm later is saved without touching this.
        /// </summary>
        private static void BuildSaveManager(
            Transform systemsRoot, GameObject player,
            FarmCameraController cameraController, GameObject worldRoot)
        {
            GameObject host = ProtoAssets.Empty("SaveSystem", systemsRoot, Vector3.zero);
            SaveManager manager = host.AddComponent<SaveManager>();

            manager.EditorConfigure(
                player.GetComponent<CurrencyWallet>(),
                player.GetComponent<PlayerInventory>(),
                player.GetComponent<PlayerController>(),
                cameraController,
                worldRoot.GetComponentsInChildren<FarmGrid>(true),
                worldRoot.GetComponentsInChildren<AnimalHabitat>(true),
                SaveDiagnostics);
        }

        // ================================================================ data assets

        /// <summary>
        /// Loads a ScriptableObject asset, creating it when missing, and re-resolves it if the
        /// managed reference has been unloaded. Unity returns a "fake null" for an asset whose
        /// wrapper was unloaded, so a plain null check would create a duplicate at a path that
        /// already exists; loading again by path is what actually revives the reference.
        /// </summary>
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
            {
                return asset;
            }

            if (System.IO.File.Exists(path))
            {
                // The file is there but the reference is dead: force a reimport and reload.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset != null)
                {
                    return asset;
                }
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        // ---------------------------------------------------------------- economy assets

        private static EconomySettings EnsureEconomySettings()
        {
            EconomySettings settings =
                LoadOrCreate<EconomySettings>(ProtoAssets.DataFolder + "/EconomySettings.asset");

            if (settings == null)
            {
                Debug.LogError("Little Farm Story: could not create EconomySettings; trade will be impossible.");
                return null;
            }

            settings.EditorConfigure(StartingCoins, 0.35f, 0, EconomyDiagnostics);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// Authors one shop line. Ids come from the crop asset rather than from literals, so the
        /// shop can never trade an id the farming system does not recognise.
        /// </summary>
        private static ShopItemDefinition EnsureShopItem(
            string assetName, string itemId, string displayName, string iconName,
            bool canBuy, int buyPrice, bool canSell, int sellPrice,
            int minQuantity, int maxQuantity, int step)
        {
            ShopItemDefinition item =
                LoadOrCreate<ShopItemDefinition>(ProtoAssets.DataFolder + "/" + assetName + ".asset");

            if (item == null)
            {
                Debug.LogError("Little Farm Story: could not create the shop item asset " + assetName + ".");
                return null;
            }

            item.EditorConfigure(
                itemId, displayName, UiIconLibrary.Get(iconName),
                canBuy, buyPrice, canSell, sellPrice,
                minQuantity, maxQuantity, step, true);

            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>
        /// The shop catalogue. Wheat seeds are the only thing on sale in this phase; wheat is
        /// bought back. Adding a crop here is one more entry - no runtime code knows what wheat
        /// is, and nothing branches on an item id anywhere.
        ///
        /// Egg and milk are deliberately absent: AnimalDefinition now carries a produce sell
        /// price slot, but it is unset, so they have no price and are not tradeable.
        /// </summary>
        private static ShopDefinition EnsureShopDefinition(CropDefinition wheat)
        {
            ShopDefinition shop =
                LoadOrCreate<ShopDefinition>(ProtoAssets.DataFolder + "/ShopDefinition.asset");

            if (shop == null)
            {
                Debug.LogError("Little Farm Story: could not create the ShopDefinition; trade will be impossible.");
                return null;
            }

            if (wheat == null)
            {
                Debug.LogError("Little Farm Story: the wheat crop asset is missing, so the shop " +
                               "catalogue cannot be authored from it.");
                shop.EditorConfigure(new ShopItemDefinition[0]);
                EditorUtility.SetDirty(shop);
                return shop;
            }

            ShopItemDefinition wheatSeeds = EnsureShopItem(
                "Shop_WheatSeeds", wheat.SeedItemId, wheat.DisplayName + " Seeds",
                UiIconLibrary.Names.Seed,
                true, WheatSeedPrice, false, 0,
                1, 99, 1);

            ShopItemDefinition wheatProduce = EnsureShopItem(
                "Shop_Wheat", wheat.HarvestItemId, wheat.DisplayName,
                UiIconLibrary.Names.Wheat,
                false, 0, true, WheatSellPrice,
                1, 99, 1);

            shop.EditorConfigure(new[] { wheatSeeds, wheatProduce });
            EditorUtility.SetDirty(shop);
            return shop;
        }

        private static AnimalDefinition EnsureAnimal(string assetName)
        {
            return LoadOrCreate<AnimalDefinition>(ProtoAssets.DataFolder + "/" + assetName + ".asset");
        }

        /// <summary>
        /// Chicken: cheap feed, a fast cycle. Tuned so a full feed-to-collect loop can be seen
        /// inside a short play session without touching any dev multiplier.
        /// </summary>
        private static AnimalDefinition EnsureChickenDefinition(GameObject prefab)
        {
            AnimalDefinition definition = EnsureAnimal("Animal_Chicken");

            definition.EditorConfigureIdentity("chicken", "Chicken", AnimalType.Chicken, prefab, 120);
            definition.EditorConfigureLoop(
                ItemIds.Harvest("wheat"), 1, 45f,
                ItemIds.Egg, 1, 25f);
            definition.EditorConfigureMovement(0.62f, 260f, 2.6f, 0.8f, 0.55f, 1);

            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>Cow: dearer feed, a slower cycle, a bigger yield.</summary>
        private static AnimalDefinition EnsureCowDefinition(GameObject prefab)
        {
            AnimalDefinition definition = EnsureAnimal("Animal_Cow");

            definition.EditorConfigureIdentity("cow", "Cow", AnimalType.Cow, prefab, 450);
            definition.EditorConfigureLoop(
                ItemIds.Harvest("corn"), 2, 75f,
                ItemIds.Milk, 2, 50f);
            definition.EditorConfigureMovement(0.34f, 110f, 5.5f, 0.6f, 1.1f, 1);

            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static CropDefinition EnsureCrop(
            string assetName, string id, string display, Color crop, Color soil, int seedCost, int sellValue)
        {
            string path = ProtoAssets.DataFolder + "/" + assetName + ".asset";
            CropDefinition definition = LoadOrCreate<CropDefinition>(path);

            if (definition == null)
            {
                Debug.LogError("Little Farm Story: could not load or create the crop asset at " + path);
                return null;
            }

            definition.EditorConfigure(id, display, crop, soil, seedCost, sellValue);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>
        /// Generates the growth-stage prefabs for a crop and writes its growth data.
        ///
        /// Every failure path here logs. A previous build silently produced crops with an
        /// empty stagePrefabs array, which meant planted crops rendered nothing at all and
        /// there was no way to tell from the console - so this now reports rather than
        /// returning quietly.
        /// </summary>
        private static void ConfigureCropGrowth(
            CropDefinition definition, ProtoPalette p, float secondsPerStage, int yieldMin, int yieldMax)
        {
            if (definition == null)
            {
                Debug.LogError("Little Farm Story: ConfigureCropGrowth was handed a null CropDefinition. " +
                               "The crop asset failed to load or create, so no crop visuals will exist.");
                return;
            }

            if (string.IsNullOrEmpty(definition.CropId))
            {
                Debug.LogError("Little Farm Story: crop asset '" + definition.name +
                               "' has an empty CropId; cannot generate its stage prefabs.");
                return;
            }

            int stages = definition.GrowthStages;

            GameObject[] visuals = CropVisualBuilder.BuildStagePrefabs(
                definition.CropId,
                p,
                CropVisualBuilder.ShapeForCrop(definition.CropId),
                definition.CropColor,
                stages + 1);

            if (visuals == null || visuals.Length == 0)
            {
                Debug.LogError("Little Farm Story: no stage prefabs were produced for crop '" +
                               definition.CropId + "'. Planted crops will be invisible.");
                return;
            }

            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] == null)
                {
                    Debug.LogError("Little Farm Story: crop '" + definition.CropId +
                                   "' stage " + i + " prefab is null.");
                }
            }

            definition.EditorConfigureGrowth(stages, secondsPerStage, yieldMin, yieldMax, visuals);
            EditorUtility.SetDirty(definition);
        }

        /// <summary>
        /// Post-generation check: confirms each crop actually ended up with visuals.
        /// Cheap insurance against the silent failure described above.
        /// </summary>
        /// <summary>
        /// Fails loudly if the generated scene is not actually playable.
        ///
        /// The regression this exists to prevent was not a logic bug: the builder reported
        /// success while three null guards quietly returned, so the scene looked correctly
        /// wired and played dead. Every gameplay precondition is asserted here, and the final
        /// line says PLAYABLE or NOT PLAYABLE rather than "scene built".
        /// </summary>
        private static bool VerifyBuild(
            GameObject player, GameObject worldRoot, HudController hud,
            CropDefinition wheat, CropDefinition tomato, CropDefinition corn,
            AnimalDefinition chicken, AnimalDefinition cow,
            ShopDefinition shop, EconomySettings economySettings)
        {
            int failures = 0;

            failures += Require(player != null, "the Player object is missing");
            failures += Require(player != null && player.GetComponent<PlayerController>() != null,
                "Player has no PlayerController");
            failures += Require(player != null && player.GetComponent<PlayerInputProvider>() != null,
                "Player has no PlayerInputProvider");
            failures += Require(player != null && player.GetComponent<PlayerInventory>() != null,
                "Player has no PlayerInventory");
            failures += Require(player != null && player.GetComponent<ActionFeedbackChannel>() != null,
                "Player has no ActionFeedbackChannel");
            failures += Require(player != null && player.GetComponent<InteractionController>() != null,
                "Player has no InteractionController");

            // ---------------------------------------------------------------- farming
            CropDefinition[] crops = { wheat, tomato, corn };

            for (int i = 0; i < crops.Length; i++)
            {
                CropDefinition crop = crops[i];

                if (crop == null)
                {
                    failures += Require(false, "a CropDefinition failed to load");
                    continue;
                }

                failures += Require(crop.GetStagePrefab(0) != null,
                    "crop " + crop.CropId + " has no stage-0 prefab, so planted crops would be invisible");
                failures += Require(crop.GetStagePrefab(crop.GrowthStages) != null,
                    "crop " + crop.CropId + " has no mature-stage prefab");
                failures += Require(!string.IsNullOrEmpty(crop.SeedItemId),
                    "crop " + crop.CropId + " has no seed item id");
                failures += Require(crop.SeedItemId != crop.HarvestItemId,
                    "crop " + crop.CropId + " uses the same id for seed and harvest");
            }

            FarmGrid[] grids = worldRoot != null
                ? worldRoot.GetComponentsInChildren<FarmGrid>(true)
                : new FarmGrid[0];

            failures += Require(grids.Length >= 3, "expected three FarmGrid fields, found " + grids.Length);

            for (int i = 0; i < grids.Length; i++)
            {
                failures += Require(grids[i].AssignedCrop != null,
                    "FarmGrid " + grids[i].FieldId + " has no assigned crop");
            }

            GameObject plotAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlotPrefabPath);
            FarmPlot plotPrefabAsset = plotAsset != null ? plotAsset.GetComponent<FarmPlot>() : null;

            failures += Require(plotPrefabAsset != null, "the FarmPlot prefab is missing");
            failures += Require(plotPrefabAsset != null && plotPrefabAsset.GetComponent<Collider>() != null,
                "the FarmPlot prefab has no collider, so the player could never focus a plot");

            // ---------------------------------------------------------------- animals
            AnimalDefinition[] animalDefinitions = { chicken, cow };

            for (int i = 0; i < animalDefinitions.Length; i++)
            {
                AnimalDefinition definition = animalDefinitions[i];

                if (definition == null)
                {
                    failures += Require(false, "an AnimalDefinition failed to load");
                    continue;
                }

                failures += Require(!string.IsNullOrEmpty(definition.FeedItemId),
                    "animal " + definition.AnimalId + " has no feed item id");
                failures += Require(!string.IsNullOrEmpty(definition.ProductItemId),
                    "animal " + definition.AnimalId + " has no product item id");
            }

            AnimalHabitat[] habitats = worldRoot != null
                ? worldRoot.GetComponentsInChildren<AnimalHabitat>(true)
                : new AnimalHabitat[0];

            failures += Require(habitats.Length >= 2, "expected two AnimalHabitats, found " + habitats.Length);

            AnimalController[] animals = worldRoot != null
                ? worldRoot.GetComponentsInChildren<AnimalController>(true)
                : new AnimalController[0];

            failures += Require(animals.Length >= 3, "expected at least three animals, found " + animals.Length);

            for (int i = 0; i < animals.Length; i++)
            {
                AnimalController animal = animals[i];

                failures += Require(animal.Definition != null,
                    "animal " + animal.name + " has no AnimalDefinition");
                failures += Require(animal.Habitat != null,
                    "animal " + animal.name + " has no habitat, so it would never simulate");
                failures += Require(animal.GetComponent<AnimalInteraction>() != null,
                    "animal " + animal.name + " has no AnimalInteraction");
                failures += Require(animal.GetComponent<Collider>() != null,
                    "animal " + animal.name + " has no collider, so the player could never focus it");
            }

            // ------------------------------------------- inventory covers every id the loops need
            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;

            if (inventory != null && wheat != null && chicken != null && cow != null)
            {
                SerializedProperty items = new SerializedObject(inventory).FindProperty("startingItems");

                failures += Require(items != null && items.arraySize > 0, "PlayerInventory has no starting stock");
                failures += Require(ContainsItem(items, wheat.SeedItemId, 1),
                    "the player starts with no " + wheat.SeedItemId + ", so planting could never be tested");
                failures += Require(ContainsItem(items, chicken.FeedItemId, chicken.FeedAmount),
                    "the player starts with no " + chicken.FeedItemId + ", so the chicken could never be fed");
                failures += Require(ContainsItem(items, cow.FeedItemId, cow.FeedAmount),
                    "the player starts with no " + cow.FeedItemId + ", so the cow could never be fed");
            }

            // ---------------------------------------------------------------- economy
            CurrencyWallet wallet = player != null ? player.GetComponent<CurrencyWallet>() : null;
            EconomyManager economy = player != null ? player.GetComponent<EconomyManager>() : null;

            failures += Require(wallet != null, "the Player has no CurrencyWallet, so coins do not exist");
            failures += Require(economy != null, "the Player has no EconomyManager, so nothing can be traded");
            failures += Require(economySettings != null, "the EconomySettings asset is missing");
            failures += Require(economySettings == null || economySettings.StartingCoins > 0,
                "the player would start with no coins and could never make a first purchase");

            // Existence alone is not enough: the manager has to point at THIS player's own
            // wallet and inventory, not merely at some wallet/inventory somewhere. A stray
            // duplicate would let coins or items exist that a trade could never touch.
            failures += Require(economy == null || wallet == null || economy.Wallet == wallet,
                "the EconomyManager's wallet does not match the Player's own CurrencyWallet");
            failures += Require(economy == null || inventory == null || economy.Inventory == inventory,
                "the EconomyManager's inventory does not match the Player's own PlayerInventory");
            failures += Require(economy == null || shop == null || economy.Shop == shop,
                "the EconomyManager is not pointed at the built ShopDefinition");

            if (Require(shop != null, "the ShopDefinition asset is missing") > 0)
            {
                failures++;
            }
            else
            {
                failures += Require(shop.Count > 0, "the shop catalogue is empty");

                bool anyPurchasable = false;
                bool anySellable = false;

                for (int i = 0; i < shop.Items.Count; i++)
                {
                    ShopItemDefinition item = shop.Items[i];

                    if (Require(item != null, "shop catalogue slot " + i + " is empty") > 0)
                    {
                        failures++;
                        continue;
                    }

                    failures += Require(!string.IsNullOrEmpty(item.ItemId),
                        "shop item '" + item.name + "' has no inventory id");

                    // A price of zero on a tradeable line is the failure that would let the
                    // player buy for nothing or sell for nothing, and it is silent otherwise.
                    failures += Require(!item.Purchasable || item.BuyPrice > 0,
                        "shop item '" + item.name + "' is purchasable at a price of " + item.BuyPrice);
                    failures += Require(!item.Sellable || item.SellPrice > 0,
                        "shop item '" + item.name + "' is sellable at a price of " + item.SellPrice);
                    failures += Require(item.MinQuantity <= item.MaxQuantity,
                        "shop item '" + item.name + "' has a minimum above its maximum");

                    anyPurchasable |= item.Purchasable;
                    anySellable |= item.Sellable;
                }

                failures += Require(anyPurchasable, "nothing in the shop can be bought");
                failures += Require(anySellable, "nothing in the shop can be sold");

                // The ids the shop trades must be ids the farming system actually produces,
                // or the player would buy seeds that no plot recognises.
                if (wheat != null)
                {
                    failures += Require(shop.Find(wheat.SeedItemId) != null,
                        "the shop does not stock '" + wheat.SeedItemId + "', so seeds cannot be bought");
                    failures += Require(shop.Find(wheat.HarvestItemId) != null,
                        "the shop does not buy '" + wheat.HarvestItemId + "', so the loop cannot close");
                }
            }

            // Produce pricing is deliberately absent this phase; assert that, so it cannot be
            // half-enabled by accident.
            failures += Require(chicken == null || !chicken.HasProduceSellValue,
                "the chicken has a produce sell value but egg selling is not implemented yet");
            failures += Require(cow == null || !cow.HasProduceSellValue,
                "the cow has a produce sell value but milk selling is not implemented yet");

            // ---------------------------------------------------------------- shop UI
            ShopPanel shopPanel = hud != null ? hud.GetComponentInChildren<ShopPanel>(true) : null;
            failures += Require(shopPanel != null, "the HUD has no ShopPanel, so the shop can never open");

            int buyCardCount = hud != null ? hud.GetComponentsInChildren<ShopBuyCard>(true).Length : 0;
            int sellRowCount = hud != null ? hud.GetComponentsInChildren<ShopSellRow>(true).Length : 0;

            failures += Require(buyCardCount > 0,
                "the shop panel has no buy cards, so nothing can be purchased through the UI");
            failures += Require(sellRowCount > 0,
                "the shop panel has no sell rows, so nothing can be sold through the UI");

            MarketInteractable market = worldRoot != null
                ? worldRoot.GetComponentInChildren<MarketInteractable>(true)
                : null;

            failures += Require(market != null,
                "no MarketInteractable exists in the world, so the shop can never be reached");

            if (market != null)
            {
                SerializedProperty marketShopPanel = new SerializedObject(market).FindProperty("shopPanel");

                failures += Require(marketShopPanel != null && marketShopPanel.objectReferenceValue != null,
                    "the market landmark is not wired to a ShopPanel; interacting with it will do nothing");

                // Not just "wired to a ShopPanel" - wired to the ONE ShopPanel the HUD actually
                // built. Two shop panels existing would mean the market opens the wrong one.
                failures += Require(shopPanel == null || marketShopPanel == null ||
                    ReferenceEquals(marketShopPanel.objectReferenceValue, shopPanel),
                    "the market is wired to a ShopPanel that is not the one the HUD built");
            }

            if (hud != null)
            {
                SerializedProperty hudWallet = new SerializedObject(hud).FindProperty("wallet");
                SerializedProperty hudInventory = new SerializedObject(hud).FindProperty("inventory");

                failures += Require(hudWallet != null && hudWallet.objectReferenceValue != null,
                    "the HUD is not wired to a CurrencyWallet, so the coin display would never update");
                failures += Require(wallet == null || hudWallet == null ||
                    ReferenceEquals(hudWallet.objectReferenceValue, wallet),
                    "the HUD's CurrencyWallet is not the Player's own wallet");

                failures += Require(hudInventory != null && hudInventory.objectReferenceValue != null,
                    "the HUD is not wired to a PlayerInventory, so resource counts would never appear on screen");
                failures += Require(inventory == null || hudInventory == null ||
                    ReferenceEquals(hudInventory.objectReferenceValue, inventory),
                    "the HUD's PlayerInventory is not the Player's own inventory");
            }

            // ---------------------------------------------------------------- persistence
            SaveManager saveManager = Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);

            if (Require(saveManager != null, "the scene has no SaveManager, so no progress would persist") > 0)
            {
                failures++;
            }
            else
            {
                SerializedObject saveObject = new SerializedObject(saveManager);
                SerializedProperty saveWallet = saveObject.FindProperty("wallet");
                SerializedProperty saveInventory = saveObject.FindProperty("inventory");
                SerializedProperty savePlayer = saveObject.FindProperty("playerController");
                SerializedProperty saveFields = saveObject.FindProperty("fields");
                SerializedProperty saveHabitats = saveObject.FindProperty("habitats");

                // Same reasoning as the economy checks above: pointing at *a* wallet is not
                // enough. Saving a duplicate would silently persist coins nobody can spend.
                failures += Require(wallet == null || saveWallet == null ||
                    ReferenceEquals(saveWallet.objectReferenceValue, wallet),
                    "the SaveManager's CurrencyWallet is not the Player's own wallet");
                failures += Require(inventory == null || saveInventory == null ||
                    ReferenceEquals(saveInventory.objectReferenceValue, inventory),
                    "the SaveManager's PlayerInventory is not the Player's own inventory");
                failures += Require(savePlayer != null && savePlayer.objectReferenceValue != null,
                    "the SaveManager has no PlayerController, so the farmer's position would not persist");

                failures += Require(saveFields != null && saveFields.arraySize >= grids.Length,
                    "the SaveManager knows about " +
                    (saveFields != null ? saveFields.arraySize : 0) + " fields but the farm has " +
                    grids.Length + "; crops in the missing fields would not persist");

                AnimalHabitat[] builtHabitats = worldRoot != null
                    ? worldRoot.GetComponentsInChildren<AnimalHabitat>(true)
                    : new AnimalHabitat[0];

                failures += Require(saveHabitats != null && saveHabitats.arraySize >= builtHabitats.Length,
                    "the SaveManager knows about " +
                    (saveHabitats != null ? saveHabitats.arraySize : 0) + " habitats but the farm has " +
                    builtHabitats.Length + "; animals in the missing pens would not persist");
            }

            // ---------------------------------------------------------------- HUD
            if (hud != null)
            {
                SerializedProperty chips = new SerializedObject(hud).FindProperty("chips");

                failures += Require(chips != null && chips.arraySize > 0,
                    "the HUD has no resource chips, so item quantities would never appear on screen");

                failures += Require(hud.GetComponentInChildren<LittleFarmStory.UI.ActionPrompt>(true) != null,
                    "the HUD has no ActionPrompt, so the player would never be told what USE does");

                failures += Require(hud.GetComponentInChildren<LittleFarmStory.UI.ToastPresenter>(true) != null,
                    "the HUD has no ToastPresenter, so action feedback would never appear");

                failures += Require(hud.GetComponentInChildren<LittleFarmStory.UI.SafeAreaPanel>(true) != null,
                    "the HUD has no SafeAreaPanel, so it would sit under a notch on many phones");

                failures += Require(TextMeshProSetup.IsImported,
                    "TextMeshPro Essential Resources are missing, so every HUD label would render blank");

                // A TMP_Text with no font asset draws nothing at all, which looks like a broken
                // build rather than a missing import - so it is checked explicitly.
                TMPro.TMP_Text[] labels = hud.GetComponentsInChildren<TMPro.TMP_Text>(true);
                failures += Require(labels.Length > 0, "the HUD has no text at all");

                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i].font == null)
                    {
                        failures += Require(false,
                            "HUD label '" + labels[i].name + "' has no font asset and would render blank");
                        break;
                    }
                }
            }

            if (failures == 0)
            {
                Debug.Log("Little Farm Story: build verification PASSED - the farm is PLAYABLE. " +
                          grids.Length + " fields, " + habitats.Length + " habitats, " +
                          animals.Length + " animals.");
                return true;
            }

            Debug.LogError("Little Farm Story: build verification FAILED with " + failures +
                           " problem(s). The generated farm is NOT PLAYABLE - see the errors above.");
            return false;
        }

        /// <summary>Logs and counts one verification failure. Returns 1 when the condition failed.</summary>
        private static int Require(bool condition, string failureMessage)
        {
            if (condition)
            {
                return 0;
            }

            Debug.LogError("Little Farm Story: VERIFY - " + failureMessage + ".");
            return 1;
        }

        private static bool ContainsItem(SerializedProperty startingItems, string itemId, int minimumAmount)
        {
            if (startingItems == null || string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < startingItems.arraySize; i++)
            {
                SerializedProperty element = startingItems.GetArrayElementAtIndex(i);
                SerializedProperty id = element.FindPropertyRelative("ItemId");
                SerializedProperty amount = element.FindPropertyRelative("Amount");

                if (id != null && id.stringValue == itemId && amount != null && amount.intValue >= minimumAmount)
                {
                    return true;
                }
            }

            return false;
        }

        private static void VerifyCropVisuals(params CropDefinition[] crops)
        {
            for (int i = 0; i < crops.Length; i++)
            {
                CropDefinition crop = crops[i];
                if (crop == null)
                {
                    // Never skipped silently again: a null crop here is precisely the failure
                    // that left every planted crop invisible.
                    Debug.LogError("Little Farm Story: a CropDefinition reference was null during " +
                                   "verification. Crop visuals and the HUD counters will be missing.");
                    continue;
                }

                if (crop.GetStagePrefab(0) == null || crop.GetStagePrefab(crop.GrowthStages) == null)
                {
                    Debug.LogError("Little Farm Story: crop '" + crop.CropId +
                                   "' has no stage prefabs assigned after generation. " +
                                   "Planted crops of this type will render nothing.");
                }
            }
        }

        private static FarmingSettings EnsureFarmingSettings()
        {
            string path = ProtoAssets.DataFolder + "/FarmingSettings.asset";
            bool existed = System.IO.File.Exists(path);

            FarmingSettings settings = LoadOrCreate<FarmingSettings>(path);

            if (settings == null)
            {
                Debug.LogError("Little Farm Story: could not load or create " + path);
                return null;
            }

            // The builder is the source of truth for the development farm, so it always
            // re-applies the development pacing rather than preserving a stale value. The flag
            // is editor-only, so a device build still runs at the authored speed either way.
            settings.EditorConfigure(DevelopmentGrowthMultiplier, true, 0.2f);
            EditorUtility.SetDirty(settings);

            if (!existed)
            {
                Debug.Log("Little Farm Story: created " + path + ".");
            }

            return settings;
        }

        /// <summary>
        /// The plot prefab. The soil is a single furrowed slab mesh, so FarmPlot can keep
        /// doing nothing but tint one renderer while the plot still reads as ploughed earth.
        /// </summary>
        private static FarmPlot BuildPlotPrefab(ProtoPalette p, int layer)
        {
            GameObject temp = new GameObject("FarmPlot");
            if (layer >= 0)
            {
                temp.layer = layer;
            }

            BoxCollider trigger = temp.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.95f, 1.1f, 1.95f);
            trigger.center = new Vector3(0f, 0.55f, 0f);

            GameObject soil = ProtoAssets.MeshObject(
                StylizedMeshLibrary.FurrowedSlab(4), "Soil", temp.transform,
                new Vector3(0f, 0.09f, 0f), new Vector3(1.92f, 0.3f, 1.92f), p.Soil, false);

            if (layer >= 0)
            {
                soil.layer = layer;
            }

            GameObject anchor = ProtoAssets.Empty("CropAnchor", temp.transform, new Vector3(0f, 0.24f, 0f));

            AttachReadyMarker(temp.transform, UiIconLibrary.Names.Harvest, 1.35f);

            FarmPlot plot = temp.AddComponent<FarmPlot>();
            plot.SetLabel("Soil Plot");
            plot.SetPriority(InteractableBase.Priority.Plot);
            Wire(plot, "soilRenderer", soil.GetComponent<MeshRenderer>());
            Wire(plot, "cropAnchor", anchor.transform);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, PlotPrefabPath, out bool success);
            Object.DestroyImmediate(temp);

            if (!success || asset == null)
            {
                Debug.LogError("Little Farm Story: failed to save the FarmPlot prefab.");
                return null;
            }

            return asset.GetComponent<FarmPlot>();
        }

        // ================================================================ lighting

        /// <summary>
        /// Sunny mid-morning: one warm key light, a cool sky fill so shadows stay blue rather
        /// than muddy grey, and gentle distance fog that hides the world edge and adds depth.
        /// No post-processing, no reflections, no realtime GI.
        /// </summary>
        private static void ConfigureLighting(ProtoPalette p)
        {
            GameObject lightGo = new GameObject("Sun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = ProtoPalette.Hex("FFF1D2");
            sun.intensity = 1.38f;
            sun.shadows = LightShadows.Soft;

            // Deliberately weak: strong shadows fight the cheerful, low-contrast look.
            sun.shadowStrength = 0.46f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.2f;

            // Lower and further round than a face-on key, so roof planes and wall faces each
            // catch a different value and buildings separate from the ground by shading alone.
            lightGo.transform.rotation = Quaternion.Euler(40f, -52f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Cool sky fill keeps shadowed faces blue rather than muddy grey, which is what
            // makes shadows read as pleasant instead of dirty.
            RenderSettings.ambientSkyColor = ProtoPalette.Hex("AFD2EE");
            RenderSettings.ambientEquatorColor = ProtoPalette.Hex("C9D6B6");
            RenderSettings.ambientGroundColor = ProtoPalette.Hex("6B6450");
            RenderSettings.ambientIntensity = 1f;

            // Fog starts beyond the farm so nothing the player interacts with is washed out;
            // it only softens the new background hills into a horizon.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = ProtoPalette.Hex("D3E6EA");
            RenderSettings.fogStartDistance = 74f;
            RenderSettings.fogEndDistance = 148f;
        }

        private static void BuildEventSystem(Transform parent)
        {
            GameObject go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();

            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static void RegisterSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================ world

        private static void BuildWorld(
            Transform root, ProtoPalette p, FarmPlot plotPrefab,
            CropDefinition wheat, CropDefinition tomato, CropDefinition corn,
            int layer, FarmingSettings farmingSettings, PropLibrary.Props props,
            PropLibraryExtra.Extras extras, GameObject chickenPrefabA, GameObject chickenPrefabB,
            GameObject cowPrefab, AnimalDefinition chickenDefinition, AnimalDefinition cowDefinition)
        {
            Transform environment = ProtoAssets.Empty("Environment", root, Vector3.zero).transform;
            FarmEnvironmentBuilder.BuildGround(environment, p);

            // Relief lives beyond the fence only, so the walkable ground stays one flat collider.
            TerrainDressing.BuildBackgroundHills(environment, p);

            FarmEnvironmentBuilder.BuildPaths(environment, p);
            FarmEnvironmentBuilder.BuildBoundary(environment, p);

            Transform fields = ProtoAssets.Empty("Fields", root, Vector3.zero).transform;
            BuildField(fields, p, plotPrefab, layer, farmingSettings, props, extras, "Wheat", wheat,
                WheatCentre, new Vector2(13f, 11f), new Vector2Int(5, 4), p.Wheat);
            BuildField(fields, p, plotPrefab, layer, farmingSettings, props, extras, "Tomato", tomato,
                TomatoCentre, new Vector2(11f, 9f), new Vector2Int(4, 3), p.Tomato);
            BuildField(fields, p, plotPrefab, layer, farmingSettings, props, extras, "Corn", corn,
                CornCentre, new Vector2(13f, 11f), new Vector2Int(5, 3), p.Corn);

            Transform areas = ProtoAssets.Empty("Areas", root, Vector3.zero).transform;
            BuildProductionArea(areas, p, layer, props, extras, ProductionCentre);
            BuildChickenArea(areas, p, layer, props, extras, chickenPrefabA, chickenPrefabB,
                chickenDefinition, ChickenCentre);
            BuildCowArea(areas, p, layer, props, extras, cowPrefab, cowDefinition, CowCentre);
            BuildMarketArea(areas, p, layer, props, extras, MarketCentre);
            BuildHomeArea(areas, p, layer, props, extras, HomeCentre);

            Transform decor = ProtoAssets.Empty("Decoration", root, Vector3.zero).transform;
            ZoneDressing.BuildTreeline(decor, props);
            ZoneDressing.BuildGroundcover(decor, props);
            ZoneDressing.DressPlaza(decor, p, props, extras);
            FarmEnvironmentBuilder.Pond(decor, p, PondCentre, 3.1f, props);
        }

        private static void BuildField(
            Transform parent, ProtoPalette p, FarmPlot plotPrefab, int layer,
            FarmingSettings farmingSettings, PropLibrary.Props props, PropLibraryExtra.Extras extras,
            string label, CropDefinition crop,
            Vector3 centre, Vector2 padSize, Vector2Int gridSize, Material accent)
        {
            Transform field = ProtoAssets.Empty("Field_" + label, parent, Vector3.zero).transform;
            FarmEnvironmentBuilder.BuildFieldPad(field, p, "Pad_" + label, centre, padSize);

            GameObject gridGo = ProtoAssets.Empty("Grid_" + label, field, centre + new Vector3(0f, 0.16f, 0f));
            FarmGrid grid = gridGo.AddComponent<FarmGrid>();
            grid.EditorConfigure("field_" + crop.CropId, crop, gridSize, 2.2f, plotPrefab);
            grid.EditorSetFarmingSettings(farmingSettings);

            // Sign faces the path, painted in the crop colour, so the field is identifiable
            // from across the farm without reading any UI.
            float hx = padSize.x * 0.5f;
            bool eastSide = centre.x > 0f;
            float signX = eastSide ? centre.x - hx - 2.1f : centre.x + hx + 2.1f;
            Vector3 signPos = new Vector3(signX, 0f, centre.z);

            FarmEnvironmentBuilder.Signpost(field, p, "Sign_" + label, signPos, accent, eastSide ? 90f : -90f);

            ZoneDressing.DressField(field, props, extras, centre, padSize, eastSide);

            FarmEnvironmentBuilder.Landmark(field, "Interact_" + label, "field_" + crop.CropId,
                crop.DisplayName + " Field", FarmLandmark.LandmarkKind.Field,
                signPos, new Vector3(3.2f, 2.2f, 3.2f), layer);
        }

        private static void BuildProductionArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props,
            PropLibraryExtra.Extras extras, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Production", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);
            Mesh trim = StylizedMeshLibrary.ChamferBox(0.2f);

            ProtoAssets.MeshObject(box, "Yard", area, new Vector3(0f, 0.05f, 0f),
                new Vector3(11.5f, 0.1f, 9.5f), p.Concrete, false);

            BuildingBuilder.ProductionShelter(area, p, new Vector3(0f, 0f, 1.4f));

            // Three empty machine pads: the footprint the production phase will fill.
            for (int i = 0; i < 3; i++)
            {
                float x = -3.2f + i * 3.2f;

                ProtoAssets.MeshObject(trim, "MachinePad_" + i, area, new Vector3(x, 0.16f, 1.4f),
                    new Vector3(2.6f, 0.16f, 2.6f), p.Stone, false);
                ProtoAssets.MeshObject(trim, "PadStripe_" + i, area, new Vector3(x, 0.25f, 1.4f),
                    new Vector3(2.1f, 0.04f, 2.1f), p.RoofMustard, false);

                // Anchor bolts, so a bare pad still reads as prepared for machinery.
                for (int b = 0; b < 4; b++)
                {
                    float bx = x + ((b % 2 == 0) ? -0.85f : 0.85f);
                    float bz = 1.4f + ((b < 2) ? -0.85f : 0.85f);
                    ProtoAssets.MeshObject(trim, "Bolt_" + i + "_" + b, area,
                        new Vector3(bx, 0.29f, bz), new Vector3(0.16f, 0.08f, 0.16f), p.Metal, false);
                }
            }

            BuildingBuilder.Silo(area, p, new Vector3(4.4f, 0f, -3.2f), 7.2f);
            ZoneDressing.DressProductionYard(area, props, extras);

            FarmEnvironmentBuilder.Landmark(area, "Interact_Production", "area_production",
                "Production Yard", FarmLandmark.LandmarkKind.Production,
                new Vector3(0f, 0f, -2.6f), new Vector3(5f, 2.4f, 3f), layer);
        }

        /// <summary>
        /// Turns a placed visual prefab instance into a gameplay animal: the definition, the
        /// habitat it belongs to and a stable save id. The controller and the interaction
        /// component already come from the prefab; only the per-instance references are set here.
        /// </summary>
        private static void WireAnimal(
            GameObject instance, AnimalDefinition definition, AnimalHabitat habitat,
            string instanceId, int layer)
        {
            if (instance == null)
            {
                Debug.LogError("Little Farm Story: an animal prefab failed to instantiate; '" +
                               instanceId + "' will be missing from the farm.");
                return;
            }

            AnimalController controller = instance.GetComponent<AnimalController>();
            if (controller == null)
            {
                Debug.LogError("Little Farm Story: '" + instance.name +
                               "' has no AnimalController; the animal prefab is out of date.");
                return;
            }

            controller.EditorConfigure(
                definition, habitat, instance.GetComponent<AnimalIdleAnimator>(), instanceId);

            if (layer >= 0)
            {
                // Only the root carries the trigger, so only the root needs the layer.
                instance.layer = layer;
            }

            instance.name = definition != null ? definition.DisplayName + "_" + instanceId : instance.name;
        }

        private static void BuildChickenArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props,
            PropLibraryExtra.Extras extras, GameObject chickenPrefabA, GameObject chickenPrefabB,
            AnimalDefinition definition, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Chicken", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);

            ProtoAssets.MeshObject(box, "PenGround", area, new Vector3(0f, 0.045f, 0f),
                new Vector3(12.5f, 0.09f, 9.5f), p.PathEdge, false);

            // Bare scratched earth where the birds work, with an irregular outline.
            ProtoAssets.MeshObject(StylizedMeshLibrary.IrregularDisc(710, 14, 0.3f),
                "ScratchPatch", area, new Vector3(-1.6f, 0.1f, -1.2f),
                new Vector3(5.6f, 1f, 4.4f), p.Soil, false);

            FarmEnvironmentBuilder.FenceRect(area, p, "PenFence", Vector3.zero,
                new Vector2(12f, 9f), 2.4f, 1.15f, 3.2f);

            BuildingBuilder.Coop(area, p, new Vector3(3.4f, 0f, 2.2f));
            ZoneDressing.DressChickenRun(area, props, extras);

            // The run is the habitat: it owns capacity, the walkable rectangle, and the single
            // Update that ticks every bird inside it. The coop footprint is fenced off so the
            // flock walks around the building instead of through it.
            AnimalHabitat habitat = area.gameObject.AddComponent<AnimalHabitat>();
            habitat.EditorConfigure(
                "habitat_coop", AnimalType.Chicken, 8,
                Vector2.zero, new Vector2(5.2f, 3.7f),
                new[]
                {
                    new AnimalHabitat.ExclusionZone
                    {
                        Centre = new Vector2(3.4f, 2.2f),
                        HalfExtents = new Vector2(2.4f, 2.1f)
                    }
                });
            habitat.EditorConfigureDevelopmentSpeed(DevelopmentAnimalMultiplier, true, AnimalDiagnostics);

            Vector3[] spots =
            {
                new Vector3(-1.3f, 0.09f, 1.7f), new Vector3(0.7f, 0.09f, -2.4f),
                new Vector3(-3.3f, 0.09f, -2.7f), new Vector3(0.4f, 0.09f, 2.6f),
                new Vector3(-0.4f, 0.09f, 3.0f), new Vector3(2.6f, 0.09f, -1.4f)
            };
            float[] yaws = { 35f, 150f, 250f, 300f, 80f, 200f };

            for (int i = 0; i < spots.Length; i++)
            {
                GameObject variant = (i % 3 == 1) ? chickenPrefabB : chickenPrefabA;
                GameObject bird = FarmEnvironmentBuilder.PlaceProp(
                    variant, area, spots[i], 0.94f + (i % 3) * 0.05f, yaws[i]);

                WireAnimal(bird, definition, habitat, "chicken_" + i, layer);
            }

            FarmEnvironmentBuilder.Landmark(area, "Interact_Chicken", "area_chicken",
                "Chicken Coop", FarmLandmark.LandmarkKind.AnimalPen,
                new Vector3(3.4f, 0f, -0.4f), new Vector3(4.5f, 2.2f, 3f), layer);
        }

        private static void BuildCowArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props,
            PropLibraryExtra.Extras extras, GameObject cowPrefab,
            AnimalDefinition definition, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Cow", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);

            ProtoAssets.MeshObject(box, "PenGround", area, new Vector3(0f, 0.045f, 0f),
                new Vector3(15.5f, 0.09f, 11.5f), p.GrassDeep, false);

            // Worn tracks where the herd walks to the trough.
            ProtoAssets.MeshObject(StylizedMeshLibrary.IrregularDisc(720, 14, 0.28f),
                "WornPatch", area, new Vector3(-1.2f, 0.1f, -3.0f),
                new Vector3(6.4f, 1f, 3.4f), p.PathEdge, false);

            FarmEnvironmentBuilder.FenceRect(area, p, "PenFence", Vector3.zero,
                new Vector2(15f, 11f), 2.6f, 1.4f, 3.4f);

            BuildingBuilder.Barn(area, p, new Vector3(3.4f, 0f, 2.2f));
            ZoneDressing.DressCowPasture(area, props, extras);

            AnimalHabitat habitat = area.gameObject.AddComponent<AnimalHabitat>();
            habitat.EditorConfigure(
                "habitat_barn", AnimalType.Cow, 4,
                Vector2.zero, new Vector2(6.4f, 4.5f),
                new[]
                {
                    new AnimalHabitat.ExclusionZone
                    {
                        Centre = new Vector2(3.4f, 2.2f),
                        HalfExtents = new Vector2(4.4f, 3.6f)
                    }
                });
            habitat.EditorConfigureDevelopmentSpeed(DevelopmentAnimalMultiplier, true, AnimalDiagnostics);

            Vector3[] cowSpots =
            {
                new Vector3(-2.0f, 0.09f, -0.6f),
                new Vector3(1.6f, 0.09f, -2.4f),
                new Vector3(-2.6f, 0.09f, 2.6f)
            };
            float[] cowYaws = { 55f, 205f, 320f };
            float[] cowScales = { 1f, 1f, 0.92f };

            for (int i = 0; i < cowSpots.Length; i++)
            {
                GameObject cow = FarmEnvironmentBuilder.PlaceProp(
                    cowPrefab, area, cowSpots[i], cowScales[i], cowYaws[i]);

                WireAnimal(cow, definition, habitat, "cow_" + i, layer);
            }

            FarmEnvironmentBuilder.Landmark(area, "Interact_Cow", "area_cow",
                "Cow Barn", FarmLandmark.LandmarkKind.AnimalPen,
                new Vector3(3.4f, 0f, -1.2f), new Vector3(5f, 2.4f, 3.2f), layer);
        }

        private static void BuildMarketArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props,
            PropLibraryExtra.Extras extras, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Market", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);

            ProtoAssets.MeshObject(box, "Plaza", area, new Vector3(0f, 0.04f, 0f),
                new Vector3(12.5f, 0.08f, 11.5f), p.Path, false);
            ProtoAssets.MeshObject(StylizedMeshLibrary.IrregularDisc(730, 18, 0.14f),
                "PlazaInlay", area, new Vector3(0f, 0.09f, 0.6f),
                new Vector3(8.8f, 1f, 8.8f), p.PathEdge, false);

            BuildingBuilder.MarketStall(area, p, new Vector3(0f, 0f, 1.4f));
            ZoneDressing.DressMarket(area, props, extras);

            BuildMarketLandmark(area, "Interact_Market", "area_market", "Shop",
                new Vector3(0f, 0f, -3.1f), new Vector3(8f, 2.4f, 3f), layer);
        }

        /// <summary>
        /// The market's physical entrance - same trigger, same position every other landmark
        /// would get from <see cref="FarmEnvironmentBuilder.Landmark"/> - wired to a
        /// <see cref="MarketInteractable"/> instead of the generic <see cref="FarmLandmark"/>,
        /// since the market is the one landmark that now does something more than log a
        /// placeholder message. Every other landmark (Home, Production, Field) is untouched.
        /// </summary>
        private static MarketInteractable BuildMarketLandmark(
            Transform parent, string objectName, string id, string label,
            Vector3 position, Vector3 triggerSize, int layer)
        {
            GameObject go = ProtoAssets.Empty(objectName, parent, position);
            if (layer >= 0)
            {
                go.layer = layer;
            }

            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;
            trigger.center = new Vector3(0f, triggerSize.y * 0.5f, 0f);

            MarketInteractable market = go.AddComponent<MarketInteractable>();
            market.EditorConfigure(id, label);
            return market;
        }

        private static void BuildHomeArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props,
            PropLibraryExtra.Extras extras, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Home", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);

            ProtoAssets.MeshObject(box, "Yard", area, new Vector3(0f, 0.03f, 0f),
                new Vector3(12.5f, 0.06f, 11.5f), p.GrassLight, false);

            ProtoAssets.MeshObject(StylizedMeshLibrary.IrregularDisc(740, 14, 0.2f),
                "FrontPath", area, new Vector3(1.9f, 0.07f, -5.0f),
                new Vector3(5.0f, 1f, 4.6f), p.Path, false);

            BuildingBuilder.Farmhouse(area, p, new Vector3(0f, 0f, 2.2f));
            ZoneDressing.DressHome(area, props, extras);

            FarmEnvironmentBuilder.Landmark(area, "Interact_Home", "area_home",
                "Farmhouse", FarmLandmark.LandmarkKind.Home,
                new Vector3(1.9f, 0f, -3.4f), new Vector3(5f, 2.4f, 2.6f), layer);
        }

        // ================================================================ actors

        private static GameObject BuildPlayer(
            Transform parent, ProtoPalette p, GameObject farmerPrefab,
            EconomySettings economySettings, ShopDefinition shopDefinition)
        {
            GameObject player = ProtoAssets.Empty("Player", parent, new Vector3(0f, 0.2f, -6f));

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.7f;
            controller.center = new Vector3(0f, 0.87f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.03f;

            // Three writers, three disjoint transform sets, so nothing ever fights:
            //   PlayerController    -> Visual.rotation        (facing)
            //   PlayerVisualBob     -> BobRoot local TRS      (body bob)
            //   FarmerLimbAnimator  -> the rig's joint rotations only
            Transform visual = ProtoAssets.Empty("Visual", player.transform, Vector3.zero).transform;
            Transform bobRoot = ProtoAssets.Empty("BobRoot", visual, Vector3.zero).transform;

            if (farmerPrefab != null)
            {
                GameObject farmer = (GameObject)PrefabUtility.InstantiatePrefab(farmerPrefab, bobRoot);
                farmer.transform.localPosition = Vector3.zero;
                farmer.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogError("Little Farm Story: the Farmer prefab is missing; the player will be invisible.");
            }

            // Soft contact shadow: cheaper and more readable than relying on the sun alone.
            ProtoAssets.MeshObject(StylizedMeshLibrary.Disc(14), "ContactShadow", visual,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 1f, 0.8f), p.GrassDeep, false);

            player.AddComponent<PlayerInputProvider>();
            player.AddComponent<KeyboardMoveInputSource>();
            PlayerController movement = player.AddComponent<PlayerController>();
            player.AddComponent<InteractionController>();
            PlayerInventory inventory = player.AddComponent<PlayerInventory>();
            ActionFeedbackChannel feedbackChannel = player.AddComponent<ActionFeedbackChannel>();

            // Coins are per-player runtime state, so the wallet lives here beside the inventory
            // rather than in an asset. The manager owns no state of its own; it just runs
            // transactions against these two.
            CurrencyWallet wallet = player.AddComponent<CurrencyWallet>();
            wallet.EditorConfigure(economySettings != null ? economySettings.StartingCoins : 100);

            EconomyManager economy = player.AddComponent<EconomyManager>();
            economy.EditorConfigure(shopDefinition, economySettings, wallet, inventory, feedbackChannel);

            // Starting stock. Seeds drive the farming loop; the wheat and corn exist so the
            // animal feeding loop is testable from a cold start, before the first harvest.
            WireStartingItems(inventory, new[]
            {
                new KeyValuePair<string, int>(ItemIds.Seed("wheat"), 10),
                new KeyValuePair<string, int>(ItemIds.Harvest("wheat"), 6),
                new KeyValuePair<string, int>(ItemIds.Harvest("corn"), 4),
                new KeyValuePair<string, int>(ItemIds.Egg, 0),
                new KeyValuePair<string, int>(ItemIds.Milk, 0)
            });

            PlayerVisualBob bob = bobRoot.gameObject.AddComponent<PlayerVisualBob>();
            Wire(bob, "player", movement);

            return player;
        }

        private static FarmCameraController BuildCamera(Transform parent, Transform target)
        {
            GameObject go = ProtoAssets.Empty("GameplayCamera", parent, new Vector3(0f, 20f, -22f));
            go.tag = "MainCamera";

            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ProtoPalette.Hex("D3E6EA");

            // Narrow field of view plus a long distance gives the flatter, near-isometric
            // read of a polished mobile farm game, and keeps a whole field on a portrait screen.
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 190f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = true;

            go.AddComponent<AudioListener>();

            FarmCameraController controller = go.AddComponent<FarmCameraController>();
            Wire(controller, "target", target);
            WireRect(controller, "focusBounds", new Rect(-26f, -26f, 52f, 52f));

            return controller;
        }

        /// <summary>
        /// Hangs a small world-space badge over a plot or an animal. It self-wires to whichever
        /// gameplay component sits above it, so this only has to build the geometry.
        /// </summary>
        private static void AttachReadyMarker(Transform parent, string iconName, float height)
        {
            Sprite icon = UiIconLibrary.Get(iconName);

            if (icon == null)
            {
                Debug.LogError("Little Farm Story: ready-marker icon '" + iconName + "' is missing.");
                return;
            }

            GameObject root = ProtoAssets.Empty("ReadyMarker", parent, new Vector3(0f, height, 0f));

            GameObject badgeGo = new GameObject("Badge");
            badgeGo.transform.SetParent(root.transform, false);
            badgeGo.transform.localScale = Vector3.one * 0.75f;

            SpriteRenderer badge = badgeGo.AddComponent<SpriteRenderer>();
            badge.sprite = icon;
            badge.sortingOrder = 100;

            ReadyMarker marker = root.AddComponent<ReadyMarker>();
            marker.EditorConfigure(badge, 0.12f, 0.85f);
        }

        // ================================================================ HUD data

        /// <summary>
        /// What the HUD is allowed to display, in display order. Ids come from the crop and
        /// animal assets rather than from string literals, so renaming one cannot silently
        /// desync the readout from the inventory.
        ///
        /// Only the resources the player spends constantly are pinned to the permanent HUD;
        /// the rest are one tap away in the storage sheet. A farming game that shows every
        /// counter at all times stops being a game about a farm and becomes a spreadsheet.
        /// </summary>
        private static HudBuilder.ResourceEntry[] BuildResourceTable(
            CropDefinition wheat, CropDefinition corn,
            AnimalDefinition chicken, AnimalDefinition cow)
        {
            List<HudBuilder.ResourceEntry> entries = new List<HudBuilder.ResourceEntry>();

            if (wheat != null)
            {
                entries.Add(new HudBuilder.ResourceEntry
                {
                    ItemId = wheat.SeedItemId,
                    DisplayName = wheat.DisplayName + " Seeds",
                    IconName = UiIconLibrary.Names.Seed,
                    Pinned = true
                });

                entries.Add(new HudBuilder.ResourceEntry
                {
                    ItemId = wheat.HarvestItemId,
                    DisplayName = wheat.DisplayName,
                    IconName = UiIconLibrary.Names.Wheat,
                    Pinned = true
                });
            }

            if (corn != null)
            {
                entries.Add(new HudBuilder.ResourceEntry
                {
                    ItemId = corn.HarvestItemId,
                    DisplayName = corn.DisplayName,
                    IconName = UiIconLibrary.Names.Corn,
                    Pinned = false
                });
            }

            if (chicken != null)
            {
                entries.Add(new HudBuilder.ResourceEntry
                {
                    ItemId = chicken.ProductItemId,
                    DisplayName = "Eggs",
                    IconName = UiIconLibrary.Names.Egg,
                    Pinned = false
                });
            }

            if (cow != null)
            {
                entries.Add(new HudBuilder.ResourceEntry
                {
                    ItemId = cow.ProductItemId,
                    DisplayName = "Milk",
                    IconName = UiIconLibrary.Names.Milk,
                    Pinned = false
                });
            }

            return entries.ToArray();
        }

        // ================================================================ wiring

        private static void WireEverything(
            GameObject player, FarmCameraController cameraController, HudBuilder.Result hud,
            int layer, CropDefinition primaryCrop)
        {
            PlayerInputProvider provider = player.GetComponent<PlayerInputProvider>();
            KeyboardMoveInputSource keyboard = player.GetComponent<KeyboardMoveInputSource>();
            PlayerController movement = player.GetComponent<PlayerController>();
            InteractionController interaction = player.GetComponent<InteractionController>();
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            ActionFeedbackChannel feedbackChannel = player.GetComponent<ActionFeedbackChannel>();

            WireList(provider, "sourceBehaviours", hud.Joystick, hud.ActionButton, keyboard);

            Wire(movement, "input", provider);
            Wire(movement, "inputSpace", cameraController.transform);
            Wire(movement, "visualRoot", player.transform.Find("Visual"));

            Wire(interaction, "input", provider);
            WireInt(interaction, "interactableLayers", layer >= 0 ? 1 << layer : ~0);
            WireBool(interaction, "logInteractions", InteractionDiagnostics);

            // Off until the automated gameplay test passes with it on. A missed interaction is
            // a far worse bug than being able to reach through a wall.
            WireBool(interaction, "requireLineOfSight", false);

            // The HUD's own widgets were wired when it was built - including the coin display,
            // which HudBuilder binds straight to the player's CurrencyWallet since that wallet
            // already exists by the time HudBuilder.Build runs. Only the gameplay sources the
            // HUD observes but did not create are connected here. It reads them and never
            // writes to them.
            Wire(hud.Hud, "interaction", interaction);
            Wire(hud.Hud, "inventory", inventory);
            Wire(hud.Hud, "feedback", feedbackChannel);

            cameraController.SnapToTarget();
        }

        /// <summary>
        /// Authors PlayerInventory's starting stock without touching the runtime class.
        /// The array is a serialized default, so the scene is the right place to set it.
        /// </summary>
        private static void WireStartingItems(
            PlayerInventory inventory, KeyValuePair<string, int>[] items)
        {
            SerializedObject so = new SerializedObject(inventory);
            SerializedProperty list = so.FindProperty("startingItems");

            if (list == null)
            {
                Debug.LogError("Little Farm Story: PlayerInventory has no 'startingItems' property.");
                return;
            }

            list.arraySize = items.Length;

            for (int i = 0; i < items.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("ItemId").stringValue = items[i].Key;
                element.FindPropertyRelative("Amount").intValue = items[i].Value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Wire(Object target, string propertyPath, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyPath);

            if (property == null)
            {
                Debug.LogError("Little Farm Story: could not find serialized property '" + propertyPath + "' on " + target.GetType().Name);
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBool(Object target, string propertyPath, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyPath);

            if (property == null)
            {
                Debug.LogError("Little Farm Story: could not find serialized property '" + propertyPath +
                               "' on " + target.GetType().Name);
                return;
            }

            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireInt(Object target, string propertyPath, int value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyPath);

            if (property == null)
            {
                Debug.LogError("Little Farm Story: could not find serialized property '" + propertyPath + "' on " + target.GetType().Name);
                return;
            }

            property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireRect(Object target, string propertyPath, Rect value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyPath);

            if (property == null)
            {
                Debug.LogError("Little Farm Story: could not find serialized property '" + propertyPath + "' on " + target.GetType().Name);
                return;
            }

            property.rectValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireList(Object target, string propertyPath, params Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyPath);

            if (property == null || !property.isArray)
            {
                Debug.LogError("Little Farm Story: could not find array property '" + propertyPath + "' on " + target.GetType().Name);
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
