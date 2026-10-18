# NCS Event Inventory

Captured: 2026-10-18 (Executor session 2, Task 2)

## Reconciled sources

| Source | Observed state | Interpretation |
|---|---|---|
| `Docs/NcsIntegrationReport.md` | 38 event headings; `Errors: 0`; `Warnings: 0`; every heading is `static_pass` | Current generated static scan. It proves configuration shape, not real map/DayFlow acceptance. |
| `Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv` | 38 CSV rows; every row has `status=config_pass` | Current queue snapshot. It is the per-event integration index, not runtime evidence. |
| `Docs/NcsFormalDependencies.md` | 9 formal story roots listed; all require real map/DayFlow, loading, cancellation, retry, save/restore, or exactly-once evidence | Current acceptance boundary and source of the formal-root set. |
| `Docs/superpowers/reports/2026-10-15-october07-acceptance.md` | October 7 S1/S2 historical acceptance; 59/59 direct Editor assertions; Unity Test Framework runner unavailable | Existing evidence applies to `oct07.s1`/`oct07.s2` only and is not a current all-event pass. |
| `Docs/superpowers/reports/2026-10-16-rose-content-implementation.md` | Rose text/config work and a blocked `rose.1009.s1` intermediate snapshot; no real-map or shared-scene acceptance | Historical batch evidence. It must not override the current generated inventory without a newer runtime result. |
| `Docs/superpowers/reports/2026-10-16-A-neutral-implementation.md` | A01-A05 not formally configured or runtime-tested | Neutral continuation content remains outside the current 9-root formal set. |
| `Docs/superpowers/reports/2026-10-16-D-church-implementation.md` | Church 1009 text-only delivery; generation initially blocked by the shared `speakerNameKey` schema mismatch; no runtime acceptance | Historical partial delivery, not proof of `church.1009.s2` playability. |
| `Docs/superpowers/reports/2026-10-16-content-batch-E-church-engineering.md` | E text delivery; later official generation/compiler/config-load success, but no formal E root or real playable chain | `church.1104.s1` is intentionally not in the current formal-root set. |
| `Docs/superpowers/specs/2026-10-16-content-batches/*/handoff.md` | A/B/C/D/E handoffs define additional planned content, dependencies, and blockers | Handoff specifications are source context only; they do not add roots to the current generated inventory. |

## Current partition

The 38 current rows reconcile as follows:

| Category | Count | IDs |
|---|---:|---|
| Formal story roots | 9 | `oct07.s1`, `oct07.s2`, `neutral.1008.s1`, `rose.1009.s1`, `rose.1011.s1`, `church.1009.s2`, `revolution.1013.s2`, `revolution.1014.s1`, `revolution.1028.s1` |
| Arrival continuation chain | 12 | `arrival.start`, `arrival.insight`, `arrival.request`, `arrival.decision`, `arrival.refuse`, `arrival.accept`, `arrival.reply.1`, `arrival.reply.2`, `arrival.agent`, `arrival.check`, `arrival.leave`, `arrival.complete` |
| Cafe demo/support events | 9 | `EV_DEMO_CAFE_ARRIVE`, `EV_DEMO_CAFE_BEFORE`, `EV_DEMO_CAFE_CANCEL`, `EV_DEMO_CAFE_CHAT`, `EV_DEMO_CAFE_DELAYED_RESERVE`, `EV_DEMO_CAFE_ENDED`, `EV_DEMO_CAFE_LEAVING`, `EV_DEMO_CAFE_RESERVE`, `EV_DEMO_CAFE_SERVED` |
| Delivery-test events | 4 | `EV_TEST_DELIVERY_CONSECUTIVE`, `EV_TEST_DELIVERY_CONSECUTIVE_MILESTONE`, `EV_TEST_DELIVERY_CUMULATIVE`, `EV_TEST_DELIVERY_CUMULATIVE_MILESTONE` |
| First-day perception helpers | 3 | `EV_FIRST_DAY_PERCEPTION_CHECK`, `EV_FIRST_DAY_PERCEPTION_FAILURE`, `EV_FIRST_DAY_PERCEPTION_SUCCESS` |
| Retained legacy Revolution event | 1 | `EV_REVOLUTION` |

The category counts sum to 38 and match both the generated report and the queue row count.

## Formal root inventory

The following table combines the queue entry/source metadata with the generated report's enforced conditions and presentation counts. Empty `day` cells in the queue are expected for MapAction rows; the generated report is the source for their day condition.

