# Date, Time, and Once-Only Validation

Date: 2026-10-18
Scope: Task 20 of `ncs-formal-integration-overnight`
Status: Passed.

## DayFlow calendar and phase contract

`DayFlowController` defines the first in-game date as `670-10-06` and derives later dates with `FirstDayDate.AddDays(day - 1)` (`Assets/Scripts/GameFlow/UI/DayFlowController.cs:35-36, 76`). Its formal handoff is:

- day transition -> `MorningBeforeMap` before entering the map;
- business completion -> `AfterBusiness` before entering the map;
- map end-day -> `BeforeNight` before completing the day.

The entry-to-time-slot mapping is implemented in `NarrativeController.Entries.cs:74-93` and the official-day scheduling path is `NarrativeController.cs:167-188`.

## Runtime-loaded date rows

The connected Unity Editor loaded the generated configuration and returned:

```text
oct07.s2 day=2 slot=Night repeat=Once
church.1009.s2 day=4 slot=Night repeat=Once
rose.1011.s1 day=6 slot=Morning repeat=Once
revolution.1013.s2 day=8 slot=Noon repeat=Once
revolution.1014.s1 day=9 slot=Morning repeat=Once
revolution.1028.s1 day=23 slot=Night repeat=Once
```

The source `EventDate` sheet contains the same six non-empty rows, and the generated `tbncseventdate.json` contains six rows. All six use `repeatRule=once`.

## Formal day matrix

| Game day | Formal route | Authored slot | Runtime phase | Once condition |
|---:|---|---|---|---|
| 2 | `oct07.s2` | `night` | `BeforeNight` | `once` |
| 3 | `neutral.1008.s1` | no `EventDate` | explicit `MapAction` at `market` | action is authored `ONCE`; no date row |
| 4 | `church.1009.s2` | `night` | `BeforeNight` | `once` |
| 4 | `rose.1009.s1` | no `EventDate` | explicit `MapAction` at authored Saints Street position | action is authored `ONCE`; no date row |
| 6 | `rose.1011.s1` | `morning` | `MorningBeforeMap` | `once` |
| 8 | `revolution.1013.s2` | `noon` | `AfterBusiness` | `once` |
| 9 | `revolution.1014.s1` | `morning` | `MorningBeforeMap` | `once` |
| 23 | `revolution.1028.s1` | `night` | `BeforeNight` | `once` |

`oct07.s1` is also an explicit `MapAction` root at the cafe and has no `EventDate`; it is the map-action prerequisite for the day-2 night route rather than a second date-dispatched row.

## Boundary assertions

Read-only assertions over source workbook rows, generated event/date/action/map-position data, and runtime entry strings passed:

- Every generated date row has the expected formal event, day, time slot, and `once` rule.
- Every dated formal event has either the exact derived phase declaration or the supported legacy `Immediate` declaration (`church.1009.s2`, `revolution.1013.s2`).
- `oct07.s1`, `neutral.1008.s1`, and `rose.1009.s1` have no date row and declare `MapAction`.
- Day 3 is not fabricated as a date event; its formal route is the market map action.
- Day 4 keeps both the night Church date route and the separate Rose map action; neither is silently substituted for the other.
- All formal action/date once-only boundaries remain authored values; no repeat rule was invented.

Conclusion: the configured day 2, day 3, day 4, day 6, day 8, day 9, and day 23 routes agree with the actual DayFlow and NCS phase contracts.

