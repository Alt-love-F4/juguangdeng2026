# Formal NCS Runtime Path Trace

Date: 2026-10-18
Scope: Task 6 of `ncs-formal-integration-overnight`
Status: Static trace complete; no project code was changed by this task.

## Baseline verification

- Unity Editor is connected, stopped, and the shared `Assets/Scenes/PresentationDebugScene.unity` remains the active clean scene.
- Focused `Spotlight.Narrative.Tests.NarrativeEntryTests` passed `14/14` tests with `0` failures and `0` skips.
- `git diff --check` reported only existing trailing whitespace in `MainMenuPanel.prefab` and font assets; no findings are in the runtime files traced here.
- This is code-path evidence, not real-map acceptance. Tasks 53 onward still require that acceptance.

## Runtime construction and ownership

1. `GameFlowBootstrap` creates or finds the single `NarrativeGameFlowIntegration` in `Awake`, calls `EnsureRuntime`, and injects the formal reading, notice, conversation, letter, character, and player-name adapters (`Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:152-167`).
2. `NarrativeGameFlowIntegration.EnsureRuntime` creates or reuses the persistent `NarrativeRuntime`, then calls `NarrativeRuntime.Initialize` (`Assets/Scripts/Narrative/NarrativeGameFlowIntegration.cs:65-76`). Formal gameplay hides the old demo HUD and uses existing map/business panels (`:85-90`).
3. `NarrativeRuntime.Initialize` loads Luban data when needed, creates `NarrativePresenter` and `NarrativeController`, creates `NarrativeActionController` plus map inventory, mounts configured actions, and wires controller/presenter events (`Assets/Scripts/Narrative/NarrativeRuntime.cs:98-152`).
4. Ownership is single-source: the controller owns eligibility, candidates, queueing, result transactions, once flags, and completion/failure events; the presenter owns visual lifetime/effects; the action controller owns action costs, progress/delivery state, and action callbacks.

## Date and phase dispatch

1. `DayFlowController.BeginNewDayFlow` synchronizes the official day through `SetOfficialDay`, advances action state, and opens the date transition (`Assets/Scripts/GameFlow/UI/DayFlowController.cs:172-208`).
2. `CompleteDateTransition` calls `ProcessOfficialDay(day)` at a safe boundary, then `RunNarrativeEntry(MorningBeforeMap, EnterPreBusiness)` (`DayFlowController.cs:277-291`).
3. `FinishBusiness` shows the business-end panel, then runs `AfterBusiness` before entering the map (`DayFlowController.cs:681-689`).
4. `EndDayFromMap` rejects busy narrative/action state and blocking actions, then runs `BeforeNight` before `CompleteDay` (`DayFlowController.cs:770-784`).
5. `RunNarrativeEntry` closes the prior presentation, waits for `RunAtSafeBoundary`, calls `NarrativeController.ProcessEntry`, and advances DayFlow only after success. Refused/failed entries use the retry path; `BeforeNight` returns to the map with a retry hint (`DayFlowController.cs:816-853`).
6. `ProcessEntry` rejects `MapAction`, invalid entries, concurrent entries, ended sessions, unavailable presenters, unsafe boundaries, and busy state. It records the phase/day, sets the formal time slot, evaluates the boundary, and pumps the queue (`Assets/Scripts/Narrative/NarrativeController.Entries.cs:20-46`). Morning is idempotent per official day.

## Trigger and candidate selection

1. Official-day processing and value changes call `EvaluateBoundary`; `CaptureHit` records event, trigger/date source, declared entry, priority/order, day, count, and sequence (`Assets/Scripts/Narrative/NarrativeController.cs:168-188`; `NarrativeController.Entries.cs:159-178`).
2. `PromoteCandidates` only promotes candidates for the active formal phase or an allowed immediate/date case, then rechecks locks, once state, date, phase, and conditions (`NarrativeController.Entries.cs:181-190`).
3. `EnqueueCandidate` freezes the condition decision made at candidate creation, while fresh lock/completion/date/phase checks remain live at start (`NarrativeController.Entries.cs:194-205`).
4. `QueueMapActionEvent` is the explicit map entrance. It reevaluates eligibility, uses an existing candidate when present, creates a manual candidate only when allowed, enqueues the same controller request, and pumps it (`NarrativeController.Entries.cs:48-71`). It does not bypass dates or a non-map declared entry.

