# Official Luban Generation Run

Date: 2026-10-18
Scope: Task 13 of `ncs-formal-integration-overnight`
Status: Passed. The official batch completed with exit code 0.

## Command and environment

- Entry point: `Assets/ConfigTables/GenerateAll.bat`
- Working directory: `E:\Unity project\聚光灯2026`
- Source mode: saved `Narrative.xlsx` and `Shared/Text.xlsx` used directly; no split-workbook merge was required.
- Player semantic validator: `Assets/ConfigTables/validate_player.py`
- Luban directory selected by the batch: `D:\Luban\src\Luban\bin\Release\net8.0`
- Batch-selected .NET path: `C:\Program Files\dotnet\dotnet.exe`
- Generation targets: `cs-newtonsoft-json` and strict JSON data output.
- Complete stdout/stderr: `generation-20261018-051832.log`

## Result

The batch output reported:

```text
[1/2] Using saved Narrative.xlsx and Shared/Text.xlsx directly.
Player semantic validation passed.
[2/2] Generating with "D:\Luban\src\Luban\bin\Release\net8.0"...
...
validation begin
validation end
process data target:"json" begin
process data target:"json" end
bye~

SUCCESS: Generated typed C#: ...\Assets\Scripts\Config\Generated
SUCCESS: Generated Unity JSON: ...\Assets\Settings\Resources\Luban
```

The executor reported `exitCode=0`. The log contains the complete Luban output and no `ERROR` line.

## Post-run diff boundary

The authoritative workbook was already modified before this run and remains preserved. The generated output diff after the run is limited to the expected tracked narrative JSON files:

- `Assets/Settings/Resources/Luban/tbncscondition.json`
- `Assets/Settings/Resources/Luban/tbncsevent.json`
- `Assets/Settings/Resources/Luban/tbncseventdate.json`
- `Assets/Settings/Resources/Luban/tbncsresult.json`

No generated C# file changed. `Text.xlsx` was not modified. No unrelated project path was changed by the batch invocation.

## Unity follow-up

After generation, the connected Unity `6000.3.5f2` Editor reported:

- `playMode=stopped`
- `compiling=false`
- `domainReloadInProgress=false`
- `recompile_status=completed`
- `failed=false`
- `compilationFailed=false`

This is generation evidence only. Strict validation, generated/source reconciliation, Unity focused tests, and runtime acceptance remain separate ordered tasks.

## Preservation boundary

The batch did not edit the source workbook, reports, queue, scenes, save slots, or presentation assets. Existing user/Locus modifications and unrelated untracked files remain untouched.
