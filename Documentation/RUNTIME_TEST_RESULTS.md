# Runtime Test Results

Generated 2026-09-13 16:01:19 by `Little Farm Story/Run Phase 7 Runtime Verification`.

Every step below drove the real interaction chain in Play mode:
player position -> proximity scan -> priority -> focus -> Interact ->
state change -> inventory change -> visual change.

- **PASS** Scene: gameplay objects exist - 20 plots, 9 animals; coop 6/8, barn 3/4; seed_wheat=10, wheat=6, corn=4
- **PASS** Scene: economy and shop objects exist - wallet=100 coins; buy seed_wheat @2; sell wheat @4
- **PASS** HUD: starting coins and starting wheat seeds are visible - HUD shows coins=100, seed_wheat=10 - both read from the real components, not assumed
- **PASS** Shop: walk to the Market and open the shop - shop open, header coins=100; PlayerController and InteractionController both disabled while open
- **PASS** Shop: buy 5 Wheat Seeds via the real Plus/Buy buttons - clicked Plus x4 + Buy: coins 100 -> 90, seed_wheat 10 -> 15
- **PASS** Shop: close the shop; movement and interaction re-enable - shop closed; PlayerController and InteractionController both re-enabled
- **PASS** Farming: return to the plot - approach angle favoured a grid neighbour (Plot_1_0 [Till]); adopted it as the plot for the rest of this suite
- **PASS** Farming: till - state Empty -> Tilled, prompt now "Plant Wheat"
- **PASS** Farming: plant consumes a purchased seed - seed_wheat 15 -> 14, visual "Crop_wheat_Stage0(Clone)" spawned
- **PASS** Farming: crop grows to ready - reached stage 3 of 3 with the player 14m away, visual "Crop_wheat_Stage3(Clone)"
- **PASS** Farming: harvest grants Wheat - wheat 6 -> 7, plot cleared and the crop visual removed
- **PASS** Shop: return to the Market and reopen the shop - shop open, header coins=90; PlayerController and InteractionController both disabled while open
- **PASS** Shop: sell 1 Wheat via the real SellOne button - clicked SellOne: wheat 7 -> 6, coins 90 -> 94
- **PASS** Shop: an unaffordable purchase is refused and changes nothing - Purchase(57 seeds, cost 114) refused with NotEnoughCoins against a balance of 94; nothing changed
- **PASS** Shop: selling at zero inventory is disabled and refused - Sell All drained wheat to 0; SellOne shows disabled, and invoking its handler directly still changes nothing
- **PASS** Shop: close the shop; movement and interaction re-enable - shop closed; PlayerController and InteractionController both re-enabled
- **PASS** Regression: farming still works after the shop closes - approach angle favoured a grid neighbour (Plot_2_0 [Till]); adopted it as the plot for the rest of this suite
- **PASS** Regression: the plot can still be tilled - state Empty -> Tilled, prompt now "Plant Wheat"
- **PASS** Regression: the chicken can still be focused and fed - prompt reads "Chicken" (priority 30)
- **PASS** Regression: the cow can still be focused and fed - prompt reads "Cow" (priority 30)

## Result: ALL STEPS PASSED