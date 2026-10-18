# Formal Cafe Dialogue Scene Handoff Validation

Date: 2026-10-18
Scope: Task 36 of `ncs-formal-integration-overnight`
Status: Verified. No production or test source change was required.

## Runtime ownership

`GameFlowBootstrap.Awake` forces the formal session to use the single
`NarrativeGameFlowIntegration` runtime and assigns
`NarrativeConversationEventPlayback.Play` as its conversation playback
delegate (`Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:152-165`).
`NarrativeRuntime.Initialize` forwards that delegate into the live
`NarrativePresenter` (`Assets/Scripts/Narrative/NarrativeRuntime.cs:98-120`),
so a formal conversation does not use a separate map or preview presenter.

The map controller also assigns the same playback delegate when it needs to
ensure a runtime (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:800-812`).
The map-action handoff remains the authored venue -> action -> event route
documented in `map-action-handoff-validation-2026-10-18.md`.

## Cafe conversation handoff

`NarrativeConversationEventPlayback.Play` creates the conversation script,
then checks the resolved background. For a formal (non-preview) conversation
whose `BackgroundVisual.useSceneBackground` is true, and while the active
scene is not `CoffeeShop`, it finds the live `GameFlowBootstrap` and passes
`BeginPlayback` as the transition callback
(`Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs:84-113`).
The `scene.cafe` registry entry is the current real-scene marker established
in Task 26; preview playback deliberately sets `isPreview=true` and does not
load the formal gameplay scene.

`GameFlowBootstrap.TransitionToGameplayScene` captures registered save
participants, enters `Loading`, closes details and the resident map, hides
the narrative presentation, and starts the existing short loading panel
(`Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:358-383`). Its coroutine
loads `CoffeeShop` additively, validates readiness, makes it the active scene,
keeps the resident `Map` scene available, registers the destination gameplay
panel, and opens that panel before completing the handoff
(`GameFlowBootstrap.cs:388-439`). Only after the panel is prepared does it
complete pending player creation, restore `Playing`, re-enable narrative
visibility, and invoke the callback (`GameFlowBootstrap.cs:441-445`).
Therefore `BeginPlayback` calls `DialoguePlayback.Play` after the active
scene and gameplay panel are ready, rather than presenting over the map.

The ordinary cafe location follows the same scene owner: `MapSceneController`
delegates to `DayFlowController.StartCafeFromMap`, which calls
`TransitionToGameplayScene("CoffeeShop", EnterBusinessTransition)` when the
map is active (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:1194-1215`,
`Assets/Scripts/GameFlow/UI/DayFlowController.cs:739-757`). Formal cafe map
actions retain their event identity and enter the same conversation playback
delegate after `QueueMapActionEvent`, so the presentation boundary is shared
rather than duplicated.

## Runtime verification

Connected Unity Editor: `6000.3.5f2`, `StandaloneWindows64`, stopped after
tests, `compilationFailed=false`, `compiling=false`.

Focused PlayMode job `4e4739fb2f4c4011bdfa2f8769b4f935` passed all 3 tests:

- `Spotlight.GameFlow.Tests.GameplayHudPlayModeTests.MainFlowCafeAndMapOverlayUseSharedHudWithoutReplacingFormalSession` passed. The formal main-flow session reached the map, transitioned to `CoffeeShop`, preserved the same player, NCS runtime, shared HUD, and money state, then reopened the resident map without replacing the formal session.
- `Spotlight.GameFlow.Tests.MapStandaloneEntryPlayModeTests.StandaloneMapEntersCafeAndClosesLoading` passed. The authored map cafe entry reached active `CoffeeShop`, kept `Map` loaded, hid the map only after loading, and released the loading blocker.
- `Spotlight.GameFlow.Tests.MapStandaloneEntryPlayModeTests.StandaloneMapEntersCafeAndClosesLoadingWhileTimeScaleIsZero` passed with the same scene-readiness and loading-release assertions while scaled time was paused.

The test job reported `3/3 passed`, `0 failed`, and `0 skipped`. The Unity
console was inspected after the run; retained diagnostics were expected
negative-result tests, the previously recorded missing tutorial fixture from
Task 35, and an unrelated empty-asmdef warning. The ephemeral console was
cleared afterward; current ground truth is `0` errors, `0` warnings, and
`compilationFailed=false`.

## Boundary

This task verifies the scene/presentation handoff and its real loading path.
It does not claim that every formal root has been completed through the map,
nor does it replace the later cancellation, retry, save/restore, and
exactly-once acceptance tasks. No scene, prefab, workbook, generated file,
save slot, or production source was modified.

