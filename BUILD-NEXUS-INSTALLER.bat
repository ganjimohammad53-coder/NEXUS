@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD-NEXUS-INSTALLER.ps1"
if errorlevel 1 (
  echo.
  echo NEXUS installer build FAILED. Read the error above.
  pause
  exit /b 1
)
echo.
echo Installer created in artifacts\installer\NEXUS-1.0-Setup-x64.exe
pause
