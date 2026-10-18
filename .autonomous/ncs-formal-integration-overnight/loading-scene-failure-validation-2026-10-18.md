# Loading and Scene Handoff Validation

Date: 2026-10-18
Scope: Task 37 of `ncs-formal-integration-overnight`
Status: Implementation complete; Task 37 remains unchecked pending Unity compile and PlayMode verification.

## Success-path contract

`GameFlowBootstrap.TransitionToGameplayScene` switches the session to `Loading`, hides narrative UI, and opens the modal `ShortLoadingPanel` before attempting an additive `SceneManager.LoadSceneAsync`. On success, it verifies the destination scene is loaded, makes it active, registers and opens the gameplay panel, then returns the session to `Playing`, shows narrative UI, and invokes the completion callback (`Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:358-445`). The loading panel keeps raycasts blocked until its close animation completes (`Assets/Scripts/GameFlow/UI/ShortLoadingPanel.cs:53-85, 106-137`).

The shared-owner and success ordering are recorded in `cafe-dialogue-scene-handoff-validation-2026-10-18.md`. That prior focused PlayMode job passed 3/3, including scene readiness and loading release with `timeScale=0`.

`NarrativeConversationEventPlayback` starts formal dialogue only from the transition's completion callback, while preview playback bypasses the formal scene load (`Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs:84-113`). The normal success path therefore prepares the destination gameplay panel before dialogue begins.

## Failure-path finding

The transition API accepts only a success callback. Its terminal failure branches do not notify that owner:

- A null `LoadSceneAsync` result logs an error, calls `FinishGameplaySceneTransition`, and exits (`GameFlowBootstrap.cs:391-397`).
- An invalid or unloaded destination after the async operation exits without calling either finish or a failure callback (`GameFlowBootstrap.cs:402`).
- A missing map panel or gameplay panel logs an error, finishes the loading panel, and exits without restoring the session state or notifying the request owner (`GameFlowBootstrap.cs:410-436`).
- `FinishGameplaySceneTransition` only clears `loadingTransitioning` and ends the visual loading panel; it does not set `Playing`, show narrative UI, restore the previous map visibility, invoke completion, or signal failure (`GameFlowBootstrap.cs:493-499`).

For formal dialogue, `NarrativeConversationEventPlayback.Play` returns `true` immediately after starting the asynchronous scene transition. `NarrativeController` has already set `activePresentation=true` before invoking the presenter (`Assets/Scripts/Narrative/NarrativeController.cs:798-811`). `NarrativePresenter.Play` owns the completion callback and acquires a presentation pause lease (`Assets/Scripts/Narrative/NarrativePresenter.cs:86-107, 141-145`). If the transition fails, `BeginPlayback` is never invoked, so neither normal completion nor the existing presentation-cancellation path runs. The event does not settle results, but remains active/busy and is not reported as retryable. On the null-load and missing-panel branches the bootstrap remains in `Loading`; on the post-load invalid-destination branch the loading blocker may also remain open.

This violates Task 37's required state consistency and is a concrete prerequisite for Tasks 40, 43, and 48. The problem is distinct from the successful scene-readiness behavior verified in Task 36.

## Verification attempt in this session

- At session start, connected Editor status was Unity `6000.3.5f2`, ready, stopped, and not compiling. Console ground truth was zero errors and zero warnings.
- A fresh PlayMode run filtered to `StandaloneMapEntersCafeAndClosesLoading` did not return within the tool's 300-second timeout. The test status remained `running`; subsequent Editor status/cancel commands timed out and the Unity MCP websocket reported a closed connection. No test result was returned, so this attempt is not counted as a pass or failure.
- The Unity Editor process remained present and OS-reported as responsive. It was not killed, and generated `InitTestScene*` artifacts were not removed.

## Bounded repair implemented; verification pending

The repair now extends `GameFlowBootstrap.TransitionToGameplayScene` with an optional failure callback while preserving all existing two-argument callers. Null/throwing load requests, invalid post-load destinations, missing map panels, missing gameplay panels, and player-creation failures route through `FailGameplaySceneTransition`, which ends the short loading panel, restores the previously active scene and host panel/cameras, returns the session to `Playing`, restores narrative visibility, and invokes failure once. Destination panel validation and player binding now occur before unloading the previous gameplay scene so rollback remains possible.

`NarrativeConversationEventPlayback` passes a failure callback for formal scene-backed conversation. It invokes the existing per-page cancellation hook and interrupts the reusable presenter, so the active NCS request is cancelled rather than completed and its configured results remain uncommitted. `GameplayHudPlayModeTests.MissingGameplaySceneRestoresMapAndReportsFailure` covers a missing-scene failure through the shared `Map` route and asserts failure callback, no success callback, loading cleanup, `Playing` state, active map restoration, and no live presentation.

Unity verification is still pending. The connected Editor lost its MCP session after the prior timed-out PlayMode run; `mcp__unityMCP` currently reports `no_unity_session`, `refresh_unity` timed out waiting for readiness, and the local Unity CLI also timed out. The Editor process remains open, so no process termination or project reset was performed. Task 37 must remain unchecked until a fresh compile and the new/related PlayMode tests run successfully.
