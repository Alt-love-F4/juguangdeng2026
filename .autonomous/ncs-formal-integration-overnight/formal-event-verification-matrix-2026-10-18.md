# Formal Event Verification Matrix

Date: 2026-10-18
Scope: Task 10 of `ncs-formal-integration-overnight`
Status: Matrix established from the current generated report, queue, dependency report, source inventory, presentation inventory, and runtime path trace. This document does not claim runtime acceptance where it is marked pending.

## Evidence keys

- `T`: trigger/route and prerequisite facts are present in the current source/generated queue and report.
- `P`: presentation/setup, page/line/choice/check/reading metadata is present in the current report; resource caveats are listed in the presentation inventory.
- `C1`: code-path evidence covers cancellation, panel/effect cleanup, no settlement, and stale-callback invalidation; real event-specific cancellation is pending.
- `R1`: code-path evidence leaves failed work retryable from the original valid route; real retry is pending.
- `S1`: code-path evidence covers `blocksSave`, snapshot, restore, and resume ownership; event-specific save/restore acceptance is pending.
- `E1`: configured completion/result or continuation is known and the controller's exactly-once settlement guard is traced; duplicate-input/reload acceptance is pending.
- `A`: current real-map/DayFlow acceptance evidence exists. `A07` means the historical October 7 report only; it is not a current full Unity Test Framework run.
- `U`: runtime acceptance is not yet evidenced for this event.

The shared lifecycle codes are intentionally repeated in every row so each event has an explicit cancellation, retry, save/restore, and completion boundary.

## Formal story roots

| Event | Trigger / entry | Prerequisites | Presentation | Cancellation | Retry | Save/restore | Completion/result | Downstream unlock | Evidence status |
|---|---|---|---|---|---|---|---|---|---|
| `oct07.s1` | `MapAction`; `oct07.cafe.s1 @ cafe`; day 2 action path | own `completed=false`; formal day-2/action availability | `Conversation/oct07.s1.conversation`; 2 pages / 40 lines | `C1` | `R1` | `S1` | `SetFlag story.oct07.s1.completed`; `E1` | Enables `oct07.s2` | `T/P/E1/A07` historical only; `U` current rerun |
| `oct07.s2` | `BeforeNight`; day 2 / Night / once | `story.oct07.s1.completed=true`; own completion false | Conversation; 7 pages / 34 lines; 2 reading pages / 7 reading lines; `oct07.s2.perception` | `C1` | `R1` | `S1` | `SetFlag story.oct07.s2.completed`; `SetFlag story.oct09.meeting.known`; `E1` | Enables `rose.1009.s1`; prerequisite for Revolution roots | `T/P/E1/A07` historical only; `U` current rerun |
| `neutral.1008.s1` | `MapAction`; `neutral.1008.s1.action @ market`; day 3 | `world.map.active=true`; own completion false | Conversation; 9 pages / 60 lines / 3 choices; `neutral.1008.s1.q1b` | `C1` | `R1` | `S1` | `SetFlag story.neutral.1008.s1.completed`; `E1` | No current formal root consumes this fact after the current source update | `T/P/E1/U` |
| `rose.1009.s1` | `MapAction`; `rose.1009.s1.action @ Saints Street`; day 4 | `story.oct09.meeting.known=true`; report has no separate own-completion-false condition, while `onceOnly` and root result remain configured | Conversation; 9 pages / 67 lines / 2 choices; `rose.1009.s1.perception` | `C1` | `R1` | `S1` | `SetFlag story.rose.1009.s1.completed`; `E1` | Enables `church.1009.s2` and `rose.1011.s1` | `T/P/E1/U`; map/CG tail acceptance pending |
| `church.1009.s2` | `Immediate` entry with day 4 / Night / once date route | `story.rose.1009.s1.completed=true`; own completion false | Conversation; 4 pages / 36 lines / 2 choices | `C1` | `R1` | `S1` | `SetFlag story.church.1009.s2.completed`; `E1` | No current formal root consumes this completion fact | `T/P/E1/U` |
| `rose.1011.s1` | `MorningBeforeMap`; day 6 / Morning / once | `story.rose.1009.s1.completed=true`; own completion false | Conversation; 27 pages / 123 lines / 11 choices; 1 reading page / 3 lines; three checks | `C1` | `R1` | `S1` | `SetFlag story.rose.1011.s1.completed`; `E1` | Enables the Rose prerequisite for `revolution.1014.s1` | `T/P/E1/U`; production/reading/three-person acceptance pending |
| `revolution.1013.s2` | `Immediate` entry with day 8 / Noon / once date route | `story.oct07.s2.completed=true`; own completion false | Conversation; 6 pages / 52 lines / 3 choices | `C1` | `R1` | `S1` | `SetFlag story.revolution.1013.s2.completed`; `E1` | No current formal root consumes this completion fact | `T/P/E1/U` |
| `revolution.1014.s1` | `MorningBeforeMap`; day 9 / Morning / once | `story.oct07.s2.completed=true`; `story.rose.1011.s1.completed=true`; own completion false | Conversation; 23 pages / 171 lines / 12 choices; 2 reading pages / 1 line; three inline checks | `C1` | `R1` | `S1` | `SetFlag story.revolution.1014.s1.completed`; `E1` | No current formal root consumes this completion fact | `T/P/E1/U`; unresolved local dependency pages remain safely gated |
| `revolution.1028.s1` | `BeforeNight`; day 23 / Night / once | `story.oct07.s2.completed=true`; own completion false | Conversation; 10 pages / 144 lines / 3 choices | `C1` | `R1` | `S1` | `SetFlag story.revolution.1028.s1.completed`; `E1` | No current formal root consumes this completion fact | `T/P/E1/U` |

