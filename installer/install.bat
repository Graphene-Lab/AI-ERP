@echo off
REM AI ERP - one-shot installer for Windows.
REM Runs the PowerShell installer next to this file.
setlocal
set "PS=%~dp0install.ps1"
if not exist "%PS%" (
  echo Installer script not found: %PS% 1>&2
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS%"
endlocal
