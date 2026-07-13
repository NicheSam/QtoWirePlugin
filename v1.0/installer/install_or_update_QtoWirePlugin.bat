@echo off
setlocal
tasklist /FI "IMAGENAME eq acad.exe" 2>NUL | find /I "acad.exe" >NUL
if not errorlevel 1 (
  echo Close AutoCAD before installing QtoWirePlugin.
  pause
  exit /b 1
)
if not exist "%~dp0QtoWirePlugin.bundle\Contents\Windows\QtoWirePlugin.dll" (
  echo Installation source is missing. Extract the complete ZIP before running this file.
  pause
  exit /b 1
)
set "TARGET=%APPDATA%\Autodesk\ApplicationPlugins\QtoWirePlugin.bundle"
if exist "%TARGET%" rmdir /s /q "%TARGET%"
xcopy "%~dp0QtoWirePlugin.bundle" "%TARGET%" /E /I /Y >NUL
if errorlevel 1 (
  echo Installation failed. Check write access to AppData.
  pause
  exit /b 1
)
set "SOURCE_DLL=%~dp0QtoWirePlugin.bundle\Contents\Windows\QtoWirePlugin.dll"
set "TARGET_DLL=%TARGET%\Contents\Windows\QtoWirePlugin.dll"
powershell.exe -NoProfile -Command "$a=(Get-FileHash -LiteralPath $env:SOURCE_DLL -Algorithm SHA256).Hash; $b=(Get-FileHash -LiteralPath $env:TARGET_DLL -Algorithm SHA256).Hash; if($a -ne $b){exit 1}"
if errorlevel 1 (
  echo Installed file verification failed. Extract the installer again and retry.
  pause
  exit /b 1
)
echo.
echo QtoWirePlugin v1.0.0 Beta installed and verified.
echo Start AutoCAD 2023 and check the QTO ribbon tab.
echo Excel will only open after you choose the sync command.
pause
