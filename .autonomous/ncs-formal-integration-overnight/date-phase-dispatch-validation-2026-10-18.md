# Formal Date and Phase Dispatch Validation

Date: 2026-10-18
Scope: Task 35 of `ncs-formal-integration-overnight`
Status: Code/configuration path verified. No dispatch bypass or code correction was required. One direct PlayMode test remains blocked by an isolated fixture that omits cafe-tutorial data; that failure is recorded separately below.

## Phase API contract

`NarrativeController.ProcessEntry` accepts formal phase entries only when the controller is at a safe boundary and not busy. It explicitly rejects `NarrativeEntryPoint.MapAction`, so map actions cannot enter through the date-phase API (`Assets/Scripts/Narrative/NarrativeController.Entries.cs:23-45`). The API maps `MorningBeforeMap` to Morning, `AfterBusiness` to Noon, and `BeforeNight` to Night through `EntryTimeSlot` (`NarrativeController.Entries.cs:72-89`).

`ProcessOfficialDay` requires the official calendar to be synchronized first, rejects duplicate/backward scheduling, clears only the drained boundary queue, evaluates date/trigger candidates once, and processes them through the same controller queue (`Assets/Scripts/Narrative/NarrativeController.cs:148-178`). `DateBoundaryMatches` requires a dated event's authored slot to equal the active formal phase when official-day management is enabled (`NarrativeController.Entries.cs:90-95`).

The source scan found no formal call to `ProcessEntry(NarrativeEntryPoint.MapAction)`. The only map-action runtime entrance is `QueueMapActionEvent`; the preview helper also selects that explicit API rather than bypassing it (`Assets/Scripts/GameFlow/UI/Presentation/NarrativeChapterPreview.cs:94-98`).

## DayFlow handoff

- Date transition synchronizes and schedules the official day, then invokes `RunNarrativeEntry(MorningBeforeMap, EnterPreBusiness)` only at the safe boundary (`Assets/Scripts/GameFlow/UI/DayFlowController.cs:278-291`).
- Business completion enters `PreLeisure`, shows `BusinessEndPanel`, and invokes `RunNarrativeEntry(AfterBusiness, EnterCityMap)` after that presentation completes (`DayFlowController.cs:688-690`, `1093`).
- The real map day-end action rejects busy/action-executing states and invokes `RunNarrativeEntry(BeforeNight, CompleteDay)` (`DayFlowController.cs:774-790`). A failed BeforeNight batch returns to the map and exposes the existing retry hint rather than advancing the day (`DayFlowController.cs:844-853`).
- `RunNarrativeEntry` closes the prior panel, captures a version token, enters `DayFlowState.NarrativeEntry`, calls `controller.ProcessEntry`, and advances only from the success callback. Refusal and failure use the existing retry path (`DayFlowController.cs:816-842`).
- `MapAction` remains a map/action-controller path, not a DayFlow phase. The action controller calls `QueueMapActionEvent` with the authored event ID and waits for `EventCompleted` before completing a deferred Once action.

## Current formal route inventory

The loaded configuration still has nine formal roots: three `MapAction` roots (`oct07.s1`, `neutral.1008.s1`, `rose.1009.s1`) and six date-dispatched roots (`oct07.s2`, `rose.1011.s1`, `church.1009.s2`, `revolution.1013.s2`, `revolution.1014.s1`, `revolution.1028.s1`). Their EventDate rows use the expected Morning, Noon, or Night slots documented in Task 20. No formal route uses an unowned phase or a second scheduler.

## Verification

Connected Unity Editor: `6000.3.5f2`, stopped after the focused tests, compile-complete, `compilationFailed=false`.

Passing focused evidence:

- `Spotlight.Narrative.Tests.NarrativeEntryTests`: 14 passed, 0 failed, 0 skipped. This covers phase mismatch, Morning idempotence, Night date isolation, cancellation/retry, and safe-boundary callbacks.
- `Spotlight.Narrative.Editor.Tests.FormalNarrativeFlowConfigurationTests`: 3 passed, 0 failed, 0 skipped.

The direct PlayMode test `Spotlight.GameFlow.Tests.DayFlowPresentationTests.FormalDayStartsOnMapAndPresentationsAdvanceOnlyOnCompletion` was attempted by exact full name and failed before exercising the phase assertions because its isolated `PlayerPresentationTestSession` supplies no `first_cafe_tutorial/workstation_open` step or opening presentation. DayFlow correctly threw its guard at `Assets/Scripts/GameFlow/UI/DayFlowController.cs:432`: `InvalidOperationException: Missing cafe tutorial presentation: workstation_open`. This is a fixture/content setup blocker, not evidence of a date-phase dispatch bypass, and no fixture or production code was changed in this task.

## Conclusion

Formal Morning, Noon/AfterBusiness, Night/BeforeNight, and MapAction dispatch use the existing DayFlow/controller/action ownership boundaries. The production source path is verified; the remaining PlayMode evidence is blocked by the pre-existing isolated cafe-tutorial fixture gap and should be repaired or explicitly accepted before full runtime acceptance.
