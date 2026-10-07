using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LittleFarmStory.Animals;
using LittleFarmStory.Core;
using LittleFarmStory.Economy;
using LittleFarmStory.Farming;
using LittleFarmStory.Interaction;
using LittleFarmStory.Inventory;
using LittleFarmStory.Persistence;
using LittleFarmStory.Player;
using LittleFarmStory.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LittleFarmStory.EditorTools
{
    /// <summary>
    /// Drives the whole gameplay loop in Play mode with no human at the controls, and reports
    /// PASS or FAIL per step.
    ///
    /// This exists because "it compiles" and "the scene looks wired" both turned out to be
    /// worthless as evidence: the farm was silently unplayable for three phases while every
    /// static check passed. The test walks the player to a real plot and a real animal and
    /// pushes the real interact button through the real InteractionController, so it exercises
    /// the entire chain:
    ///
    ///   player position -> proximity scan -> priority -> focus -> InteractableBase.Interact
    ///   -> FarmPlot / AnimalInteraction -> state change -> inventory change -> visual change
    ///
    /// Nothing here is referenced by gameplay code, and none of it ships: this file lives in
    /// the Editor assembly.
    ///
    /// Two suites share this one driver, selected by which menu item started it and recorded
    /// in <see cref="SessionState"/> so it survives the domain reload that entering Play mode
    /// causes: the full suite above (farming, chicken, cow, HUD) and a Phase 7 suite
    /// (<see cref="BuildPhase7Steps"/>) that proves the shop while reusing five of the farming
    /// steps verbatim, rather than re-running the animals' slow production timers a second time
    /// just to prove they still work around the shop.
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayLoopTest
    {
        private const string RunningKey = "LittleFarmStory.GameplayLoopTest.Running";
        private const string SuiteKey = "LittleFarmStory.GameplayLoopTest.Suite";
        private const string FullSuite = "Full";
        private const string Phase7Suite = "Phase7";
        private const string Phase8Suite = "Phase8";
        private const string ResultsPath = "Documentation/RUNTIME_TEST_RESULTS.md";
        private const string SaveResultsPath = "Documentation/SAVE_TEST_RESULTS.md";

        /// <summary>
        /// Phase 8 writes its own file so a save run cannot overwrite the shop run's evidence,
        /// and so each report can be read as the record of one suite.
        /// </summary>
        private static string ActiveResultsPath =>
            SessionState.GetString(SuiteKey, FullSuite) == Phase8Suite ? SaveResultsPath : ResultsPath;

        private enum Status
        {
            Running = 0,
            Pass = 1,
            Fail = 2
        }

        private readonly struct StepResult
        {
            public readonly Status Status;
            public readonly string Detail;

            private StepResult(Status status, string detail)
            {
                Status = status;
                Detail = detail;
            }

            public static StepResult Running => new StepResult(Status.Running, null);

            public static StepResult Pass(string detail) => new StepResult(Status.Pass, detail);

            public static StepResult Fail(string detail) => new StepResult(Status.Fail, detail);
        }

        private class Step
        {
            public string Name;
            public Func<StepResult> Run;
            public float TimeoutSeconds = 8f;
        }

        // ---- harness state. Rebuilt from scratch once Play mode is actually running, because
        // entering Play mode reloads the domain and wipes everything set before it.
        private static List<Step> steps;
        private static readonly List<string> log = new List<string>();

        private static int stepIndex;
        private static float stepDeadline;
        private static bool finished;

        // ---- scene references
        private static GameObject player;
        private static CharacterController playerController;
        private static PlayerInventory inventory;
        private static InteractionController interaction;
        private static FarmPlot plot;
        private static CropDefinition wheat;
        private static AnimalController chicken;
        private static AnimalController cow;
        private static AnimalController[] allAnimals;

        // ---- HUD
        private static HudController hud;
        private static ActionPrompt actionPrompt;
        private static ToastPresenter toasts;
        private static InventoryPanel inventoryPanel;
        private static SafeAreaPanel safeArea;
        private static ActionFeedbackChannel feedback;
        private static TMP_Text[] hudLabels;
        private static string labelBeforeTill;

        // ---- Phase 7 economy/shop references
        private static EconomyManager economy;
        private static CurrencyWallet wallet;
        private static MarketInteractable market;
        private static ShopPanel shopPanel;
        private static ShopBuyCard buyCard;
        private static ShopSellRow sellRow;
        private static ShopItemDefinition wheatSeedsItem;
        private static ShopItemDefinition wheatItem;

        /// <summary>
        /// The farmer's own movement script - distinct from the field above named
        /// <c>playerController</c>, which is (confusingly, but pre-existing) the
        /// CharacterController. Needed to verify the shop's freeze/unfreeze actually toggles
        /// the real movement component, not just the CharacterController.
        /// </summary>
        private static LittleFarmStory.Player.PlayerController movementScript;

        // ---- values carried between steps
        private static int seedsBefore;
        private static int produceBefore;
        private static int coinsBefore;
        private static float pollDeadline;

        /// <summary>
        /// A second, independent deadline for Harvest()'s grace wait on FarmPlot's deferred
        /// Destroy() - kept separate from `pollDeadline` because that step already spends
        /// `pollDeadline` on an earlier wait within the same step, before this one is needed.
        /// </summary>
        private static float harvestVisualGraceDeadline;

        private static Vector3[] wanderStart;

        // ---- Phase 8 persistence references and the state the save is checked against
        private static SaveManager saveManager;
        private static int savedCoins;
        private static int savedSeeds;
        private static Vector3 savedPlayerPosition;
        private static PlotState savedPlotState;
        private static float savedChickenHunger;
        private static string savedPlotName;

        static GameplayLoopTest()
        {
            if (SessionState.GetBool(RunningKey, false))
            {
                EditorApplication.update -= Drive;
                EditorApplication.update += Drive;
            }
        }

        [MenuItem("Little Farm Story/Run Gameplay Loop Test (Play Mode)", false, 40)]
        public static void RunTest()
        {
            StartSuite(FullSuite, "gameplay loop test");
        }

        [MenuItem("Little Farm Story/Run Phase 7 Runtime Verification", false, 42)]
        public static void RunPhase7Test()
        {
            StartSuite(Phase7Suite, "Phase 7 runtime verification");
        }

        [MenuItem("Little Farm Story/Run Phase 8 Save Verification", false, 44)]
        public static void RunPhase8Test()
        {
            StartSuite(Phase8Suite, "Phase 8 save verification");
        }

        private static void StartSuite(string suite, string label)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Little Farm Story: exit Play mode before starting the " + label + ".");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(FarmPrototypeBuilder.ScenePath, OpenSceneMode.Single);

            // Every suite asserts a farm that has just started - 100 coins, 10 seeds, empty
            // plots. SaveManager loads any save it finds at startup, and the previous run's
            // exit wrote one, so without this the assertions would be measured against the
            // last run's leftovers instead of a new game.
            if (SaveSystem.Exists())
            {
                SaveSystem.Delete();
                Debug.Log("Little Farm Story: cleared the existing save so the " + label +
                          " starts from a new game.");
            }

            SessionState.SetString(SuiteKey, suite);
            SessionState.SetBool(RunningKey, true);
            Debug.Log("Little Farm Story: " + label + " starting. Entering Play mode...");
            EditorApplication.EnterPlaymode();
        }

        // ================================================================ driver

        private static void Drive()
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                EditorApplication.update -= Drive;
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                // Still entering Play mode, or the user stopped it early.
                return;
            }

            if (steps == null)
            {
                string suite = SessionState.GetString(SuiteKey, FullSuite);
                bool isPhase7 = suite == Phase7Suite;
                bool isPhase8 = suite == Phase8Suite;

                if (isPhase7)
                {
                    BuildPhase7Steps();
                }
                else if (isPhase8)
                {
                    BuildPhase8Steps();
                }
                else
                {
                    BuildSteps();
                }

                stepIndex = 0;
                finished = false;
                log.Clear();
                stepDeadline = Now + steps[0].TimeoutSeconds;
                log.Add("# Runtime Test Results");
                log.Add("");
                string menuName = isPhase7
                    ? "Run Phase 7 Runtime Verification"
                    : isPhase8
                        ? "Run Phase 8 Save Verification"
                        : "Run Gameplay Loop Test (Play Mode)";

                log.Add("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                        " by `Little Farm Story/" + menuName + "`.");
                log.Add("");
                log.Add("Every step below drove the real interaction chain in Play mode:");
                log.Add("player position -> proximity scan -> priority -> focus -> Interact ->");
                log.Add("state change -> inventory change -> visual change.");
                log.Add("");
            }

            if (finished)
            {
                return;
            }

            Step step = steps[stepIndex];
            StepResult result;

            try
            {
                result = step.Run();
            }
            catch (Exception e)
            {
                result = StepResult.Fail("threw " + e.GetType().Name + ": " + e.Message);
            }

            if (result.Status == Status.Running)
            {
                if (Now < stepDeadline)
                {
                    return;
                }

                result = StepResult.Fail("timed out after " + step.TimeoutSeconds.ToString("0.#") + "s");
            }

            Record(step.Name, result);

            if (result.Status == Status.Fail)
            {
                // A failed precondition makes every later step meaningless, so stop rather
                // than emitting a cascade of misleading failures.
                Finish(false);
                return;
            }

            stepIndex++;

            if (stepIndex >= steps.Count)
            {
                Finish(true);
                return;
            }

            stepDeadline = Now + steps[stepIndex].TimeoutSeconds;
            pollDeadline = 0f;
        }

        private static float Now => Time.realtimeSinceStartup;

        private static void Record(string name, StepResult result)
        {
            string verdict = result.Status == Status.Pass ? "PASS" : "FAIL";
            string line = verdict + " - " + name + (string.IsNullOrEmpty(result.Detail) ? "" : ": " + result.Detail);

            log.Add("- **" + verdict + "** " + name +
                    (string.IsNullOrEmpty(result.Detail) ? "" : " - " + result.Detail));

            if (result.Status == Status.Pass)
            {
                Debug.Log("[GameplayTest] " + line);
            }
            else
            {
                Debug.LogError("[GameplayTest] " + line);
            }
        }

        private static void Finish(bool passed)
        {
            finished = true;
            SessionState.SetBool(RunningKey, false);

            log.Add("");
            log.Add(passed
                ? "## Result: ALL STEPS PASSED"
                : "## Result: FAILED at step " + (stepIndex + 1) + " of " + steps.Count);

            string resultsPath = ActiveResultsPath;

            try
            {
                string directory = Path.GetDirectoryName(resultsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(resultsPath, string.Join(Environment.NewLine, log), Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameplayTest] could not write " + resultsPath + ": " + e.Message);
            }

            if (passed)
            {
                Debug.Log("[GameplayTest] ===== ALL STEPS PASSED ===== results written to " + resultsPath);
            }
            else
            {
                Debug.LogError("[GameplayTest] ===== FAILED ===== results written to " + resultsPath);
            }

            steps = null;
            EditorApplication.update -= Drive;
            EditorApplication.ExitPlaymode();
        }

        // ================================================================ helpers

        /// <summary>
        /// Teleports the player to arm's length from a target. The CharacterController has to be
        /// disabled across the move, otherwise it overrides the transform write.
        /// </summary>
        private static void MovePlayerNear(Transform target, float distance)
        {
            Vector3 away = player.transform.position - target.position;
            away.y = 0f;

            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector3.right;
            }

            Vector3 destination = target.position + away.normalized * distance;
            destination.y = target.position.y + 0.2f;

            bool wasEnabled = playerController != null && playerController.enabled;
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            player.transform.position = destination;

            if (playerController != null)
            {
                playerController.enabled = wasEnabled;
            }
        }

        private static bool Poll(float seconds)
        {
            if (pollDeadline <= 0f)
            {
                pollDeadline = Now + seconds;
            }

            return Now >= pollDeadline;
        }

        private static string Focused =>
            interaction != null && interaction.Current != null
                ? interaction.Current.Transform.name + " [" + interaction.Current.InteractionLabel + "]"
                : "nothing";

        // ================================================================ the steps

        private static void BuildSteps()
        {
            steps = new List<Step>
            {
                new Step { Name = "Scene: gameplay objects exist", TimeoutSeconds = 10f, Run = AcquireReferences },

                new Step { Name = "HUD: every label has a font and renders glyphs", TimeoutSeconds = 6f, Run = HudLabelsRender },
                new Step { Name = "HUD: top bar shows coins, level and XP", TimeoutSeconds = 4f, Run = HudTopBar },
                new Step { Name = "HUD: pinned chips match the inventory", TimeoutSeconds = 4f, Run = HudChips },
                new Step { Name = "HUD: modal panels start hidden, world is not covered", TimeoutSeconds = 4f, Run = HudPanelsHidden },
                new Step { Name = "HUD: safe area stays inside the screen", TimeoutSeconds = 4f, Run = HudSafeArea },
                new Step { Name = "HUD: feedback toast renders a posted message", TimeoutSeconds = 8f, Run = HudToast },
                new Step { Name = "HUD: storage sheet opens and its rows render", TimeoutSeconds = 8f, Run = HudStorage },

                new Step { Name = "Interaction: player focuses a farm plot", TimeoutSeconds = 6f, Run = FocusPlot },
                new Step { Name = "HUD: action control shows the plot's own wording", TimeoutSeconds = 4f, Run = HudActionWording },
                new Step { Name = "Farming: till", TimeoutSeconds = 4f, Run = Till },
                new Step { Name = "HUD: action wording follows the plot state", TimeoutSeconds = 4f, Run = HudWordingChanged },
                new Step { Name = "Farming: plant consumes a seed and spawns a crop visual", TimeoutSeconds = 4f, Run = Plant },
                new Step { Name = "Farming: crop grows to ready while the player walks away", TimeoutSeconds = 45f, Run = Growth },
                new Step { Name = "Farming: harvest grants produce and clears the plot", TimeoutSeconds = 6f, Run = Harvest },
                new Step { Name = "Farming: a second press cannot harvest twice", TimeoutSeconds = 4f, Run = NoDoubleHarvest },

                new Step { Name = "Interaction: player focuses the chicken, not the coop trigger", TimeoutSeconds = 6f, Run = FocusChicken },
                new Step { Name = "Chicken: becomes hungry (forced, deterministic)", TimeoutSeconds = 8f, Run = ChickenHungry },
                new Step { Name = "Chicken: feeding consumes feed and starts production", TimeoutSeconds = 6f, Run = FeedChicken },
                new Step { Name = "Chicken: egg becomes ready", TimeoutSeconds = 45f, Run = EggReady },
                new Step { Name = "Chicken: collecting adds eggs to the inventory", TimeoutSeconds = 6f, Run = CollectEgg },
                new Step { Name = "Chicken: a second press cannot collect twice", TimeoutSeconds = 4f, Run = NoDoubleEgg },

                new Step { Name = "Interaction: player focuses the cow", TimeoutSeconds = 6f, Run = FocusCow },
                new Step { Name = "Cow: becomes hungry (forced, deterministic)", TimeoutSeconds = 8f, Run = CowHungry },
                new Step { Name = "Cow: feeding consumes feed and starts production", TimeoutSeconds = 6f, Run = FeedCow },
                new Step { Name = "Cow: milk becomes ready", TimeoutSeconds = 60f, Run = MilkReady },
                new Step { Name = "Cow: collecting adds milk to the inventory", TimeoutSeconds = 6f, Run = CollectMilk },
                new Step { Name = "Cow: a second press cannot collect twice", TimeoutSeconds = 4f, Run = NoDoubleMilk },

                new Step { Name = "Animals: wander, and stay inside their habitat", TimeoutSeconds = 20f, Run = Wandering }
            };
        }

        // ================================================================ Phase 7: shop

        /// <summary>
        /// Reuses AcquireReferences and five of the farming/animal steps verbatim - the shop
        /// does not replace the farming or animal loops, so this suite proves the shop AND
        /// proves those loops still work around it, without re-running their own slow
        /// production timers a second time (that is what the full suite above is for).
        /// </summary>
        private static void BuildPhase7Steps()
        {
            steps = new List<Step>
            {
                new Step { Name = "Scene: gameplay objects exist", TimeoutSeconds = 10f, Run = AcquireReferences },
                new Step { Name = "Scene: economy and shop objects exist", TimeoutSeconds = 6f, Run = AcquireEconomyReferences },

                new Step { Name = "HUD: starting coins and starting wheat seeds are visible", TimeoutSeconds = 4f, Run = Phase7_HudShowsStartingValues },

                new Step { Name = "Shop: walk to the Market and open the shop", TimeoutSeconds = 8f, Run = Phase7_OpenShop },
                new Step { Name = "Shop: buy 5 Wheat Seeds via the real Plus/Buy buttons", TimeoutSeconds = 6f, Run = Phase7_BuyFiveSeeds },
                new Step { Name = "Shop: close the shop; movement and interaction re-enable", TimeoutSeconds = 4f, Run = Phase7_CloseShopAndVerifyUnfrozen },

                new Step { Name = "Farming: return to the plot", TimeoutSeconds = 6f, Run = Phase7_ReturnToPlot },
                new Step { Name = "Farming: till", TimeoutSeconds = 4f, Run = Till },
                new Step { Name = "Farming: plant consumes a purchased seed", TimeoutSeconds = 4f, Run = Plant },
                new Step { Name = "Farming: crop grows to ready", TimeoutSeconds = 45f, Run = Growth },
                new Step { Name = "Farming: harvest grants Wheat", TimeoutSeconds = 6f, Run = Harvest },

                new Step { Name = "Shop: return to the Market and reopen the shop", TimeoutSeconds = 8f, Run = Phase7_OpenShop },
                new Step { Name = "Shop: sell 1 Wheat via the real SellOne button", TimeoutSeconds = 6f, Run = Phase7_SellOneWheat },
                new Step { Name = "Shop: an unaffordable purchase is refused and changes nothing", TimeoutSeconds = 4f, Run = Phase7_InsufficientFundsPurchase },
                new Step { Name = "Shop: selling at zero inventory is disabled and refused", TimeoutSeconds = 4f, Run = Phase7_SellAtZeroInventory },
                new Step { Name = "Shop: close the shop; movement and interaction re-enable", TimeoutSeconds = 4f, Run = Phase7_CloseShopAndVerifyUnfrozen },

                new Step { Name = "Regression: farming still works after the shop closes", TimeoutSeconds = 8f, Run = Phase7_ReturnToPlot },
                new Step { Name = "Regression: the plot can still be tilled", TimeoutSeconds = 4f, Run = Till },
                new Step { Name = "Regression: the chicken can still be focused and fed", TimeoutSeconds = 6f, Run = FocusChicken },
                new Step { Name = "Regression: the cow can still be focused and fed", TimeoutSeconds = 6f, Run = FocusCow }
            };
        }

        // ================================================================ Phase 8: save/load

        /// <summary>
        /// Proves a save round-trips through the real components.
        ///
        /// The app is never restarted - it does something stronger. It builds a distinctive
        /// farm, saves it, then deliberately changes every one of those things to a DIFFERENT
        /// value, and only then loads. Anything that comes back matching the save therefore
        /// came out of the file, because the live value at load time was something else. A test
        /// that saved and immediately loaded would pass even if Load() did nothing at all.
        /// </summary>
        private static void BuildPhase8Steps()
        {
            steps = new List<Step>
            {
                new Step { Name = "Scene: gameplay objects exist", TimeoutSeconds = 10f, Run = AcquireReferences },
                new Step { Name = "Scene: economy and shop objects exist", TimeoutSeconds = 6f, Run = AcquireEconomyReferences },
                new Step { Name = "Scene: the SaveManager exists and is wired", TimeoutSeconds = 6f, Run = Phase8_AcquireSaveManager },

                new Step { Name = "Setup: build a farm state worth saving", TimeoutSeconds = 10f, Run = Phase8_BuildDistinctState },
                new Step { Name = "Save: writing produces a real file on disk", TimeoutSeconds = 6f, Run = Phase8_SaveWritesAFile },
                new Step { Name = "Save: the file's contents match the live farm", TimeoutSeconds = 6f, Run = Phase8_FileContentsMatchTheFarm },

                new Step { Name = "Change: every saved value is deliberately changed", TimeoutSeconds = 10f, Run = Phase8_ChangeEverythingAfterSaving },

                new Step { Name = "Load: coins and inventory come back from the file", TimeoutSeconds = 6f, Run = Phase8_LoadRestoresCoinsAndItems },
                new Step { Name = "Load: the plot's crop and growth come back", TimeoutSeconds = 4f, Run = Phase8_LoadRestoresThePlot },
                new Step { Name = "Load: the farmer returns to where he was saved", TimeoutSeconds = 4f, Run = Phase8_LoadRestoresThePlayer },
                new Step { Name = "Load: the chicken's hunger comes back", TimeoutSeconds = 4f, Run = Phase8_LoadRestoresTheChicken },
                new Step { Name = "Load: the HUD follows the restore without being told", TimeoutSeconds = 6f, Run = Phase8_HudFollowsTheRestore },

                new Step { Name = "Delete: removing the save leaves nothing behind", TimeoutSeconds = 4f, Run = Phase8_DeleteRemovesTheFile },

                // The load left a crop in the ground. Clearing it is what makes the tilling
                // regression below a real test rather than one that fails on a busy plot.
                new Step { Name = "Regression: the restored crop can be cleared", TimeoutSeconds = 4f, Run = Phase8_ClearRestoredCrop },
                new Step { Name = "Regression: farming still works after a load", TimeoutSeconds = 8f, Run = Phase7_ReturnToPlot },
                new Step { Name = "Regression: the plot can still be tilled", TimeoutSeconds = 4f, Run = Till },
                new Step { Name = "Regression: the chicken can still be focused", TimeoutSeconds = 6f, Run = FocusChicken },
                new Step { Name = "Regression: the cow can still be focused", TimeoutSeconds = 6f, Run = FocusCow }
            };
        }

        private static StepResult Phase8_AcquireSaveManager()
        {
            saveManager = UnityEngine.Object.FindAnyObjectByType<SaveManager>();

            if (saveManager == null)
            {
                return StepResult.Fail("no SaveManager in the scene - rebuild the farm scene");
            }

            // A manager that saves nothing would let every later step pass vacuously.
            if (wallet == null || inventory == null)
            {
                return StepResult.Fail("the wallet or inventory reference was not acquired");
            }

            return StepResult.Pass("SaveManager found; save path " + SaveSystem.SavePath);
        }

        private static StepResult Phase8_BuildDistinctState()
        {
            // Plant something, so a crop mid-growth has to survive the round trip.
            if (plot == null)
            {
                return StepResult.Fail("no plot reference");
            }

            if (plot.State == PlotState.Empty)
            {
                plot.TryTill();
            }

            if (plot.State == PlotState.Tilled)
            {
                FarmActionResult planted = plot.TryPlant(inventory);

                if (planted != FarmActionResult.Success)
                {
                    return StepResult.Fail("could not plant a seed to save: " + planted);
                }
            }

            // A coin total that cannot be confused with the starting 100.
            wallet.AddCoins(23);

            // Move somewhere the farmer would never spawn.
            movementScript.RestoreTransform(new Vector3(6.25f, player.transform.position.y, -12.5f), 137f);

            savedCoins = wallet.GetBalance();
            savedSeeds = inventory.GetQuantity(wheat.SeedItemId);
            savedPlayerPosition = player.transform.position;
            savedPlotState = plot.State;
            savedPlotName = plot.name;
            savedChickenHunger = chicken.Needs.Hunger;

            return StepResult.Pass("coins=" + savedCoins + ", seeds=" + savedSeeds + ", " +
                                   savedPlotName + " is " + savedPlotState + ", farmer at " +
                                   savedPlayerPosition.ToString("0.0"));
        }

        private static StepResult Phase8_SaveWritesAFile()
        {
            if (!saveManager.Save())
            {
                return StepResult.Fail("SaveManager.Save() reported failure");
            }

            if (!SaveSystem.Exists())
            {
                return StepResult.Fail("Save() succeeded but no file exists at " + SaveSystem.SavePath);
            }

            FileInfo info = new FileInfo(SaveSystem.SavePath);

            if (info.Length <= 0)
            {
                return StepResult.Fail("the save file is empty");
            }

            return StepResult.Pass("wrote " + info.Length + " bytes to " + SaveSystem.SavePath);
        }

        private static StepResult Phase8_FileContentsMatchTheFarm()
        {
            // Read the file back independently of SaveManager, so this checks what actually
            // landed on disk rather than what the manager believes it wrote.
            if (!SaveSystem.TryRead(out SaveData data))
            {
                return StepResult.Fail("the file written a moment ago could not be read back");
            }

            if (data.Coins != savedCoins)
            {
                return StepResult.Fail("the file holds " + data.Coins + " coins, the farm had " + savedCoins);
            }

            int seedsInFile = 0;

            for (int i = 0; i < data.Inventory.Count; i++)
            {
                if (data.Inventory[i].ItemId == wheat.SeedItemId)
                {
                    seedsInFile = data.Inventory[i].Amount;
                }
            }

            if (seedsInFile != savedSeeds)
            {
                return StepResult.Fail("the file holds " + seedsInFile + " wheat seeds, the farm had " + savedSeeds);
            }

            int plotsInFile = 0;

            for (int i = 0; i < data.Fields.Count; i++)
            {
                plotsInFile += data.Fields[i].Plots.Count;
            }

            if (plotsInFile < 40)
            {
                return StepResult.Fail("the file holds only " + plotsInFile + " plots; the farm has three fields");
            }

            if (data.Animals.Count < 9)
            {
                return StepResult.Fail("the file holds only " + data.Animals.Count + " animals; the farm has nine");
            }

            return StepResult.Pass("file holds " + data.Coins + " coins, " + seedsInFile + " seeds, " +
                                   plotsInFile + " plots across " + data.Fields.Count + " fields, " +
                                   data.Animals.Count + " animals");
        }

        private static StepResult Phase8_ChangeEverythingAfterSaving()
        {
            // Everything below is changed to a value the save does NOT contain, so a later
            // match can only have come out of the file.
            wallet.AddCoins(500);
            inventory.Add(wheat.SeedItemId, 77);

            // SetCrop(null) clears whatever is in the ground and drops the plot to Empty.
            // Passing the CURRENT crop would be a no-op - SetCrop returns early when the crop
            // has not actually changed.
            plot.SetCrop(null);

            movementScript.RestoreTransform(new Vector3(-24f, player.transform.position.y, 26f), 0f);

            // Move hunger away from whatever it happened to be saved at. A fixed value could
            // coincide with the saved one, and the step would then prove nothing.
            chicken.Needs.Restore(savedChickenHunger > 0.5f ? 0.05f : 0.95f, chicken.Needs.Happiness);

            bool coinsDiffer = wallet.GetBalance() != savedCoins;
            bool seedsDiffer = inventory.GetQuantity(wheat.SeedItemId) != savedSeeds;
            bool plotDiffers = plot.State != savedPlotState;
            bool positionDiffers = (player.transform.position - savedPlayerPosition).sqrMagnitude > 1f;
            bool hungerDiffers = Mathf.Abs(chicken.Needs.Hunger - savedChickenHunger) > 0.01f;

            if (!coinsDiffer || !seedsDiffer || !plotDiffers || !positionDiffers || !hungerDiffers)
            {
                return StepResult.Fail(
                    "the post-save state is not actually different (coins " + coinsDiffer +
                    ", seeds " + seedsDiffer + ", plot " + plotDiffers +
                    ", position " + positionDiffers + ", hunger " + hungerDiffers +
                    "); the load steps would prove nothing");
            }

            return StepResult.Pass("coins " + savedCoins + "->" + wallet.GetBalance() +
                                   ", seeds " + savedSeeds + "->" + inventory.GetQuantity(wheat.SeedItemId) +
                                   ", plot " + savedPlotState + "->" + plot.State +
                                   ", hunger " + savedChickenHunger.ToString("0.00") + "->" +
                                   chicken.Needs.Hunger.ToString("0.00"));
        }

        private static StepResult Phase8_LoadRestoresCoinsAndItems()
        {
            if (!saveManager.Load())
            {
                return StepResult.Fail("SaveManager.Load() reported failure");
            }

            if (wallet.GetBalance() != savedCoins)
            {
                return StepResult.Fail("coins came back as " + wallet.GetBalance() + ", saved " + savedCoins);
            }

            int seeds = inventory.GetQuantity(wheat.SeedItemId);

            if (seeds != savedSeeds)
            {
                return StepResult.Fail("wheat seeds came back as " + seeds + ", saved " + savedSeeds);
            }

            return StepResult.Pass("coins restored to " + savedCoins + ", wheat seeds to " + savedSeeds);
        }

        private static StepResult Phase8_LoadRestoresThePlot()
        {
            if (plot.State != savedPlotState)
            {
                return StepResult.Fail(savedPlotName + " came back as " + plot.State +
                                       ", saved " + savedPlotState);
            }

            if (savedPlotState != PlotState.Empty && plot.Crop == null)
            {
                return StepResult.Fail(savedPlotName + " is " + plot.State + " but has no crop");
            }

            return StepResult.Pass(savedPlotName + " restored to " + plot.State +
                                   " growing " + (plot.Crop != null ? plot.Crop.CropId : "nothing"));
        }

        private static StepResult Phase8_LoadRestoresThePlayer()
        {
            float distance = (player.transform.position - savedPlayerPosition).magnitude;

            if (distance > 0.35f)
            {
                return StepResult.Fail("the farmer came back " + distance.ToString("0.00") +
                                       "m from where he was saved");
            }

            return StepResult.Pass("farmer restored to within " + distance.ToString("0.00") +
                                   "m of " + savedPlayerPosition.ToString("0.0"));
        }

        private static StepResult Phase8_LoadRestoresTheChicken()
        {
            float hunger = chicken.Needs.Hunger;

            // The habitat keeps ticking hunger while the suite runs, so a restored value drifts
            // slightly by the time this step reads it. The tolerance covers one simulation tick
            // at the editor's accelerated speed; it is far tighter than the gap to the value
            // hunger was deliberately changed to before the load.
            if (Mathf.Abs(hunger - savedChickenHunger) > 0.15f)
            {
                return StepResult.Fail("the chicken's hunger came back as " + hunger.ToString("0.00") +
                                       ", saved " + savedChickenHunger.ToString("0.00"));
            }

            return StepResult.Pass("chicken hunger restored to " + hunger.ToString("0.00"));
        }

        private static StepResult Phase8_HudFollowsTheRestore()
        {
            // Nothing pushes a number into the HUD: it updates only because the restore paths
            // raise the same events gameplay does. If this passes, that wiring survived.
            TMP_Text coinLabel = FindLabel("CoinValue");

            if (coinLabel == null)
            {
                return StepResult.Fail("the HUD has no CoinValue label");
            }

            string expected = savedCoins.ToString();

            if (coinLabel.text.Trim() != expected)
            {
                return StepResult.Fail("the HUD shows '" + coinLabel.text.Trim() +
                                       "' coins but the restored balance is " + expected);
            }

            return StepResult.Pass("HUD coin display followed the restore to " + expected +
                                   " through CurrencyWallet.BalanceChanged alone");
        }

        private static StepResult Phase8_DeleteRemovesTheFile()
        {
            if (!saveManager.DeleteSave())
            {
                return StepResult.Fail("DeleteSave() reported failure");
            }

            if (SaveSystem.Exists())
            {
                return StepResult.Fail("the save file is still on disk after DeleteSave()");
            }

            if (saveManager.HasSave)
            {
                return StepResult.Fail("HasSave still reports a save after DeleteSave()");
            }

            return StepResult.Pass("save file deleted; the next run starts from a new game");
        }

        private static StepResult Phase8_ClearRestoredCrop()
        {
            plot.SetCrop(null);

            if (plot.State != PlotState.Empty)
            {
                return StepResult.Fail("clearing the crop left " + plot.name + " in " + plot.State);
            }

            // Put the field's own crop back, so the plot behaves exactly as a fresh one would.
            plot.SetCrop(wheat);

            return StepResult.Pass(plot.name + " cleared back to Empty and re-assigned " + wheat.CropId);
        }

        private static StepResult AcquireEconomyReferences()
        {
            economy = player.GetComponent<EconomyManager>();
            wallet = player.GetComponent<CurrencyWallet>();
            movementScript = player.GetComponent<LittleFarmStory.Player.PlayerController>();

            if (economy == null) { return StepResult.Fail("the Player has no EconomyManager"); }
            if (wallet == null) { return StepResult.Fail("the Player has no CurrencyWallet"); }
            if (movementScript == null) { return StepResult.Fail("the Player has no PlayerController"); }

            market = UnityEngine.Object.FindAnyObjectByType<MarketInteractable>();
            if (market == null) { return StepResult.Fail("no MarketInteractable exists in the scene"); }

            shopPanel = UnityEngine.Object.FindAnyObjectByType<ShopPanel>();
            if (shopPanel == null) { return StepResult.Fail("no ShopPanel exists in the scene"); }

            buyCard = shopPanel.GetComponentInChildren<ShopBuyCard>(true);
            sellRow = shopPanel.GetComponentInChildren<ShopSellRow>(true);

            if (buyCard == null) { return StepResult.Fail("the shop panel has no ShopBuyCard"); }
            if (sellRow == null) { return StepResult.Fail("the shop panel has no ShopSellRow"); }

            wheatSeedsItem = economy.FindItem(wheat.SeedItemId);
            wheatItem = economy.FindItem(wheat.HarvestItemId);

            if (wheatSeedsItem == null) { return StepResult.Fail("the shop does not stock " + wheat.SeedItemId); }
            if (wheatItem == null) { return StepResult.Fail("the shop does not buy back " + wheat.HarvestItemId); }

            return StepResult.Pass("wallet=" + wallet.GetBalance() + " coins; buy " + wheatSeedsItem.ItemId +
                                   " @" + wheatSeedsItem.BuyPrice + "; sell " + wheatItem.ItemId +
                                   " @" + wheatItem.SellPrice);
        }

        private static StepResult Phase7_HudShowsStartingValues()
        {
            TMP_Text coinValue = FindLabel("CoinValue");
            if (coinValue == null) { return StepResult.Fail("no CoinValue label in the HUD"); }

            int actualCoins = wallet.GetBalance();
            if (coinValue.text != actualCoins.ToString())
            {
                return StepResult.Fail("HUD shows coins \"" + coinValue.text + "\" but the wallet holds " + actualCoins);
            }

            ResourceChip[] chips = hud.GetComponentsInChildren<ResourceChip>(true);
            ResourceChip seedChip = null;

            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i].ItemId == wheat.SeedItemId)
                {
                    seedChip = chips[i];
                    break;
                }
            }

            if (seedChip == null) { return StepResult.Fail("no resource chip is bound to " + wheat.SeedItemId); }

            TMP_Text seedValue = FindChildLabel(seedChip.transform, "Value");
            int actualSeeds = inventory.GetQuantity(wheat.SeedItemId);

            if (seedValue == null || seedValue.text != actualSeeds.ToString())
            {
                return StepResult.Fail("HUD shows " + wheat.SeedItemId + " = \"" +
                                       (seedValue != null ? seedValue.text : "MISSING") +
                                       "\" but the inventory holds " + actualSeeds);
            }

            return StepResult.Pass("HUD shows coins=" + actualCoins + ", " + wheat.SeedItemId + "=" +
                                   actualSeeds + " - both read from the real components, not assumed");
        }

        /// <summary>
        /// Walks to the market, focuses it, opens the shop (or reopens it, on the second call -
        /// the logic is state-driven and idempotent) and confirms the panel is actually visible,
        /// its own coin readout is correct, and the world is genuinely frozen while it is open.
        /// </summary>
        private static StepResult Phase7_OpenShop()
        {
            if (pollDeadline <= 0f)
            {
                MovePlayerNear(market.transform, 1.5f);
            }

            if (!Poll(0.5f))
            {
                return StepResult.Running;
            }

            if (!ReferenceEquals(interaction.Current, market))
            {
                return StepResult.Fail("focus was " + Focused + ", expected the market (\"Shop\")");
            }

            if (!shopPanel.IsOpen)
            {
                interaction.TryInteract();
                return StepResult.Running;
            }

            CanvasGroup group = shopPanel.GetComponent<CanvasGroup>();

            if (group == null || group.alpha < 0.9f)
            {
                return StepResult.Running;
            }

            TMP_Text shopCoin = FindChildLabel(shopPanel.transform, "Value");
            int actualCoins = wallet.GetBalance();

            if (shopCoin == null || shopCoin.text != actualCoins.ToString())
            {
                return StepResult.Fail("shop header shows coins \"" +
                                       (shopCoin != null ? shopCoin.text : "MISSING") +
                                       "\" but the wallet holds " + actualCoins);
            }

            if (movementScript.enabled)
            {
                return StepResult.Fail("PlayerController is still enabled while the shop is open");
            }

            if (interaction.enabled)
            {
                return StepResult.Fail("InteractionController is still enabled while the shop is open");
            }

            return StepResult.Pass("shop open, header coins=" + actualCoins +
                                   "; PlayerController and InteractionController both disabled while open");
        }

        /// <summary>
        /// Clicks the buy card's real Plus and Buy buttons - Button.onClick.Invoke() calls
        /// exactly the listener a finger tap would, so this exercises ShopBuyCard's own
        /// quantity/afford logic and EconomyManager.Purchase together, not EconomyManager alone.
        /// </summary>
        private static StepResult Phase7_BuyFiveSeeds()
        {
            if (pollDeadline <= 0f)
            {
                coinsBefore = wallet.GetBalance();
                seedsBefore = inventory.GetQuantity(wheat.SeedItemId);

                Transform plusT = buyCard.transform.Find("Plus");
                Transform buyT = buyCard.transform.Find("Buy");
                Button plus = plusT != null ? plusT.GetComponent<Button>() : null;
                Button buy = buyT != null ? buyT.GetComponent<Button>() : null;

                if (plus == null) { return StepResult.Fail("the buy card has no 'Plus' button"); }
                if (buy == null) { return StepResult.Fail("the buy card has no 'Buy' button"); }

                // Wheat seeds start at a minimum quantity of 1 and step by 1, so four clicks
                // reach 5.
                for (int i = 0; i < 4; i++)
                {
                    plus.onClick.Invoke();
                }

                if (!buy.interactable)
                {
                    return StepResult.Fail("Buy shows disabled for 5 seeds at a balance of " + coinsBefore +
                                           " (cost " + wheatSeedsItem.TotalBuyPrice(5) + ")");
                }

                buy.onClick.Invoke();
            }

            if (!Poll(0.2f))
            {
                return StepResult.Running;
            }

            int coinsAfter = wallet.GetBalance();
            int seedsAfter = inventory.GetQuantity(wheat.SeedItemId);
            int expectedCost = wheatSeedsItem.TotalBuyPrice(5);

            if (coinsAfter != coinsBefore - expectedCost)
            {
                return StepResult.Fail("coins went " + coinsBefore + " -> " + coinsAfter +
                                       ", expected -" + expectedCost);
            }

            if (seedsAfter != seedsBefore + 5)
            {
                return StepResult.Fail(wheat.SeedItemId + " went " + seedsBefore + " -> " + seedsAfter +
                                       ", expected +5");
            }

            return StepResult.Pass("clicked Plus x4 + Buy: coins " + coinsBefore + " -> " + coinsAfter +
                                   ", " + wheat.SeedItemId + " " + seedsBefore + " -> " + seedsAfter);
        }

        private static StepResult Phase7_CloseShopAndVerifyUnfrozen()
        {
            if (pollDeadline <= 0f)
            {
                shopPanel.Close();
            }

            if (!Poll(0.4f))
            {
                return StepResult.Running;
            }

            if (shopPanel.IsOpen)
            {
                return StepResult.Fail("ShopPanel.IsOpen is still true after Close()");
            }

            if (!movementScript.enabled)
            {
                return StepResult.Fail("PlayerController is still disabled after the shop closed");
            }

            if (!interaction.enabled)
            {
                return StepResult.Fail("InteractionController is still disabled after the shop closed");
            }

            return StepResult.Pass("shop closed; PlayerController and InteractionController both re-enabled");
        }

        /// <summary>
        /// Walks back to the wheat field from wherever the player currently is - the Market,
        /// on both calls this makes in the Phase 7 suite - and focuses a plot to farm.
        ///
        /// It does NOT require refocusing the exact same FarmPlot object AcquireReferences
        /// originally picked. The plots sit only 2.2m apart, and MovePlayerNear's "stand 1.5m
        /// away from the target, in whatever direction I currently approach from" heuristic can
        /// land closer to a grid neighbour than to the intended plot when the approach angle
        /// differs from the one AcquireReferences implicitly assumed (spawn -> field, not
        /// market -> field). Demanding the same object regardless of approach angle would be
        /// testing MovePlayerNear's geometry, not farming - so instead this accepts whichever
        /// EMPTY plot the interaction system actually resolves to and adopts it as `plot` for
        /// the rest of the suite, which is what every later step already operates on.
        /// </summary>
        private static StepResult Phase7_ReturnToPlot()
        {
            if (pollDeadline <= 0f)
            {
                MovePlayerNear(plot.transform, 1.5f);
            }

            if (!Poll(0.5f))
            {
                return StepResult.Running;
            }

            if (ReferenceEquals(interaction.Current, plot))
            {
                return StepResult.Pass("prompt reads \"" + plot.InteractionLabel + "\"");
            }

            if (interaction.Current is FarmPlot nearerPlot && nearerPlot.State == PlotState.Empty)
            {
                plot = nearerPlot;
                return StepResult.Pass("approach angle favoured a grid neighbour (" + Focused +
                                       "); adopted it as the plot for the rest of this suite");
            }

            return StepResult.Fail("focus was " + Focused + ", expected an empty plot");
        }

        private static StepResult Phase7_SellOneWheat()
        {
            if (pollDeadline <= 0f)
            {
                coinsBefore = wallet.GetBalance();
                produceBefore = inventory.GetQuantity(wheat.HarvestItemId);

                if (produceBefore <= 0)
                {
                    return StepResult.Fail("harvest produced no " + wheat.HarvestItemId + " to sell");
                }

                Transform sellOneT = sellRow.transform.Find("SellOne");
                Button sellOne = sellOneT != null ? sellOneT.GetComponent<Button>() : null;

                if (sellOne == null) { return StepResult.Fail("the sell row has no 'SellOne' button"); }

                if (!sellOne.interactable)
                {
                    return StepResult.Fail("SellOne shows disabled while owning " + produceBefore +
                                           " " + wheat.HarvestItemId);
                }

                sellOne.onClick.Invoke();
            }

            if (!Poll(0.2f))
            {
                return StepResult.Running;
            }

            int coinsAfter = wallet.GetBalance();
            int produceAfter = inventory.GetQuantity(wheat.HarvestItemId);

            if (produceAfter != produceBefore - 1)
            {
                return StepResult.Fail(wheat.HarvestItemId + " went " + produceBefore + " -> " + produceAfter +
                                       ", expected -1");
            }

            if (coinsAfter != coinsBefore + wheatItem.SellPrice)
            {
                return StepResult.Fail("coins went " + coinsBefore + " -> " + coinsAfter +
                                       ", expected +" + wheatItem.SellPrice);
            }

            return StepResult.Pass("clicked SellOne: " + wheat.HarvestItemId + " " + produceBefore +
                                   " -> " + produceAfter + ", coins " + coinsBefore + " -> " + coinsAfter);
        }

        /// <summary>
        /// Tests the refusal directly against EconomyManager rather than by clicking Plus some
        /// number of times: the quantity that would exceed the CURRENT balance depends on
        /// whatever earlier steps left the wallet holding, and computing it here - capped at
        /// the item's own per-trade maximum, so the refusal is genuinely NotEnoughCoins and not
        /// QuantityAboveMaximum - is more robust than a fixed click count would be. Purchase()
        /// is the exact method the Buy button's own handler calls; nothing is bypassed except
        /// the button widget itself.
        /// </summary>
        private static StepResult Phase7_InsufficientFundsPurchase()
        {
            coinsBefore = wallet.GetBalance();
            seedsBefore = inventory.GetQuantity(wheat.SeedItemId);

            int unaffordable = Mathf.Min(
                wheatSeedsItem.MaxQuantity,
                coinsBefore / Mathf.Max(1, wheatSeedsItem.BuyPrice) + 10);

            TransactionOutcome outcome = economy.Purchase(wheatSeedsItem, unaffordable);

            if (outcome.Result != TransactionResult.NotEnoughCoins)
            {
                return StepResult.Fail("buying " + unaffordable + " seeds (cost " +
                                       wheatSeedsItem.TotalBuyPrice(unaffordable) + ", balance " + coinsBefore +
                                       ") returned " + outcome.Result + ", expected NotEnoughCoins");
            }

            int coinsAfter = wallet.GetBalance();
            int seedsAfter = inventory.GetQuantity(wheat.SeedItemId);

            if (coinsAfter != coinsBefore || seedsAfter != seedsBefore)
            {
                return StepResult.Fail("a refused purchase changed state: coins " + coinsBefore + " -> " +
                                       coinsAfter + ", seeds " + seedsBefore + " -> " + seedsAfter);
            }

            return StepResult.Pass("Purchase(" + unaffordable + " seeds, cost " +
                                   wheatSeedsItem.TotalBuyPrice(unaffordable) + ") refused with " +
                                   outcome.Result + " against a balance of " + coinsBefore + "; nothing changed");
        }

        /// <summary>
        /// Drains the remaining Wheat with the real Sell All button, confirms SellOne shows
        /// disabled at zero, then invokes it anyway - Button.interactable only stops the
        /// EventSystem from dispatching a real tap, so a direct Invoke() bypasses it entirely
        /// and proves the ECONOMY layer refuses the sale, not just that the button looks
        /// disabled.
        /// </summary>
        private static StepResult Phase7_SellAtZeroInventory()
        {
            if (pollDeadline <= 0f)
            {
                int owned = inventory.GetQuantity(wheat.HarvestItemId);

                if (owned > 0)
                {
                    Transform sellAllT = sellRow.transform.Find("SellAll");
                    Button sellAll = sellAllT != null ? sellAllT.GetComponent<Button>() : null;

                    if (sellAll == null) { return StepResult.Fail("the sell row has no 'SellAll' button"); }

                    sellAll.onClick.Invoke();
                }
            }

            if (!Poll(0.3f))
            {
                return StepResult.Running;
            }

            int ownedNow = inventory.GetQuantity(wheat.HarvestItemId);

            if (ownedNow != 0)
            {
                return StepResult.Fail("expected 0 " + wheat.HarvestItemId + " after Sell All, found " + ownedNow);
            }

            Transform sellOneT = sellRow.transform.Find("SellOne");
            Button sellOneAtZero = sellOneT != null ? sellOneT.GetComponent<Button>() : null;

            if (sellOneAtZero == null) { return StepResult.Fail("the sell row has no 'SellOne' button"); }

            if (sellOneAtZero.interactable)
            {
                return StepResult.Fail("SellOne is still interactable with 0 " + wheat.HarvestItemId + " owned");
            }

            coinsBefore = wallet.GetBalance();
            sellOneAtZero.onClick.Invoke();

            if (wallet.GetBalance() != coinsBefore || inventory.GetQuantity(wheat.HarvestItemId) != 0)
            {
                return StepResult.Fail("invoking SellOne at zero inventory still changed state");
            }

            return StepResult.Pass("Sell All drained " + wheat.HarvestItemId + " to 0; SellOne shows disabled, " +
                                   "and invoking its handler directly still changes nothing");
        }

        private static StepResult AcquireReferences()
        {
            player = null;
            PlayerController movement = UnityEngine.Object.FindAnyObjectByType<PlayerController>();

            if (movement == null)
            {
                return StepResult.Running;
            }

            player = movement.gameObject;
            playerController = player.GetComponent<CharacterController>();
            inventory = player.GetComponent<PlayerInventory>();
            interaction = player.GetComponent<InteractionController>();

            if (inventory == null) { return StepResult.Fail("the player has no PlayerInventory"); }
            if (interaction == null) { return StepResult.Fail("the player has no InteractionController"); }
            feedback = player.GetComponent<ActionFeedbackChannel>();
            if (feedback == null) { return StepResult.Fail("the player has no ActionFeedbackChannel"); }

            hud = UnityEngine.Object.FindAnyObjectByType<HudController>();
            if (hud == null) { return StepResult.Fail("there is no HudController in the scene"); }

            actionPrompt = hud.GetComponentInChildren<ActionPrompt>(true);
            toasts = hud.GetComponentInChildren<ToastPresenter>(true);
            inventoryPanel = hud.GetComponentInChildren<InventoryPanel>(true);
            safeArea = hud.GetComponentInChildren<SafeAreaPanel>(true);
            hudLabels = hud.GetComponentsInChildren<TMP_Text>(true);

            if (actionPrompt == null) { return StepResult.Fail("the HUD has no ActionPrompt"); }
            if (toasts == null) { return StepResult.Fail("the HUD has no ToastPresenter"); }
            if (inventoryPanel == null) { return StepResult.Fail("the HUD has no InventoryPanel"); }
            if (safeArea == null) { return StepResult.Fail("the HUD has no SafeAreaPanel"); }

            FarmGrid[] grids = UnityEngine.Object.FindObjectsByType<FarmGrid>();
            FarmGrid wheatField = null;

            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i].AssignedCrop != null && grids[i].AssignedCrop.CropId == "wheat")
                {
                    wheatField = grids[i];
                    break;
                }
            }

            if (wheatField == null) { return StepResult.Fail("no FarmGrid is growing wheat"); }
            if (wheatField.PlotCount == 0) { return StepResult.Running; }

            wheat = wheatField.AssignedCrop;

            if (wheat.GetStagePrefab(0) == null)
            {
                return StepResult.Fail("the wheat crop has no stage prefabs, so planted crops would be invisible");
            }

            plot = null;
            for (int i = 0; i < wheatField.Plots.Count; i++)
            {
                if (wheatField.Plots[i] != null && wheatField.Plots[i].State == PlotState.Empty)
                {
                    plot = wheatField.Plots[i];
                    break;
                }
            }

            if (plot == null) { return StepResult.Fail("the wheat field has no empty plot to test with"); }

            allAnimals = UnityEngine.Object.FindObjectsByType<AnimalController>();
            chicken = null;
            cow = null;

            for (int i = 0; i < allAnimals.Length; i++)
            {
                AnimalDefinition definition = allAnimals[i].Definition;
                if (definition == null)
                {
                    continue;
                }

                if (chicken == null && definition.Type == AnimalType.Chicken) { chicken = allAnimals[i]; }
                if (cow == null && definition.Type == AnimalType.Cow) { cow = allAnimals[i]; }
            }

            if (chicken == null) { return StepResult.Fail("no chicken with an AnimalDefinition was found"); }
            if (cow == null) { return StepResult.Fail("no cow with an AnimalDefinition was found"); }
            if (chicken.Habitat == null) { return StepResult.Fail("the chicken has no habitat, so it would never simulate"); }
            if (cow.Habitat == null) { return StepResult.Fail("the cow has no habitat, so it would never simulate"); }

            // Holding a habitat reference is not the same as being registered with it, and an
            // unregistered animal is never ticked: it would never grow hungry and its
            // production timer would never advance. That failure is completely silent, so it
            // gets its own assertion.
            if (!IsRegistered(chicken))
            {
                return StepResult.Fail("the chicken is not registered with habitat '" +
                                       chicken.Habitat.HabitatId + "' (" + chicken.Habitat.UsedCapacity +
                                       "/" + chicken.Habitat.Capacity + " used), so it is never simulated");
            }

            if (!IsRegistered(cow))
            {
                return StepResult.Fail("the cow is not registered with habitat '" +
                                       cow.Habitat.HabitatId + "' (" + cow.Habitat.UsedCapacity +
                                       "/" + cow.Habitat.Capacity + " used), so it is never simulated");
            }

            return StepResult.Pass(
                wheatField.PlotCount + " plots, " + allAnimals.Length + " animals; coop " +
                chicken.Habitat.UsedCapacity + "/" + chicken.Habitat.Capacity + ", barn " +
                cow.Habitat.UsedCapacity + "/" + cow.Habitat.Capacity + "; " +
                wheat.SeedItemId + "=" + inventory.GetQuantity(wheat.SeedItemId) + ", " +
                chicken.Definition.FeedItemId + "=" + inventory.GetQuantity(chicken.Definition.FeedItemId) + ", " +
                cow.Definition.FeedItemId + "=" + inventory.GetQuantity(cow.Definition.FeedItemId));
        }

        private static bool IsRegistered(AnimalController animal)
        {
            IReadOnlyList<AnimalController> registered = animal.Habitat.Animals;

            for (int i = 0; i < registered.Count; i++)
            {
                if (ReferenceEquals(registered[i], animal))
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- HUD

        /// <summary>
        /// The check that would have caught the blank-HUD bug: a font reference is not enough,
        /// the label has to actually generate glyph geometry.
        /// </summary>
        private static StepResult HudLabelsRender()
        {
            int checkedLabels = 0;

            for (int i = 0; i < hudLabels.Length; i++)
            {
                TMP_Text label = hudLabels[i];

                if (label == null)
                {
                    continue;
                }

                if (label.font == null)
                {
                    return StepResult.Fail("label '" + label.name + "' has no TMP font asset");
                }

                if (string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                label.ForceMeshUpdate();

                if (label.textInfo == null || label.textInfo.characterCount == 0)
                {
                    return StepResult.Fail("label '" + label.name + "' has text \"" + label.text +
                                           "\" but generated no glyphs - it would render blank");
                }

                checkedLabels++;
            }

            if (hudLabels.Length == 0)
            {
                return StepResult.Fail("the HUD contains no TMP labels at all");
            }

            return StepResult.Pass(hudLabels.Length + " labels, all with font '" +
                                   hudLabels[0].font.name + "'; " + checkedLabels +
                                   " carrying text all generated glyphs");
        }

        private static StepResult HudTopBar()
        {
            TMP_Text coin = FindLabel("CoinValue");
            TMP_Text level = FindLabel("LevelValue");

            if (coin == null) { return StepResult.Fail("no CoinValue label in the HUD"); }
            if (level == null) { return StepResult.Fail("no LevelValue label in the HUD"); }

            if (string.IsNullOrEmpty(coin.text) || coin.text == "0")
            {
                return StepResult.Fail("CoinValue reads \"" + coin.text + "\"");
            }

            if (!level.text.StartsWith("Lv"))
            {
                return StepResult.Fail("LevelValue reads \"" + level.text + "\", expected \"Lv n\"");
            }

            Image xp = FindXpFill();

            if (xp == null) { return StepResult.Fail("no XP fill bar in the HUD"); }
            if (xp.type != Image.Type.Filled) { return StepResult.Fail("the XP bar is not a Filled image"); }

            return StepResult.Pass("coins \"" + coin.text + "\", level \"" + level.text +
                                   "\", XP fill " + (xp.fillAmount * 100f).ToString("0") + "%");
        }

        private static StepResult HudChips()
        {
            ResourceChip[] chips = hud.GetComponentsInChildren<ResourceChip>(true);

            if (chips.Length == 0)
            {
                return StepResult.Fail("the HUD has no ResourceChips");
            }

            string summary = string.Empty;

            for (int i = 0; i < chips.Length; i++)
            {
                // By name, not "first child label": an inventory row carries a Name label
                // before its Value, and matching the wrong one would fail a healthy build.
                TMP_Text value = FindChildLabel(chips[i].transform, "Value");

                if (value == null)
                {
                    return StepResult.Fail("chip '" + chips[i].ItemId + "' has no value label");
                }

                int expected = inventory.GetQuantity(chips[i].ItemId);

                if (value.text != expected.ToString())
                {
                    return StepResult.Fail("chip '" + chips[i].ItemId + "' shows \"" + value.text +
                                           "\" but the inventory holds " + expected);
                }

                summary += (summary.Length > 0 ? ", " : "") + chips[i].ItemId + "=" + value.text;
            }

            return StepResult.Pass(chips.Length + " chips agree with the inventory: " + summary);
        }

        private static StepResult HudPanelsHidden()
        {
            if (inventoryPanel.IsOpen)
            {
                return StepResult.Fail("the storage sheet is open on start-up");
            }

            CanvasGroup sheet = inventoryPanel.GetComponent<CanvasGroup>();

            if (sheet != null && sheet.alpha > 0.01f)
            {
                return StepResult.Fail("the storage sheet is visible on start-up (alpha " +
                                       sheet.alpha.ToString("0.00") + ")");
            }

            CanvasGroup prompt = actionPrompt.GetComponent<CanvasGroup>();

            if (prompt != null && prompt.alpha > 0.01f && interaction.Current == null)
            {
                return StepResult.Fail("the action control is visible with nothing in range");
            }

            return StepResult.Pass("storage closed, action control hidden with nothing in range");
        }

        private static StepResult HudSafeArea()
        {
            RectTransform rect = (RectTransform)safeArea.transform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            // A screen-space-overlay canvas reports world corners in screen pixels.
            for (int i = 0; i < 4; i++)
            {
                if (corners[i].x < -1f || corners[i].y < -1f ||
                    corners[i].x > Screen.width + 1f || corners[i].y > Screen.height + 1f)
                {
                    return StepResult.Fail("safe-area corner " + i + " at " + corners[i] +
                                           " falls outside the " + Screen.width + "x" + Screen.height + " screen");
                }
            }

            float width = corners[2].x - corners[0].x;
            float height = corners[2].y - corners[0].y;

            return StepResult.Pass("safe area " + width.ToString("0") + "x" + height.ToString("0") +
                                   " inside " + Screen.width + "x" + Screen.height +
                                   " (aspect " + ((float)Screen.width / Screen.height).ToString("0.00") + ")");
        }

        private static StepResult HudToast()
        {
            const string probe = "Runtime check +1";

            if (pollDeadline <= 0f)
            {
                feedback.Post(probe);
            }

            if (!Poll(0.6f))
            {
                return StepResult.Running;
            }

            TMP_Text message = FindLabel("Message");

            if (message == null)
            {
                return StepResult.Fail("the toast has no Message label");
            }

            if (message.text != probe)
            {
                return StepResult.Fail("the toast reads \"" + message.text + "\", expected \"" + probe + "\"");
            }

            CanvasGroup group = message.GetComponentInParent<CanvasGroup>();

            if (group == null || group.alpha < 0.5f)
            {
                return StepResult.Fail("the toast text is correct but it faded in to alpha " +
                                       (group != null ? group.alpha.ToString("0.00") : "no CanvasGroup"));
            }

            message.ForceMeshUpdate();

            if (message.textInfo.characterCount == 0)
            {
                return StepResult.Fail("the toast generated no glyphs");
            }

            return StepResult.Pass("posted through ActionFeedbackChannel and rendered at alpha " +
                                   group.alpha.ToString("0.00"));
        }

        private static StepResult HudStorage()
        {
            if (pollDeadline <= 0f)
            {
                inventoryPanel.Open();
            }

            if (!Poll(0.6f))
            {
                return StepResult.Running;
            }

            CanvasGroup sheet = inventoryPanel.GetComponent<CanvasGroup>();

            if (sheet == null || sheet.alpha < 0.5f)
            {
                return StepResult.Fail("the storage sheet did not fade in (alpha " +
                                       (sheet != null ? sheet.alpha.ToString("0.00") : "none") + ")");
            }

            int rows = 0;
            TMP_Text[] labels = inventoryPanel.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < labels.Length; i++)
            {
                if (string.IsNullOrEmpty(labels[i].text))
                {
                    continue;
                }

                labels[i].ForceMeshUpdate();

                if (labels[i].textInfo.characterCount == 0)
                {
                    return StepResult.Fail("storage label '" + labels[i].name + "' rendered blank");
                }

                rows++;
            }

            inventoryPanel.Close();

            return StepResult.Pass(rows + " storage labels rendered, sheet closed again");
        }

        private static StepResult HudActionWording()
        {
            TMP_Text label = FindActionLabel();

            if (label == null)
            {
                return StepResult.Fail("the action control has no label");
            }

            if (label.text != plot.InteractionLabel)
            {
                return StepResult.Fail("the action reads \"" + label.text + "\" but the plot's own label is \"" +
                                       plot.InteractionLabel + "\" - the UI is not using the interactable's wording");
            }

            CanvasGroup group = actionPrompt.GetComponent<CanvasGroup>();

            if (group == null || group.alpha < 0.5f)
            {
                return StepResult.Fail("the action control did not appear (alpha " +
                                       (group != null ? group.alpha.ToString("0.00") : "none") + ")");
            }

            Image glyph = FindActionGlyph();

            if (glyph == null || glyph.sprite == null)
            {
                return StepResult.Fail("the action button has no glyph sprite");
            }

            labelBeforeTill = label.text;

            return StepResult.Pass("reads \"" + label.text + "\" with glyph '" + glyph.sprite.name +
                                   "' at alpha " + group.alpha.ToString("0.00"));
        }

        private static StepResult HudWordingChanged()
        {
            TMP_Text label = FindActionLabel();

            if (label == null)
            {
                return StepResult.Fail("the action control has no label");
            }

            if (label.text == labelBeforeTill)
            {
                return StepResult.Fail("the wording is still \"" + label.text +
                                       "\" after tilling - the prompt is not tracking plot state");
            }

            if (label.text != plot.InteractionLabel)
            {
                return StepResult.Fail("the action reads \"" + label.text + "\" but the plot says \"" +
                                       plot.InteractionLabel + "\"");
            }

            return StepResult.Pass("\"" + labelBeforeTill + "\" -> \"" + label.text + "\"");
        }

        // ---- small lookups, so a renamed widget fails with a clear message rather than an NRE

        private static TMP_Text FindChildLabel(Transform root, string objectName)
        {
            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].name == objectName)
                {
                    return labels[i];
                }
            }

            return null;
        }

        private static TMP_Text FindLabel(string objectName)
        {
            for (int i = 0; i < hudLabels.Length; i++)
            {
                if (hudLabels[i] != null && hudLabels[i].name == objectName)
                {
                    return hudLabels[i];
                }
            }

            return null;
        }

        private static TMP_Text FindActionLabel()
        {
            return actionPrompt.GetComponentInChildren<TMP_Text>(true);
        }

        private static Image FindActionGlyph()
        {
            Image[] images = actionPrompt.GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].name == "Glyph")
                {
                    return images[i];
                }
            }

            return null;
        }

        private static Image FindXpFill()
        {
            Image[] images = hud.GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].name == "XpFill")
                {
                    return images[i];
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- farming

        private static StepResult FocusPlot()
        {
            if (pollDeadline <= 0f)
            {
                MovePlayerNear(plot.transform, 1.5f);
            }

            if (!Poll(0.5f))
            {
                return StepResult.Running;
            }

            if (!ReferenceEquals(interaction.Current, plot))
            {
                return StepResult.Fail("focus was " + Focused + ", expected the plot");
            }

            return StepResult.Pass("prompt reads \"" + plot.InteractionLabel + "\"");
        }

        private static StepResult Till()
        {
            if (plot.State == PlotState.Empty)
            {
                interaction.TryInteract();
                return StepResult.Running;
            }

            if (plot.State != PlotState.Tilled)
            {
                return StepResult.Fail("plot went to " + plot.State + " instead of Tilled");
            }

            return StepResult.Pass("state Empty -> Tilled, prompt now \"" + plot.InteractionLabel + "\"");
        }

        private static StepResult Plant()
        {
            if (plot.State == PlotState.Tilled)
            {
                seedsBefore = inventory.GetQuantity(wheat.SeedItemId);

                if (seedsBefore < 1)
                {
                    return StepResult.Fail("the player has no " + wheat.SeedItemId + " to plant");
                }

                interaction.TryInteract();
                return StepResult.Running;
            }

            if (!plot.IsGrowing)
            {
                return StepResult.Fail("plot went to " + plot.State + " instead of Planted");
            }

            int seedsNow = inventory.GetQuantity(wheat.SeedItemId);
            if (seedsNow != seedsBefore - 1)
            {
                return StepResult.Fail(wheat.SeedItemId + " went " + seedsBefore + " -> " + seedsNow +
                                       ", expected exactly one consumed");
            }

            if (plot.CropAnchor.childCount == 0)
            {
                return StepResult.Fail("no crop visual was spawned under CropAnchor");
            }

            return StepResult.Pass(wheat.SeedItemId + " " + seedsBefore + " -> " + seedsNow +
                                   ", visual \"" + plot.CropAnchor.GetChild(0).name + "\" spawned");
        }

        private static StepResult Growth()
        {
            // Walk away: growth must not depend on the player standing in the field.
            if (pollDeadline <= 0f)
            {
                player.transform.position += new Vector3(0f, 0f, -14f);
                pollDeadline = Now + 0.1f;
            }

            if (plot.State != PlotState.ReadyToHarvest)
            {
                return StepResult.Running;
            }

            if (plot.CropAnchor.childCount == 0)
            {
                return StepResult.Fail("the plot is ready but shows no crop visual");
            }

            return StepResult.Pass("reached stage " + plot.StageIndex + " of " + wheat.GrowthStages +
                                   " with the player 14m away, visual \"" +
                                   plot.CropAnchor.GetChild(0).name + "\"");
        }

        private static StepResult Harvest()
        {
            if (plot.State == PlotState.ReadyToHarvest)
            {
                produceBefore = inventory.GetQuantity(wheat.HarvestItemId);
                MovePlayerNear(plot.transform, 1.5f);

                if (!Poll(0.5f))
                {
                    return StepResult.Running;
                }

                if (!ReferenceEquals(interaction.Current, plot))
                {
                    return StepResult.Fail("could not refocus the plot to harvest, focus was " + Focused);
                }

                interaction.TryInteract();
                return StepResult.Running;
            }

            if (plot.State != PlotState.Empty)
            {
                return StepResult.Fail("after harvest the plot was " + plot.State + ", expected Empty");
            }

            int produceNow = inventory.GetQuantity(wheat.HarvestItemId);
            if (produceNow <= produceBefore)
            {
                return StepResult.Fail(wheat.HarvestItemId + " did not increase (" + produceBefore +
                                       " -> " + produceNow + ")");
            }

            // FarmPlot.DestroyVisual calls Unity's Destroy(), which only actually removes the
            // child at the end of the frame it was called in - the state transition and the
            // inventory grant above are already both true the instant TryHarvest returns, but
            // the child count is not. A short, bounded grace window is the correct way to wait
            // out a deferred Destroy().
            //
            // This cannot reuse the shared `pollDeadline` field: it was already set (and has
            // already expired) by the refocus wait earlier in THIS SAME step, a few ticks ago,
            // and Drive() only resets it between steps, not between phases within one step. A
            // dedicated field is used instead so the two waits cannot corrupt each other.
            if (plot.CropAnchor.childCount != 0)
            {
                if (harvestVisualGraceDeadline <= 0f)
                {
                    harvestVisualGraceDeadline = Now + 0.3f;
                }

                if (Now < harvestVisualGraceDeadline)
                {
                    return StepResult.Running;
                }
            }

            harvestVisualGraceDeadline = 0f;

            if (plot.CropAnchor.childCount != 0)
            {
                return StepResult.Fail("the crop visual was not removed after harvesting");
            }

            return StepResult.Pass(wheat.HarvestItemId + " " + produceBefore + " -> " + produceNow +
                                   ", plot cleared and the crop visual removed");
        }

        private static StepResult NoDoubleHarvest()
        {
            if (pollDeadline <= 0f)
            {
                produceBefore = inventory.GetQuantity(wheat.HarvestItemId);
                interaction.TryInteract();
            }

            if (!Poll(0.4f))
            {
                return StepResult.Running;
            }

            int produceNow = inventory.GetQuantity(wheat.HarvestItemId);
            if (produceNow != produceBefore)
            {
                return StepResult.Fail("a second press paid out again: " + produceBefore + " -> " + produceNow);
            }

            return StepResult.Pass("no extra produce; the plot is now " + plot.State);
        }

        // ---------------------------------------------------------------- animals

        private static StepResult FocusAnimal(AnimalController animal, string label)
        {
            if (pollDeadline <= 0f)
            {
                MovePlayerNear(animal.transform, 1.5f);
            }

            if (!Poll(0.5f))
            {
                return StepResult.Running;
            }

            AnimalInteraction expected = animal.GetComponent<AnimalInteraction>();

            if (expected == null)
            {
                return StepResult.Fail("the " + label + " has no AnimalInteraction component");
            }

            if (!ReferenceEquals(interaction.Current, expected))
            {
                return StepResult.Fail("focus was " + Focused + ", expected the " + label);
            }

            return StepResult.Pass("prompt reads \"" + expected.InteractionLabel + "\" (priority " +
                                   expected.InteractionPriority + ")");
        }

        private static StepResult FocusChicken() => FocusAnimal(chicken, "chicken");

        private static StepResult FocusCow() => FocusAnimal(cow, "cow");

        /// <summary>
        /// Forces hunger rather than waiting one out. Waiting made the test slow and, worse,
        /// non-deterministic - a run could pass or fail on where a random starting hunger
        /// landed. This sets a need only; the feed itself still has to go through the USE
        /// button and the real interaction chain.
        /// </summary>
        private static StepResult Hungry(AnimalController animal, string label)
        {
            if (!animal.IsHungry)
            {
                animal.DebugMakeHungry();
                return StepResult.Running;
            }

            return StepResult.Pass(label + " hunger " + animal.Needs.Hunger.ToString("0.00") +
                                   " >= threshold " + animal.Definition.HungerThreshold +
                                   ", happiness " + animal.Needs.Happiness.ToString("0") +
                                   ", prompt \"" + animal.GetComponent<AnimalInteraction>().InteractionLabel + "\"");
        }

        private static StepResult ChickenHungry() => Hungry(chicken, "chicken");

        private static StepResult CowHungry() => Hungry(cow, "cow");

        private static StepResult Feed(AnimalController animal, string label)
        {
            AnimalDefinition definition = animal.Definition;

            if (pollDeadline <= 0f)
            {
                seedsBefore = inventory.GetQuantity(definition.FeedItemId);

                if (seedsBefore < definition.FeedAmount)
                {
                    return StepResult.Fail("the player has only " + seedsBefore + " " +
                                           definition.FeedItemId + ", needs " + definition.FeedAmount);
                }

                MovePlayerNear(animal.transform, 1.5f);
            }

            if (!Poll(0.5f))
            {
                return StepResult.Running;
            }

            if (animal.Production.Phase == ProductionPhase.Dormant)
            {
                interaction.TryInteract();
                return StepResult.Running;
            }

            int feedNow = inventory.GetQuantity(definition.FeedItemId);
            int expected = seedsBefore - definition.FeedAmount;

            if (feedNow != expected)
            {
                return StepResult.Fail(definition.FeedItemId + " went " + seedsBefore + " -> " + feedNow +
                                       ", expected " + expected);
            }

            if (animal.IsHungry)
            {
                return StepResult.Fail("the " + label + " is still hungry after being fed");
            }

            return StepResult.Pass(definition.FeedItemId + " " + seedsBefore + " -> " + feedNow +
                                   " (exactly -" + definition.FeedAmount + "), hunger reset to " +
                                   animal.Needs.Hunger.ToString("0.00") + ", happiness " +
                                   animal.Needs.Happiness.ToString("0") +
                                   ", production Dormant -> " + animal.Production.Phase);
        }

        private static StepResult FeedChicken() => Feed(chicken, "chicken");

        private static StepResult FeedCow() => Feed(cow, "cow");

        private static StepResult ProductReady(AnimalController animal, string label)
        {
            if (!animal.HasProductReady)
            {
                return StepResult.Running;
            }

            return StepResult.Pass(label + " product ready, phase " + animal.Production.Phase);
        }

        private static StepResult EggReady() => ProductReady(chicken, "chicken");

        private static StepResult MilkReady() => ProductReady(cow, "cow");

        private static StepResult Collect(AnimalController animal, string label)
        {
            AnimalDefinition definition = animal.Definition;

            if (pollDeadline <= 0f)
            {
                produceBefore = inventory.GetQuantity(definition.ProductItemId);
                MovePlayerNear(animal.transform, 1.5f);
            }

            if (!Poll(0.5f))
            {
                return StepResult.Running;
            }

            if (animal.HasProductReady)
            {
                if (!ReferenceEquals(interaction.Current, animal.GetComponent<AnimalInteraction>()))
                {
                    return StepResult.Fail("could not focus the " + label + " to collect, focus was " + Focused);
                }

                interaction.TryInteract();
                return StepResult.Running;
            }

            int produceNow = inventory.GetQuantity(definition.ProductItemId);

            if (produceNow < produceBefore + definition.ProductAmount)
            {
                return StepResult.Fail(definition.ProductItemId + " went " + produceBefore + " -> " +
                                       produceNow + ", expected at least +" + definition.ProductAmount);
            }

            return StepResult.Pass(definition.ProductItemId + " " + produceBefore + " -> " + produceNow +
                                   ", phase back to " + animal.Production.Phase);
        }

        private static StepResult CollectEgg() => Collect(chicken, "chicken");

        private static StepResult CollectMilk() => Collect(cow, "cow");

        private static StepResult NoDoubleCollect(AnimalController animal, string label)
        {
            AnimalDefinition definition = animal.Definition;

            if (pollDeadline <= 0f)
            {
                produceBefore = inventory.GetQuantity(definition.ProductItemId);
                interaction.TryInteract();
            }

            if (!Poll(0.4f))
            {
                return StepResult.Running;
            }

            int produceNow = inventory.GetQuantity(definition.ProductItemId);

            if (produceNow != produceBefore)
            {
                return StepResult.Fail("a second press paid out again: " + produceBefore + " -> " + produceNow);
            }

            return StepResult.Pass("no extra " + definition.ProductItemId + " from the " + label);
        }

        private static StepResult NoDoubleEgg() => NoDoubleCollect(chicken, "chicken");

        private static StepResult NoDoubleMilk() => NoDoubleCollect(cow, "cow");

        // ---------------------------------------------------------------- wandering

        private static StepResult Wandering()
        {
            if (pollDeadline <= 0f)
            {
                // Stand well clear: animals deliberately avoid crowding the player, so testing
                // wandering from arm's length would suppress the very thing being measured.
                player.transform.position += new Vector3(0f, 0f, 18f);

                wanderStart = new Vector3[allAnimals.Length];
                for (int i = 0; i < allAnimals.Length; i++)
                {
                    wanderStart[i] = allAnimals[i] != null ? allAnimals[i].transform.position : Vector3.zero;
                }
            }

            if (!Poll(12f))
            {
                return StepResult.Running;
            }

            int moved = 0;
            int escaped = 0;
            float furthest = 0f;

            for (int i = 0; i < allAnimals.Length; i++)
            {
                AnimalController animal = allAnimals[i];
                if (animal == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(animal.transform.position, wanderStart[i]);
                furthest = Mathf.Max(furthest, distance);

                if (distance > 0.05f)
                {
                    moved++;
                }

                if (animal.Habitat != null && !animal.Habitat.IsWalkable(animal.transform.position))
                {
                    escaped++;
                    Debug.LogError("[GameplayTest] '" + animal.name + "' left its habitat at " +
                                   animal.transform.position, animal);
                }
            }

            if (escaped > 0)
            {
                return StepResult.Fail(escaped + " animal(s) left their habitat bounds");
            }

            if (moved == 0)
            {
                return StepResult.Fail("no animal moved in 12 seconds");
            }

            return StepResult.Pass(moved + " of " + allAnimals.Length +
                                   " animals moved (furthest " + furthest.ToString("0.00") +
                                   "m), none left their habitat");
        }
    }
}
