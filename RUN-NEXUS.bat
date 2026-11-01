@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD-AND-RUN.ps1"
if errorlevel 1 (
  echo.
  echo NEXUS did not launch. Read the error above and share it for debugging.
  pause
)
endlocal
