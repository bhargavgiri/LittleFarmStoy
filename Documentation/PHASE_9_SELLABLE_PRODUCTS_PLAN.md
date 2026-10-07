# Phase 9 Plan — Every Product Can Be Grown and Sold

**Status:** plan only. No code written.
**Written:** 2026-10-08
**Depends on:** Phase 8 (save/load) being tested and committed first. It is built but its Unity tests have not run yet.

Phase 9 is the first of three phases that lead to customers buying from the farm:

| Phase | What it adds |
|---|---|
| **9 (this plan)** | Every crop can be grown; every raw product can be sold at the Market shop |
| 10 | Customer characters who walk in with orders, at the farm gate and the Market |
| 11 | Dairy processing: butter and cheese made from milk |

---

## 1. Goal

When Phase 9 is done, a player can:

- buy **wheat, tomato and corn seeds** at the Market
- grow and harvest **all three fields** (today only wheat works)
- sell **wheat, tomato, corn, eggs and milk** at the Market
- keep cows fed indefinitely, because corn can now be grown

Nothing else changes. No customers, no butter or cheese, no new buildings.

## 2. Why this comes first

Read from the current code and data:

| Problem today | Evidence |
|---|---|
| Tomato and corn can never be planted | Starting inventory has only `seed_wheat`; the shop sells only wheat seeds. 27 of 47 plots are unusable. |
| Cows starve after 2 feedings | The player starts with 4 corn, a cow eats 2, and corn cannot be grown or bought. |
| Eggs and milk pile up with no use | The shop has no entry for them, and `VerifyBuild` (lines 726–728 of `FarmPrototypeBuilder.cs`) deliberately fails the build if an animal has a produce price. |
| The shop has two rows | `EnsureShopDefinition(wheat)` authors exactly `Shop_WheatSeeds` and `Shop_Wheat`. |

Customers in Phase 10 would have almost nothing to ask for until this is fixed.

## 3. Scope

### In scope
1. Shop catalogue grows from 2 entries to 8.
2. Prices for all eight entries, defined in one place.
3. HUD and storage sheet show tomato and the new seed types.
4. A tomato icon (the icon set has wheat, corn, egg, milk and a generic seed, but no tomato).
5. Shop panel layout that fits 3 buy rows and 5 sell rows on a phone.
6. Build checks and tests updated to match.

### Out of scope
- Customer characters and orders (Phase 10)
- Butter, cheese, any processing machine (Phase 11)
- Buying animals, new animals, new crops
- Level / XP
- Changing growth times, yields or animal timings
- Any change to `EconomyManager`, `CurrencyWallet` or `PlayerInventory` logic. The transaction code is already generic; this phase is data and UI.

## 4. Proposed prices (need your approval)

Crop prices below are the values already sitting on the crop assets (`seedCost` / `sellValue`). Egg and milk prices are new proposals.

| Item | Buy | Sell | Notes |
|---|---|---|---|
| Wheat Seeds | 2 | — | unchanged |
| Tomato Seeds | 9 | — | from `Crop_Tomato` |
| Corn Seeds | 14 | — | from `Crop_Corn` |
| Wheat | — | 4 | unchanged |
| Tomato | — | 22 | from `Crop_Tomato` |
| Corn | — | 34 | from `Crop_Corn` |
| Egg | — | 10 | **new, proposed** |
| Milk | — | 45 | **new, proposed** |

Rough earnings per cycle at these prices (average yield, authored speed):

| Loop | Cost in | Value out | Time |
|---|---|---|---|
| Wheat plot | 2 | 8 (2 wheat) | 18 s |
| Tomato plot | 9 | 33 (1.5 tomato) | 27 s |
| Corn plot | 14 | 51 (1.5 corn) | 36 s |
| Chicken | 1 wheat (worth 4) | 10–20 (1–2 eggs) | 25 s |
| Cow | 2 corn (worth 68) | 90–135 (2–3 milk) | 50 s |

**Balance point to decide:** a cow eats 68 coins' worth of corn per feeding. Milk has to sell well above 34 each or feeding a cow loses money. At 45 it earns 22–67 per cycle. If that feels too thin or too generous, the milk price is the number to change.

## 5. Changes, file by file

### 5.1 Data and scene builder — `Editor/FarmPrototypeBuilder.cs`
- Replace the two wheat price constants with one price table covering all eight items.
- `EnsureShopDefinition` takes all three crops and both animal definitions, and authors eight `ShopItemDefinition` assets. Item ids keep coming from the crop and animal assets, never typed by hand.
- `EnsureCrop` calls use the same table, so a crop asset and the shop can never quote different prices.
- Set `produceSellValue` on the chicken and cow definitions from the same table.
- `BuildResourceTable` adds tomato, tomato seeds and corn seeds so the storage sheet lists them.
- Starting inventory: **no change proposed** (10 wheat seeds, 6 wheat, 4 corn, 100 coins). Tomato and corn seeds are bought, which gives the 100 starting coins a purpose. See decision 3.

