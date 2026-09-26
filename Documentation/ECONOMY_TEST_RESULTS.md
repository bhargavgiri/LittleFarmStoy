# Economy Test Results

Generated 2026-09-13 15:43:02 by `Little Farm Story/Run Economy Tests`.
Executed by the Unity Test Framework inside the running Editor.

**23 passed, 0 failed, 0 other, 23 total.**

- **PASS** `A_StartingCoins_UnconfiguredWalletDefault_Is100` (19 ms)
- **PASS** `A_StartingCoins_WalletAuthoredFromSettings_Is100` (1 ms)
- **PASS** `B_BuyFiveWheatSeeds_ById_BehavesIdentically` (4 ms)
- **PASS** `B_BuyFiveWheatSeeds_Costs10_AndAddsFiveSeeds` (1 ms)
- **PASS** `C_PurchaseAfterSpendingDown_IsRejected_AndChangesNothing` (1 ms)
- **PASS** `C_PurchaseBeyondBalance_IsRejected_AndChangesNothing` (1 ms)
- **PASS** `C_PurchaseCostingExactlyTheBalance_Succeeds_AndLeavesZero` (0 ms)
- **PASS** `D_SellOneOwnedWheat_RemovesOne_AndPaysFour` (1 ms)
- **PASS** `E_SellMoreWheatThanOwned_IsRejected_WithNoPartialSale` (1 ms)
- **PASS** `E_SellWheatWhenOwningNone_IsRejected_AndChangesNothing` (1 ms)
- **PASS** `F_NegativeOrZeroPurchaseQuantity_IsRejected_AndChangesNothing(-1)` (1 ms)
- **PASS** `F_NegativeOrZeroPurchaseQuantity_IsRejected_AndChangesNothing(-5)` (1 ms)
- **PASS** `F_NegativeOrZeroPurchaseQuantity_IsRejected_AndChangesNothing(0)` (1 ms)
- **PASS** `F_NegativeOrZeroSellQuantity_IsRejected_AndChangesNothing(-1)` (1 ms)
- **PASS** `F_NegativeOrZeroSellQuantity_IsRejected_AndChangesNothing(-5)` (1 ms)
- **PASS** `F_NegativeOrZeroSellQuantity_IsRejected_AndChangesNothing(0)` (0 ms)
- **PASS** `F_WalletRejectsNegativeAmounts_OnBothPaths` (4 ms)
- **PASS** `G_BurstOfDistinctValidPurchases_EachIsProcessedExactlyOnce` (1 ms)
- **PASS** `G_BurstOfIdenticalPurchases_IsProcessedExactlyOnce` (1 ms)
- **PASS** `G_BurstOfIdenticalSales_IsProcessedExactlyOnce` (1 ms)
- **PASS** `G_InvalidCallsInterleavedWithValidOnes_CannotCorruptState` (4 ms)
- **PASS** `G_PurchaseThatWouldOverfillTheStack_SpendsNoCoins` (1 ms)
- **PASS** `G_WithDuplicateWindowOff_EveryRepeatIsProcessedExactlyOnce` (1 ms)
