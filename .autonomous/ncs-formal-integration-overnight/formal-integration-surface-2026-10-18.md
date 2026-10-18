# Formal NCS Integration Surface

Date: 2026-10-18
Scope: Task 12 of `ncs-formal-integration-overnight`
Status: Contract review complete. No runtime, scene, prefab, source workbook, generated data, or save service was changed by this task.

## Smallest supported surface

Formal NCS flow is already split across existing owners. Later work should connect configuration and acceptance evidence at these boundaries only:

1. `NarrativeGameFlowIntegration` owns the persistent NCS runtime lifetime and creates `NarrativeRuntime` without requiring a new scene or prefab edit.
2. `NarrativeController` owns eligibility, date/phase/trigger evaluation, candidate promotion, event queueing, presentation requests, result transactions, once state, completion, failure, and retry state.
3. `DayFlowController` owns official day progression and calls the controller at `MorningBeforeMap`, `AfterBusiness`, and `BeforeNight` safe boundaries.
4. `MapActionDetailsView` and `NarrativeActionController` own map action availability, cost consumption, action callbacks, and the explicit `MapAction` handoff into the same controller.
5. `NarrativeConversationEventPlayback` owns the conversation adapter. For a formal `scene.cafe` background it asks `GameFlowBootstrap` to prepare `CoffeeShop` before beginning the existing `DialoguePanel` playback.
6. `NarrativePresenter` and the presentation panels own visual lifetime, pause leases, stage/effect cleanup, cancellation, and callback release. They do not own story results.
7. `GameFlowBootstrap` and `GameSaveService` own scene loading, save gating, participant capture, and restore. `NarrativeRuntime` contributes its `NarrativeSnapshot` through `NarrativeSaveBridge`.

No parallel story scheduler, scene-specific completion writer, alternate save format, or replacement debug scene is required by the current contracts.

## Contract matrix

| Boundary | Existing owner and entry point | Required precondition | Success handoff | Failure/cancel contract | Evidence |
|---|---|---|---|---|---|
| Runtime creation | `NarrativeGameFlowIntegration.EnsureRuntime` | Module enabled and host object available | One persistent `NarrativeRuntime` is initialized with Luban configuration and existing adapters | Disable/scene teardown hides runtime and HUD; no duplicate persistent runtime | `Assets/Scripts/Narrative/NarrativeGameFlowIntegration.cs:65-76` |
| Phase/date dispatch | `DayFlowController.ProcessOfficialDay`, `RunNarrativeEntry` | Official day and phase are synchronized; no unsafe busy/presentation state | `NarrativeController.ProcessEntry` evaluates the active phase and pumps eligible candidates | Refused/failed entry does not advance DayFlow; safe retry remains available | `Assets/Scripts/GameFlow/UI/DayFlowController.cs:277-291,816-853`; `Assets/Scripts/Narrative/NarrativeController.Entries.cs:23-46` |
| Map action handoff | `NarrativeRuntime.QueueMapActionEvent` from `MapActionDetailsView.OnExecute` | Authored action is available; cost/AP checks pass; active map action is not busy | Action consumption and NCS map candidate use the same controller queue | Failed result/event keeps mandatory action pending; no alternate direct presentation | `Assets/Scripts/GameFlow/UI/MapActionDetailsView.cs:691-742`; `Assets/Scripts/Narrative/NarrativeController.Entries.cs:48-71`; runtime trace section 4 |
| Candidate and trigger ownership | `NarrativeController.EvaluateBoundary`, `PromoteCandidates`, `EnqueueCandidate` | Current date/phase/conditions and once state are rechecked at start | Candidate source, entry, priority, and condition decision are preserved in the request | Stale or invalid candidates are rejected; failed phase work remains retryable | `Assets/Scripts/Narrative/NarrativeController.cs:614-669`; `Assets/Scripts/Narrative/NarrativeController.Entries.cs:181-205,235-241` |
| Formal cafe scene handoff | `NarrativeConversationEventPlayback.Play` | Non-preview presentation resolves `scene.cafe`; gameplay host is in Playing state; active scene is not `CoffeeShop` | `GameFlowBootstrap.TransitionToGameplayScene("CoffeeShop", BeginPlayback)` loads/activates CoffeeShop, then starts existing dialogue playback | Loading version and scene validity guard late callbacks; failed transition must leave event uncommitted and retryable | `Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs:87-119`; `Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:358-430` |
| Presentation ownership | `NarrativePresenter` plus `DialoguePanel`, `ReadingPanel`, and effect channels | Presenter request owns the active epoch and pause lease | Panel completion calls back to `NarrativeController.CompleteEvent` only after full presentation/reading/choice flow | Interrupt calls `AbortPresentation`; releases pause/stage/effects/callbacks and reports cancellation; no result is written | `Assets/Scripts/Narrative/NarrativePresenter.cs:187-217`; runtime trace cancellation section |
| Settlement | `NarrativeController.CompleteEvent` | Presentation completed and active request epoch still matches | Preflighted configured results commit once, then `EventCompleted` and downstream queue work run | Transaction failure clears active phase and leaves root uncommitted | `Assets/Scripts/Narrative/NarrativeController.cs:830-914` |
| Save gate | `GameFlowBootstrap.TryCaptureSaveData` and `NarrativeRuntime.BlocksSave` | Gameplay is visible, not loading, not in tutorial/presentation, and NCS/action transaction is save-safe | Capture includes participants and `NarrativeSnapshot` | Unsafe save is refused; atomic result settlement cannot be snapshotted | `Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:1383-1418`; `Assets/Scripts/Narrative/NarrativeRuntime.cs:255-264`; `Assets/Scripts/Narrative/NarrativeController.Diagnostics.cs:29` |
| Restore/resume | `GameFlowBootstrap.OpenGameplayPlaceholder`, `NarrativeRuntime.RestoreSnapshot`, `ResumeRestoredSession` | Save payload and participant data validate; gameplay scene is ready | Restore invalidates old presentation epoch, reinstates controller/action state, then DayFlow resumes the saved phase | Stale callbacks and old scene instances cannot commit after restore; resume is explicit | `Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:1028-1078`; `Assets/Scripts/Narrative/NarrativeRuntime.cs:272-298` |
| Save storage isolation for acceptance | `GameSaveService(string rootPath)` | Acceptance run supplies a temporary root | Six-slot service is pointed at an isolated test directory | Original service must be restored in cleanup; user slots remain untouched | `Assets/Scripts/GameFlow/Saving/GameSaveService.cs:24-39`; `evidence-conventions-2026-10-18.md` |

