# Formal Map-Action Route Validation

Date: 2026-10-18
Scope: Task 21 of `ncs-formal-integration-overnight`
Status: Passed with one documented configuration distinction.

## Runtime ownership

The authored `MapPosition.defaultAction`/`extendedActions` values are loaded into `MapSceneController`, which binds only authored location objects and displays the configured action list (`Assets/Scripts/GameFlow/UI/MapSceneController.cs:732-776`, `1019-1065`). `NarrativeActionController.Mount` rejects unknown action keys and requires a non-empty map position (`NarrativeActionController.cs:133-148`). Executing a mounted action queues its configured event through `QueueMapActionEvent` only after `IsAvailable` succeeds (`NarrativeActionController.cs:236-306`, `482-499`).

DayFlow owns the `world.map.active` fact: it is set false when the flow is hidden, and true only while `DayFlowState.CityMapLeisure` is active (`DayFlowController.cs:125-137`, `965-970`).

## Formal action matrix

| Root | Action key | Authored mount | Type / availability | Map-active and prerequisite conditions | Result event |
|---|---|---|---|---|---|
| `oct07.s1` | `oct07.cafe.s1` | `cafe` via `extendedActions` | `ONCE`, `mustExecute=true`, no cost | action: day 2, `world.map.active=true`, `story.oct07.s1.completed=false`; event: same day/map/incomplete guards | `oct07.s1` |
| `neutral.1008.s1` | `neutral.1008.s1.action` | `market` via `extendedActions` | `ONCE`, `mustExecute=true`, no cost | action and event: day 3, `world.map.active=true`, `story.neutral.1008.s1.completed=false` | `neutral.1008.s1` |
| `rose.1009.s1` | `rose.1009.s1.action` | authored Saints Street position via `extendedActions` | `ONCE`, `mustExecute=true`, `ActionPoint(1)` | action: day 4, `story.oct09.meeting.known=true`, `story.rose.1009.s1.completed=false`; no separate authored `world.map.active` condition; event: day 4 and meeting-known guard | `rose.1009.s1` |

The generated action and map-position data were checked read-only. Each action exists, has exactly the expected result event, and is mounted at exactly one expected position. The current generated position key for Saints Street retains the project's existing non-ASCII encoding artifact; its `extendedActions` value is the authoritative `rose.1009.s1.action` route.

The absence of a separate `world.map.active` condition on the Rose action is recorded rather than silently treated as present. The route is still exposed through the authored map position and `MapSceneController` map controls, while arbitrary callers of the public narrative API are a separate runtime acceptance boundary. No new condition was invented in generated data.

## Focused verification

The connected Unity Editor ran the focused EditMode selection containing:

- `Spotlight.Narrative.Editor.Tests.NarrativeConfigurationValidatorTests`
- `Spotlight.Narrative.Tests.NarrativeMapContentTests`

Result: `32/32` passed, `0` failed, `0` skipped. This includes generated-data loading/report checks, map-content visibility, map condition refresh, action availability, action-point gating, mandatory once-action behavior, cancellation retryability, and stale action-instance protection.

## Conclusion

The three formal map-action roots have valid authored names, mounts, result-event links, and action availability rules. Cafe and market explicitly require `world.map.active=true`; Rose relies on the authored map mount plus the map UI route and has no separate map-active condition in source. That distinction is preserved as an acceptance note for later real-map testing and is not replaced with fabricated configuration.

