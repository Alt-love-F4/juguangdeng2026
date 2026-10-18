# NPC Test Narrative Boundary Recheck

Date: 2026-10-18
Scope: Task 32 of `ncs-formal-integration-overnight`
Status: Verified. No project asset, workbook, generated-data file, scene, runtime source, or save slot was modified by this recheck.

## Boundary

The removed `npc_a`, `npc_b`, `npc_c`, and `npc_d` test narratives must not reappear in authoritative source, generated narrative data, runtime-loaded NCS tables, formal triggers, or formal event references. Historical documentation and explicitly identified legacy/manual snapshots remain outside the authoritative narrative boundary.

## Authoritative workbook audit

A Python `openpyxl` audit with an explicit UTF-8 token regex scanned every sheet in all 10 workbooks under `Assets/ConfigTables/Data`:

- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`: 28 sheets, 0 matches.
- `Assets/ConfigTables/Data/Shared/Text.xlsx`: 1 sheet, 0 matches.
- `Assets/ConfigTables/Data/Player/Player.xlsx`: 5 sheets, 0 matches.
- The seven Simulation workbooks (`Device`, `GameParam`, `Ingredient`, `Npc`, `Recipe`, `RecipeStep`, `WorldObject`): 0 matches.

The authoritative Narrative and Text source hashes at this recheck are:

```text
70F9055F23AA7CC3E05E120E5A07674272E8A26D4DE9DE5F8658DE88F51E3175  Assets/ConfigTables/Data/Narrative/Narrative.xlsx
CAD8194771CA4A0E3AC040A4D007191CA93E1DA846DD629D920BE0CE214F5276  Assets/ConfigTables/Data/Shared/Text.xlsx
```

## Generated and formal-route audit

- All 41 JSON files under `Assets/Settings/Resources/Luban` parsed as UTF-8 JSON and contain 0 exact matches. This includes `tbncsobject.json` (45 records), `tbncsevent.json` (38 records), `tbncstrigger.json` (1 record), and `tbnpc.json` (3 records).
- `Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv`: 0 matches.
- `Docs/NcsIntegrationReport.md`: 0 matches.
- The current generated hashes for the primary narrative tables remain:

```text
AE31B8D7DD1CB2590A67393557F4E42804F0BE889BEC45FDE05DB06DBE0D2935  Assets/Settings/Resources/Luban/tbncsobject.json
53709938E97091A62B8DEA5818AAED4A8532E588BDC8160AF4BEEFB400767544  Assets/Settings/Resources/Luban/tbncsevent.json
074ED94516D6F78794EF61A14A377F9B71F6DCC0FA4845E444267097461929B5  Assets/Settings/Resources/Luban/tbncstrigger.json
```

## Runtime-loaded verification

Connected Unity Editor: `6000.3.5f2`, stopped, compile-complete, `compilationFailed=false`.

`Spotlight.Narrative.LubanNarrativeLoader.Load()` returned:

```text
objects=45;events=38;triggers=1;npcs=42
```

The runtime assertion found no exact old IDs in object IDs, event IDs/actor/target/content references, or trigger IDs/event/bar references. `Resources.LoadAll<TextAsset>("Luban")` loaded 41 table assets and found 0 raw-token matches. Result:

```text
objects=45;events=38;triggers=1;badObjectIds=;badEventIds=;badTriggerIds=;loadedLubanAssets=41;badRawAssets=
```

Focused Unity EditMode tests passed:

- `Spotlight.Narrative.Editor.Tests.NarrativeConfigurationValidatorTests.ActualGeneratedLubanDataLoadsAndValidates`: 1 passed, 0 failed, 0 skipped.
- `Spotlight.Narrative.Editor.Tests.FormalNarrativeFlowConfigurationTests`: 3 passed, 0 failed, 0 skipped.

Post-test Unity ground truth: `compilationFailed=false`, `compiling=false`, `consoleErrors=0`, `consoleWarnings=0`.

## Non-authoritative references

The exact IDs remain only in the previously documented legacy/manual snapshots:

- `Assets/Resources/Characters/CharacterCatalog.asset`: 4 matches.
- `Assets/Resources/DetailsDisplayConfig.asset`: 13 matches.
- `Docs/NcsFormalDependencies.md`: 4 historical boundary mentions.

The scan of `Assets/Scripts` found 0 exact matches. The unrelated `npc_chen.trust` test fixture and the `npc_chen` examples in `narrative-config-tool.html` are not any of the removed A/B/C/D IDs. No deleted NPC test narrative, formal trigger, event, speaker, prerequisite, or result reappeared.

## Acceptance boundary

This recheck proves the authoritative workbook, generated tables, runtime-loaded resources, formal queue/report, and source-code boundary. It does not claim real map/DayFlow, loading, cancellation, retry, save/restore, exactly-once settlement, or Player acceptance.
