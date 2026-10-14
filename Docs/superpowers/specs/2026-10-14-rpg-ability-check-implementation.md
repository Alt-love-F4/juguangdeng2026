# RPG Ability Check Implementation and Review Report

Status: Implementation partially wired and compiled; focused automated tests and end-to-end scene verification remain incomplete.

## Decision Register

| Topic | Decision | Reason / boundary |
|---|---|---|
| Config authority | Add `AbilityCheck` and `AbilityCheckCondition` sheets to the existing `Narrative.xlsx` Luban pipeline. | Keeps config in the existing authoring source; generated JSON/C# remain generated outputs. |
| Stable identity | Use stable `checkId`, `attributeId`, `groupIndex`, and condition IDs. | No display text or GameObject paths in logic. |
| Choice binding | Add an optional check reference to a choice definition. Ordinary NCS choice conditions remain the only lock/unlock rules. | Low ability does not disable a check choice. |
| Automatic binding | Add an optional check reference to the existing presentation setup and a dedicated ability-check presentation atom. | Keeps the check inside the current event transaction and presenter sequence. |
| Branch routing | Configure success and failure as two NCS event IDs. | Both branches stay under NCS ownership and reuse the existing queue/results model. The branch events must not re-enter the same check. |
| Result ownership | Optional success/failure result-owner IDs point to ordinary NCS result owners; no owner means no persistent state change. | Check service remains read-only and branch effects stay in the NCS result pipeline. |
| Rule | Deterministic integer `>=`; within-group AND, across-group OR. | Matches the approved Spec; no dice or modifiers. |
| Snapshot | Store the immutable evaluated conditions and selected branch in the active NCS snapshot. | Restore reuses the exact values and branch without reading attributes again. |
| UI | Show requirement lines before execution and actual-value/result lines after execution; require an explicit continue action after showing the result. | The UI formats supplied data and never calculates or queries attributes. Outcome text, not color alone, differentiates pass/fail. |
| Cancellation | Invalidate late callbacks on cancel, scene unload, and session replacement using the existing presentation epoch/session lifetime model. | Prevents an old event from settling a new session. |
| Scene verification | Extend the shared `PresentationDebugScene` entry point; do not add a temporary scene. | Matches the Spec and preserves existing scene work. |
| Tutorial | Use `player.perception >= 3` for the first concrete check after confirming the current tutorial event IDs and text split. | Implements the approved first scenario without hardcoding display labels or values into prose. |

## Current-System Findings

- Player attributes are catalog-backed and read through `PlayerRuntime.TryReadAttribute`; the active session binds it to NCS-owned Bar values. No duplicate check stat pool is planned.
- NCS already evaluates grouped ordinary conditions, executes regular results, captures active/pending event requests, and restores the active request before resuming the narrative session.
- Choice presentation is currently a separate presenter path and choice selection returns to `NarrativeController.SelectChoice`.
- The existing UI has ordinary requirement hints, but those hints currently represent unavailable/locked options. Ability-check requirements must remain distinct and must not affect clickability.
- The shared workspace already contains extensive uncommitted changes in the player, NCS, config generator, UI, tests, and presentation debug scene. All such changes are treated as protected baseline; this implementation must patch current content incrementally and preserve unrelated edits.

## Planned File Scope

### Narrative/configuration

