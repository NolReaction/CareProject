@echo off
setlocal
powershell.exe -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0RepairProject.ps1" %*
set "care_exit=%errorlevel%"
echo.
if not "%care_exit%"=="0" echo CareProject repair did not complete. Read the error above.
pause
exit /b %care_exit%