## Real map action path

1. `MapSceneController` opens action details for a configured location only when the location has authored content; cafe entry itself is scene navigation, not an NCS action (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:898-915`, `:1019-1054`).
2. `MapActionDetailsView.OnExecute` checks `NarrativeActionController.IsAvailable`, displays unmet requirements without mutating state, then starts its consumption animation and calls `NarrativeRuntime.Instance.Actions.Execute` (`Assets/Scripts/GameFlow/UI/MapActionDetailsView.cs:691-742`).
3. `NarrativeActionController.Execute` atomically consumes item/action-point costs inside `BeginOperation`/`EndOperation`, queues authored result events, defers completion for mandatory once actions until every result event succeeds, and emits `ActionStarted` (`Assets/Scripts/Narrative/NarrativeActionController.cs:263-306`).
4. `PlayNextEvent` calls `NarrativeController.QueueMapActionEvent`. On refusal, the event remains pending and the action reports failure; it is not declared complete (`NarrativeActionController.cs:482-498`).
5. `NarrativeActionController` subscribes to controller `EventCompleted` and `EventFailed`. It advances the action queue only for the matching current event, and finalizes the action at a narrative safe boundary (`NarrativeActionController.cs:60-72`, `:519-543`).

## Controller queue, presentation, and settlement

1. `NarrativeController.ProcessQueue` refuses to run during value restore, an ended session, a pump, or effect draining. It drains pending presentation effects before starting the next event (`Assets/Scripts/Narrative/NarrativeController.cs:746-778`).
2. Each request is revalidated immediately before presentation. A changed prerequisite removes the pending request and reports `EventFailed` for map/manual/continuation requests (`NarrativeController.cs:779-797`).
3. A valid request becomes the active presentation and calls `NarrativePresenter.Play` with an epoch-guarded completion callback (`NarrativeController.cs:798-817`). Duplicate or late callbacks cannot settle an invalidated epoch.
4. `NarrativePresenter.Play` starts the presentation pause lease, chooses the configured presentation index/template, performs checks, and runs conversation, reading, notice, letter, or setup atoms (`Assets/Scripts/Narrative/NarrativePresenter.cs:86-125`, `:234-305`).
5. Conversation pages resolve authored lines, portraits, background/stage data, choices, and choice requirements. Choice selection calls back through `ChoiceSelected` into `NarrativeController.SelectChoice`; the panel does not apply results (`NarrativePresenter.cs:438-505`).
6. `NarrativeConversationEventPlayback` builds the `DialogueScript`, connects completion/cancellation exactly once, and routes to `DialoguePlayback`. If the authored background is a real cafe scene and the active scene is not `CoffeeShop`, it invokes `GameFlowBootstrap.TransitionToGameplayScene("CoffeeShop", BeginPlayback)` (`Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs:84-114`).
7. `GameFlowBootstrap.TransitionToGameplayScene` captures pending save participants, enters `Loading`, hides gameplay, shows the short loading panel, loads the destination additively, sets it active, registers the map/gameplay panel, sets `Playing`, makes gameplay visible, and invokes the presentation callback (`Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:358-446`).
8. On presentation completion, `NarrativeController.CompleteEvent` combines root, choice, active-check, and inline-check results; preflights all configured writes; commits money/items/conditions/results inside one operation; marks the event happened; commits candidate/once state; and emits `EventCompleted` (`Assets/Scripts/Narrative/NarrativeController.cs:830-914`). This is the authoritative settlement point.
9. `QueueEvent` results are validated as immediate continuations and inserted ahead of unrelated queued work. Continuations remain in the same controller queue (`NarrativeController.cs:969-973`, `:1039-1046`; `NarrativeController.Entries.cs:243-255`).

## Cancellation, failure, and retry

1. A conversation adapter callback or `DialoguePanel.PlaybackInterrupted` invokes the page cancellation callback only once (`NarrativeConversationEventPlayback.cs:87-106`).
2. `NarrativePresenter.AbortPresentation` invalidates its session version, releases the pause lease and active stage, clears effects, restores camera/audio/UI, and emits `Cancelled` (`Assets/Scripts/Narrative/NarrativePresenter.cs:187-217`). Host scene unload and pause-lifetime interruption use the same abort path.
3. `NarrativeController.OnPresentationCancelled` increments the presentation epoch, clears active transient choice/check state, removes the failed manual candidate when applicable, marks the active phase batch failed, reports `EventFailed`, and only repumps when the outer pump is not already active (`NarrativeController.cs:702-744`). No once marker or event result is committed by cancellation.
4. `FailEntryBatch` clears pending queue work but deliberately leaves eligibility in `candidates`, so a failed phase remains retryable (`NarrativeController.Entries.cs:235-241`). `CompleteEntryAtSafeBoundary` invokes the DayFlow callback only after the controller is idle (`:225-233`).
5. Presenter exceptions and changed start preconditions also report failure. A transaction exception occurs before `EventCompleted`, clears formal phase state, and leaves the event uncommitted (`NarrativeController.cs:886-909`).

## Save, restore, and input ownership

1. `NarrativeController.BlocksSave` is true for an active or queued event whose configured `blocksSave` is true, including pending effect work (`Assets/Scripts/Narrative/NarrativeController.Diagnostics.cs:23-30`). `DayFlowController.CanSave` also rejects first-session guidance and a pending narrative entry (`DayFlowController.cs:83-85`). `GameFlowBootstrap.TryCaptureSaveData` checks the same gate, gameplay/loading state, captures participants, and calls `NarrativeSaveBridge.TryCapture` (`GameFlowBootstrap.cs:1383-1415`).
2. A normal snapshot is rejected during an atomic result transaction, but an active presentation can be serialized at a supported save boundary. The snapshot includes active request/selected choices, pending requests, candidates, checks, effects, turn state, and event progress (`NarrativeController.cs:443-479`).
3. Restore invalidates old presentation epochs, clears live queues/transient callbacks, restores state/candidates/active request/pending effects, and leaves resumption to the caller (`NarrativeController.cs:482-526`). `NarrativeRuntime.RestoreSnapshot` resets the presenter/controller, restores action state, and synchronizes NPC locations (`NarrativeRuntime.cs:286-298`).
4. During a saved-session load, `GameFlowBootstrap.OpenGameplayPlaceholder` restores the NCS snapshot and participant data before making gameplay visible; it then calls `DayFlowController.ResumeSavedPhase` (`GameFlowBootstrap.cs:1032-1079`). `NarrativeRuntime.ResumeRestoredSession` resumes action execution and pending narrative events when the host explicitly requests it (`NarrativeRuntime.cs:272-278`).
5. Pause/input ownership is shared: `NarrativePresenter` acquires `PresentationPauseRegistry`, `GameFlowBootstrap` suspends the presenter when gameplay is hidden or paused, and `Time.timeScale` is restored to `1` after saved-session load. This is code evidence only; F9/shared-scene runtime verification remains a later task.

## Verified versus not yet accepted

Verified statically and by focused tests:

- one controller owns phase, map, immediate, and continuation event entry;
- candidate and once state are not consumed before successful settlement;
- result writes are preflighted and committed together;
- stale callbacks are epoch/version guarded;
- cancellation clears presentation state and leaves failed formal work retryable;
- map actions retain pending result events when queueing fails;
- save capture and restore have explicit controller/action boundaries.

Not verified by this task:

- real `Map` -> `CoffeeShop` -> presentation -> `Map` acceptance for each formal root;
- asynchronous loading failure/retry in the installed Player or connected Editor;
- save/restore during every active formal presentation and after each accepted root;
- exact-once settlement under duplicate real input and scene reload;
- actual visual background/portrait/effect resource resolution for every formal root.

These remain for the ordered lifecycle and acceptance tasks. No implementation change was justified by the static trace alone.


