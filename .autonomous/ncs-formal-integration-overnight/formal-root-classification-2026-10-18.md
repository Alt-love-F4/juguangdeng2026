# Formal Root Classification

Date: 2026-10-18
Scope: Task 18 of `ncs-formal-integration-overnight`
Status: Passed.

## Exhaustive partition

The classification was applied to the 38 generated `Event` rows and checked against the 38 non-empty `Event` source rows in `Narrative.xlsx`. The source and generated event-ID sets match exactly. Every event belongs to exactly one category; no event was unclassified or multiply classified.

| Category | Count | Rule |
|---|---:|---|
| Formal story roots | 9 | Explicit root set below |
| Arrival continuations | 12 | ID starts with `arrival.` |
| Cafe demos | 9 | ID starts with `EV_DEMO_CAFE_` |
| Delivery tests | 4 | ID starts with `EV_TEST_DELIVERY_` |
| First-day helpers | 3 | ID starts with `EV_FIRST_DAY_PERCEPTION_` |
| Retained Revolution legacy event | 1 | ID is `EV_REVOLUTION` |
| Unclassified | 0 | Must remain zero |
| **Total** | **38** | |

## Formal root set

The authoritative formal roots are:

- `oct07.s1`
- `oct07.s2`
- `neutral.1008.s1`
- `rose.1009.s1`
- `rose.1011.s1`
- `church.1009.s2`
- `revolution.1013.s2`
- `revolution.1014.s1`
- `revolution.1028.s1`

Each root is present in the source workbook, generated event JSON, integration report, and queue. Each has `onceOnly=true`, `presentationClass=Conversation`, and a non-empty configured presentation key. The `rose.1009.s1` and `rose.1011.s1` rows additionally use the authored `Tpl_ArrivalConversation` setup type; that is a presentation setup choice and does not move them into the `arrival.*` continuation category.

## Non-formal boundaries

- `arrival.*` entries are queued continuation nodes, not additional formal roots.
- `EV_DEMO_CAFE_*` entries remain explicit debug/demo entrances.
- `EV_TEST_DELIVERY_*` entries remain delivery-action test coverage.
- `EV_FIRST_DAY_PERCEPTION_*` entries remain first-day helper events.
- `EV_REVOLUTION` and its authored content/results remain present. It is classified as a retained legacy event, not deleted or promoted into the dated formal root set. Its separate `T_CAL_D60` trigger remains disabled as verified in the Revolution invariant evidence.

No event was removed, renamed, copied into a new parallel narrative, or assigned a fabricated formal completion role during this classification.

