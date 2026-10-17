# Revolution Trigger Guard Recheck

Date: 2026-10-18 07:36 +08:00
Scope: Task 31 of `ncs-formal-integration-overnight`
Status: Passed. The legacy trigger remains disabled; retained Revolution content and the three dated roots remain present.

## Authoritative source

Read-only inspection of `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` confirmed:

- The `Trigger` sheet has exactly one `T_CAL_D60` row: `world.calendar`, threshold `60`, direction `up`, crossing type `cross`, once type `once`, target `EV_REVOLUTION`, and `enabled=false`.
- `Event` retains `EV_REVOLUTION` with `Tpl_CutScene`, setup key `revolution`, and authored content keys `ev.rev.p1|ev.rev.p2|ev.rev.p3`.
- `EventDate` has no `EV_REVOLUTION` route. The three dated roots remain: `revolution.1013.s2` day 8/Noon, `revolution.1014.s1` day 9/Morning, and `revolution.1028.s1` day 23/Night, each once-only.
- `Result` retains `R_REV_1` (`SET_FLAG revolution=true`) and `R_REV_2` (`BAR_ADD bar:world.stability=-3`), plus one completion result for each dated root.

Current `Narrative.xlsx` SHA-256: `70F9055F23AA7CC3E05E120E5A07674272E8A26D4DE9DE5F8658DE88F51E3175`.

## Generated and report state

All generated JSON files parsed with Python's UTF-8 JSON reader and assertion checks passed:

```text
trigger rows: 1; T_CAL_D60 enabled=false -> EV_REVOLUTION
events: 38; EV_REVOLUTION: 1; dated Revolution roots: 3
dates: 6 total; dated Revolution roots: 3; EV_REVOLUTION date rows: 0
results: 33 total; EV_REVOLUTION results: R_REV_1 and R_REV_2
queue: 38 rows; EV_REVOLUTION queue rows: 1
integration report: EV_REVOLUTION [static_pass] present
assertions: PASS
```

Unity-loaded Luban TextAssets independently returned:

```text
T_CAL_D60_row=True;enabled_false=True;target_EV_REVOLUTION=True;
EV_REVOLUTION_present=True;legacy_date_absent=True;legacy_results=True;root_events=True
```

Runtime dispatch checks trigger `enabled`: `NarrativeController.cs` skips disabled triggers at lines 626, 633, and 640; `NarrativeController.Entries.cs` rejects disabled-trigger candidates at lines 62 and 140.

`NcsIntegrationQueue.csv` has one `EV_REVOLUTION` row marked `config_pass`. `Docs/NcsIntegrationReport.md` has one `EV_REVOLUTION [static_pass]` section and reports both retained results. `Docs/NcsFormalDependencies.md` documents the disabled legacy trigger.

## Unity verification

Connected Unity `6000.3.5f2` EditMode test:

```text
Spotlight.Narrative.Editor.Tests.NarrativeConfigurationValidatorTests.ActualGeneratedLubanDataLoadsAndValidates
Passed 1/1; failed 0; skipped 0.
```

The test loads generated Luban configuration, validates it, asserts there is no `EV_REVOLUTION` date route, and asserts no enabled trigger targets `EV_REVOLUTION`. After the run Unity reported `compilationFailed=false`, `compiling=false`, `consoleErrors=0`, and `consoleWarnings=0`.

## Hashes

```text
074ED94516D6F78794EF61A14A377F9B71F6DCC0FA4845E444267097461929B5  Assets/Settings/Resources/Luban/tbncstrigger.json
53709938E97091A62B8DEA5818AAED4A8532E588BDC8160AF4BEEFB400767544  Assets/Settings/Resources/Luban/tbncsevent.json
8D1018468879A56B5EA86242422C46AB7D59EFD9520A9FC0D65F278C74195BC1  Assets/Settings/Resources/Luban/tbncseventdate.json
FDA2E12597EE2DBDD45701AAEB9CAB81DF4A2DD9603048C94D33E01FDE20A60A  Assets/Settings/Resources/Luban/tbncsresult.json
25DF41F42CA6A4EEA2FF61B266C4E07D235A38CAF6B945AAEFBE273CDA9E8BEB  Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv
B3C4C31DE417E6BBE562D57057DA82E451363BDFCEDD4B904369F70377503651  Docs/NcsIntegrationReport.md
1B8E06CC8EE0DC8805D7F8B54449B1770A04E916E3C0E27A0844525CE1E43911  Docs/NcsFormalDependencies.md
```

No source workbook, generated file, report, runtime code, scene, asset, or save slot was changed by this recheck. This verifies the disabled-trigger/configuration guard; it does not claim real DayFlow or Player acceptance.