New assets created by the rebuild: `Shop_TomatoSeeds`, `Shop_CornSeeds`, `Shop_Tomato`, `Shop_Corn`, `Shop_Egg`, `Shop_Milk`.

### 5.2 Build checks — `VerifyBuild`
- **Remove** the two checks that require animal produce to have no price.
- **Add:** every crop has a purchasable seed entry and a sellable produce entry; every animal's product has a sellable entry; each shop price equals the crop or animal asset's price.
- **Add:** no item can be bought for less than it sells for (prevents an infinite-money loop if someone later makes an item both buyable and sellable).

### 5.3 Icons — `Editor/UiIconLibrary.cs`
- Add a `Tomato` icon to the procedural set (16 → 17 icons).
- Seed rows: reuse the one generic seed icon with the crop name beside it, or tint it per crop. See decision 4.

### 5.4 Shop panel — `Editor/HudBuilder.cs`, `Scripts/UI/ShopPanel.cs`
The shop card today is one fixed stack with no scrolling: 176 px per buy row, 128 px per sell row. With 3 + 5 rows it grows to roughly 1,400–1,500 px, which nearly fills a 1920-tall reference screen and will not survive Phase 11 adding butter and cheese.

Proposed: **two tabs, Buy and Sell**, each showing only its own rows. Small change, no scrolling code, and room to grow. Alternative is a scroll view. See decision 2.

`ShopBuyCard` and `ShopSellRow` need no logic change; they are already built per catalogue entry.

### 5.5 HUD top bar
Pinned chips stay as they are (coins, wheat seeds, wheat, eggs, milk). Everything else appears in the storage sheet. Adding more pinned chips would crowd a portrait screen.

### 5.6 Save compatibility
The Phase 8 save file stores inventory as plain item ids, so new ids need no format change and the save version stays at 1. An old save simply has zero of the new items.

## 6. Tests

### EditMode (`EconomyTests.cs`)
Existing 23 tests stay unchanged. Add:
- buying tomato and corn seeds charges the right price
- selling tomato, corn, egg and milk pays the right price
- egg and milk cannot be bought; seeds cannot be sold
- selling egg or milk with none owned is refused with no change

### Play Mode — new menu item `Run Phase 9 Verification`
Scripted, results written to `Documentation/PHASE_9_TEST_RESULTS.md`:
1. Shop lists 3 buy rows and 5 sell rows; both tabs open.
2. Buy tomato seeds → coins drop by the right amount.
3. Walk to the tomato field → till, plant, grow, harvest → tomato count rises.
4. Same for corn.
5. Sell tomato and corn → coins rise by the right amounts.
6. Feed a chicken, collect the egg, sell it.
7. Feed a cow with **grown** corn, collect milk, sell it.
8. Sell buttons are disabled at zero stock.
9. Save, change values, load → new items come back (re-uses the Phase 8 pattern).
10. Regression: wheat loop and shop freeze/unfreeze still work.

### Re-run for regression
Economy tests, Persistence tests, Phase 7 verification, Phase 8 verification. The Phase 7 suite asserts exact shop contents in places and may need small updates.

## 7. Done means

- Scene rebuild reports "verified PLAYABLE".
- All EditMode suites pass, read from their results files.
- `Run Phase 9 Verification` passes, read from its results file.
- Phase 7 and Phase 8 suites still pass.
- 0 compile errors.
- Committed and pushed.

## 8. Risks

| Risk | Handling |
|---|---|
| Shop UI overflows on small phones | Tabs (5.4); checked in the Play Mode suite |
| Milk price makes cows a loss or a money printer | Price table is one place; decision 1 |
| Phase 7 suite breaks on new shop rows | Update its assertions in the same change |
| Phase 8 still unverified when Phase 9 starts | Finish Phase 8 first; step 9 of the suite depends on it |

## 9. Decisions needed from you

1. **Prices.** Accept the table in section 4, or give your own numbers, especially egg (10) and milk (45).
2. **Shop layout.** Buy / Sell tabs (recommended) or one scrolling list.
3. **Starting inventory.** Keep as is and make the player buy tomato and corn seeds (recommended), or also give a few of each at the start.
4. **Seed icons.** One shared seed icon with the crop name (recommended, least work) or a tinted icon per crop.
5. **Order of work.** Finish and commit Phase 8 before starting (recommended), or start Phase 9 in parallel.

## 10. What Phase 10 will build on

Phase 9 leaves behind one price table and eight catalogue entries. Customers in Phase 10 can then pick orders from the sellable list and pay a premium over these shop prices, so serving a customer is always worth more than using the shop.
