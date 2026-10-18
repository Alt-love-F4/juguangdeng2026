# Revolution Invariant Evidence

Date: 2026-10-18
Scope: Task 8 of `ncs-formal-integration-overnight`
Status: Verified. This task made no project asset, workbook, generated-data, runtime-code, scene, or report edits.

## Invariant

The retained Revolution content must remain present while the legacy global trigger remains disabled:

- `EV_REVOLUTION` remains defined with its original world cut-scene content and results.
- `T_CAL_D60` remains present but `enabled=false`; it must not become an active formal date/event route.
- The formal dated Revolution roots remain present and independently reportable:
  - `revolution.1013.s2`: day 8, Noon, once.
  - `revolution.1014.s1`: day 9, Morning, once.
  - `revolution.1028.s1`: day 23, Night, once.
- Revolution text, conversation pages/lines, choices, inline checks, reading content, completion results, queue row, integration report, and dependency documentation remain present.

## Authoritative source checks

Source workbook rows in `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`:

- `Trigger`: one row for `T_CAL_D60`, `eventId=EV_REVOLUTION`, `barKey=world.calendar`, threshold `60`, `crossType=cross`, `enabled=false`.
- `Event`: one `EV_REVOLUTION` row with `setupType=Tpl_CutScene`, `setupKey=revolution`, and `contentKey=ev.rev.p1|ev.rev.p2|ev.rev.p3`.
- `Event`: all three formal dated Revolution root rows remain present.
- `EventDate`: three dated root rows remain present at day 8/Noon, day 9/Morning, and day 23/Night, all `once`.
- `Result`: five related rows remain present: two `EV_REVOLUTION` results and one completion result for each dated root.
- Revolution presentation rows remain present: 7/52/3 for `revolution.1013.s2`, 23/171/12 for `revolution.1014.s1`, and 10/144/3 for `revolution.1028.s1` (pages/lines/choices).
- `revolution.1014.s1` retains its three inline ability-check conditions and one reading page/line.
- Shared `Text.xlsx` retains `ev.rev.title`, `ev.rev.p1`, `ev.rev.p2`, and `ev.rev.p3`; it also retains 386 `ncs.revolution.*` keys, partitioned as 55 for C13, 184 for C14, and 147 for C28.

Source hashes:

```text
0B2825F6E692F657238F0EFFA63CBEDA4289E7493279C60923313A8EB85FB02A  Assets/ConfigTables/Data/Narrative/Narrative.xlsx
CAD8194771CA4A0E3AC040A4D007191CA93E1DA846DD629D920BE0CE214F5276  Assets/ConfigTables/Data/Shared/Text.xlsx
```

## Generated-data checks

The current Luban outputs parse successfully and retain the same invariant:

- `tbncstrigger.json`: one trigger row, `T_CAL_D60 -> EV_REVOLUTION`, `enabled=false`.
- `tbncsevent.json`: 38 events total, including `EV_REVOLUTION` and all three dated roots.
- `tbncseventdate.json`: six date rows total, including exactly the three dated Revolution roots; no `EV_REVOLUTION` date row.
- `tbncsresult.json`: 33 rows total, including:
  - `R_REV_1`: `SET_FLAG revolution=true`.
  - `R_REV_2`: `BAR_ADD bar:world.stability=-3`.
  - `story.revolution.1013.s2.completed=true`.
  - `story.revolution.1014.s1.completed=true`.
  - `story.revolution.1028.s1.completed=true`.
- `tbncsconversationpage.json`, `tbncsconversationline.json`, and `tbncschoice.json` retain the three root presentation datasets.
- `tbncsabilitycheck.json` and `tbncsabilitycheckcondition.json` retain the three C14 inline checks.
- `tbncsreadingpage.json` and `tbncsreadingline.json` retain the C14 reading step.
- `tbtext.json` retains the four `ev.rev.*` keys and the 386 `ncs.revolution.*` keys.

Key generated hashes:

```text
074ED94516D6F78794EF61A14A377F9B71F6DCC0FA4845E444267097461929B5  Assets/Settings/Resources/Luban/tbncstrigger.json
53709938E97091A62B8DEA5818AAED4A8532E588BDC8160AF4BEEFB400767544  Assets/Settings/Resources/Luban/tbncsevent.json
FDA2E12597EE2DBDD45701AAEB9CAB81DF4A2DD9603048C94D33E01FDE20A60A  Assets/Settings/Resources/Luban/tbncsresult.json
8D1018468879A56B5EA86242422C46AB7D59EFD9520A9FC0D65F278C74195BC1  Assets/Settings/Resources/Luban/tbncseventdate.json
```

## Report and queue checks

`Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv` contains the retained `EV_REVOLUTION` row with source `T_CAL_D60 / world.calendar @ 60 / Cross`, expected results `SetFlag -> revolution | BarAdd -> bar:world.stability`, and status `config_pass`. The three dated root rows are also present with their date, prerequisites, checks, and root completion flags.

`Docs/NcsIntegrationReport.md` contains `EV_REVOLUTION [static_pass]` and reports the same two results. It contains all three dated roots as `static_pass` with their formal dates, presentations, checks, and completion results. `Docs/NcsFormalDependencies.md` explicitly records that `EV_REVOLUTION` is retained and `T_CAL_D60` remains disabled.

The apparent historical report mismatch was not reproducible. The report builder associates results with the exact event ID (`item.eventId == definition.eventId`), so October 7 results cannot be assigned to `EV_REVOLUTION`. The current report and the `HEAD` version both show `SetFlag -> revolution` and `BarAdd -> bar:world.stability`; no report rewrite was necessary.

## Connected Unity verification

- Unity: `6000.3.5f2`.
- Focused test: `Spotlight.Narrative.Editor.Tests.NarrativeConfigurationValidatorTests.ActualGeneratedLubanDataLoadsAndValidates` - 1 passed, 0 failed, 0 skipped.
- Connected report evaluation: `events=38; errors=0; warnings=0; triggerEnabled=False; triggerEvent=EV_REVOLUTION; roots=static_pass,static_pass,static_pass`.
- The same evaluation returned `EV_REVOLUTION` sources `Trigger: T_CAL_D60 / world.calendar @ 60 / Cross` and results `SetFlag -> revolution; BarAdd -> bar:world.stability`.
- The existing validator also confirms no `EventDate` row exists for `EV_REVOLUTION` and no enabled trigger points to it.

## Boundary

This evidence proves source/generated/config/report retention and the disabled legacy trigger. It does not claim real map/DayFlow execution, loading, cancellation, retry, save/restore, or exactly-once acceptance; those remain later tasks.
