# Incomplete, Blocked, and Partial Content Boundary

Date: 2026-10-18
Scope: Task 30 of `ncs-formal-integration-overnight`
Status: Passed.

## Source and generated event boundary

The authoritative `Narrative.xlsx` `Event` sheet contains 38 data rows. The
generated `Assets/Settings/Resources/Luban/tbncsevent.json` contains the same
38 unique event IDs. The nine formal roots remain the only formal story root
set, and the generated `EventDate`, `Result`, and queue tables contain no
entries for incomplete source identifiers.

The following incomplete or intentionally withheld content was checked:

| Content | Source status | Event / date / result / queue state |
|---|---|---|
| `rose.1016.s1` | Partial source text only; the handoff states that the source ends before the required ending and must not be formally released. | Absent from `Event`, `EventDate`, `Result`, and `NcsIntegrationQueue.csv`. The generated text table contains 9 staging keys whose comments identify the event as disabled; text staging does not create a playable event or completion fact. |
| `revolution.1029.s1` / `MA70.10.29-S1` | Empty source document; source-map and handoff explicitly prohibit creating story, choices, checks, results, or ending content from the outline. | Absent from `Event`, `EventDate`, `Result`, and `NcsIntegrationQueue.csv`; no generated text keys were found. |
| `MA70.11.05-S1` and `MA70.11.05-S2` | Title-only, unfinished Church source documents; the Church specification says not to construct formal event placeholders or automatic completion facts. | Absent from `Event`, `EventDate`, `Result`, and `NcsIntegrationQueue.csv`; no generated text keys were found. |
| `church.1014.s2` | Not present in the current formal source table; it remains an unconnected planned/special key rather than a fabricated event. | Absent from `Event`, `EventDate`, `Result`, and `NcsIntegrationQueue.csv`. |

No excluded identifier is present as a formal event, date route, result owner,
or queue row. No new trigger or completion flag was created for any of these
items.

## Disabled test endings and delivery fixtures

The authoritative `Ending` sheet and generated `tbncsending.json` each contain
only these two ending records:

- `ending_test_cumulative`, `enabled=false`
- `ending_test_consecutive`, `enabled=false`

Both have their required `ACTION_COMPLETED` condition rows, but neither is an
event result owner and `tbncsresult.json` contains zero result rows for either
ending ID. The associated delivery actions are explicit test fixtures:

- `test.delivery.enabled` has `initialValue=false` in `ConditionKey`.
- `act_test_delivery_cumulative` and `act_test_delivery_consecutive` both have
  `mustExecute=false`.
- Their four `EV_TEST_DELIVERY_*` events are classified as delivery-test events,
  not formal roots, and have no date trigger.

This keeps ending validation available through an explicit test/debug setup
without exposing a test ending or delivery fixture as formal story completion.

## Partial formal presentation isolation

`rose.1011.s1` has a formal presentation key
`rose.1011.s1.conversation` with 27 generated pages and a separate
`rose.1011.s1.fulltext.preview` key with 1 preview page. The page IDs do not
overlap. The preview tail therefore cannot become a formal page merely because
the source contains preview material. The formal event's configured result is
still the single `story.rose.1011.s1.completed=true` result owned by the formal
root and is not inferred from the preview key.

The current formal event table does not carry legacy `blockedBy` metadata for
these roots. Unresolved source blockers remain documented in the content-batch
specifications and are not converted into fabricated runtime facts, endings,
or triggers. Where a formal root is present, its completion boundary is the
authoritative configured presentation/result contract; preview-only and
unfinished content remains outside that contract.

## Revolution and NPC invariants

- `EV_REVOLUTION` and its two authored result rows remain present.
- `T_CAL_D60` remains the only trigger and has `enabled=false`.
- No `npc_a`, `npc_b`, `npc_c`, or `npc_d` event or trigger was restored.

## Verification

The assertion-based read-only audit reported:

```text
source_events=38 generated_events=38 exact_match=true
formal_roots=9 completion_results=9 exact_match=true
disabled_endings=2 ending_conditions=2 ending_results=0
triggers=1 enabled_triggers=0 trigger_event=T_CAL_D60->EV_REVOLUTION
test_delivery_enabled_initial=false test_delivery_must_execute=false
excluded_incomplete_ids_absent_from_events_dates_results_queue=true
rose1011_formal_pages=27 preview_pages=1 isolated=true
assertions=passed
```

The focused Unity EditMode test
`Spotlight.Narrative.Editor.Tests.NarrativeConfigurationValidatorTests.ActualGeneratedLubanDataLoadsAndValidates`
passed `1/1`, and the focused
`Spotlight.Narrative.Editor.Tests.FormalNarrativeFlowConfigurationTests`
selection passed `3/3` with no failures or skips. Unity remained outside Play
Mode, not compiling, on `6000.3.5f2`.

No workbook, generated JSON, runtime source, scene, prefab, save slot, or
formal result was changed for this task.
