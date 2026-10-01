@echo off
setlocal
if not defined LUBAN_DIR set "LUBAN_DIR=D:\Luban\src\Luban\bin\Release\net8.0"
set "PROJECT_DIR=%~dp0"
set "UNITY_OUTPUT=%PROJECT_DIR%..\Settings\Resources\Luban"

if not exist "%LUBAN_DIR%\Luban.exe" (
  echo Luban.exe not found: %LUBAN_DIR%
  exit /b 1
)

if not exist "%UNITY_OUTPUT%" mkdir "%UNITY_OUTPUT%"

pushd "%PROJECT_DIR%"
"%LUBAN_DIR%\Luban.exe" --conf luban.conf -t client -d json --strict -x outputDataDir="%UNITY_OUTPUT%"
set "EXIT_CODE=%ERRORLEVEL%"
popd

if not "%EXIT_CODE%"=="0" (
  echo Luban generation failed with exit code %EXIT_CODE%.
  exit /b %EXIT_CODE%
)

echo Generated Unity text data: %UNITY_OUTPUT%\tbtext.json
endlocal
exit /b 0
