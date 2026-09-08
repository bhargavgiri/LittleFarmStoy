using System.Collections.Generic;
using LittleFarmStory.CameraSystem;
using LittleFarmStory.Core;
using LittleFarmStory.Farming;
using LittleFarmStory.Input;
using LittleFarmStory.Interaction;
using LittleFarmStory.Inventory;
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
        private static readonly Vector3 PondCentre = new Vector3(-26f, 0f, 27f);

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

            int interactableLayer = ProtoAssets.EnsureLayer(InteractableLayerName);
            ProtoPalette p = ProtoPalette.Create();

            CropDefinition wheat = EnsureCrop("Crop_Wheat", "wheat", "Wheat",
                ProtoPalette.WheatAccent, ProtoPalette.Hex("6B4A2F"), 5, 12);
            CropDefinition tomato = EnsureCrop("Crop_Tomato", "tomato", "Tomato",
                ProtoPalette.TomatoAccent, ProtoPalette.Hex("63432B"), 9, 22);
            CropDefinition corn = EnsureCrop("Crop_Corn", "corn", "Corn",
                ProtoPalette.CornAccent, ProtoPalette.Hex("6E4C30"), 14, 34);

            FarmingSettings farmingSettings = EnsureFarmingSettings();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Prefabs and crop visuals are generated after the empty scene exists, because
            // assembling a prefab needs somewhere to build the temporary hierarchy.
            ConfigureCropGrowth(wheat, p, WheatSecondsPerStage, 1, 3);
            ConfigureCropGrowth(tomato, p, TomatoSecondsPerStage, 1, 2);
            ConfigureCropGrowth(corn, p, CornSecondsPerStage, 1, 2);

            PropLibrary.Props props = PropLibrary.BuildAll(p);
            GameObject chickenPrefab = CharacterBuilder.BuildChickenPrefab(p);
            GameObject cowPrefab = CharacterBuilder.BuildCowPrefab(p);

            AssetDatabase.SaveAssets();

            FarmPlot plotPrefab = BuildPlotPrefab(p, interactableLayer);

            ConfigureLighting(p);

            GameObject systemsRoot = new GameObject("--- SYSTEMS ---");
            GameObject worldRoot = new GameObject("--- WORLD ---");
            GameObject actorsRoot = new GameObject("--- ACTORS ---");
            GameObject uiRoot = new GameObject("--- UI ---");

            systemsRoot.AddComponent<GameBootstrap>();
            BuildEventSystem(systemsRoot.transform);

            BuildWorld(worldRoot.transform, p, plotPrefab, wheat, tomato, corn,
                interactableLayer, farmingSettings, props, chickenPrefab, cowPrefab);

            GameObject player = BuildPlayer(actorsRoot.transform, p);
            FarmCameraController cameraController = BuildCamera(actorsRoot.transform, player.transform);

            HudReferences hud = BuildHud(uiRoot.transform);

            WireEverything(player, cameraController, hud, interactableLayer, wheat);

            ProtoAssets.MarkStatic(worldRoot);

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

            Debug.Log("Little Farm Story: farm scene built at " + ScenePath + ". Press Play to test.");
        }

        // ================================================================ data assets

        private static CropDefinition EnsureCrop(
            string assetName, string id, string display, Color crop, Color soil, int seedCost, int sellValue)
        {
            string path = ProtoAssets.DataFolder + "/" + assetName + ".asset";
            CropDefinition definition = AssetDatabase.LoadAssetAtPath<CropDefinition>(path);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CropDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.EditorConfigure(id, display, crop, soil, seedCost, sellValue);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>Generates the growth-stage prefabs for a crop and writes its growth data.</summary>
        private static void ConfigureCropGrowth(
            CropDefinition definition, ProtoPalette p, float secondsPerStage, int yieldMin, int yieldMax)
        {
            if (definition == null)
            {
                return;
            }

            int stages = definition.GrowthStages;

            GameObject[] visuals = CropVisualBuilder.BuildStagePrefabs(
                definition.CropId,
                p,
                CropVisualBuilder.ShapeForCrop(definition.CropId),
                definition.CropColor,
                stages + 1);

            definition.EditorConfigureGrowth(stages, secondsPerStage, yieldMin, yieldMax, visuals);
            EditorUtility.SetDirty(definition);
        }

        private static FarmingSettings EnsureFarmingSettings()
        {
            string path = ProtoAssets.DataFolder + "/FarmingSettings.asset";
            FarmingSettings settings = AssetDatabase.LoadAssetAtPath<FarmingSettings>(path);

            if (settings != null)
            {
                // Preserve whatever multiplier the developer has dialled in.
                return settings;
            }

            settings = ScriptableObject.CreateInstance<FarmingSettings>();
            settings.EditorConfigure(1f, true, 0.2f);
            AssetDatabase.CreateAsset(settings, path);
            EditorUtility.SetDirty(settings);
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

            FarmPlot plot = temp.AddComponent<FarmPlot>();
            plot.SetLabel("Soil Plot");
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
            sun.color = ProtoPalette.Hex("FFF3DC");
            sun.intensity = 1.32f;
            sun.shadows = LightShadows.Soft;

            // Deliberately weak: strong shadows fight the cheerful, low-contrast look.
            sun.shadowStrength = 0.42f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.2f;
            lightGo.transform.rotation = Quaternion.Euler(46f, -34f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ProtoPalette.Hex("BBD9F2");
            RenderSettings.ambientEquatorColor = ProtoPalette.Hex("C6D2B4");
            RenderSettings.ambientGroundColor = ProtoPalette.Hex("6E6350");
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = ProtoPalette.Hex("CFE3E8");
            RenderSettings.fogStartDistance = 62f;
            RenderSettings.fogEndDistance = 118f;
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
            GameObject chickenPrefab, GameObject cowPrefab)
        {
            Transform environment = ProtoAssets.Empty("Environment", root, Vector3.zero).transform;
            FarmEnvironmentBuilder.BuildGround(environment, p);
            FarmEnvironmentBuilder.BuildPaths(environment, p);
            FarmEnvironmentBuilder.BuildBoundary(environment, p);

            Transform fields = ProtoAssets.Empty("Fields", root, Vector3.zero).transform;
            BuildField(fields, p, plotPrefab, layer, farmingSettings, "Wheat", wheat,
                WheatCentre, new Vector2(13f, 11f), new Vector2Int(5, 4), p.Wheat);
            BuildField(fields, p, plotPrefab, layer, farmingSettings, "Tomato", tomato,
                TomatoCentre, new Vector2(11f, 9f), new Vector2Int(4, 3), p.Tomato);
            BuildField(fields, p, plotPrefab, layer, farmingSettings, "Corn", corn,
                CornCentre, new Vector2(13f, 11f), new Vector2Int(5, 3), p.Corn);

            Transform areas = ProtoAssets.Empty("Areas", root, Vector3.zero).transform;
            BuildProductionArea(areas, p, layer, props, ProductionCentre);
            BuildChickenArea(areas, p, layer, props, chickenPrefab, ChickenCentre);
            BuildCowArea(areas, p, layer, props, cowPrefab, CowCentre);
            BuildMarketArea(areas, p, layer, props, MarketCentre);
            BuildHomeArea(areas, p, layer, props, HomeCentre);

            Transform decor = ProtoAssets.Empty("Decoration", root, Vector3.zero).transform;
            BuildDecoration(decor, p, props);
        }

        private static void BuildField(
            Transform parent, ProtoPalette p, FarmPlot plotPrefab, int layer,
            FarmingSettings farmingSettings, string label, CropDefinition crop,
            Vector3 centre, Vector2 padSize, Vector2Int gridSize, Material accent)
        {
            Transform field = ProtoAssets.Empty("Field_" + label, parent, Vector3.zero).transform;
            FarmEnvironmentBuilder.BuildFieldPad(field, p, "Pad_" + label, centre, padSize);

            GameObject gridGo = ProtoAssets.Empty("Grid_" + label, field, centre + new Vector3(0f, 0.12f, 0f));
            FarmGrid grid = gridGo.AddComponent<FarmGrid>();
            grid.EditorConfigure("field_" + crop.CropId, crop, gridSize, 2.2f, plotPrefab);
            grid.EditorSetFarmingSettings(farmingSettings);

            // Sign faces the path, painted in the crop colour so the field is identifiable
            // from across the farm without reading any UI.
            float hx = padSize.x * 0.5f;
            bool eastSide = centre.x > 0f;
            float signX = eastSide ? centre.x - hx - 1.9f : centre.x + hx + 1.9f;
            Vector3 signPos = new Vector3(signX, 0f, centre.z);

            FarmEnvironmentBuilder.Signpost(field, p, "Sign_" + label, signPos, accent, eastSide ? 90f : -90f);

            FarmEnvironmentBuilder.Landmark(field, "Interact_" + label, "field_" + crop.CropId,
                crop.DisplayName + " Field", FarmLandmark.LandmarkKind.Field,
                signPos, new Vector3(3.2f, 2.2f, 3.2f), layer);
        }

        private static void BuildProductionArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Production", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);
            Mesh trim = StylizedMeshLibrary.ChamferBox(0.2f);

            ProtoAssets.MeshObject(box, "Yard", area, new Vector3(0f, 0.05f, 0f),
                new Vector3(11.5f, 0.1f, 9.5f), p.Concrete, false);

            BuildingBuilder.ProductionShelter(area, p, new Vector3(0f, 0f, 0.6f));

            // Three empty machine pads: the footprint the production phase will fill.
            for (int i = 0; i < 3; i++)
            {
                float x = -3.2f + i * 3.2f;

                ProtoAssets.MeshObject(trim, "MachinePad_" + i, area, new Vector3(x, 0.16f, 0.6f),
                    new Vector3(2.6f, 0.16f, 2.6f), p.Stone, false);

                ProtoAssets.MeshObject(trim, "PadStripe_" + i, area, new Vector3(x, 0.25f, 0.6f),
                    new Vector3(2.1f, 0.04f, 2.1f), p.RoofMustard, false);
            }

            BuildingBuilder.Silo(area, p, new Vector3(4.6f, 0f, -3.6f), 6.8f);

            FarmEnvironmentBuilder.PlaceProp(props.Crate, area, new Vector3(-4.4f, 0.1f, -3.3f), 1.1f, 18f);
            FarmEnvironmentBuilder.PlaceProp(props.Crate, area, new Vector3(-3.6f, 0.1f, -3.9f), 0.95f, -24f);
            FarmEnvironmentBuilder.PlaceProp(props.Barrel, area, new Vector3(-4.9f, 0.1f, -2.2f), 1f, 0f);
            FarmEnvironmentBuilder.PlaceProp(props.Barrel, area, new Vector3(-4.1f, 0.1f, -1.6f), 0.9f, 40f);

            FarmEnvironmentBuilder.Landmark(area, "Interact_Production", "area_production",
                "Production Yard", FarmLandmark.LandmarkKind.Production,
                new Vector3(0f, 0f, -2.6f), new Vector3(5f, 2.4f, 3f), layer);
        }

        private static void BuildChickenArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props,
            GameObject chickenPrefab, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Chicken", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);
            Mesh trim = StylizedMeshLibrary.ChamferBox(0.22f);
            Mesh disc = StylizedMeshLibrary.Disc(16);

            ProtoAssets.MeshObject(box, "PenGround", area, new Vector3(0f, 0.045f, 0f),
                new Vector3(12.5f, 0.09f, 9.5f), p.PathEdge, false);

            ProtoAssets.MeshObject(disc, "ScratchPatch", area, new Vector3(-1.6f, 0.1f, -1.2f),
                new Vector3(5.2f, 1f, 4.2f), p.Soil, false);

            FarmEnvironmentBuilder.FenceRect(area, p, "PenFence", Vector3.zero,
                new Vector2(12f, 9f), 2.4f, 1.1f, 3.2f);

            BuildingBuilder.Coop(area, p, new Vector3(3.4f, 0f, 2.2f));

            ProtoAssets.MeshObject(trim, "FeedTrough", area, new Vector3(-2.2f, 0.28f, -1.8f),
                new Vector3(2.8f, 0.42f, 0.78f), p.Wood, false);
            ProtoAssets.MeshObject(trim, "FeedGrain", area, new Vector3(-2.2f, 0.46f, -1.8f),
                new Vector3(2.4f, 0.12f, 0.5f), p.WheatStraw, false);

            ProtoAssets.MeshObject(disc, "WaterBowl", area, new Vector3(-3.9f, 0.12f, 0.5f),
                new Vector3(1.0f, 1f, 1.0f), p.WaterShallow, false);

            Vector3[] spots =
            {
                new Vector3(-1.3f, 0.09f, 1.7f), new Vector3(0.7f, 0.09f, -2.4f),
                new Vector3(-3.3f, 0.09f, -2.7f), new Vector3(1.9f, 0.09f, 0.5f),
                new Vector3(-0.4f, 0.09f, 3.0f)
            };
            float[] yaws = { 35f, 150f, 250f, 300f, 80f };

            for (int i = 0; i < spots.Length; i++)
            {
                FarmEnvironmentBuilder.PlaceProp(chickenPrefab, area, spots[i], 1f, yaws[i]);
            }

            FarmEnvironmentBuilder.PlaceProp(props.HayBale, area, new Vector3(4.6f, 0.09f, -2.8f), 0.55f, 20f);

            FarmEnvironmentBuilder.Landmark(area, "Interact_Chicken", "area_chicken",
                "Chicken Coop", FarmLandmark.LandmarkKind.AnimalPen,
                new Vector3(3.4f, 0f, -0.1f), new Vector3(4.5f, 2.2f, 3f), layer);
        }

        private static void BuildCowArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props,
            GameObject cowPrefab, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Cow", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);
            Mesh trim = StylizedMeshLibrary.ChamferBox(0.22f);

            ProtoAssets.MeshObject(box, "PenGround", area, new Vector3(0f, 0.045f, 0f),
                new Vector3(15.5f, 0.09f, 11.5f), p.GrassDeep, false);

            FarmEnvironmentBuilder.FenceRect(area, p, "PenFence", Vector3.zero,
                new Vector2(15f, 11f), 2.6f, 1.35f, 3.4f);

            BuildingBuilder.Barn(area, p, new Vector3(4.2f, 0f, 2.8f));

            ProtoAssets.MeshObject(trim, "Trough", area, new Vector3(-1.4f, 0.32f, -3.8f),
                new Vector3(4.4f, 0.6f, 1.0f), p.Wood, false);
            ProtoAssets.MeshObject(trim, "TroughWater", area, new Vector3(-1.4f, 0.56f, -3.8f),
                new Vector3(4.0f, 0.1f, 0.7f), p.WaterShallow, false);

            FarmEnvironmentBuilder.PlaceProp(props.HayBale, area, new Vector3(-5.0f, 0.09f, 2.6f), 1f, 12f);
            FarmEnvironmentBuilder.PlaceProp(props.HayBale, area, new Vector3(-5.0f, 0.09f, 0.9f), 1f, -20f);
            FarmEnvironmentBuilder.PlaceProp(props.HayBale, area, new Vector3(-5.1f, 1.35f, 1.75f), 0.9f, 44f);

            FarmEnvironmentBuilder.PlaceProp(cowPrefab, area, new Vector3(-2.0f, 0.09f, -0.6f), 1f, 55f);
            FarmEnvironmentBuilder.PlaceProp(cowPrefab, area, new Vector3(1.6f, 0.09f, -2.4f), 1f, 205f);
            FarmEnvironmentBuilder.PlaceProp(cowPrefab, area, new Vector3(-0.4f, 0.09f, 2.6f), 0.92f, 320f);

            FarmEnvironmentBuilder.Landmark(area, "Interact_Cow", "area_cow",
                "Cow Barn", FarmLandmark.LandmarkKind.AnimalPen,
                new Vector3(4.2f, 0f, -0.6f), new Vector3(5f, 2.4f, 3.2f), layer);
        }

        private static void BuildMarketArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Market", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);
            Mesh disc = StylizedMeshLibrary.Disc(20);

            ProtoAssets.MeshObject(box, "Plaza", area, new Vector3(0f, 0.04f, 0f),
                new Vector3(12.5f, 0.08f, 11.5f), p.Path, false);
            ProtoAssets.MeshObject(disc, "PlazaInlay", area, new Vector3(0f, 0.09f, 0.6f),
                new Vector3(8.4f, 1f, 8.4f), p.PathEdge, false);

            BuildingBuilder.MarketStall(area, p, new Vector3(0f, 0f, 1.4f));

            FarmEnvironmentBuilder.PlaceProp(props.Crate, area, new Vector3(-4.6f, 0.06f, -2.6f), 1.1f, 22f);
            FarmEnvironmentBuilder.PlaceProp(props.Crate, area, new Vector3(-4.2f, 0.06f, -3.6f), 0.9f, -35f);
            FarmEnvironmentBuilder.PlaceProp(props.Barrel, area, new Vector3(4.6f, 0.06f, -2.8f), 1f, 0f);
            FarmEnvironmentBuilder.PlaceProp(props.Flowers, area, new Vector3(5.2f, 0.06f, 2.0f), 1.3f, 0f);
            FarmEnvironmentBuilder.PlaceProp(props.Flowers, area, new Vector3(-5.4f, 0.06f, 1.4f), 1.2f, 60f);

            FarmEnvironmentBuilder.Landmark(area, "Interact_Market", "area_market",
                "Farmers Market", FarmLandmark.LandmarkKind.Market,
                new Vector3(0f, 0f, -2.9f), new Vector3(8f, 2.4f, 3f), layer);
        }

        private static void BuildHomeArea(
            Transform parent, ProtoPalette p, int layer, PropLibrary.Props props, Vector3 centre)
        {
            Transform area = ProtoAssets.Empty("Area_Home", parent, centre).transform;
            Mesh box = StylizedMeshLibrary.ChamferBox(0.06f);
            Mesh disc = StylizedMeshLibrary.Disc(16);

            ProtoAssets.MeshObject(box, "Yard", area, new Vector3(0f, 0.03f, 0f),
                new Vector3(12f, 0.06f, 11f), p.GrassLight, false);

            ProtoAssets.MeshObject(disc, "FrontPath", area, new Vector3(0f, 0.07f, -4.2f),
                new Vector3(4.4f, 1f, 4.4f), p.Path, false);

            BuildingBuilder.Farmhouse(area, p, new Vector3(0f, 0f, 1.6f));

            FarmEnvironmentBuilder.PlaceProp(props.BushLarge, area, new Vector3(-3.4f, 0.05f, -3.2f), 1f, 30f);
            FarmEnvironmentBuilder.PlaceProp(props.BushLarge, area, new Vector3(3.4f, 0.05f, -3.2f), 1.05f, -50f);
            FarmEnvironmentBuilder.PlaceProp(props.BushSmall, area, new Vector3(-4.6f, 0.05f, -1.6f), 1f, 12f);
            FarmEnvironmentBuilder.PlaceProp(props.Flowers, area, new Vector3(-2.2f, 0.05f, -4.4f), 1.2f, 0f);
            FarmEnvironmentBuilder.PlaceProp(props.Flowers, area, new Vector3(2.2f, 0.05f, -4.4f), 1.15f, 90f);
            FarmEnvironmentBuilder.PlaceProp(props.TreeRound, area, new Vector3(5.0f, 0.05f, 1.2f), 0.9f, 0f);
            FarmEnvironmentBuilder.PlaceProp(props.Barrel, area, new Vector3(-4.8f, 0.05f, 2.4f), 0.95f, 15f);

            FarmEnvironmentBuilder.Landmark(area, "Interact_Home", "area_home",
                "Farmhouse", FarmLandmark.LandmarkKind.Home,
                new Vector3(0f, 0f, -3.2f), new Vector3(5f, 2.4f, 2.6f), layer);
        }

        // ================================================================ decoration

        private static void BuildDecoration(Transform parent, ProtoPalette p, PropLibrary.Props props)
        {
            // ---- treeline just inside the fence, framing the farm
            Vector3[] treePositions =
            {
                new Vector3(-28.5f, 0f, 28f), new Vector3(-22f, 0f, 29f), new Vector3(-29f, 0f, 21f),
                new Vector3(28.5f, 0f, 28.5f), new Vector3(22f, 0f, 29.5f), new Vector3(29f, 0f, 22f),
                new Vector3(-29f, 0f, -28f), new Vector3(-23f, 0f, -29f), new Vector3(29f, 0f, -29f),
                new Vector3(23f, 0f, -29.5f), new Vector3(-29.5f, 0f, 1.5f), new Vector3(29.5f, 0f, 1.5f),
                new Vector3(-9.5f, 0f, 28.5f), new Vector3(10f, 0f, 28f), new Vector3(-9f, 0f, -29f),
                new Vector3(9.5f, 0f, -29f), new Vector3(-25f, 0f, 12f), new Vector3(25.5f, 0f, 12.5f),
                new Vector3(-25.5f, 0f, -1.5f), new Vector3(25.5f, 0f, -1.5f)
            };

            int[] treeKinds = { 0, 1, 0, 1, 0, 2, 0, 1, 0, 2, 1, 0, 0, 2, 1, 0, 2, 0, 1, 2 };

            for (int i = 0; i < treePositions.Length; i++)
            {
                GameObject prefab = treeKinds[i] == 0 ? props.TreeRound
                    : treeKinds[i] == 1 ? props.TreeTall
                    : props.TreeYoung;

                FarmEnvironmentBuilder.PlaceProp(
                    prefab, parent, treePositions[i], 0.86f + (i % 4) * 0.08f, i * 63f % 360f);
            }

            // ---- distant treeline outside the fence, softened by the fog
            for (int i = 0; i < 22; i++)
            {
                float angle = i / 22f * Mathf.PI * 2f + 0.35f;
                float radius = 40f + (i % 3) * 4.5f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                GameObject prefab = (i % 2 == 0) ? props.TreeTall : props.TreeRound;
                FarmEnvironmentBuilder.PlaceProp(prefab, parent, pos, 1.15f + (i % 4) * 0.1f, i * 37f % 360f);
            }

            // ---- bushes softening the path edges and zone corners
            Vector3[] bushes =
            {
                new Vector3(-8.2f, 0f, 24f), new Vector3(8.2f, 0f, 24f),
                new Vector3(-8.2f, 0f, -13.5f), new Vector3(8.2f, 0f, -13.5f),
                new Vector3(-5.2f, 0f, 11f), new Vector3(5.2f, 0f, 11f),
                new Vector3(-5.2f, 0f, -11f), new Vector3(5.2f, 0f, -11f),
                new Vector3(-26f, 0f, -3f), new Vector3(26f, 0f, -3f),
                new Vector3(-13f, 0f, 27.5f), new Vector3(13.5f, 0f, 27.5f),
                new Vector3(-24.5f, 0f, -27f), new Vector3(24.5f, 0f, -27.5f)
            };

            for (int i = 0; i < bushes.Length; i++)
            {
                GameObject prefab = (i % 3 == 0) ? props.BushSmall : props.BushLarge;
                FarmEnvironmentBuilder.PlaceProp(prefab, parent, bushes[i], 0.9f + (i % 3) * 0.16f, i * 53f % 360f);
            }

            // ---- flowers along the plaza and the main avenue
            Vector3[] flowers =
            {
                new Vector3(-4.4f, 0f, 4.4f), new Vector3(4.4f, 0f, 4.4f),
                new Vector3(-4.4f, 0f, -4.4f), new Vector3(4.4f, 0f, -4.4f),
                new Vector3(-3.6f, 0f, 13f), new Vector3(3.6f, 0f, 13f),
                new Vector3(-3.6f, 0f, -16f), new Vector3(3.6f, 0f, -16f),
                new Vector3(-20f, 0f, 25.5f), new Vector3(20f, 0f, 25.5f)
            };

            for (int i = 0; i < flowers.Length; i++)
            {
                FarmEnvironmentBuilder.PlaceProp(
                    props.Flowers, parent, flowers[i], 1.1f + (i % 3) * 0.15f, i * 71f % 360f);
            }

            // ---- rocks
            FarmEnvironmentBuilder.PlaceProp(props.Rock, parent, new Vector3(-11.5f, 0f, 25f), 1f, 20f);
            FarmEnvironmentBuilder.PlaceProp(props.RockSmall, parent, new Vector3(-10.4f, 0f, 24f), 1f, -40f);
            FarmEnvironmentBuilder.PlaceProp(props.Rock, parent, new Vector3(12.5f, 0f, -25f), 0.9f, 130f);
            FarmEnvironmentBuilder.PlaceProp(props.Rock, parent, new Vector3(-27.5f, 0f, 6f), 1.2f, 70f);
            FarmEnvironmentBuilder.PlaceProp(props.RockSmall, parent, new Vector3(27f, 0f, 17f), 1f, 250f);

            // ---- pond
            FarmEnvironmentBuilder.Pond(parent, p, PondCentre, 4.6f, props);

            // ---- crossroads signpost: the visual anchor of the player's start area
            BuildCrossroadsSign(parent, p);
        }

        private static void BuildCrossroadsSign(Transform parent, ProtoPalette p)
        {
            Transform sign = ProtoAssets.Empty("Crossroads_Signpost", parent, new Vector3(4.4f, 0f, 4.4f)).transform;

            Mesh box = StylizedMeshLibrary.ChamferBox(0.18f);
            Mesh taper = StylizedMeshLibrary.Tapered(0.8f);
            Mesh cone = StylizedMeshLibrary.Cone(8);

            GameObject post = ProtoAssets.MeshObject(taper, "Post", sign, new Vector3(0f, 1.5f, 0f),
                new Vector3(0.26f, 3.0f, 0.26f), p.Wood, true);
            post.AddComponent<BoxCollider>();

            ProtoAssets.MeshObject(cone, "Finial", sign, new Vector3(0f, 3.18f, 0f),
                new Vector3(0.34f, 0.36f, 0.34f), p.RoofTerracotta, false);

            // Four painted arms, each pointing towards a quadrant of the farm.
            Material[] armColors = { p.Wheat, p.Tomato, p.RoofTeal, p.Amber };
            float[] armYaw = { 0f, 90f, 180f, 270f };
            float[] armHeight = { 2.6f, 2.2f, 1.8f, 1.4f };

            for (int i = 0; i < 4; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, armYaw[i], 0f);

                GameObject arm = ProtoAssets.MeshObject(box, "Arm_" + i, sign,
                    rotation * new Vector3(0.85f, 0f, 0f) + new Vector3(0f, armHeight[i], 0f),
                    new Vector3(1.7f, 0.3f, 0.12f), armColors[i], false);

                arm.transform.localRotation = rotation;
            }
        }

        // ================================================================ actors

        private static GameObject BuildPlayer(Transform parent, ProtoPalette p)
        {
            GameObject player = ProtoAssets.Empty("Player", parent, new Vector3(0f, 0.2f, -6f));

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.7f;
            controller.center = new Vector3(0f, 0.87f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.03f;

            // Visual is rotated by PlayerController; BobRoot is animated by PlayerVisualBob.
            // Separating them means the two never write to the same transform property.
            Transform visual = ProtoAssets.Empty("Visual", player.transform, Vector3.zero).transform;
            Transform bobRoot = ProtoAssets.Empty("BobRoot", visual, Vector3.zero).transform;

            CharacterBuilder.BuildFarmer(bobRoot, p);

            // Soft contact shadow: cheaper and more readable than relying on the sun alone.
            ProtoAssets.MeshObject(StylizedMeshLibrary.Disc(14), "ContactShadow", visual,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 1f, 0.8f), p.GrassDeep, false);

            player.AddComponent<PlayerInputProvider>();
            player.AddComponent<KeyboardMoveInputSource>();
            PlayerController movement = player.AddComponent<PlayerController>();
            player.AddComponent<InteractionController>();
            player.AddComponent<PlayerInventory>();
            player.AddComponent<ActionFeedbackChannel>();

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
            cam.backgroundColor = ProtoPalette.Hex("CFE3E8");

            // Narrow field of view plus a long distance gives the flatter, near-isometric
            // read of a polished mobile farm game, and keeps a whole field on a portrait screen.
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 140f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = true;

            go.AddComponent<AudioListener>();

            FarmCameraController controller = go.AddComponent<FarmCameraController>();
            Wire(controller, "target", target);
            WireRect(controller, "focusBounds", new Rect(-26f, -26f, 52f, 52f));

            return controller;
        }

        // ================================================================ HUD

        private class HudReferences
        {
            public HudController Hud;
            public MobileJoystick Joystick;
            public VirtualButton ActionButton;
            public Text CoinLabel;
            public Text LevelLabel;
            public Text PromptLabel;
            public GameObject PromptRoot;
            public GameObject ActionRoot;
            public Button MenuButton;
            public Text SeedLabel;
            public Text ProduceLabel;
            public Text MessageLabel;
            public GameObject MessageRoot;
        }

        private static HudReferences BuildHud(Transform parent)
        {
            Sprite circle = ProtoAssets.CircleSprite("UI_Circle");
            Sprite ring = ProtoAssets.CircleSprite("UI_Ring", 160, 0.78f);
            Sprite panel = ProtoAssets.OutlinedBoxSprite("UI_PanelOutlined", 96, 30, 5);
            Sprite plain = ProtoAssets.RoundedBoxSprite("UI_Panel");

            GameObject canvasGo = new GameObject("HUD_Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.transform.SetParent(parent, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform canvasRoot = canvasGo.transform;
            HudReferences refs = new HudReferences();

            BuildMenuButton(canvasRoot, circle, refs);
            BuildStatusPills(canvasRoot, panel, circle, refs);
            BuildJoystick(canvasRoot, circle, ring, refs);
            BuildActionButton(canvasRoot, circle, refs);
            BuildPromptAndMessage(canvasRoot, panel, plain, refs);

            refs.Hud = canvasGo.AddComponent<HudController>();
            return refs;
        }

        private static void BuildMenuButton(Transform canvasRoot, Sprite circle, HudReferences refs)
        {
            Image menuBg = ProtoUi.Sprite("MenuButton", canvasRoot, circle, ProtoUi.Panel, true);
            ProtoUi.AnchorCorner(menuBg.rectTransform, new Vector2(0f, 1f),
                new Vector2(146f, 146f), new Vector2(44f, 44f));
            ProtoUi.AddShadowBehind(menuBg, 8f, 6f);

            Image menuInner = ProtoUi.Sprite("Inner", menuBg.transform, circle, ProtoUi.PanelDim);
            ProtoUi.Centre(menuInner.rectTransform, new Vector2(116f, 116f));

            // Three bars: a hamburger drawn from primitives rather than an icon font.
            for (int i = 0; i < 3; i++)
            {
                Image bar = ProtoUi.Sprite("Bar_" + i, menuBg.transform, null, ProtoUi.Ink);
                ProtoUi.Centre(bar.rectTransform, new Vector2(58f, 9f));
                bar.rectTransform.anchoredPosition = new Vector2(0f, 20f - i * 20f);
            }

            refs.MenuButton = menuBg.gameObject.AddComponent<Button>();
            refs.MenuButton.targetGraphic = menuBg;

            ColorBlock colors = refs.MenuButton.colors;
            colors.pressedColor = ProtoUi.PanelDim;
            colors.fadeDuration = 0.06f;
            refs.MenuButton.colors = colors;
        }

        private static void BuildStatusPills(
            Transform canvasRoot, Sprite panel, Sprite circle, HudReferences refs)
        {
            refs.CoinLabel = Pill(canvasRoot, panel, circle, "CoinPill", "Coins", ProtoUi.Coin, 44f, "250");
            refs.LevelLabel = Pill(canvasRoot, panel, circle, "LevelPill", "Level", ProtoUi.Leaf, 168f, "1");
            refs.SeedLabel = Pill(canvasRoot, panel, circle, "SeedPill", "Wheat Seeds", ProtoUi.Seed, 292f, "0");
            refs.ProduceLabel = Pill(canvasRoot, panel, circle, "ProducePill", "Wheat", ProtoPalette.WheatAccent, 416f, "0");
        }

        /// <summary>
        /// One status pill: shadow, outlined panel, coloured icon disc, caption and value.
        /// Widths are fixed so the caption and value can never collide.
        /// </summary>
        private static Text Pill(
            Transform canvasRoot, Sprite panel, Sprite circle,
            string objectName, string caption, Color iconColor, float topMargin, string initialValue)
        {
            const float width = 372f;
            const float height = 108f;

            Image pill = ProtoUi.Sprite(objectName, canvasRoot, panel, ProtoUi.Panel);
            ProtoUi.AnchorCorner(pill.rectTransform, new Vector2(1f, 1f),
                new Vector2(width, height), new Vector2(44f, topMargin));
            ProtoUi.AddShadowBehind(pill, 7f, 6f);

            Image iconRing = ProtoUi.Sprite("IconRing", pill.transform, circle, ProtoUi.PanelDim);
            ProtoUi.AnchorCorner(iconRing.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(76f, 76f), new Vector2(16f, 0f));
            iconRing.rectTransform.anchoredPosition = new Vector2(16f, 0f);

            Image icon = ProtoUi.Sprite("Icon", iconRing.transform, circle, iconColor);
            ProtoUi.Centre(icon.rectTransform, new Vector2(58f, 58f));

            Text captionLabel = ProtoUi.Label("Caption", pill.transform, caption, 25, TextAnchor.MiddleLeft);
            captionLabel.color = new Color(ProtoUi.Ink.r, ProtoUi.Ink.g, ProtoUi.Ink.b, 0.72f);
            ProtoUi.AnchorCorner(captionLabel.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(150f, 60f), new Vector2(104f, 0f));
            captionLabel.rectTransform.anchoredPosition = new Vector2(104f, 0f);

            Text value = ProtoUi.Label("Value", pill.transform, initialValue, 42, TextAnchor.MiddleRight);
            ProtoUi.AnchorCorner(value.rectTransform, new Vector2(1f, 0.5f),
                new Vector2(102f, 70f), new Vector2(22f, 0f));
            value.rectTransform.anchoredPosition = new Vector2(-22f, 0f);

            return value;
        }

        private static void BuildJoystick(
            Transform canvasRoot, Sprite circle, Sprite ring, HudReferences refs)
        {
            // Large invisible touch zone: the stick snaps to wherever the thumb lands.
            Image zone = ProtoUi.Sprite("JoystickZone", canvasRoot, null, new Color(0f, 0f, 0f, 0f), true);
            zone.rectTransform.anchorMin = new Vector2(0f, 0f);
            zone.rectTransform.anchorMax = new Vector2(0.62f, 0.4f);
            zone.rectTransform.offsetMin = Vector2.zero;
            zone.rectTransform.offsetMax = Vector2.zero;

            Image stickBg = ProtoUi.Sprite("Background", zone.transform, ring, new Color(1f, 1f, 1f, 0.62f));
            ProtoUi.Centre(stickBg.rectTransform, new Vector2(380f, 380f));
            stickBg.rectTransform.anchoredPosition = new Vector2(0f, -20f);

            CanvasGroup stickGroup = stickBg.gameObject.AddComponent<CanvasGroup>();
            stickGroup.blocksRaycasts = false;
            stickGroup.interactable = false;

            // Darkened track inside the ring lifts the thumb off bright grass.
            Image track = ProtoUi.Sprite("Track", stickBg.transform, circle, new Color(0.16f, 0.12f, 0.08f, 0.16f));
            ProtoUi.Centre(track.rectTransform, new Vector2(320f, 320f));

            Image handle = ProtoUi.Sprite("Handle", stickBg.transform, circle, new Color(1f, 1f, 1f, 0.92f));
            ProtoUi.Centre(handle.rectTransform, new Vector2(166f, 166f));

            Image handleRim = ProtoUi.Sprite("HandleRim", handle.transform, circle, ProtoUi.Accent);
            ProtoUi.Centre(handleRim.rectTransform, new Vector2(78f, 78f));

            refs.Joystick = zone.gameObject.AddComponent<MobileJoystick>();
            Wire(refs.Joystick, "background", stickBg.rectTransform);
            Wire(refs.Joystick, "handle", handle.rectTransform);
            Wire(refs.Joystick, "visuals", stickGroup);
        }

        private static void BuildActionButton(Transform canvasRoot, Sprite circle, HudReferences refs)
        {
            // Outer disc is the button body; the inner face is what scales on press.
            Image outer = ProtoUi.Sprite("ActionButton", canvasRoot, circle, ProtoUi.AccentDeep, true);
            ProtoUi.AnchorCorner(outer.rectTransform, new Vector2(1f, 0f),
                new Vector2(258f, 258f), new Vector2(52f, 104f));
            ProtoUi.AddShadowBehind(outer, 10f, 8f);

            CanvasGroup actionGroup = outer.gameObject.AddComponent<CanvasGroup>();

            Image face = ProtoUi.Sprite("Face", outer.transform, circle, ProtoUi.Accent);
            ProtoUi.Centre(face.rectTransform, new Vector2(214f, 214f));

            // Top highlight sells the button as a physical, pressable dome.
            Image gloss = ProtoUi.Sprite("Gloss", face.transform, circle, new Color(1f, 1f, 1f, 0.22f));
            ProtoUi.Centre(gloss.rectTransform, new Vector2(168f, 168f));
            gloss.rectTransform.anchoredPosition = new Vector2(0f, 22f);

            Text actionText = ProtoUi.OutlinedLabel("Label", face.transform, "USE", 56, TextAnchor.MiddleCenter, 2.5f);
            actionText.color = ProtoUi.InkLight;
            ProtoUi.Stretch(actionText.rectTransform, Vector2.zero, Vector2.one);

            refs.ActionButton = outer.gameObject.AddComponent<VirtualButton>();
            Wire(refs.ActionButton, "visuals", actionGroup);
            Wire(refs.ActionButton, "scaleTarget", face.rectTransform);

            refs.ActionRoot = outer.gameObject;
        }

        private static void BuildPromptAndMessage(
            Transform canvasRoot, Sprite panel, Sprite plain, HudReferences refs)
        {
            // Contextual prompt, sitting above the action button.
            Image prompt = ProtoUi.Sprite("PromptPanel", canvasRoot, panel, ProtoUi.Panel);
            ProtoUi.AnchorCorner(prompt.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(680f, 122f), new Vector2(0f, 396f));
            prompt.rectTransform.anchoredPosition = new Vector2(0f, 396f);
            ProtoUi.AddShadowBehind(prompt, 7f, 6f);

            refs.PromptLabel = ProtoUi.Label("Label", prompt.transform, "", 44, TextAnchor.MiddleCenter);
            ProtoUi.Stretch(refs.PromptLabel.rectTransform, Vector2.zero, Vector2.one);
            refs.PromptRoot = prompt.gameObject;

            // Transient action toast, higher up so it never covers the prompt.
            Image message = ProtoUi.Sprite("MessagePanel", canvasRoot, plain, ProtoPalette.Hex("3A2E22"));
            message.color = new Color(message.color.r, message.color.g, message.color.b, 0.88f);
            ProtoUi.AnchorCorner(message.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(700f, 104f), new Vector2(0f, 556f));
            message.rectTransform.anchoredPosition = new Vector2(0f, 556f);

            refs.MessageLabel = ProtoUi.Label("Label", message.transform, "", 42, TextAnchor.MiddleCenter);
            refs.MessageLabel.color = ProtoUi.InkLight;
            ProtoUi.Stretch(refs.MessageLabel.rectTransform, Vector2.zero, Vector2.one);

            refs.MessageRoot = message.gameObject;
            message.gameObject.SetActive(false);
        }

        // ================================================================ wiring

        private static void WireEverything(
            GameObject player, FarmCameraController cameraController, HudReferences hud,
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

            Wire(hud.Hud, "interaction", interaction);
            Wire(hud.Hud, "coinLabel", hud.CoinLabel);
            Wire(hud.Hud, "xpLabel", hud.LevelLabel);
            Wire(hud.Hud, "promptLabel", hud.PromptLabel);
            Wire(hud.Hud, "promptRoot", hud.PromptRoot);
            Wire(hud.Hud, "actionButtonRoot", hud.ActionRoot);
            Wire(hud.Hud, "menuButton", hud.MenuButton);

            Wire(hud.Hud, "inventory", inventory);
            Wire(hud.Hud, "feedback", feedbackChannel);
            Wire(hud.Hud, "messageLabel", hud.MessageLabel);
            Wire(hud.Hud, "messageRoot", hud.MessageRoot);
            WireInventoryCounters(hud.Hud, primaryCrop, hud.SeedLabel, hud.ProduceLabel);

            cameraController.SnapToTarget();
        }

        /// <summary>
        /// Binds the HUD counters to item ids taken from the crop asset, so renaming a crop id
        /// cannot silently desync the readout from the inventory.
        /// </summary>
        private static void WireInventoryCounters(
            HudController hud, CropDefinition crop, Text seedLabel, Text produceLabel)
        {
            if (hud == null || crop == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(hud);
            SerializedProperty counters = so.FindProperty("counters");

            if (counters == null || !counters.isArray)
            {
                Debug.LogError("Little Farm Story: HudController has no 'counters' array property.");
                return;
            }

            counters.arraySize = 2;
            SetCounter(counters.GetArrayElementAtIndex(0), crop.SeedItemId, seedLabel);
            SetCounter(counters.GetArrayElementAtIndex(1), crop.HarvestItemId, produceLabel);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCounter(SerializedProperty element, string itemId, Text label)
        {
            SerializedProperty idProperty = element.FindPropertyRelative("ItemId");
            SerializedProperty labelProperty = element.FindPropertyRelative("Label");
            SerializedProperty prefixProperty = element.FindPropertyRelative("Prefix");

            if (idProperty == null || labelProperty == null || prefixProperty == null)
            {
                Debug.LogError("Little Farm Story: unexpected HudController.InventoryCounter layout.");
                return;
            }

            idProperty.stringValue = itemId;
            labelProperty.objectReferenceValue = label;
            prefixProperty.stringValue = string.Empty;
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
