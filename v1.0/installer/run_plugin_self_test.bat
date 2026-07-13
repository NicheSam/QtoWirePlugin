@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_installed_self_test.ps1"
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" (
  echo QtoWirePlugin self-test failed. Send the error text to the developer.
) else (
  echo QtoWirePlugin self-test passed. The report is stored in the Windows temp folder.
)
pause
exit /b %RESULT%