- `Assets/ConfigTables/Defines/schema.xml`
- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`
- `Assets/ConfigTables/GenerateAll.bat`
- Luban generated config classes and JSON for new/extended rows
- `Assets/Scripts/Narrative/NarrativeAbilityChecks.cs` (new model/service)
- `Assets/Scripts/Narrative/NarrativeConfiguration.cs`
- `Assets/Scripts/Narrative/NarrativeDefinitions.cs`
- `Assets/Scripts/Narrative/LubanNarrativeLoader.cs`
- `Assets/Scripts/Narrative/NarrativeController.cs` and/or a focused partial
- `Assets/Scripts/Narrative/NarrativeRuntime.cs`
- `Assets/Scripts/Narrative/Editor/NarrativeConfigurationValidator.cs`

### Presentation/save

- `Assets/Scripts/Narrative/NarrativePresenter.cs`
- `Assets/Scripts/Narrative/NarrativePresentationDefinitions.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/NarrativeConversationEventPlayback.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/DialogueScript.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/DialogueOptionView.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/DialoguePanel.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/DialoguePanelEffects.cs`
- NCS snapshot serialization through the existing `NarrativeSaveBridge` envelope; no parallel check save participant is planned.

### Tests/report

- `Assets/Scripts/Narrative/Tests/NarrativeAbilityCheckTests.cs` (new)
- `Assets/Scripts/Narrative/Tests/NarrativeCoreTests.cs`
- `Assets/Scripts/Narrative/Editor/Tests/NarrativeConfigurationValidatorTests.cs`
- `Assets/Scripts/GameFlow/UI/Tests/NarrativeSaveBridgeTests.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/Tests/DialoguePresentationTests.cs`
- `Assets/Scripts/GameFlow/UI/Presentation/Tests/PresentationDebugSceneTests.cs`
- `Assets/Scenes/PresentationDebugScene.unity` only if a serialized debug control is needed; otherwise keep the scene unchanged and use its existing controller.
- This report: `Docs/superpowers/specs/2026-10-14-rpg-ability-check-implementation.md`

## Verification Plan

1. Compile generated table code and Unity scripts after schema changes.
2. Run Luban loader and configuration validator tests, including unknown attributes, empty checks/groups, duplicate IDs, invalid thresholds, invalid branch event/result owners, and invalid setup/choice bindings.
3. Run deterministic evaluator tests for equality at threshold, below threshold, group AND/OR, and missing/unready player runtime.
4. Run controller tests for choice availability independent of ability, automatic check suspension, branch routing, normal NCS results exactly once, and no mutation when result owners are absent.
5. Run presentation tests for pre-check condition formatting, post-check actual values and explicit pass/fail text, explicit continue, and stale callback cancellation.
6. Serialize/restore an active check after evaluation but before branch completion; verify no second attribute read, no branch change, no repeated result application, and legacy snapshots without check data still load.
7. Use `Assets/Scenes/PresentationDebugScene.unity` for visual checks of single and grouped conditions, success/failure labels, scrolling, and text layout.
8. Inspect Console errors and final diff/status, explicitly separating implementation-owned changes from pre-existing worktree changes.

## Implementation Update

- Added the `AbilityCheck` and `AbilityCheckCondition` Luban sheets, optional Choice/Setup bindings, generated config classes/JSON, runtime definitions, loader mapping, and loader-side reference/range checks.
- Added deterministic evaluator logic and tests for `>=`, AND within a group, OR across groups, unavailable player attributes, and supplied display-name resolution.
- Bound checks to the session-owned player attribute reader. Display labels resolve through the player catalog's `NameText` key and existing text table.
- Choice and automatic checks capture value snapshots and enqueue the configured success/failure NCS event. Configured non-branch result owners join the active event's standard preflight/commit batch; owners equal to the selected branch event are applied only by that branch event, avoiding duplicate application.
- Active check ID and evaluated values are included in the NCS snapshot. Resuming an active checked Choice reuses that snapshot and shows the result acknowledgement again; the controller avoids evaluating a restored selected Choice a second time.
- No tutorial rows or debug scene changes were made in this continuation.

## Verification Status

- Unity recompile completed successfully after the implementation changes; domain reload completed and the Editor bridge remained ready.
- Unity Console showed no errors. It showed unrelated existing warnings in presentation panels, bootstrap, and editor import/build utilities.
- A focused EditMode test run was submitted through `TestRunnerApi`, but this session did not capture a completion callback/result. Tests therefore remain unverified; do not treat the submission as a pass.
- Not yet verified: active-check save/restore with an instrumented reader and callback timing, success/failure UI visuals in `PresentationDebugScene`, and full generated-config/validator regression coverage.

## Remaining Review Items

1. Capture a completed Unity Test Runner result for `NarrativeAbilityCheckTests` and the added controller transaction test, then run loader/validator and save bridge suites.
2. Add/execute a restore regression proving the reader is called once and the same branch is used after save/restore during a checked Choice or automatic presentation.
3. Verify the result acknowledgement, choice confirmation, and branch presentation in the shared debug scene.
4. Review the full diff for integration with concurrent NCS/config work before marking delivery complete.
