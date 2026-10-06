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

:: python ���ף�Unity ���ܼ̳о� PATH ���� 9009�����䱾�� Python ·��
where python >nul 2>nul
if errorlevel 1 (
  if exist "D:\Mamba\python.exe" set "PATH=D:\Mamba;%PATH%"
)
where python >nul 2>nul
if errorlevel 1 (
  echo python not found in PATH. Please restart Unity or install Python.
  exit /b 1
)

:: ���� 1: �������ڷֱ���Narrative_*.xlsx / Text_*.xlsx��ʱ�źϲ�������ֱ��ʹ������
set "NEED_MERGE="
dir /b "%PROJECT_DIR%Data\Narrative_*.xlsx" >nul 2>nul && set "NEED_MERGE=1"
dir /b "%PROJECT_DIR%Data\Text_*.xlsx" >nul 2>nul && set "NEED_MERGE=1"

if defined NEED_MERGE (
  echo [1/2] �ϲ���ɢ����������...
  python "%PROJECT_DIR%merge_narrative.py"
  if errorlevel 1 (
    echo ERROR: �ϲ�ʧ�ܣ�����������ͻ!
    exit /b 1
  )
) else (
  echo [1/2] δ�ҵ��ֱ��������ϲ���ֱ��ʹ������ Narrative.xlsx / Text.xlsx
)

:: ���� 2: ���� Luban ����
echo [2/2] ���� Luban ����...
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
