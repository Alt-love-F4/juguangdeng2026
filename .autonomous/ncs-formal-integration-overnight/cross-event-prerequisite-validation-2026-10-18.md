# Cross-Event Prerequisite Validation

Date: 2026-10-18 05:53 +08:00
Task: 22
Status: Passed.

## Scope

This audit covers the nine formal roots and their authored map-action owners:

- `oct07.s1`
- `oct07.s2`
- `neutral.1008.s1`
- `rose.1009.s1`
- `rose.1011.s1`
- `church.1009.s2`
- `revolution.1013.s2`
- `revolution.1014.s1`
- `revolution.1028.s1`
- map actions `oct07.cafe.s1`, `neutral.1008.s1.action`, and `rose.1009.s1.action`

The authoritative source is `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`, sheet `Condition`. Generated data was produced by the official `Assets/ConfigTables/GenerateAll.bat`; Unity then refreshed the generated TextAssets before running `Spotlight/Narrative/Validate Luban`.

## Corrected source rows

The two Rose 1009 prerequisite rows now require the upstream completion fact, not the derived knowledge fact:

| Workbook row | Condition ID | Owner | Type | Target | Value |
|---:|---|---|---|---|---|
| 18 | `rose.1009.s1.meeting` | `rose.1009.s1` | event | `story.oct07.s2.completed` | `true` |
| 37 | `rose.1009.s1.action.meeting` | `rose.1009.s1.action` | action | `story.oct07.s2.completed` | `true` |

The source workbook SHA256 is `A17A3BC734A41634375C4B5519E6B75CE9F5D68FB48C84A763134FD82E18D574`. The pre-edit hash was `0B2825F6E692F657238F0EFFA63CBEDA4289E7493279C60923313A8EB85FB02A`.

The authored result `oct07.s2.meeting_known` remains present and writes `story.oct09.meeting.known=true` after `oct07.s2` completes. It is retained as a downstream fact and is not used as a substitute for the upstream completion prerequisite.

## Generated-data audit

`Assets/Settings/Resources/Luban/tbncscondition.json` contains both corrected Rose rows:

```text
rose.1009.s1.meeting        -> story.oct07.s2.completed=true
rose.1009.s1.action.meeting -> story.oct07.s2.completed=true
```

The formal BOOL condition audit found 23 rows across the formal event/action owners. It found zero cross-event prerequisite targets using `known`, `seen`, `reserved`, or `opened` without a `.completed` suffix. The only retained non-completion BOOL targets are local incomplete guards and the authored runtime map-active guards.

The cross-event completion dependencies are:

| Consumer | Required completed fact |
|---|---|
| `oct07.s2` | `story.oct07.s1.completed=true` |
| `rose.1009.s1` event and action | `story.oct07.s2.completed=true` |
| `rose.1011.s1` | `story.rose.1009.s1.completed=true` |
| `church.1009.s2` | `story.rose.1009.s1.completed=true` |
| `revolution.1013.s2` | `story.oct07.s2.completed=true` |
| `revolution.1014.s1` | `story.oct07.s2.completed=true` and `story.rose.1011.s1.completed=true` |
| `revolution.1028.s1` | `story.oct07.s2.completed=true` |

The generated `oct07.s2` results remain authoritative:

```text
oct07.s2.complete      SET_FLAG story.oct07.s2.completed=true
oct07.s2.meeting_known SET_FLAG story.oct09.meeting.known=true
```

## Report and queue regeneration

Unity menu `Spotlight/Narrative/Validate Luban` completed after the AssetDatabase refresh and regenerated both artifacts at 05:50:05 local time:

- `Docs/NcsIntegrationReport.md`: 38 event sections, 0 errors, 0 warnings; `rose.1009.s1` lists `story.oct07.s2.completed=true`.
- `Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv`: 38 rows; formal prerequisite rows contain no forbidden `known`, `seen`, `reserved`, or `opened` substitute.

Current hashes:

- `tbncscondition.json`: `CED79D3FC4FE7E011FC45776620A4A485C46B9C17496B9412DBD913D52911652`
- `NcsIntegrationReport.md`: `B3C4C31DE417E6BBE562D57057DA82E451363BDFCEDD4B904369F70377503651`
- `NcsIntegrationQueue.csv`: `25DF41F42CA6A4EEA2FF61B266C4E07D235A38CAF6B945AAEFBE273CDA9E8BEB`

The official generation log is `.autonomous/ncs-formal-integration-overnight/generation-20261018-task22.log`; its exit code is `0`, with Player semantic validation and Luban validation/data generation passed.

## Unity verification

After regeneration and refresh:

- `Spotlight.Narrative.Editor.Tests`: 28/28 passed, 0 failed, 0 skipped.
- `Spotlight.Narrative.Core.Tests`: 195/195 passed, 0 failed, 0 skipped.
- Unity compile ground truth remained `compilationFailed=false` and `compiling=false`.

The only current validation warning is the existing `[NCS] No guaranteed anchor events` advisory. It is unrelated to prerequisite target correctness; the generated report itself remains at 0 warnings.

## Conclusion

Task 22 passes. Formal cross-event gates now consume real `*.completed` facts. `story.oct09.meeting.known` remains an authored result of `oct07.s2`, but it is no longer used to prove that `oct07.s2` completed or that Rose 1009 was completed.
