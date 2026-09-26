@echo off
REM AI ERP - one-shot installer launcher for Windows.
REM Runs install.ps1 next to this file. If that file is not present (for example when
REM only install.bat was downloaded), it downloads the installer from GitHub and runs
REM that instead. The window stays open at the end so you can read what happened.

set "PS1_LOCAL=%~dp0install.ps1"
set "PS1_URL=https://raw.githubusercontent.com/Graphene-Lab/AI-ERP/master/installer/install.ps1"

where powershell >nul 2>nul
if errorlevel 1 (
  echo PowerShell was not found on this computer.
  echo Install Windows PowerShell or .NET, then run this again.
  echo.
  pause
  exit /b 1
)

if exist "%PS1_LOCAL%" (
  echo Starting the AI ERP installer...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1_LOCAL%"
  if errorlevel 1 goto failed
  goto finish
)

echo install.ps1 was not found next to install.bat.
echo Downloading the installer from GitHub...
set "PS1_TMP=%TEMP%\aierp-install.ps1"
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri '%PS1_URL%' -OutFile '%PS1_TMP%' -UseBasicParsing"
if errorlevel 1 (
  echo.
  echo Could not download the installer. Check your internet connection and try again.
  echo Guide: https://github.com/Graphene-Lab/AI-ERP/blob/master/installer/README.md
  echo.
  pause
  exit /b 1
)

echo Starting the AI ERP installer...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1_TMP%"
if errorlevel 1 goto failed

:finish
echo.
echo The installer has finished. You can close this window.
echo.
pause
exit /b 0

:failed
echo.
echo The installer stopped with an error. Read the message above to find the cause,
echo fix it and run this file again: running it again is safe and never wipes data.
echo Guide: https://github.com/Graphene-Lab/AI-ERP/blob/master/installer/README.md
echo.
pause
exit /b 1
