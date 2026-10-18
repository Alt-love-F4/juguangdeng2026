# NCS Source and Generation Manifest

Captured: 2026-10-18 (Executor session 2, Task 3)

## Authoritative inputs

| Path | Size | Last write | SHA-256 | Role |
|---|---:|---|---|---|
| `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` | 104,547 bytes | 2026-10-18 01:53:49 | `0B2825F6E692F657238F0EFFA63CBEDA4289E7493279C60923313A8EB85FB02A` | Authoritative NCS source workbook |
| `Assets/ConfigTables/Data/Shared/Text.xlsx` | 208,986 bytes | 2026-10-18 01:02:33 | `CAD8194771CA4A0E3AC040A4D007191CA93E1DA846DD629D920BE0CE214F5276` | Authoritative shared text source workbook |
| `Assets/ConfigTables/Defines/schema.xml` | 21,676 bytes | 2026-10-17 12:12:03 | `A73582C487BFDE9BCADC8E0659C11271E756105461F6B66914817DEEB8B9D0E5` | 41 Luban bean/table definitions |
| `Assets/ConfigTables/luban.conf` | 349 bytes | 2026-10-02 00:41:09 | `B38242BB2B4F894554506F50AE5B3AF1C2D7B9AE6B66B830D98F0783FACDA9DA` | Client target, schema directory, data directory, output groups |
| `Assets/ConfigTables/GenerateAll.bat` | 3,476 bytes | 2026-10-13 21:46:35 | `EF5956E1348D5331C5A440EFC83FB7231C3281D30EBB229245E159448FA594A8` | Official merge, Player validation, and strict Luban generation entry point |
| `Assets/ConfigTables/validate_player.py` | 2,920 bytes | 2026-10-13 21:46:22 | `FCAE946FC9009BCF33C71CD4F77F47A2C8FD34CAECE82EFFB4264987F9D0960B` | Semantic validation invoked by `GenerateAll.bat` |

Only the two `.xlsx` files above are the authoritative Narrative/Text inputs for this task. There are no split `Narrative_*.xlsx` or `Text_*.xlsx` workbooks in the current source folders, so the batch file's merge branch is currently bypassed and the saved workbooks are used directly.

## Workbook structure

Read-only `openpyxl` inspection (`openpyxl 3.1.5`) found:

- `Narrative.xlsx`: 28 sheets. The NCS sheets are `Object`, `Bar`, `Trigger`, `Event`, `EventDate`, `Result`, `Condition`, `Template`, `SetupParams`, `Choice`, `OpeningPresentation`, `OpeningLine`, `Action`, `MapPosition`, `PresentationIndex`, `ConversationPage`, `ConversationLine`, `Notice`, `ReadingPage`, `ReadingLine`, `ConditionKey`, `ActionMilestone`, `Ending`, `CafeVisit`, `CafeVisitHook`, `AbilityCheck`, `AbilityCheckCondition`, and `CafeTutorialStep`.
- `Text.xlsx`: one `Text` sheet.
- Current workbook dimensions: Narrative sheets range from 4 to 919 rows and 4 to 19 columns; `ConversationLine` is 919 x 13 and `ConversationPage` is 116 x 12. `Text` is 2,015 x 4.

## Luban configuration

`luban.conf` currently defines the `c`/`s`/`e` groups, reads schemas from `Defines`, reads data from `Data`, and targets the `client` manager `Tables` with top module `cfg`. `schema.xml` maps `Text@Shared/Text.xlsx` to `TbText` and the Narrative sheets to the `TbNcs*` tables, including `Event`, `EventDate`, `Result`, `Condition`, `ConditionKey`, `Choice`, `ConversationPage`, `ConversationLine`, `ReadingPage`, `ReadingLine`, `PresentationIndex`, `Object`, `Action`, and `MapPosition`.

`GenerateAll.bat` resolves the official tool from `LUBAN_DIR`, then `D:\Luban\src\Luban\bin\Release\net8.0` when present, otherwise `%LOCALAPPDATA%\Luban\5.1.0\Luban`. It runs `validate_player.py` before invoking strict Luban with `cs-newtonsoft-json`, writing C# to `Assets/Scripts/Config/Generated` and JSON to `Assets/Settings/Resources/Luban`.

## Generated outputs

| Output set | Count | Time range | Aggregate manifest SHA-256 |
|---|---:|---|---|
| `Assets/Scripts/Config/Generated/*.cs` | 83 | 2026-10-05 23:08:02 to 2026-10-17 12:12:03 | Per-file hashes were captured during this inventory; generated subset below is the NCS comparison set. |
| NCS-related `*Ncs*.cs` | 50 | 2026-10-05 23:08:03 to 2026-10-17 12:12:03 | `94F78EF09705D2B104AE5DBD9C05E83D733076AC73A78E3D987A304DEE1C8E7E` |
| `Assets/Settings/Resources/Luban/*.json` | 41 | 2026-10-05 22:18:09 to 2026-10-18 01:55:18 | Per-file hashes were captured during this inventory; generated subset below is the NCS comparison set. |
| NCS-related `tbncs*.json` | 25 | 2026-10-13 19:54:34 to 2026-10-18 01:55:18 | `B86DD72499989564458E676D02E269C840190BD5EF23915A64F29DA2A109D52D` |

The aggregate manifest hash is computed from sorted records of `relative path | byte length | LastWriteTime (yyyy-MM-dd HH:mm:ss) | per-file SHA-256`, joined with LF and hashed as UTF-8. This makes later stale-output checks reproducible without storing generated files in the task directory.

### NCS generated file names

