# Generated Output Reconciliation

Date: 2026-10-18
Scope: Task 16 of `ncs-formal-integration-overnight`
Status: Passed.

## Schema and output-set comparison

The comparison read `Assets/ConfigTables/Defines/schema.xml`, `Assets/ConfigTables/luban.conf`, both authoritative workbook trees, all generated JSON files, and all generated C# files. It did not modify any input or output.

| Check | Result |
|---|---|
| Schema table definitions | 41 |
| Schema bean definitions | 41 |
| Expected JSON files | 41 |
| Actual JSON files | 41 |
| Missing JSON files | None |
| Unexpected JSON files | None |
| Expected C# files | 83 (`41` table classes + `41` entry classes + `Tables.cs`) |
| Actual C# files | 83 |
| Missing C# files | None |
| Unexpected C# files | None |

Expected file names were derived from the schema table and bean names, not from the current generated directory listing. JSON names were matched using the existing Luban lower-case naming convention; C# names were matched to the generated table and bean names.

## Source input comparison

Every schema input was present. For the `@`-qualified inputs, the named sheet was present in the referenced workbook:

- `Narrative.xlsx`: all 28 configured Narrative sheets were found.
- `Text.xlsx`: the configured `Text` sheet was found.
- `Player.xlsx`: all configured Player sheets were found.
- The configured Simulation workbooks were present for the legacy non-NCS tables.

For every table, the number of generated JSON records equals the number of non-empty source rows after the four Luban header rows. No source/generated row-count mismatch was found. The NCS-relevant counts include:

- `TbNcsEvent`: 38
- `TbNcsEventDate`: 6
- `TbNcsResult`: 33
- `TbNcsCondition`: 39
- `TbNcsConversationPage`: 112
- `TbNcsConversationLine`: 915
- `TbNcsPresentationIndex`: 25
- `TbText`: 2,011

The complete comparison covered all 41 tables; no mismatch was reported.

## Field and generated-code comparison

For each table, the comparison derived the expected field set from the referenced schema bean and lower-camel-cased it according to Luban's JSON convention. It then compared that set with:

1. the union of fields present in the generated JSON records; and
2. the `GetValue("...")` constructor bindings in the generated entry C# file.

Results:

- JSON field mismatches: none.
- Generated C# constructor field mismatches: none.
- Missing or extra schema fields in generated outputs: none.

The empty `tbncsnotice.json` is expected: its source sheet has only the four Luban header rows. Its generated `NcsNoticeEntry` still contains all seven schema fields, so the empty data file is not a missing-output condition.

## Current generated diff boundary

`git diff --name-only -- Assets/Scripts/Config/Generated Assets/Settings/Resources/Luban` reports exactly these four paths:

- `Assets/Settings/Resources/Luban/tbncscondition.json`
- `Assets/Settings/Resources/Luban/tbncsevent.json`
- `Assets/Settings/Resources/Luban/tbncseventdate.json`
- `Assets/Settings/Resources/Luban/tbncsresult.json`

No generated C# file changed. No generated JSON file outside the four expected narrative outputs changed. No untracked generated output was found in either generated directory. The four JSON differences are retained as outputs of the strict generation run and are not hand-edited.

## Conclusion

The generated C# and JSON trees are complete and structurally consistent with the current schema and authoritative workbook inputs. No stale, missing, unexpected, or field-incomplete generated artifact was found. Event/report/queue reconciliation remains the next ordered task because this comparison does not assign formal status to event roots.

