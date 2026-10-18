# Formal Trigger Dispatch Trace

Date: 2026-10-18
Scope: Task 33 of `ncs-formal-integration-overnight`
Status: Verified. The existing runtime already has one authoritative NCS controller for candidate selection and event start; no code correction was required.

## Single runtime owner

`GameFlowBootstrap` explicitly documents and constructs the sole formal narrative/condition runtime (`Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:152-167`). `NarrativeRuntime.Initialize` creates one `NarrativeController`, one `NarrativeActionController`, and the runtime presenter (`Assets/Scripts/Narrative/NarrativeRuntime.cs:108-140`). The runtime exposes only that controller for formal `QueueMapActionEvent` and `ProcessEntry` calls (`NarrativeRuntime.cs:253-254`).

The debug presentation session is a separate explicit preview/test entrance. It is not used by formal DayFlow or map dispatch and does not create a second formal scheduler.

## Trigger and date path

1. State changes enter `NarrativeController.AddBar`/`SetBar`, which call `OnBarChanged` and record the settled value change (`Assets/Scripts/Narrative/NarrativeController.cs:229-256`).
2. `EvaluateBoundary` is the only candidate collector for enabled Cross/Hold triggers and EventDate rows (`NarrativeController.cs:614-706`). Disabled triggers are skipped before hit creation. Once-fired trigger, reach-count, depth, event-once, date, and phase checks are applied before `CaptureHit`.
3. `CaptureHit` preserves the source, trigger ID, entry point, priority, source row, reach count, day, date slot, repeat rule, and sequence in one `NarrativeCandidateValue` (`Assets/Scripts/Narrative/NarrativeController.Entries.cs:159-178`).
4. `PromoteCandidates` applies the active phase and fresh eligibility checks, then `EnqueueCandidate` constructs the `NarrativeEventRequest` from the same candidate and its trigger (`NarrativeController.Entries.cs:181-205`).
5. `ProcessQueue` rechecks candidate/event eligibility immediately before presentation and calls the single injected presenter. The completion callback is guarded by `presentationEpoch` and `activePresentation` (`Assets/Scripts/Narrative/NarrativeController.cs:746-814`).
6. `CompleteEvent` preflights and commits the result transaction, marks the event/trigger/date only after success, calls `CommitCandidate`, raises `EventCompleted`, and continues the same queue (`NarrativeController.cs:817-934`; `NarrativeController.Entries.cs:208-220`).

This prevents a date or bar trigger from directly starting a panel outside the controller. It also means once state is not consumed when a presentation is merely queued or cancelled.

## Map-action path

The map UI calls `NarrativeActionController.Execute` through `MapSceneController.ExecuteAction` (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:1056-1065`). The action controller validates and consumes its configured costs, keeps mandatory Once actions incomplete until their event chain succeeds, and queues each configured result through `narrative.QueueMapActionEvent` (`Assets/Scripts/Narrative/NarrativeActionController.cs:263-315`, `466-501`).

`QueueManualEvent` performs the event rejection check, refreshes current Hold/Cross eligibility, consumes an existing eligible candidate when one exists, and refuses a MapAction event with an enabled configured trigger when no candidate exists (`Assets/Scripts/Narrative/NarrativeController.Entries.cs:51-70`). It never invents a trigger candidate. The formal map-action roots currently resolve as:

```text
formalRoots=9
mapRoots=oct07.s1,rose.1009.s1,neutral.1008.s1
datedRoots=oct07.s2,revolution.1013.s2,revolution.1028.s1,revolution.1014.s1,church.1009.s2,rose.1011.s1
formalActions=oct07.cafe.s1->oct07.s1;neutral.1008.s1.action->neutral.1008.s1;rose.1009.s1.action->rose.1009.s1
mountedPositions=cafe=oct07.cafe.s1;market=neutral.1008.s1.action;Saints Street=rose.1009.s1.action
```

The action controller listens to the same controller's `EventCompleted` and only records a deferred Once action after its entire result-event chain succeeds. It is an action/cost adapter, not a second event scheduler.

## DayFlow path

`DayFlowController` obtains `NarrativeRuntime.Instance.Controller`, schedules the official day, and invokes `ProcessEntry` for `MorningBeforeMap`, `AfterBusiness`, and `BeforeNight` (`Assets/Scripts/GameFlow/UI/DayFlowController.cs:212-217`, `278-290`, `688`, `784`, `816-842`). The callback advances DayFlow only after the controller reports a successful safe-boundary completion; refusal or failed presentation routes to the existing retry path.

No formal UI path calls `NarrativePresenter.Play` directly. Presenter start is subscribed back into `NarrativeRuntime.EventPresented`, while the controller remains the owner of queueing, eligibility, cancellation epoch, and settlement.

## Verification

Connected Unity Editor: `6000.3.5f2`, stopped, compile-complete, `compilationFailed=false`.

Focused tests passed:

- `Spotlight.Narrative.Tests.NarrativeEntryTests`: 14 passed, 0 failed, 0 skipped.
- `Spotlight.Narrative.Tests.NarrativeActionTests`: 27 passed, 0 failed, 0 skipped.

The entry tests cover map-action candidate refusal, cancellation/retry, stale callbacks, Hold/Cross/date routing, phase ordering, and once consumption. The action tests cover configured event order, deferred Once completion, action-state restore, and failure recovery.

## Conclusion

The formal trigger, date, map-action, and DayFlow paths converge on the same `NarrativeController` instance and its queue. There is no observed split-brain dispatch or bypass that requires a code change. This is lifecycle/code evidence only; real map/scene loading, cancellation, retry, save/restore, and exactly-once Player acceptance remain later tasks.
