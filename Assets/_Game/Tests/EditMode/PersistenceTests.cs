using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LittleFarmStory.Animals;
using LittleFarmStory.Farming;
using LittleFarmStory.Inventory;
using LittleFarmStory.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LittleFarmStory.Tests
{
    /// <summary>
    /// EditMode tests for the save layer: the data shape, the file round-trip and the restore
    /// APIs the save system depends on.
    ///
    /// These prove serialization and restore LOGIC in the Editor that is already open - no
    /// scene, no Play mode. Whether the scene is wired to a SaveManager is asserted by
    /// FarmPrototypeBuilder.VerifyBuild, and whether a real farm round-trips is proven by the
    /// Play Mode suite; neither belongs here.
    ///
    /// The file tests write to the real persistentDataPath, which is where a player's actual
    /// save lives. Any existing file is therefore stashed in SetUp and put back in TearDown, so
    /// running the suite can never cost someone their farm.
    /// </summary>
    public class PersistenceTests
    {
        private string stashedSave;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            stashedSave = File.Exists(SaveSystem.SavePath)
                ? File.ReadAllText(SaveSystem.SavePath)
                : null;

            SaveSystem.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Delete();

            if (stashedSave != null)
            {
                File.WriteAllText(SaveSystem.SavePath, stashedSave);
                stashedSave = null;
            }

            if (host != null)
            {
                Object.DestroyImmediate(host);
                host = null;
            }
        }

        private static SaveData BuildSample()
        {
            SaveData data = new SaveData
            {
                Coins = 137,
                SavedUtcTicks = 638000000000000000L,
                Player = new PlayerSnapshot
                {
                    PositionX = 1.5f,
                    PositionY = 0.25f,
                    PositionZ = -3.75f,
                    VisualYaw = 214f
                }
            };

            data.Inventory.Add(new InventoryEntry { ItemId = ItemIds.Seed(ItemIds.Wheat), Amount = 7 });
            data.Inventory.Add(new InventoryEntry { ItemId = ItemIds.Harvest(ItemIds.Wheat), Amount = 3 });

            FieldSnapshot field = new FieldSnapshot { FieldId = "field_wheat" };
            field.Plots.Add(new FarmPlotSnapshot
            {
                CoordX = 2,
                CoordZ = 1,
                State = PlotState.Growing,
                CropId = ItemIds.Wheat,
                StageIndex = 2,
                GrowthElapsed = 12.5f,
                PlantedUtcTicks = 637900000000000000L
            });
            data.Fields.Add(field);

            data.Animals.Add(new AnimalSnapshot
            {
                InstanceId = "chicken_0",
                AnimalId = "chicken",
                HabitatId = "habitat_coop",
                Hunger = 0.4f,
                Happiness = 62f,
                ProductionPhase = ProductionPhase.Producing,
                ProductionElapsed = 5.5f,
                PositionX = -16f,
                PositionZ = -7f,
                Yaw = 90f
            });

            return data;
        }

        // ================================================================ A. JSON round-trip

        [Test]
        public void A_SaveData_SurvivesAJsonRoundTrip_Intact()
        {
            SaveData original = BuildSample();

            SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original));

            Assert.AreEqual(SaveData.CurrentVersion, restored.Version);
            Assert.AreEqual(original.Coins, restored.Coins);
            Assert.AreEqual(original.SavedUtcTicks, restored.SavedUtcTicks);
            Assert.AreEqual(original.Inventory.Count, restored.Inventory.Count);
            Assert.AreEqual(original.Fields.Count, restored.Fields.Count);
            Assert.AreEqual(original.Animals.Count, restored.Animals.Count);
        }

        [Test]
        public void A_PlayerSnapshot_KeepsPositionAndFacing()
        {
            SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(BuildSample()));

            Assert.AreEqual(1.5f, restored.Player.PositionX, 0.0001f);
            Assert.AreEqual(0.25f, restored.Player.PositionY, 0.0001f);
            Assert.AreEqual(-3.75f, restored.Player.PositionZ, 0.0001f);
            Assert.AreEqual(214f, restored.Player.VisualYaw, 0.0001f);
        }

        [Test]
        public void A_PlotSnapshot_KeepsItsEnumAndGrowthProgress()
        {
            SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(BuildSample()));
            FarmPlotSnapshot plot = restored.Fields[0].Plots[0];

            Assert.AreEqual("field_wheat", restored.Fields[0].FieldId);
            Assert.AreEqual(PlotState.Growing, plot.State);
            Assert.AreEqual(ItemIds.Wheat, plot.CropId);
            Assert.AreEqual(2, plot.CoordX);
            Assert.AreEqual(1, plot.CoordZ);
            Assert.AreEqual(2, plot.StageIndex);
            Assert.AreEqual(12.5f, plot.GrowthElapsed, 0.0001f);

            // A long must not come back through a float and lose its low digits.
            Assert.AreEqual(637900000000000000L, plot.PlantedUtcTicks);
        }

        [Test]
        public void A_AnimalSnapshot_KeepsNeedsAndProductionPhase()
        {
            SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(BuildSample()));
            AnimalSnapshot animal = restored.Animals[0];

            Assert.AreEqual("chicken_0", animal.InstanceId);
            Assert.AreEqual("habitat_coop", animal.HabitatId);
            Assert.AreEqual(0.4f, animal.Hunger, 0.0001f);
            Assert.AreEqual(62f, animal.Happiness, 0.0001f);
            Assert.AreEqual(ProductionPhase.Producing, animal.ProductionPhase);
            Assert.AreEqual(5.5f, animal.ProductionElapsed, 0.0001f);
        }

        // ================================================================ B. file round-trip

        [Test]
        public void B_WriteThenRead_ReturnsTheSameFarm()
        {
            Assert.IsTrue(SaveSystem.Write(BuildSample()));
            Assert.IsTrue(SaveSystem.Exists());

            Assert.IsTrue(SaveSystem.TryRead(out SaveData loaded));
            Assert.AreEqual(137, loaded.Coins);
            Assert.AreEqual(2, loaded.Inventory.Count);
            Assert.AreEqual(1, loaded.Fields[0].Plots.Count);
            Assert.AreEqual("chicken_0", loaded.Animals[0].InstanceId);
        }

        [Test]
        public void B_ReadWithNoFile_ReturnsFalseAndNoData()
        {
            Assert.IsFalse(SaveSystem.Exists());
            Assert.IsFalse(SaveSystem.TryRead(out SaveData loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void B_WritingTwice_ReplacesRatherThanAppends()
        {
            SaveData first = BuildSample();
            Assert.IsTrue(SaveSystem.Write(first));

            SaveData second = BuildSample();
            second.Coins = 4;
            Assert.IsTrue(SaveSystem.Write(second));

            Assert.IsTrue(SaveSystem.TryRead(out SaveData loaded));
            Assert.AreEqual(4, loaded.Coins);
        }

        [Test]
        public void B_Delete_RemovesTheFile()
        {
            Assert.IsTrue(SaveSystem.Write(BuildSample()));
            Assert.IsTrue(SaveSystem.Delete());

            Assert.IsFalse(SaveSystem.Exists());
            Assert.IsFalse(SaveSystem.TryRead(out _));
        }

        [Test]
        public void B_WriteLeavesNoTemporaryFileBehind()
        {
            Assert.IsTrue(SaveSystem.Write(BuildSample()));
            Assert.IsFalse(File.Exists(SaveSystem.SavePath + ".tmp"));
        }

        [Test]
        public void B_RefusesToWriteNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("refused to write a null SaveData"));

            Assert.IsFalse(SaveSystem.Write(null));
            Assert.IsFalse(SaveSystem.Exists());
        }

        // ================================================================ C. corrupt / future files

        [Test]
        public void C_AnEmptyFile_IsIgnoredRatherThanLoaded()
        {
            File.WriteAllText(SaveSystem.SavePath, string.Empty);
            LogAssert.Expect(LogType.Warning, new Regex("is empty"));

            Assert.IsFalse(SaveSystem.TryRead(out SaveData loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void C_AFileFromANewerBuild_IsIgnoredRatherThanLoadedWrongly()
        {
            SaveData future = BuildSample();
            future.Version = SaveData.CurrentVersion + 1;
            File.WriteAllText(SaveSystem.SavePath, JsonUtility.ToJson(future));

            LogAssert.Expect(LogType.Warning, new Regex("version"));

            Assert.IsFalse(SaveSystem.TryRead(out SaveData loaded));
            Assert.IsNull(loaded);
        }

        // ================================================================ D. inventory restore

        [Test]
        public void D_RestoreAll_ReplacesTheWholeStore()
        {
            host = new GameObject("PersistenceTestPlayer");
            PlayerInventory inventory = host.AddComponent<PlayerInventory>();

            inventory.Add("wheat", 5);
            inventory.Add("corn", 2);

            inventory.RestoreAll(new[]
            {
                new KeyValuePair<string, int>("wheat", 11),
                new KeyValuePair<string, int>("egg", 4)
            });

            Assert.AreEqual(11, inventory.GetQuantity("wheat"));
            Assert.AreEqual(4, inventory.GetQuantity("egg"));

            // Corn was held before the restore and is not in the saved data, so it must be gone.
            Assert.AreEqual(0, inventory.GetQuantity("corn"));
        }

        [Test]
        public void D_RestoreAll_ReportsItemsThatDroppedToZero()
        {
            host = new GameObject("PersistenceTestPlayer");
            PlayerInventory inventory = host.AddComponent<PlayerInventory>();

            inventory.Add("corn", 2);

            Dictionary<string, int> reported = new Dictionary<string, int>();
            inventory.Changed += (id, quantity) => reported[id] = quantity;

            inventory.RestoreAll(new[] { new KeyValuePair<string, int>("wheat", 1) });

            // A HUD chip showing corn has to be told it is now zero, or it would keep showing 2.
            Assert.IsTrue(reported.ContainsKey("corn"), "no Changed event was raised for the cleared item");
            Assert.AreEqual(0, reported["corn"]);
            Assert.AreEqual(1, reported["wheat"]);
        }

        [Test]
        public void D_RestoreAll_ClampsNegativeAmountsAndSkipsBlankIds()
        {
            host = new GameObject("PersistenceTestPlayer");
            PlayerInventory inventory = host.AddComponent<PlayerInventory>();

            inventory.RestoreAll(new[]
            {
                new KeyValuePair<string, int>("wheat", -5),
                new KeyValuePair<string, int>("", 3)
            });

            Assert.AreEqual(0, inventory.GetQuantity("wheat"));
            Assert.AreEqual(0, inventory.GetQuantity(""));
        }

        [Test]
        public void D_RestoreAll_WithNothingSaved_ClearsTheStore()
        {
            host = new GameObject("PersistenceTestPlayer");
            PlayerInventory inventory = host.AddComponent<PlayerInventory>();

            inventory.Add("wheat", 9);
            inventory.RestoreAll(new KeyValuePair<string, int>[0]);

            Assert.AreEqual(0, inventory.GetQuantity("wheat"));
        }

        // ================================================================ E. captured shape

        [Test]
        public void E_InventoryAll_RoundTripsThroughSaveDataEntries()
        {
            host = new GameObject("PersistenceTestPlayer");
            PlayerInventory source = host.AddComponent<PlayerInventory>();

            source.Add(ItemIds.Seed(ItemIds.Wheat), 6);
            source.Add(ItemIds.Egg, 2);

            SaveData data = new SaveData();

            foreach (KeyValuePair<string, int> item in source.All)
            {
                data.Inventory.Add(new InventoryEntry { ItemId = item.Key, Amount = item.Value });
            }

            SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            List<KeyValuePair<string, int>> items = new List<KeyValuePair<string, int>>();

            for (int i = 0; i < loaded.Inventory.Count; i++)
            {
                items.Add(new KeyValuePair<string, int>(
                    loaded.Inventory[i].ItemId, loaded.Inventory[i].Amount));
            }

            source.RestoreAll(items);

            Assert.AreEqual(6, source.GetQuantity(ItemIds.Seed(ItemIds.Wheat)));
            Assert.AreEqual(2, source.GetQuantity(ItemIds.Egg));
        }
    }
}
