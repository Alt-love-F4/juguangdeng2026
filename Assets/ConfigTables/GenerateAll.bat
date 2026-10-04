@echo off
setlocal
if not defined LUBAN_DIR set "LUBAN_DIR=D:\Luban\src\Luban\bin\Release\net8.0"
set "PROJECT_DIR=%~dp0"
set "UNITY_DATA_OUTPUT=%PROJECT_DIR%..\Settings\Resources\Luban"
set "UNITY_CODE_OUTPUT=%PROJECT_DIR%..\Scripts\Config\Generated"

if not exist "%LUBAN_DIR%\Luban.exe" (
  echo Luban.exe not found: %LUBAN_DIR%
  exit /b 1
)

:: python 兜底：Unity 可能继承旧 PATH 导致 9009，补充本机 Python 路径
where python >nul 2>nul
if errorlevel 1 (
  if exist "D:\Mamba\python.exe" set "PATH=D:\Mamba;%PATH%"
)
where python >nul 2>nul
if errorlevel 1 (
  echo python not found in PATH. Please restart Unity or install Python.
  exit /b 1
)

:: 步骤 1: 仅当存在分表（Narrative_*.xlsx / Text_*.xlsx）时才合并，否则直接使用主表
set "NEED_MERGE="
dir /b "%PROJECT_DIR%Data\Narrative_*.xlsx" >nul 2>nul && set "NEED_MERGE=1"
dir /b "%PROJECT_DIR%Data\Text_*.xlsx" >nul 2>nul && set "NEED_MERGE=1"

if defined NEED_MERGE (
  echo [1/2] 合并分散的叙事配置...
  python "%PROJECT_DIR%merge_narrative.py"
  if errorlevel 1 (
    echo ERROR: 合并失败，存在主键冲突!
    exit /b 1
  )
) else (
  echo [1/2] 未找到分表，跳过合并，直接使用主表 Narrative.xlsx / Text.xlsx
)

:: 步骤 2: 运行 Luban 生成
echo [2/2] 生成 Luban 配置...
pushd "%PROJECT_DIR%"
"%LUBAN_DIR%\Luban.exe" --conf luban.conf -t client -c cs-newtonsoft-json -d json --strict -x outputCodeDir="%UNITY_CODE_OUTPUT%" -x outputDataDir="%UNITY_DATA_OUTPUT%"
set "EXIT_CODE=%ERRORLEVEL%"
popd

if not "%EXIT_CODE%"=="0" (
  echo Luban generation failed with exit code %EXIT_CODE%.
  exit /b %EXIT_CODE%
)

echo.
echo Generated typed C#: %UNITY_CODE_OUTPUT%
echo Generated Unity JSON: %UNITY_DATA_OUTPUT%
endlocal
exit /b 0
