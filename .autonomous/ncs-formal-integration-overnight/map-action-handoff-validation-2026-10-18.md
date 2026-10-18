# Formal Map-Action Handoff Validation

Date: 2026-10-18
Scope: Task 34 of `ncs-formal-integration-overnight`
Status: Verified. The active-map, venue mount, action-point, condition, and event-identity handoff already uses the existing runtime contracts; no code correction was required.

## Active map gate

`DayFlowController.SetState` calls `SetMapAvailability`, which sets the authored `world.map.active` fact only when the state is `CityMapLeisure` (`Assets/Scripts/GameFlow/UI/DayFlowController.cs:968-973`, `855-863`). The map controller's `ControlsAvailable` requires its host controls, no standalone load, and `flow.MapControlsAvailable` (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:156`). `GameFlowBootstrap.MapControlsAvailable` rejects paused/loading/details-open/action-executing states and only allows the map during submitted gameplay or the city-map leisure state (`Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs:84-93`).

The map detail view receives the same `ControlsAvailable` callback when initialized (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:232`, `324`). Both location clicks and action execution recheck that callback, so a stale popup cannot execute after the map leaves its active state.

## Authored venue mounts

The current loaded Luban configuration reports the three formal map roots and mounts as:

```text
oct07.cafe.s1 -> oct07.s1, mounted at cafe
neutral.1008.s1.action -> neutral.1008.s1, mounted at market
rose.1009.s1.action -> rose.1009.s1, mounted at Saints Street
```

`NarrativeRuntime.Initialize` mounts every enabled authored map position into its single `NarrativeActionController` (`Assets/Scripts/Narrative/NarrativeRuntime.cs:125-131`). `MapSceneController` resolves the focused venue's authored `defaultAction` and `extendedActions`, then looks up the action by key from that same controller (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:918-944`). It does not reconstruct event IDs from venue names.

## Conditions and action-point rules

The runtime-loaded formal action rows are:

```text
oct07.cafe.s1       type=Once  mustExecute=true  requirements=(none)          event=oct07.s1
neutral.1008.s1.action type=Once mustExecute=true requirements=(none)          event=neutral.1008.s1
rose.1009.s1.action type=Once mustExecute=true requirements=ActionPoint(1)     event=rose.1009.s1
```

Their authored conditions are:

- `oct07.cafe.s1`: day 2, `world.map.active=true`, and `story.oct07.s1.completed=false`.
- `neutral.1008.s1.action`: day 3, `world.map.active=true`, and `story.neutral.1008.s1.completed=false`.
- `rose.1009.s1.action`: day 4, `story.oct07.s2.completed=true`, and `story.rose.1009.s1.completed=false`; it has no separate `world.map.active` condition in source, so no condition was invented here.

The map card performs an availability check before the cost animation and the controller rechecks it atomically in `NarrativeActionController.Execute` (`Assets/Scripts/GameFlow/UI/MapActionDetailsView.cs:691-744`, `Assets/Scripts/Narrative/NarrativeActionController.cs:263-315`). The Rose action consumes one action point only after the second check succeeds. Cafe entry is a scene navigation action, not an NCS action and does not consume action points (`MapActionDetailsView.cs:648-668`; existing map UI test coverage below).

## Event identity and completion boundary

`NarrativeActionController.Execute` stores the configured `resultEvents` in order and `PlayNextEvent` passes the exact dequeued event ID to `narrative.QueueMapActionEvent` (`Assets/Scripts/Narrative/NarrativeActionController.cs:466-501`). The current event ID is held until the same controller raises `EventCompleted`; only then does the action advance to the next event or record a deferred Once action as completed (`NarrativeActionController.cs:519-548`). A refusal reports failure and retains the event for recovery.

This preserves venue -> action key -> configured event ID -> candidate/request identity. No map UI path starts a presentation directly or substitutes a different formal root.

## Verification

Connected Unity Editor: `6000.3.5f2`, stopped, compile-complete, `compilationFailed=false`.

Focused EditMode tests passed:

- `Spotlight.GameFlow.Tests.MapUiRepairTests`: 36 passed, 0 failed, 0 skipped. This includes cafe navigation without NCS/AP settlement, authored action visibility, mandatory action behavior, host-control blocking, and map-action execution.
- `Spotlight.GameFlow.Tests.MapActionPointsViewTests`: 15 passed, 0 failed, 0 skipped.
- `Spotlight.GameFlow.Tests.MapLayoutTests`: 5 passed, 0 failed, 0 skipped.
- `Spotlight.Narrative.Tests.NarrativeEntryTests`: 14 passed, 0 failed, 0 skipped, including the map-action candidate and once-boundary cases.

Post-test Unity ground truth: `compilationFailed=false`, `compiling=false`, `consoleErrors=0`, `consoleWarnings=0`.

## Conclusion

Formal map actions are mounted by authored venue keys, gated by the active map/DayFlow state and authored conditions, charged through the action controller's atomic AP/item path, and dispatched to the exact configured NCS event ID. No additional handoff wiring was needed. Real map acceptance and scene-loading evidence remain later tasks.
