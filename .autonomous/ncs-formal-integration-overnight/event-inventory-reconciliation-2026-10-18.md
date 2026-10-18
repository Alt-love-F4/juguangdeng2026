# Event Inventory Reconciliation

Date: 2026-10-18
Scope: Task 17 of `ncs-formal-integration-overnight`
Status: Passed.

## Count and identity checks

The current generated event table, integration report, and queue CSV were parsed read-only:

- Generated `tbncsevent.json`: 38 events.
- `Docs/NcsIntegrationReport.md`: 38 event sections and header count `Events: 38`.
- `Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv`: 38 data rows.
- Generated/report/queue event-ID sets: identical.
- Duplicate generated IDs: none.
- Duplicate report headings: none.
- Duplicate queue IDs: none.
- Queue statuses: every row is `config_pass`.
- Report statuses: every section is `static_pass`.

The different status labels are intentional layers: `config_pass` is the queue's configuration status, while `static_pass` is the generated integration report's static validation status. Neither label claims real map/DayFlow or runtime acceptance.

## Formal root reconciliation

Every formal root has exactly one row in each source and retains the same ID:

| Root | Entry | Queue source | Date route | Queue | Report |
|---|---|---|---|---|---|
| `oct07.s1` | `MapAction` | `oct07.cafe.s1 @ cafe` | none | `config_pass` | `static_pass` |
| `oct07.s2` | `BeforeNight` | day 2 / Night / Once | day 2 Night | `config_pass` | `static_pass` |
| `neutral.1008.s1` | `MapAction` | `neutral.1008.s1.action @ market` | none | `config_pass` | `static_pass` |
| `rose.1009.s1` | `MapAction` | `rose.1009.s1.action @ Saints Street` | none | `config_pass` | `static_pass` |
| `rose.1011.s1` | `MorningBeforeMap` | day 6 / Morning / Once | day 6 Morning | `config_pass` | `static_pass` |
| `church.1009.s2` | `Immediate` | day 4 / Night / Once | day 4 Night | `config_pass` | `static_pass` |
| `revolution.1013.s2` | `Immediate` | day 8 / Noon / Once | day 8 Noon | `config_pass` | `static_pass` |
| `revolution.1014.s1` | `MorningBeforeMap` | day 9 / Morning / Once | day 9 Morning | `config_pass` | `static_pass` |
| `revolution.1028.s1` | `BeforeNight` | day 23 / Night / Once | day 23 Night | `config_pass` | `static_pass` |

The six dated roots are present in the generated `tbncseventdate.json` with the declared day, time slot, and `once` repeat rule. The three map-action roots are intentionally absent from the date table because their source is an action rather than a date dispatch.

## Non-formal partition

The remaining 29 generated events have one of the documented non-formal roles:

- 12 `arrival.*` queued continuation events.
- 9 `EV_DEMO_CAFE_*` explicit debug/demo events.
- 4 `EV_TEST_DELIVERY_*` delivery-test events.
- 3 `EV_FIRST_DAY_PERCEPTION_*` first-day helper events.
- 1 retained `EV_REVOLUTION` legacy event.

`EV_REVOLUTION` remains present in the generated event table, report, and queue. Its trigger is still represented separately in `tbncstrigger.json` as `T_CAL_D60` with `enabled=false`; retaining this event does not promote it to a formal dated root.

## Conclusion

The generated event inventory, report, and queue have one authoritative row/section per event ID and a single, unambiguous formal-root classification. No event was silently dropped, duplicated, or promoted from demo/continuation/legacy content into the formal root set. Runtime acceptance remains a separate later phase.

