# Persistence Test Results

Generated 2026-09-27 01:51:13 by `Little Farm Story/Run Persistence Tests`.
Executed by the Unity Test Framework inside the running Editor.

**17 passed, 0 failed, 0 other, 17 total.**

- **PASS** `A_AnimalSnapshot_KeepsNeedsAndProductionPhase` (30 ms)
- **PASS** `A_PlayerSnapshot_KeepsPositionAndFacing` (1 ms)
- **PASS** `A_PlotSnapshot_KeepsItsEnumAndGrowthProgress` (2 ms)
- **PASS** `A_SaveData_SurvivesAJsonRoundTrip_Intact` (1 ms)
- **PASS** `B_Delete_RemovesTheFile` (9 ms)
- **PASS** `B_ReadWithNoFile_ReturnsFalseAndNoData` (1 ms)
- **PASS** `B_RefusesToWriteNull` (4 ms)
- **PASS** `B_WriteLeavesNoTemporaryFileBehind` (3 ms)
- **PASS** `B_WriteThenRead_ReturnsTheSameFarm` (5 ms)
- **PASS** `B_WritingTwice_ReplacesRatherThanAppends` (11 ms)
- **PASS** `C_AFileFromANewerBuild_IsIgnoredRatherThanLoadedWrongly` (10 ms)
- **PASS** `C_AnEmptyFile_IsIgnoredRatherThanLoaded` (3 ms)
- **PASS** `D_RestoreAll_ClampsNegativeAmountsAndSkipsBlankIds` (3 ms)
- **PASS** `D_RestoreAll_ReplacesTheWholeStore` (1 ms)
- **PASS** `D_RestoreAll_ReportsItemsThatDroppedToZero` (1 ms)
- **PASS** `D_RestoreAll_WithNothingSaved_ClearsTheStore` (1 ms)
- **PASS** `E_InventoryAll_RoundTripsThroughSaveDataEntries` (2 ms)