**C# (50):** `NcsAbilityCheckConditionEntry.cs`, `NcsAbilityCheckEntry.cs`, `NcsActionEntry.cs`, `NcsActionMilestoneEntry.cs`, `NcsBarEntry.cs`, `NcsCafeVisitEntry.cs`, `NcsCafeVisitHookEntry.cs`, `NcsChoiceEntry.cs`, `NcsConditionEntry.cs`, `NcsConditionKeyEntry.cs`, `NcsConversationLineEntry.cs`, `NcsConversationPageEntry.cs`, `NcsEndingEntry.cs`, `NcsEventDateEntry.cs`, `NcsEventEntry.cs`, `NcsMapPositionEntry.cs`, `NcsNoticeEntry.cs`, `NcsObjectEntry.cs`, `NcsPresentationIndexEntry.cs`, `NcsReadingLineEntry.cs`, `NcsReadingPageEntry.cs`, `NcsResultEntry.cs`, `NcsSetupParamsEntry.cs`, `NcsTemplateEntry.cs`, `NcsTriggerEntry.cs`, `TbNcsAbilityCheck.cs`, `TbNcsAbilityCheckCondition.cs`, `TbNcsAction.cs`, `TbNcsActionMilestone.cs`, `TbNcsBar.cs`, `TbNcsCafeVisit.cs`, `TbNcsCafeVisitHook.cs`, `TbNcsChoice.cs`, `TbNcsCondition.cs`, `TbNcsConditionKey.cs`, `TbNcsConversationLine.cs`, `TbNcsConversationPage.cs`, `TbNcsEnding.cs`, `TbNcsEvent.cs`, `TbNcsEventDate.cs`, `TbNcsMapPosition.cs`, `TbNcsNotice.cs`, `TbNcsObject.cs`, `TbNcsPresentationIndex.cs`, `TbNcsReadingLine.cs`, `TbNcsReadingPage.cs`, `TbNcsResult.cs`, `TbNcsSetupParams.cs`, `TbNcsTemplate.cs`, `TbNcsTrigger.cs`.

**JSON (25):** `tbncsabilitycheck.json`, `tbncsabilitycheckcondition.json`, `tbncsaction.json`, `tbncsactionmilestone.json`, `tbncsbar.json`, `tbncscafevisit.json`, `tbncscafevisithook.json`, `tbncschoice.json`, `tbncscondition.json`, `tbncsconditionkey.json`, `tbncsconversationline.json`, `tbncsconversationpage.json`, `tbncsending.json`, `tbncsevent.json`, `tbncseventdate.json`, `tbncsmapposition.json`, `tbncsnotice.json`, `tbncsobject.json`, `tbncspresentationindex.json`, `tbncsreadingline.json`, `tbncsreadingpage.json`, `tbncsresult.json`, `tbncssetupparams.json`, `tbncstemplate.json`, `tbncstrigger.json`.

## Key NCS output hashes

| Artifact | Bytes | Last write | SHA-256 |
|---|---:|---|---|
| `Assets/Settings/Resources/Luban/tbncsevent.json` | 16,558 | 2026-10-18 01:55:18 | `53709938E97091A62B8DEA5818AAED4A8532E588BDC8160AF4BEEFB400767544` |
| `Assets/Settings/Resources/Luban/tbncseventdate.json` | 803 | 2026-10-18 01:55:18 | `8D1018468879A56B5EA86242422C46AB7D59EFD9520A9FC0D65F278C74195BC1` |
| `Assets/Settings/Resources/Luban/tbncscondition.json` | 9,213 | 2026-10-18 01:55:18 | `D1C02895CD96E25802AB36CAA9A7643D14D2592FB82660EE6A602B7F9CB5B44C` |
| `Assets/Settings/Resources/Luban/tbncsresult.json` | 8,158 | 2026-10-18 01:55:18 | `FDA2E12597EE2DBDD45701AAEB9CAB81DF4A2DD9603048C94D33E01FDE20A60A` |
| `Assets/Settings/Resources/Luban/tbncsconversationpage.json` | 38,734 | 2026-10-18 01:05:44 | `03650FF8842DE8FE8E03355F842951B9D311E7220942936228167B5FFEA18B1F` |
| `Assets/Settings/Resources/Luban/tbncsconversationline.json` | 314,758 | 2026-10-18 01:05:44 | `46D5C6250BBCBB22DE33C6484C2F6CDA6A402559DE653E643C742EF43B95D930` |
| `Assets/Scripts/Config/Generated/TbNcsEvent.cs` | 1,728 | 2026-10-05 23:08:05 | `389D3D89B7B2CD3B774AADBA53E2E8EAE5D4E3F709AEBFF366FE400FCD4D75DA` |
| `Assets/Scripts/Config/Generated/TbNcsEventDate.cs` | 1,193 | 2026-10-05 23:08:05 | `A96E379BE8DE070BE330037312E177BDC7B3296F81A6217ECE0B0EF6482F7109` |
| `Assets/Scripts/Config/Generated/TbNcsConversationPage.cs` | 1,881 | 2026-10-10 10:23:02 | `EFE1B7913E6C12E117CE4ED61891CF6862B8C8CC0F6562748EF4A5E084D2D06C` |
| `Assets/Scripts/Config/Generated/TbNcsConversationLine.cs` | 1,744 | 2026-10-10 10:23:02 | `7F0BA9C3EDF4EB4A2DEBB62E5149D3E7AC19D06DDE7004C7A2A59861616AB315` |

## Boundary

This is a read-only inventory. No workbook, schema, Luban config, generated C# file, or generated JSON was modified while collecting it. Generation success and Unity import/compile status are separate later tasks.
