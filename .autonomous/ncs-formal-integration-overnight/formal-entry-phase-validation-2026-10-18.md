# Formal Entry and Phase Validation

Date: 2026-10-18
Scope: Task 19 of `ncs-formal-integration-overnight`
Status: Passed.

## Runtime contract

The actual runtime enum in `Assets/Scripts/Narrative/NarrativeDefinitions.cs:18` is:

```text
Immediate, MorningBeforeMap, AfterBusiness, BeforeNight, MapAction
```

`LubanNarrativeLoader.ReadEntryPoint` (`LubanNarrativeLoader.cs:519-526`) parses the authored `Event.eventProperty`, defaults an omitted entry to `Immediate`, rejects unknown or numeric values, and `ValidateEntryConfiguration` (`LubanNarrativeLoader.cs:538-563`) checks date compatibility and requires queued continuation targets to use `Immediate`.

The connected Unity Editor evaluated the loaded generated configuration with the runtime loader:

```text
entries=Immediate,MorningBeforeMap,AfterBusiness,BeforeNight,MapAction
events=38
dates=6
validationErrors=0
```

## Phase handoff contract

The runtime maps date slots to phase entry points in `NarrativeController.Entries.cs:74-86`:

- `morning` -> `MorningBeforeMap`
- `noon` -> `AfterBusiness`
- `night` -> `BeforeNight`

`NarrativeController.CaptureHit` derives the candidate entry from the date slot and permits a dated event whose declared entry is either `Immediate` or the derived phase (`NarrativeController.Entries.cs:159-177`). This is the supported compatibility for dated roots that retain the legacy/default `Immediate` declaration. A date constraint cannot be bypassed by a manual click (`NarrativeController.Entries.cs:147-156`).

`DayFlowController` calls the phase API at the actual gameplay boundaries:

- `CompleteDateTransition` synchronizes and processes the official day, then calls `RunNarrativeEntry(MorningBeforeMap)` (`DayFlowController.cs:277-290`).
- `FinishBusiness` calls `RunNarrativeEntry(AfterBusiness, EnterCityMap)` (`DayFlowController.cs:681-689`).
- `EndDayFromMap` calls `RunNarrativeEntry(BeforeNight, CompleteDay)` (`DayFlowController.cs:770-785`).

Map actions remain on the explicit map route: `MapSceneController` binds authored map positions and their `extendedActions`, displays the action list, and executes the configured action (`MapSceneController.cs:732-776`, `1019-1065`); `NarrativeActionController` queues the action's result event through `QueueMapActionEvent` (`NarrativeActionController.cs:471-499`). `NarrativeController.ProcessEntry` rejects `MapAction` as a date phase, while `QueueMapActionEvent` is the dedicated map entrance (`NarrativeController.Entries.cs:23-71`).

## Formal route matrix

| Root | Declared entry | Authoritative route | Phase result |
|---|---|---|---|
| `oct07.s1` | `MapAction` | `oct07.cafe.s1` action mounted at `cafe` | explicit map action |
| `neutral.1008.s1` | `MapAction` | `neutral.1008.s1.action` mounted at `market` | explicit map action |
| `rose.1009.s1` | `MapAction` | `rose.1009.s1.action` mounted at the authored Saints Street position | explicit map action |
| `oct07.s2` | `BeforeNight` | day 2 / `night` / `once` | `BeforeNight` |
| `rose.1011.s1` | `MorningBeforeMap` | day 6 / `morning` / `once` | `MorningBeforeMap` |
| `church.1009.s2` | `Immediate` | day 4 / `night` / `once` | derived `BeforeNight`; `Immediate` compatibility allowed |
| `revolution.1013.s2` | `Immediate` | day 8 / `noon` / `once` | derived `AfterBusiness`; `Immediate` compatibility allowed |
| `revolution.1014.s1` | `MorningBeforeMap` | day 9 / `morning` / `once` | `MorningBeforeMap` |
| `revolution.1028.s1` | `BeforeNight` | day 23 / `night` / `once` | `BeforeNight` |

Read-only assertions over generated event, date, action, and map-position JSON passed `9/9`. The three map-action routes resolve to the expected authored actions and map positions; all six date routes have the expected day, time slot, and `once` repeat rule, with no phase conflict under the loader's compatibility rule.

## Conclusion

All formal events use values understood by the actual runtime enum. Their action/date sources map to the existing map-action and DayFlow phase APIs without bypassing the authoritative controller. No new entry type, scheduler, trigger alias, or parallel handoff was introduced.

