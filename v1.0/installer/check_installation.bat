@echo off
setlocal
set "TARGET=%APPDATA%\Autodesk\ApplicationPlugins\QtoWirePlugin.bundle"
set "SOURCE_DLL=%~dp0QtoWirePlugin.bundle\Contents\Windows\QtoWirePlugin.dll"
set "TARGET_DLL=%TARGET%\Contents\Windows\QtoWirePlugin.dll"
set "MANIFEST=%TARGET%\PackageContents.xml"

if not exist "%TARGET_DLL%" (
  echo QtoWirePlugin.dll is not installed. Run install_or_update_QtoWirePlugin.bat first.
  pause
  exit /b 1
)
if not exist "%MANIFEST%" (
  echo PackageContents.xml is missing from the installed bundle.
  pause
  exit /b 1
)
powershell.exe -NoProfile -Command "$xml=[xml](Get-Content -LiteralPath $env:MANIFEST -Raw); if($xml.ApplicationPackage.AppVersion -ne '1.0.0'){exit 1}"
if errorlevel 1 (
  echo The installed version is not v1.0.0.
  pause
  exit /b 1
)
if exist "%SOURCE_DLL%" (
  powershell.exe -NoProfile -Command "$a=(Get-FileHash -LiteralPath $env:SOURCE_DLL -Algorithm SHA256).Hash; $b=(Get-FileHash -LiteralPath $env:TARGET_DLL -Algorithm SHA256).Hash; if($a -ne $b){exit 1}"
  if errorlevel 1 (
    echo The installed DLL does not match this installer.
    pause
    exit /b 1
  )
)

echo QtoWirePlugin v1.0.0 Beta installation is valid.
echo Installed at: %TARGET%
pause
