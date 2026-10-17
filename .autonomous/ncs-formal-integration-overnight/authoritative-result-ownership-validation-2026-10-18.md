# Task 29: Authoritative Result Ownership Validation

Date: 2026-10-18

## Formal root result audit

The generated `tbncsresult.json` contains 33 result rows. The nine formal roots have the following authoritative result ownership:

- `oct07.s1`: `SET_FLAG story.oct07.s1.completed=true`
- `oct07.s2`: `SET_FLAG story.oct07.s2.completed=true`; `SET_FLAG story.oct09.meeting.known=true`
- `neutral.1008.s1`: `SET_FLAG story.neutral.1008.s1.completed=true`
- `rose.1009.s1`: `SET_FLAG story.rose.1009.s1.completed=true`
- `rose.1011.s1`: `SET_FLAG story.rose.1011.s1.completed=true`
- `church.1009.s2`: `SET_FLAG story.church.1009.s2.completed=true`
- `revolution.1013.s2`: `SET_FLAG story.revolution.1013.s2.completed=true`
- `revolution.1014.s1`: `SET_FLAG story.revolution.1014.s1.completed=true`
- `revolution.1028.s1`: `SET_FLAG story.revolution.1028.s1.completed=true`

Each formal root has exactly one configured `story.<eventId>.completed=true` result. The only additional formal-root fact is the explicitly authored `oct07.s2` meeting-known result. No formal root currently owns `REL_ADD`, `BAR_ADD`, `BAR_SET`, money, item, NPC, or production result rows. No relationship, money, production, or ending fact was invented to fill a source gap.

The retained legacy `EV_REVOLUTION` rows remain authoritative source content: `SET_FLAG revolution=true` and `BAR_ADD bar:world.stability=-3`. `T_CAL_D60` remains disabled, and `EV_REVOLUTION` is not a dated formal root.

## Runtime ownership and transaction boundary

The existing `NarrativeController.CompleteEvent` implementation is the only formal settlement path:

- It obtains root results from `resultsByEvent[request.Event.eventId]`.
- It adds configured choice result owners and evaluated inline-check result owners only when those owners are present in generated configuration.
- `PreflightResults` validates every result type, writable bar/relation, money account, item quantity, condition write, and queued event before mutation.
- The validated money/item/condition batch commits together, then configured effects are applied and the event is marked happened.
- A preflight or commit failure emits an error and leaves the event transaction unsuccessful; `EventCompleted` is not raised for the failed batch.
- `ApplyResult` does not invent completion or fact writes; boolean and item writes are already committed from the validated configured batch, while other effect types use their configured result row.

This preserves the existing exactly-once/result-owner boundary and prevents presentation callbacks or arbitrary page text from becoming formal world facts.

## Evidence

- Generated-data audit: all nine formal roots have the expected completion result; no formal root has an unsourced economic, relationship, bar, item, NPC, or production result.
- `Spotlight.Narrative.Core.Tests`: 195/195 passed, 0 failed, 0 skipped after the current generated data was loaded.
- `Spotlight.Narrative.Editor.Tests`: 28/28 passed in the preceding current-data validation run.
- The integration report refresh remains `38 events / 0 errors / 0 warnings`.

No source workbook, generated JSON, runtime code, scene, save slot, or result row was changed by this task.

Status: Task 29 evidence complete; Task 30 remains open.