@echo off
setlocal
set "PROJECT_DIR=%~dp0"
set "UNITY_DATA_OUTPUT=%PROJECT_DIR%..\Settings\Resources\Luban"
set "UNITY_CODE_OUTPUT=%PROJECT_DIR%..\Scripts\Config\Generated"

rem Per-machine installation; an explicit LUBAN_DIR takes precedence.
if not defined LUBAN_DIR if exist "D:\Luban\src\Luban\bin\Release\net8.0\Luban.dll" set "LUBAN_DIR=D:\Luban\src\Luban\bin\Release\net8.0"
if not defined LUBAN_DIR set "LUBAN_DIR=%LOCALAPPDATA%\Luban\5.1.0\Luban"
if exist "%LUBAN_DIR%\Luban.dll" goto find_dotnet
if exist "%LUBAN_DIR%\Luban.exe" goto tool_ready
echo ERROR: Luban.dll or Luban.exe not found in "%LUBAN_DIR%".
echo Set LUBAN_DIR to the directory containing the official Luban tool.
exit /b 1

:find_dotnet
rem A Unity process opened before installation may still have an old PATH.
set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
if exist "%DOTNET_EXE%" goto tool_ready
set "DOTNET_EXE="
for /f "delims=" %%I in ('where dotnet.exe 2^>nul') do if not defined DOTNET_EXE set "DOTNET_EXE=%%I"
if defined DOTNET_EXE goto tool_ready
echo ERROR: .NET is required to run Luban.dll. Install the .NET 8 runtime.
exit /b 1

:tool_ready
rem Only use Python when split authoring workbooks actually need merging.
set "NEED_MERGE="
dir /b "%PROJECT_DIR%Data\Narrative\Narrative_*.xlsx" >nul 2>nul && set "NEED_MERGE=1"
dir /b "%PROJECT_DIR%Data\Shared\Text_*.xlsx" >nul 2>nul && set "NEED_MERGE=1"
if not defined NEED_MERGE goto generate

set "PYTHON_EXE="
for /f "delims=" %%I in ('where python.exe 2^>nul') do if not defined PYTHON_EXE set "PYTHON_EXE=%%I"
if not defined PYTHON_EXE if exist "%LOCALAPPDATA%\Programs\Python\Python312\python.exe" set "PYTHON_EXE=%LOCALAPPDATA%\Programs\Python\Python312\python.exe"
if not defined PYTHON_EXE if exist "D:\Mamba\python.exe" set "PYTHON_EXE=D:\Mamba\python.exe"
if not defined PYTHON_EXE (
  echo ERROR: Python with openpyxl is required to merge split Excel files.
  exit /b 1
)
echo [1/2] Merging split Narrative/Text Excel workbooks...
"%PYTHON_EXE%" "%PROJECT_DIR%merge_narrative.py"
if errorlevel 1 (
  echo ERROR: Excel merge failed. Generation stopped.
  exit /b 1
)
goto run_luban

:generate
echo [1/2] Using saved Narrative.xlsx and Shared/Text.xlsx directly.

:run_luban
if not defined PYTHON_EXE for /f "delims=" %%I in ('where python.exe 2^>nul') do if not defined PYTHON_EXE set "PYTHON_EXE=%%I"
if not defined PYTHON_EXE (
  echo ERROR: Python with openpyxl is required for Player semantic validation.
  exit /b 1
)
"%PYTHON_EXE%" "%PROJECT_DIR%validate_player.py"
if errorlevel 1 exit /b 1
echo [2/2] Generating with "%LUBAN_DIR%"...
pushd "%PROJECT_DIR%"
if errorlevel 1 exit /b 1
if exist "%LUBAN_DIR%\Luban.dll" (
  "%DOTNET_EXE%" "%LUBAN_DIR%\Luban.dll" --conf luban.conf -t client -c cs-newtonsoft-json -d json --strict -x outputCodeDir="%UNITY_CODE_OUTPUT%" -x outputDataDir="%UNITY_DATA_OUTPUT%"
) else (
  "%LUBAN_DIR%\Luban.exe" --conf luban.conf -t client -c cs-newtonsoft-json -d json --strict -x outputCodeDir="%UNITY_CODE_OUTPUT%" -x outputDataDir="%UNITY_DATA_OUTPUT%"
)
set "EXIT_CODE=%ERRORLEVEL%"
popd
if not "%EXIT_CODE%"=="0" (
  echo ERROR: Luban generation failed with exit code %EXIT_CODE%.
  exit /b %EXIT_CODE%
)
echo.
echo SUCCESS: Generated typed C#: %UNITY_CODE_OUTPUT%
echo SUCCESS: Generated Unity JSON: %UNITY_DATA_OUTPUT%
echo Wait for Unity import, then restart Play Mode to reload text.
endlocal
exit /b 0