## Arrival continuation chain

These events are queued continuations rather than independent formal date or map roots. Their route is a `QueueEvent` edge in the same NCS controller, and their final completion is not a formal story completion flag.

| Event | Trigger / route | Prerequisites | Presentation | Cancellation | Retry | Save/restore | Completion/result | Downstream unlock | Evidence status |
|---|---|---|---|---|---|---|---|---|---|
| `arrival.start` | Explicit/code-owned immediate entrance | none in queue; caller owns eligibility | Conversation `arrival.start` | `C1` | `R1` | `S1` | No direct result row | Starts `arrival.insight` flow | `T/P/U` |
| `arrival.insight` | Immediate continuation entrance | caller/request context | Conversation `arrival.insight`; queues `arrival.request` | `C1` | `R1` | `S1` | `QueueEvent -> arrival.request`; `E1` | `arrival.request` | `T/P/E1/U` |
| `arrival.request` | `QueueEvent` from `arrival.insight` | prior insight completed | Conversation `arrival.request`; queues `arrival.decision` | `C1` | `R1` | `S1` | `QueueEvent -> arrival.decision`; `E1` | `arrival.decision` | `T/P/E1/U` |
| `arrival.decision` | `QueueEvent` from `arrival.request` or `MA70.10.06-S1-3.1` | prior request or action branch | Conversation `arrival.decision` | `C1` | `R1` | `S1` | No direct result row | `arrival.refuse` or `arrival.accept` branch | `T/P/U` |
| `arrival.refuse` | `QueueEvent` from `MA70.10.06-S1-2.1` | refusal branch selected | Conversation `arrival.refuse` | `C1` | `R1` | `S1` | No direct result row | Chain ends at refusal branch | `T/P/U` |
| `arrival.accept` | `QueueEvent` from `MA70.10.06-S1-2.2` | acceptance branch selected | Conversation `arrival.accept`; queues `arrival.complete` | `C1` | `R1` | `S1` | `QueueEvent -> arrival.complete`; `E1` | `arrival.complete` | `T/P/E1/U` |
| `arrival.reply.1` | `QueueEvent` from `MA70.10.06-S1-1.1` | reply branch selected | Conversation `arrival.reply.1`; queues `arrival.agent` | `C1` | `R1` | `S1` | `QueueEvent -> arrival.agent`; `E1` | `arrival.agent` | `T/P/E1/U` |
| `arrival.reply.2` | `QueueEvent` from `MA70.10.06-S1-1.2` | reply branch selected | Conversation `arrival.reply.2`; queues `arrival.agent` | `C1` | `R1` | `S1` | `QueueEvent -> arrival.agent`; `E1` | `arrival.agent` | `T/P/E1/U` |
| `arrival.agent` | `QueueEvent` from either reply | one of `arrival.reply.1`/`.2` completed | Conversation `arrival.agent`; queues `arrival.check` | `C1` | `R1` | `S1` | `QueueEvent -> arrival.check`; `E1` | `arrival.check` | `T/P/E1/U` |
| `arrival.check` | `QueueEvent` from `arrival.agent` | agent step completed | `NoClass setup=arrival.check`; `arrival.perception` check | `C1` | `R1` | `S1` | No direct result row | Branch caller owns next step | `T/P/U` |
| `arrival.leave` | `QueueEvent` from `MA70.10.06-S1-3.2` | leave branch selected | `NoClass setupType=Tpl_Narration` | `C1` | `R1` | `S1` | No direct result row | Chain ends at leave branch | `T/P/U` |
| `arrival.complete` | `QueueEvent` from `arrival.accept` | accept branch completed | `NoClass setupType=Tpl_Narration` | `C1` | `R1` | `S1` | No direct result row; terminal continuation only | Returns to caller; no formal story unlock | `T/P/U` |

## Acceptance gaps

- The matrix records static/configuration and code-path evidence, not successful real-map execution.
- `oct07.s1` and `oct07.s2` retain historical October 7 acceptance evidence, but that report used direct Editor assertions and is not a current Unity Test Framework pass.
- `neutral.1008.s1`, `rose.1009.s1`, `rose.1011.s1`, `church.1009.s2`, all three dated Revolution roots, and the arrival chain still require the ordered runtime tasks for real trigger, loading, cancellation, retry, save/restore, and exactly-once proof.
- A continuation's `QueueEvent` result is a route edge, not a formal completion fact. Formal story completion is written only by the configured root result rows.
- Missing authoritative relationship, money, production, or dependency values remain blocked or safely gated; this matrix does not convert static pass into completion.
