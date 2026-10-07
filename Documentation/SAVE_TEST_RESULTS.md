# Runtime Test Results

Generated 2026-09-27 01:52:37 by `Little Farm Story/Run Phase 8 Save Verification`.

Every step below drove the real interaction chain in Play mode:
player position -> proximity scan -> priority -> focus -> Interact ->
state change -> inventory change -> visual change.

- **PASS** Scene: gameplay objects exist - 20 plots, 9 animals; coop 6/8, barn 3/4; seed_wheat=10, wheat=6, corn=4
- **PASS** Scene: economy and shop objects exist - wallet=100 coins; buy seed_wheat @2; sell wheat @4
- **PASS** Scene: the SaveManager exists and is wired - SaveManager found; save path C:/Users/BHAVDEEP/AppData/LocalLow/DefaultCompany/LittleFarmStory\littlefarmstory.save.json
- **PASS** Setup: build a farm state worth saving - coins=123, seeds=9, Plot_0_0 is Planted, farmer at (6.3, 0.0, -12.5)
- **PASS** Save: writing produces a real file on disk - wrote 18401 bytes to C:/Users/BHAVDEEP/AppData/LocalLow/DefaultCompany/LittleFarmStory\littlefarmstory.save.json
- **PASS** Save: the file's contents match the live farm - file holds 123 coins, 9 seeds, 47 plots across 3 fields, 9 animals
- **PASS** Change: every saved value is deliberately changed - coins 123->623, seeds 9->86, plot Planted->Empty, hunger 0.25->0.95
- **PASS** Load: coins and inventory come back from the file - coins restored to 123, wheat seeds to 9
- **PASS** Load: the plot's crop and growth come back - Plot_0_0 restored to Planted growing wheat
- **PASS** Load: the farmer returns to where he was saved - farmer restored to within 0.00m of (6.3, 0.0, -12.5)
- **PASS** Load: the chicken's hunger comes back - chicken hunger restored to 0.25
- **PASS** Load: the HUD follows the restore without being told - HUD coin display followed the restore to 123 through CurrencyWallet.BalanceChanged alone
- **PASS** Delete: removing the save leaves nothing behind - save file deleted; the next run starts from a new game
- **PASS** Regression: the restored crop can be cleared - Plot_0_0 cleared back to Empty and re-assigned wheat
- **PASS** Regression: farming still works after a load - prompt reads "Till"
- **PASS** Regression: the plot can still be tilled - state Empty -> Tilled, prompt now "Plant Wheat"
- **PASS** Regression: the chicken can still be focused - prompt reads "Chicken" (priority 30)
- **PASS** Regression: the cow can still be focused - prompt reads "Cow" (priority 30)

## Result: ALL STEPS PASSED