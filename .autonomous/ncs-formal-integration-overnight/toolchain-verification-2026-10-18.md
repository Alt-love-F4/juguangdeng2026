# NCS Toolchain Verification

Captured: 2026-10-18 (Executor session 2, Task 4)

## Toolchain

| Tool | Result | Evidence |
|---|---|---|
| Unity Editor | Available and ready | `ProjectSettings/ProjectVersion.txt` is `6000.3.5f2` with revision `3fa8bc678cb0`; connected Editor status reports Unity `6000.3.5f2`, `ready`, stopped, not compiling, and no domain reload in progress. |
| Unity recompile state | Clean at capture time | `recompile_status`: `completed`, `failed=false`, `compilationFailed=false`, no reported errors. |
| Official Luban | Available | `D:\Luban\src\Luban\bin\Release\net8.0\Luban.exe` and `Luban.dll` exist; `Luban.exe --version` reports `5.1.0+df34215d2e22035073d1af83d44e3ac0744dc3b3`. Official skill files under `D:\Luban\ai\skills\` are present. |
| .NET runtime for Luban | Available | `C:\Program Files\dotnet\dotnet.exe --version` reports `8.0.425`; the installed x64 runtimes include `Microsoft.NETCore.App 8.0.12`, `8.0.16`, `8.0.23`, `8.0.25`, and `8.0.31`. |
| Python | Available | `python` resolves to `D:\Mamba\python.exe`, version `3.10.14`. |
| `openpyxl` | Available | `python -c "import openpyxl"` succeeds; version `3.1.5`. |
| .NET on PATH | Available but not the batch-selected runtime | PATH `dotnet` resolves to `D:\dotnet10\dotnet.exe`, version `10.0.401`. It cannot launch the net8 Luban DLL by itself; `GenerateAll.bat` explicitly prefers `%ProgramFiles%\dotnet\dotnet.exe`, which is the working .NET 8 installation above. |

The official `Luban.exe --help` and `--version` commands execute. A bare `dotnet D:\Luban\...\Luban.dll` command fails with a missing x64 .NET 8 framework because it uses the PATH .NET 10 installation; this is a command-selection trap, not a project blocker, because the official batch file selects `C:\Program Files\dotnet\dotnet.exe` first.

## Project entry points

| Entry point | Availability |
|---|---|
| `Assets/ConfigTables/GenerateAll.bat` | Present; official source merge/Player semantic validation/Luban strict generation entry point. |
| Unity test discovery | Connected Editor `list_tests(mode=all)` succeeds and reports 1,515 tests across EditMode and PlayMode. Actual execution remains a later task. |
| Relevant test projects | `Spotlight.GameFlow.Narrative.Tests.csproj`, `Spotlight.GameFlow.Presentation.Tests.csproj`, `Spotlight.Narrative.Editor.Tests.csproj`, `Spotlight.FreeCafe.Tests.EditMode.csproj`, and `Spotlight.FreeCafe.Tests.PlayMode.csproj` are present. Relevant `.asmdef` files are present under Narrative, GameFlow, and FreeCafe test folders. |
| Formal build entry point | Connected Editor `build` API is available; active target is `StandaloneWindows64`. `list_build_targets` reports `StandaloneWindows64` installed. |
| Current build scenes | Enabled: `Assets/Scenes/启动场景.unity`, `Assets/Scenes/CoffeeShop.unity`, `Assets/Scenes/Map.unity`. `Assets/Scenes/PresentationDebugScene.unity` is present on disk but not in Build Settings. |

## Current Editor/build diagnostics

- `get_build_settings`: active target `StandaloneWindows64`, development build false, scripting settings available, three enabled scenes.
- Current console ground truth reports `consoleErrors=0`, `consoleWarnings=0`, and `compilationFailed=false` at the sample time.
- The retained console buffer reports historical counts of 471 errors and 1,240 warnings, while the ground-truth sample has no current error/warning entries. Later verification must use the ground-truth status and a fresh console capture, not the retained counters alone.

## Blockers and limits

- No toolchain availability blocker is present after selecting the batch file's explicit .NET 8 path.
- No test execution result is claimed here; only discovery and entry-point availability were verified.
- No generation result is claimed here; invoking `GenerateAll.bat` is reserved for Task 13 so its complete output and exit code can be captured in a dedicated evidence file.
- No Player build result is claimed here; the installed Windows 64-bit target is only a prerequisite for the later build task.