| Root | Entry/source | Current conditions and prerequisite facts | Presentation | Checks | Root result | Static state |
|---|---|---|---|---|---|---|
| `oct07.s1` | `MapAction`; `oct07.cafe.s1 @ cafe` | Day 2; `story.oct07.s1.completed=false` | `Conversation/oct07.s1.conversation`; 2 pages / 40 lines / 0 choices | none | `story.oct07.s1.completed=true` | `static_pass` / `config_pass` |
| `oct07.s2` | `BeforeNight`; day 2 / Night / Once | `story.oct07.s1.completed=true`; own completion false | `Conversation/oct07.s2.conversation`; 7 pages / 34 lines / 0 choices; 2 reading pages / 7 reading lines | `oct07.s2.perception` | `story.oct07.s2.completed=true`; `story.oct09.meeting.known=true` | `static_pass` / `config_pass` |
| `neutral.1008.s1` | `MapAction`; `neutral.1008.s1.action @ market` | Day 3; `world.map.active=true`; own completion false | `Conversation/neutral.1008.s1`; 9 pages / 60 lines / 3 choices | `neutral.1008.s1.q1b` | `story.neutral.1008.s1.completed=true` | `static_pass` / `config_pass` |
| `rose.1009.s1` | `MapAction`; `rose.1009.s1.action @ Saints Street` (queue venue text) | Day 4; `story.oct09.meeting.known=true`; generated report does not list an own completion-false gate | `Conversation/rose.1009.s1.conversation`; 9 pages / 67 lines / 2 choices | `rose.1009.s1.perception` | `story.rose.1009.s1.completed=true` | `static_pass` / `config_pass` |
| `church.1009.s2` | `Immediate` entry; day 4 / Night / Once | `story.rose.1009.s1.completed=true`; own completion false | `Conversation/church.1009.s2.conversation`; 4 pages / 36 lines / 2 choices | none | `story.church.1009.s2.completed=true` | `static_pass` / `config_pass` |
| `rose.1011.s1` | `MorningBeforeMap`; day 6 / Morning / Once | `story.rose.1009.s1.completed=true`; own completion false | `Conversation/rose.1011.s1.conversation`; 27 pages / 123 lines / 11 choices; 1 reading page / 3 reading lines | `rose.1011.s1.check.sparrow`, `.check.portrait`, `.check.wisdom` | `story.rose.1011.s1.completed=true` | `static_pass` / `config_pass` |
| `revolution.1013.s2` | `Immediate` entry; day 8 / Noon / Once | `story.oct07.s2.completed=true`; own completion false | `Conversation/revolution.1013.s2.conversation`; 6 pages / 52 lines / 3 choices | none | `story.revolution.1013.s2.completed=true` | `static_pass` / `config_pass` |
| `revolution.1014.s1` | `MorningBeforeMap`; day 9 / Morning / Once | `story.oct07.s2.completed=true`; `story.rose.1011.s1.completed=true`; own completion false | `Conversation/revolution.1014.s1.conversation`; 23 pages / 171 lines / 12 choices; 2 reading pages / 1 reading line | `wisdom4`, `perception6`, `wisdom5` | `story.revolution.1014.s1.completed=true` | `static_pass` / `config_pass` |
| `revolution.1028.s1` | `BeforeNight`; day 23 / Night / Once | `story.oct07.s2.completed=true`; own completion false | `Conversation/revolution.1028.s1.conversation`; 10 pages / 144 lines / 3 choices | none | `story.revolution.1028.s1.completed=true` | `static_pass` / `config_pass` |

## Cross-report reconciliation

- The current generated artifacts expose nine roots, while the A/B/C/D/E handoffs describe broader planned content. Planned or text-only events such as `neutral.1010.*`, `church.1014.*`, `church.1104.s1`, and later continuations are not current formal roots and must not be inferred from handoff prose alone.
- The October 7 report is the only handoff-level runtime evidence in this set. It covers the existing `oct07` path, isolated temporary save snapshots, cancellation/retry behavior, and the shared presentation debug workflow; it explicitly says the direct Editor assertions are not a full Unity Test Framework run.
- Rose, Neutral, Revolution, and Church batch reports document text extraction, source mapping, configuration attempts, or blockers. They do not establish real-map, DayFlow, save/restore, cancellation, retry, and exactly-once acceptance for the current roots.
- Intermediate batch reports record a `rose.1009.s1` blocked configuration and a `rose.1009.s1.eventKind=narrative` generation failure at different points in time. The current report/queue snapshot is the current static inventory; the historical failures remain evidence that runtime acceptance must be re-run, not reasons to silently rewrite the current source.
- `EV_REVOLUTION` remains present in the current inventory. The legacy `T_CAL_D60` enablement invariant is preserved for the later dedicated check; this inventory does not treat the retained event as an active formal story root.

## Acceptance boundary

All 38 entries currently pass the static/configuration scan. Only `oct07.s1` and `oct07.s2` have the historical real-map acceptance report described above. The other seven formal roots remain configuration-pass/static-pass only until the runtime lifecycle and event-specific tasks produce current evidence.
