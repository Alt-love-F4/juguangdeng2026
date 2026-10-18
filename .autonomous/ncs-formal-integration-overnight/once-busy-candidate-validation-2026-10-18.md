# Once, Busy, Save, and Candidate Validation

Date: 2026-10-18 06:01 +08:00
Task: 23
Status: Passed for the configured contracts; continuation save behavior remains an explicit later runtime acceptance case.

## Configuration inventory

The generated `tbncsevent.json` was parsed after the Task 22 regeneration. Every formal root is `onceOnly=true` and has `blocksSave=true` in `eventProperty`:

| Event | Entry | onceOnly | blocksSave |
|---|---|---:|---:|
| `oct07.s1` | `MapAction` | true | true |
| `oct07.s2` | `BeforeNight` | true | true |
| `neutral.1008.s1` | `MapAction` | true | true |
| `rose.1009.s1` | `MapAction` | true | true |
| `rose.1011.s1` | `MorningBeforeMap` | true | true |
| `church.1009.s2` | `Immediate` | true | true |
| `revolution.1013.s2` | `Immediate` | true | true |
| `revolution.1014.s1` | `MorningBeforeMap` | true | true |
| `revolution.1028.s1` | `BeforeNight` | true | true |

All twelve `arrival.*` continuation events are intentionally `onceOnly=false` and have no `blocksSave` property, which parses as `blocksSave=false`:

```text
arrival.start, arrival.insight, arrival.request, arrival.decision,
arrival.refuse, arrival.accept, arrival.reply.1, arrival.reply.2,
arrival.agent, arrival.check, arrival.leave, arrival.complete
```

The three formal map actions are all `type=ONCE` and `mustExecute=true`; each has exactly one result event:

| Action | Result event | AP cost | Location replacement |
|---|---|---:|---:|
| `oct07.cafe.s1` | `oct07.s1` | 0 | true |
| `neutral.1008.s1.action` | `neutral.1008.s1` | 0 | false |
| `rose.1009.s1.action` | `rose.1009.s1` | 1 | false |

## Runtime ownership

The existing controller is the single owner of the lifecycle:

- `NarrativeController.IsBusy` is true while processing, presenting, draining effects, holding queued requests, or ending a turn (`Assets/Scripts/Narrative/NarrativeController.cs:191-195`). This is the gate used by `NarrativeActionController.IsAvailable`, phase processing, and official-day scheduling.
- `NarrativeController.BlocksSave` reports an active or queued event with `blocksSave=true`, and also preserves the save block while that event's pending effects drain (`Assets/Scripts/Narrative/NarrativeController.Diagnostics.cs:25-30`). `DayFlowController.CanSave` consumes that gate (`Assets/Scripts/GameFlow/UI/DayFlowController.cs:84-85`).
- Formal candidate eligibility rechecks the event lock, once marker, completion state, conditions, trigger state, date, and phase before presentation (`NarrativeController.Entries.cs:127-156`). A manual map click cannot bypass an authored date restriction.
- A candidate is marked `conditionsFrozen` only when enqueued; fresh completion, lock, date, and phase checks remain live (`NarrativeController.Entries.cs:181-205`). This preserves the condition decision without allowing a stale request to bypass a newly invalid state.

## Consumption and retry semantics

Successful settlement is the only consumption point:

1. `CompleteEvent` preflights and commits the entire result batch, marks the event happened, and calls `CommitCandidate` only after the transaction succeeds (`NarrativeController.cs:830-884`).
2. `CommitCandidate` writes `event.once.<eventId>` for `onceOnly` roots, writes the date-once marker for a once date candidate, marks a once trigger fired, and removes the consumed candidate (`NarrativeController.Entries.cs:208-221`).
3. Presentation cancellation invalidates the callback epoch, clears the active request and pending presentation state, and does not write the event, date, or trigger once markers (`NarrativeController.cs:702-743`). Manual candidates are explicitly removed so a retry must be a new explicit request; date/hold/cross eligibility remains available for the next valid boundary.
4. Map-action once completion is deferred until all authored result events finish. `NarrativeActionController.Execute` consumes requirements immediately but does not record a once action as completed until `Finish(true)` after the result chain succeeds (`NarrativeActionController.cs:263-307, 501-542`). A cancelled or failed mandatory action therefore remains visible/blocking and retryable.
5. Continuation requests are inserted ahead of unrelated phase requests in the same controller queue (`NarrativeController.Entries.cs:243-255`). They are not independent formal roots or once markers; a successful continuation candidate is settled through the same `CompleteEvent` path, while cancellation discards the active batch and requires the owning chain/root to be retried.

The continuation events intentionally do not set their own save block. While a continuation is active, `IsBusy` still prevents concurrent action/phase work. Whether a particular continuation chain must also remain unsaveable is covered by the later active-presentation/save acceptance tasks; this validation does not silently change the authored continuation contract.

## Verification tests

The following focused EditMode tests passed individually, each with 1 passed, 0 failed, and 0 skipped:

- `NarrativeEntryTests.MapActionOnlyStartsOnClickAndCancellationDoesNotConsumeOnceOrSaveGate`
- `NarrativeEntryTests.CancelledDateBatchRetriesAndStaleCallbackCannotComplete`
- `NarrativeMapContentTests.ZeroCostMandatoryOnceReplacesLocationAndCompletesOnlyAfterSuccess`
- `NarrativeEntryTests.ContinuationStaysAheadOfUnrelatedPhaseEventInSameQueue`
- `NarrativeCoreTests.ConditionsFreezeAtEnqueueAndDuplicateCallbacksSettleOnce`
- `NarrativeMapContentTests.CancelledOnceKeepsMandatoryActionRetryableAndRejectsOldCompletion`

The complete narrative core/map/content suite also passed 195/195 with no failures or skips. The editor configuration suite passed 28/28 in the preceding Task 22 verification. After the negative transaction tests emitted their expected preflight error logs, the Unity console was cleared and the final ground truth was `compilationFailed=false`, `compiling=false`, `consoleErrors=0`, and `consoleWarnings=0`.

## Conclusion

Task 23 passes for the current authoritative configuration and controller ownership. Formal roots are once-only, save-blocking presentations; map actions consume their once state only after successful result settlement; active and queued work is owned by the single NCS controller; and cancellation/stale callbacks do not consume formal candidates or completion markers. Continuation events remain repeatable branch nodes with busy ownership and no independent `blocksSave` flag, an explicit boundary for the later save/restore acceptance tasks.
