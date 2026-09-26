using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LittleFarmStory.Economy;
using LittleFarmStory.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LittleFarmStory.Tests
{
    /// <summary>
    /// EditMode tests for the economy core: CurrencyWallet, EconomyManager and the shop data.
    ///
    /// Every fixture builds its own in-memory shop - no generated asset, no scene, no Play
    /// mode - so these prove the transaction LOGIC, deterministically, in the Editor that is
    /// already open. The shipped prices (wheat seeds 2, wheat 4) are mirrored here but are
    /// enforced on the real assets by FarmPrototypeBuilder.VerifyBuild, not by these tests.
    ///
    /// Nothing relies on Awake having run: in EditMode, AddComponent does not call it. The
    /// wallet initialises lazily, and the manager is wired explicitly, exactly as the scene
    /// builder wires it.
    ///
    /// Assertions are written as deltas from a captured "before" wherever the absolute
    /// starting stock is not the thing under test, so a change to PlayerInventory's default
    /// starting items cannot make them fail for the wrong reason.
    /// </summary>
    public class EconomyTests
    {
        private const int StartingCoins = 100;
        private const int SeedPrice = 2;
        private const int WheatSellPrice = 4;
        private const float DuplicateWindow = 0.35f;

        private static readonly string SeedId = ItemIds.Seed(ItemIds.Wheat);
        private static readonly string WheatId = ItemIds.Harvest(ItemIds.Wheat);

        private readonly List<Object> created = new List<Object>();

        private GameObject player;
        private PlayerInventory inventory;
        private CurrencyWallet wallet;
        private EconomyManager economy;
        private EconomySettings settings;
        private ShopDefinition shop;
        private ShopItemDefinition wheatSeeds;
        private ShopItemDefinition wheat;

        // ================================================================ fixture

        [SetUp]
        public void SetUp()
        {
            settings = Create<EconomySettings>();
            settings.EditorConfigure(StartingCoins, DuplicateWindow, 0, false);

            wheatSeeds = Create<ShopItemDefinition>();
            wheatSeeds.EditorConfigure(
                SeedId, "Wheat Seeds", null,
                true, SeedPrice, false, 0,
                1, 99, 1, true);

            wheat = Create<ShopItemDefinition>();
            wheat.EditorConfigure(
                WheatId, "Wheat", null,
                false, 0, true, WheatSellPrice,
                1, 99, 1, true);

            shop = Create<ShopDefinition>();
            shop.EditorConfigure(new[] { wheatSeeds, wheat });

            player = new GameObject("EconomyTestPlayer");
            inventory = player.AddComponent<PlayerInventory>();

            wallet = player.AddComponent<CurrencyWallet>();
            wallet.EditorConfigure(settings.StartingCoins);

            economy = player.AddComponent<EconomyManager>();
            economy.EditorConfigure(shop, settings, wallet, inventory, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null)
            {
                Object.DestroyImmediate(player);
            }

            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            created.Add(instance);
            return instance;
        }

        // ================================================================ A. starting coins

        [Test]
        public void A_StartingCoins_WalletAuthoredFromSettings_Is100()
        {
            Assert.AreEqual(StartingCoins, wallet.GetBalance());
            Assert.AreEqual(StartingCoins, wallet.Balance);
        }

        [Test]
        public void A_StartingCoins_UnconfiguredWalletDefault_Is100()
        {
            // The component's own default, independent of the settings asset - so a wallet
            // added by hand, without the builder, still starts where the design says.
            GameObject bare = new GameObject("BareWallet");

            try
            {
                CurrencyWallet bareWallet = bare.AddComponent<CurrencyWallet>();
                Assert.AreEqual(100, bareWallet.GetBalance());
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        // ================================================================ B. buy 5 seeds

        [Test]
        public void B_BuyFiveWheatSeeds_Costs10_AndAddsFiveSeeds()
        {
            int seedsBefore = inventory.GetQuantity(SeedId);

            TransactionOutcome outcome = economy.Purchase(wheatSeeds, 5);

            Assert.AreEqual(TransactionResult.Success, outcome.Result, outcome.ToString());
            Assert.AreEqual(90, wallet.GetBalance());
            Assert.AreEqual(seedsBefore + 5, inventory.GetQuantity(SeedId));

            // The outcome reports what actually happened, so no caller has to recompute it.
            Assert.AreEqual(10, outcome.Coins);
            Assert.AreEqual(90, outcome.Balance);
            Assert.AreEqual(5, outcome.Quantity);
            Assert.AreEqual(SeedId, outcome.ItemId);
        }

        [Test]
        public void B_BuyFiveWheatSeeds_ById_BehavesIdentically()
        {
            int seedsBefore = inventory.GetQuantity(SeedId);

            TransactionOutcome outcome = economy.Purchase(SeedId, 5);

            Assert.AreEqual(TransactionResult.Success, outcome.Result, outcome.ToString());
            Assert.AreEqual(90, wallet.GetBalance());
            Assert.AreEqual(seedsBefore + 5, inventory.GetQuantity(SeedId));
        }

        // ================================================================ C. insufficient funds

        [Test]
        public void C_PurchaseBeyondBalance_IsRejected_AndChangesNothing()
        {
            int seedsBefore = inventory.GetQuantity(SeedId);

            // 51 seeds x 2 = 102 coins, against a balance of 100.
            TransactionOutcome outcome = economy.Purchase(wheatSeeds, 51);

            Assert.AreEqual(TransactionResult.NotEnoughCoins, outcome.Result, outcome.ToString());
            Assert.AreEqual(StartingCoins, wallet.GetBalance(), "coins must not change on a refused purchase");
            Assert.AreEqual(seedsBefore, inventory.GetQuantity(SeedId), "inventory must not change on a refused purchase");
            Assert.AreEqual(0, outcome.Coins);
        }

        [Test]
        public void C_PurchaseCostingExactlyTheBalance_Succeeds_AndLeavesZero()
        {
            // The boundary: affordable to the last coin, and never below zero.
            TransactionOutcome outcome = economy.Purchase(wheatSeeds, 50);

            Assert.AreEqual(TransactionResult.Success, outcome.Result, outcome.ToString());
            Assert.AreEqual(0, wallet.GetBalance());
        }

        [Test]
        public void C_PurchaseAfterSpendingDown_IsRejected_AndChangesNothing()
        {
            Assert.AreEqual(TransactionResult.Success, economy.Purchase(wheatSeeds, 49).Result);
            Assert.AreEqual(2, wallet.GetBalance());

            int seedsBefore = inventory.GetQuantity(SeedId);

            // 2 seeds cost 4; only 2 coins remain.
            TransactionOutcome outcome = economy.Purchase(wheatSeeds, 2);

            Assert.AreEqual(TransactionResult.NotEnoughCoins, outcome.Result, outcome.ToString());
            Assert.AreEqual(2, wallet.GetBalance());
            Assert.AreEqual(seedsBefore, inventory.GetQuantity(SeedId));
        }

        // ================================================================ D. sell one wheat

        [Test]
        public void D_SellOneOwnedWheat_RemovesOne_AndPaysFour()
        {
            inventory.Add(WheatId, 3);
            int wheatBefore = inventory.GetQuantity(WheatId);

            TransactionOutcome outcome = economy.Sell(wheat, 1);

            Assert.AreEqual(TransactionResult.Success, outcome.Result, outcome.ToString());
            Assert.AreEqual(wheatBefore - 1, inventory.GetQuantity(WheatId));
            Assert.AreEqual(StartingCoins + WheatSellPrice, wallet.GetBalance());
            Assert.AreEqual(WheatSellPrice, outcome.Coins);
        }

        // ================================================================ E. oversell

        [Test]
        public void E_SellMoreWheatThanOwned_IsRejected_WithNoPartialSale()
        {
            inventory.Add(WheatId, 2);
            int wheatBefore = inventory.GetQuantity(WheatId);

            TransactionOutcome outcome = economy.Sell(wheat, wheatBefore + 3);

            Assert.AreEqual(TransactionResult.NotEnoughItems, outcome.Result, outcome.ToString());
            Assert.AreEqual(wheatBefore, inventory.GetQuantity(WheatId),
                "not even the units the player DOES own may be removed");
            Assert.AreEqual(StartingCoins, wallet.GetBalance(), "no coins for a refused sale");
            Assert.AreEqual(0, outcome.Coins);
        }

        [Test]
        public void E_SellWheatWhenOwningNone_IsRejected_AndChangesNothing()
        {
            int wheatBefore = inventory.GetQuantity(WheatId);
            Assume.That(wheatBefore, Is.EqualTo(0), "fixture should start with no wheat");

            TransactionOutcome outcome = economy.Sell(wheat, 1);

            Assert.AreEqual(TransactionResult.NotEnoughItems, outcome.Result, outcome.ToString());
            Assert.AreEqual(0, inventory.GetQuantity(WheatId));
            Assert.AreEqual(StartingCoins, wallet.GetBalance());
        }

        // ================================================================ F. negative quantity

        [Test]
        public void F_NegativeOrZeroPurchaseQuantity_IsRejected_AndChangesNothing(
            [Values(-1, -5, 0)] int quantity)
        {
            int seedsBefore = inventory.GetQuantity(SeedId);

            TransactionOutcome outcome = economy.Purchase(wheatSeeds, quantity);

            Assert.AreEqual(TransactionResult.InvalidQuantity, outcome.Result, outcome.ToString());
            Assert.AreEqual(StartingCoins, wallet.GetBalance());
            Assert.AreEqual(seedsBefore, inventory.GetQuantity(SeedId));
        }

        [Test]
        public void F_NegativeOrZeroSellQuantity_IsRejected_AndChangesNothing(
            [Values(-1, -5, 0)] int quantity)
        {
            inventory.Add(WheatId, 5);
            int wheatBefore = inventory.GetQuantity(WheatId);

            TransactionOutcome outcome = economy.Sell(wheat, quantity);

            // A negative sale must never become "remove minus N" - i.e. a free gift.
            Assert.AreEqual(TransactionResult.InvalidQuantity, outcome.Result, outcome.ToString());
            Assert.AreEqual(StartingCoins, wallet.GetBalance());
            Assert.AreEqual(wheatBefore, inventory.GetQuantity(WheatId));
        }

        [Test]
        public void F_WalletRejectsNegativeAmounts_OnBothPaths()
        {
            wallet.AddCoins(-50);
            Assert.AreEqual(StartingCoins, wallet.GetBalance(), "a negative credit must not subtract");

            LogAssert.Expect(LogType.Error, new Regex("refused a negative spend"));
            Assert.IsFalse(wallet.TrySpendCoins(-10), "a negative spend must not add coins");
            Assert.AreEqual(StartingCoins, wallet.GetBalance());
        }

        // ================================================================ G. rapid / repeated calls

        [Test]
        public void G_BurstOfIdenticalPurchases_IsProcessedExactlyOnce()
        {
            // A double tap, a repeated event or a stuck button: ten identical calls inside the
            // duplicate window. Exactly one may apply.
            int seedsBefore = inventory.GetQuantity(SeedId);
            int successes = 0;
            int duplicates = 0;

            for (int i = 0; i < 10; i++)
            {
                TransactionOutcome outcome = economy.Purchase(wheatSeeds, 1);

                if (outcome.Succeeded)
                {
                    successes++;
                }
                else if (outcome.Result == TransactionResult.DuplicateTransaction)
                {
                    duplicates++;
                }
                else
                {
                    Assert.Fail("unexpected result on call " + i + ": " + outcome);
                }
            }

            Assert.AreEqual(1, successes);
            Assert.AreEqual(9, duplicates);
            Assert.AreEqual(StartingCoins - SeedPrice, wallet.GetBalance());
            Assert.AreEqual(seedsBefore + 1, inventory.GetQuantity(SeedId));
        }

        [Test]
        public void G_BurstOfIdenticalSales_IsProcessedExactlyOnce()
        {
            inventory.Add(WheatId, 10);
            int wheatBefore = inventory.GetQuantity(WheatId);
            int successes = 0;

            for (int i = 0; i < 10; i++)
            {
                if (economy.Sell(wheat, 1).Succeeded)
                {
                    successes++;
                }
            }

            Assert.AreEqual(1, successes);
            Assert.AreEqual(wheatBefore - 1, inventory.GetQuantity(WheatId));
            Assert.AreEqual(StartingCoins + WheatSellPrice, wallet.GetBalance());
        }

        [Test]
        public void G_BurstOfDistinctValidPurchases_EachIsProcessedExactlyOnce()
        {
            // Rapid but genuinely different requests are not duplicates, and every one of them
            // must land exactly once.
            int seedsBefore = inventory.GetQuantity(SeedId);
            int[] quantities = { 1, 2, 3, 4 };

            for (int i = 0; i < quantities.Length; i++)
            {
                TransactionOutcome outcome = economy.Purchase(wheatSeeds, quantities[i]);
                Assert.AreEqual(TransactionResult.Success, outcome.Result, "call " + i + ": " + outcome);
            }

            // 1+2+3+4 = 10 seeds, 20 coins.
            Assert.AreEqual(StartingCoins - 20, wallet.GetBalance());
            Assert.AreEqual(seedsBefore + 10, inventory.GetQuantity(SeedId));
        }

        [Test]
        public void G_WithDuplicateWindowOff_EveryRepeatIsProcessedExactlyOnce()
        {
            // Proves the guard is the ONLY thing collapsing repeats: with it off, each call is
            // applied exactly once - never zero times, never twice.
            settings.EditorConfigure(StartingCoins, 0f, 0, false);
            int seedsBefore = inventory.GetQuantity(SeedId);

            for (int i = 0; i < 5; i++)
            {
                TransactionOutcome outcome = economy.Purchase(wheatSeeds, 1);
                Assert.AreEqual(TransactionResult.Success, outcome.Result, "call " + i + ": " + outcome);
            }

            Assert.AreEqual(StartingCoins - 5 * SeedPrice, wallet.GetBalance());
            Assert.AreEqual(seedsBefore + 5, inventory.GetQuantity(SeedId));
        }

        [Test]
        public void G_InvalidCallsInterleavedWithValidOnes_CannotCorruptState()
        {
            // Every call - valid or not - is checked for atomicity on its own: either it moved
            // exactly what its outcome says, or it moved nothing at all. The end state must
            // equal the valid calls alone.
            ShopItemDefinition notInShop = Create<ShopItemDefinition>();
            notInShop.EditorConfigure(
                ItemIds.Seed(ItemIds.Corn), "Corn Seeds", null,
                true, 3, false, 0, 1, 99, 1, true);

            inventory.Add(WheatId, 2);

            int seedsBefore = inventory.GetQuantity(SeedId);
            int wheatBefore = inventory.GetQuantity(WheatId);

            Atomic(() => economy.Purchase(wheatSeeds, 3), SeedId, true);          // valid: -6
            Atomic(() => economy.Purchase(wheatSeeds, -4), SeedId, true);         // invalid quantity
            Atomic(() => economy.Sell(wheat, 99), WheatId, false);                // more than owned
            Atomic(() => economy.Purchase(wheatSeeds, 60), SeedId, true);         // unaffordable
            Atomic(() => economy.Purchase(notInShop, 1), notInShop.ItemId, true); // not in this shop
            Atomic(() => economy.Purchase("no_such_item", 1), "no_such_item", true);
            Atomic(() => economy.Sell(wheatSeeds, 1), SeedId, false);             // seeds not bought back
            Atomic(() => economy.Purchase(wheat, 1), WheatId, true);              // wheat not for sale
            Atomic(() => economy.Sell(wheat, 1), WheatId, false);                 // valid: +4
            Atomic(() => economy.Purchase(wheatSeeds, 2), SeedId, true);          // valid: -4

            Assert.AreEqual(StartingCoins - 6 + 4 - 4, wallet.GetBalance());
            Assert.AreEqual(seedsBefore + 3 + 2, inventory.GetQuantity(SeedId));
            Assert.AreEqual(wheatBefore - 1, inventory.GetQuantity(WheatId));
            Assert.AreEqual(0, inventory.GetQuantity(notInShop.ItemId), "an item outside the catalogue must never appear");
        }

        [Test]
        public void G_PurchaseThatWouldOverfillTheStack_SpendsNoCoins()
        {
            // Capacity is validated before the spend, so coins can never be lost on items that
            // would not fit.
            settings.EditorConfigure(StartingCoins, DuplicateWindow, 5, false);
            inventory.Add(SeedId, 3);
            int seedsBefore = inventory.GetQuantity(SeedId);

            TransactionOutcome outcome = economy.Purchase(wheatSeeds, 3);

            Assert.AreEqual(TransactionResult.NotEnoughRoom, outcome.Result, outcome.ToString());
            Assert.AreEqual(StartingCoins, wallet.GetBalance());
            Assert.AreEqual(seedsBefore, inventory.GetQuantity(SeedId));
        }

        // ================================================================ helpers

        /// <summary>
        /// Runs one transaction and asserts it was atomic: a success moved exactly the coins and
        /// items its outcome reports, and a refusal moved nothing. The balance is never negative.
        /// </summary>
        private TransactionOutcome Atomic(Func<TransactionOutcome> transaction, string itemId, bool isPurchase)
        {
            int coinsBefore = wallet.GetBalance();
            int itemsBefore = inventory.GetQuantity(itemId);

            TransactionOutcome outcome = transaction();

            int coinsAfter = wallet.GetBalance();
            int itemsAfter = inventory.GetQuantity(itemId);

            if (outcome.Succeeded)
            {
                Assert.AreEqual(isPurchase ? coinsBefore - outcome.Coins : coinsBefore + outcome.Coins,
                    coinsAfter, "coins moved by a different amount than reported: " + outcome);
                Assert.AreEqual(isPurchase ? itemsBefore + outcome.Quantity : itemsBefore - outcome.Quantity,
                    itemsAfter, "items moved by a different amount than reported: " + outcome);
            }
            else
            {
                Assert.AreEqual(coinsBefore, coinsAfter, "a refused transaction changed coins: " + outcome);
                Assert.AreEqual(itemsBefore, itemsAfter, "a refused transaction changed items: " + outcome);
                Assert.AreEqual(0, outcome.Coins, "a refused transaction reported coins moved: " + outcome);
            }

            Assert.GreaterOrEqual(coinsAfter, 0, "balance went negative");
            return outcome;
        }
    }
}
