# Luban Strict Validation and Generation

Date: 2026-10-18
Scope: Task 15 of `ncs-formal-integration-overnight`
Status: Passed.

## Direct command

The official Luban executable was invoked directly with the same strict client configuration used by `Assets/ConfigTables/GenerateAll.bat`:

```text
"C:\Program Files\dotnet\dotnet.exe" "D:\Luban\src\Luban\bin\Release\net8.0\Luban.dll" --conf luban.conf -t client -c cs-newtonsoft-json -d json --strict -x outputCodeDir="E:\Unity project\聚光灯2026\Assets\Scripts\Config\Generated" -x outputDataDir="E:\Unity project\聚光灯2026\Assets\Settings\Resources\Luban"
```

Working directory: `E:\Unity project\聚光灯2026\Assets\ConfigTables`

The command used the authoritative saved workbooks already validated by Task 14:

- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`
- `Assets/ConfigTables/Data/Shared/Text.xlsx`

Schema and configuration were loaded from the existing `schema.xml` and `luban.conf`; no generated JSON or C# file was used as an input source.

## Result

- Captured stdout/stderr: `luban-strict-20261018-052213.log`
- Process exit code: `0`
- Schema load: completed
- Data load: completed
- `cs-newtonsoft-json` code target: completed
- Strict validation: completed with no validation errors
- JSON data target: completed
- Luban termination: `bye~`

The complete log contains the following final stages:

```text
load schema. collector: "default"
load datas begin
process code target:"cs-newtonsoft-json" begin
process code target:"cs-newtonsoft-json" end
load datas end
validation begin
validation end
process data target:"json" begin
process data target:"json" end
bye~
```

## Output and hand-edit boundary

The command wrote only to the configured generated output directories:

- `Assets/Scripts/Config/Generated`
- `Assets/Settings/Resources/Luban`

The post-run tracked generated diff remains limited to the expected narrative JSON files:

- `Assets/Settings/Resources/Luban/tbncscondition.json`
- `Assets/Settings/Resources/Luban/tbncsevent.json`
- `Assets/Settings/Resources/Luban/tbncseventdate.json`
- `Assets/Settings/Resources/Luban/tbncsresult.json`

No generated C# file changed. No hand edit was applied to generated JSON after this strict run. The JSON differences are preserved as generated output and are reconciled separately by Task 16. The authoritative workbooks, schema, Luban configuration, scenes, presentation assets, reports, queue source, and save slots were not edited by the strict invocation.

## Unity post-import check

After the direct run, the connected Editor reported:

- Unity `6000.3.5f2`
- `playMode=stopped`
- `compiling=false`
- `domainReloadInProgress=false`
- `recompile_status=completed`
- `failed=false`
- `compilationFailed=false`
- current console ground truth: `0` errors and `0` warnings

This proves the strict source validation and generation step only. Runtime map/DayFlow, loading, cancellation, retry, save/restore, and exactly-once acceptance remain later tasks.

