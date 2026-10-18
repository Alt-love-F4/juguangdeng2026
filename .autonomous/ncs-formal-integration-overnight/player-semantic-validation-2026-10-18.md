# Player Semantic Validation

Date: 2026-10-18
Scope: Task 14 of `ncs-formal-integration-overnight`
Status: Passed.

## Direct validation

- Script: `Assets/ConfigTables/validate_player.py`
- Command: `python Assets/ConfigTables/validate_player.py`
- Working directory: `E:\Unity project\聚光灯2026`
- Complete output: `player-validation-20261018-052000.log`
- Exit code: `0`
- Output: `Player semantic validation passed.`

The direct run validates the Player workbook against the narrative Bar definitions, including:

- exactly one `player` PlayerConfig with non-negative money and action-point limits;
- the five required creation groups and unique sequences;
- unique creation option IDs and per-group sequences;
- legal starting money/origin ordering and specialization ownership;
- the four formal player attributes and their internal NCS Bar references;
- unique creation bonus pairs.

## Source boundary

The authoritative workbooks remained unchanged during the direct validation:

```text
0B2825F6E692F657238F0EFFA63CBEDA4289E7493279C60923313A8EB85FB02A  Assets/ConfigTables/Data/Narrative/Narrative.xlsx
CAD8194771CA4A0E3AC040A4D007191CA93E1DA846DD629D920BE0CE214F5276  Assets/ConfigTables/Data/Shared/Text.xlsx
```

The same validator also passed inside the official `GenerateAll.bat` run recorded by Task 13. This direct run is separate evidence and does not replace Luban strict validation or Unity compilation/testing.