## DayFlow phase map

| Phase | DayFlow caller | Formal entries | Return boundary |
|---|---|---|---|
| `MorningBeforeMap` | `CompleteDateTransition` -> `RunNarrativeEntry` | `rose.1011.s1`, `revolution.1014.s1`, and any future configured morning roots | Enter pre-business/map only after controller success or explicit retry handling |
| `AfterBusiness` | `FinishBusiness` -> `RunNarrativeEntry` | Current generated roots may be added here by source `EventDate`/entry configuration; no new scheduler is needed | Enter city map after success; remain at safe boundary on failure |
| `BeforeNight` | `EndDayFromMap` -> `RunNarrativeEntry` | `oct07.s2`, `church.1009.s2`, `revolution.1028.s1` | Complete day only after controller success; return to map with retry hint on failure |
| `MapAction` | Map action details -> `QueueMapActionEvent` | `oct07.s1`, `neutral.1008.s1`, `rose.1009.s1` | Map remains authoritative; action costs and result event must settle before the action completes |
| `Immediate` / continuation | Controller queue or configured direct caller | Arrival continuations and the retained disabled Revolution event | Same controller queue; no date bypass or second result owner |

## Loading and scene ownership

- `GameFlowBootstrap.TransitionToGameplayScene` captures pending participant state, marks the session `Loading`, closes map/details panels, opens the short loading panel, loads additive gameplay scenes, validates the destination, sets the active scene, unloads incompatible gameplay scenes, and invokes the callback only after readiness.
- `NarrativeConversationEventPlayback` requests this path only for non-preview real-scene backgrounds. Preview playback remains isolated and must not load the formal `CoffeeShop` scene.
- Scene load version checks and presenter epochs are separate guards. Both must remain active; one must not be replaced by the other.
- `PresentationDebugScene.unity` is a preview/debug surface and is excluded from formal Build Settings. It must not be used as a substitute for map or DayFlow acceptance.

## Save and restore ownership

- `GameSaveService` owns slot storage and accepts an injected root path for isolated acceptance runs.
- `GameFlowBootstrap.TryCaptureSaveData` is the only supported high-level capture boundary. It refuses tutorial, active presentation, loading, and unsafe NCS/action transactions before writing.
- `NarrativeSaveBridge` serializes NCS state into the existing `GameSaveData`; formal code must not add a second narrative save payload.
- `NarrativeRuntime.RestoreSnapshot` invalidates live presentation epochs and restores controller/action state. The host then calls `ResumeRestoredSession` and `DayFlowController.ResumeSavedPhase` at the supported scene boundary.
- Save/restore during an active presentation remains an acceptance case to exercise, not a reason to bypass `BlocksSave` or write partial results.

## What later tasks may change

Later lifecycle work may add focused tests, diagnostics, or narrowly scoped fixes at the existing boundaries above. It must not:

- create a second event scheduler or parallel story/result system;
- write completion, relationship, money, production, or ending facts from a panel callback;
- bypass `DayFlowController` for date/phase entries;
- bypass `NarrativeController.QueueMapActionEvent` for map actions;
- load formal cafe dialogue through preview playback;
- capture or restore through user save slots during acceptance;
- replace the shared presentation debug scene with a temporary layout.

## Acceptance boundary

This contract review confirms the smallest integration surface and ownership model. It does not claim that every formal event has passed real loading, map, cancellation, retry, save/restore, or exactly-once acceptance; those remain the ordered lifecycle and event-flow tasks.
