@echo off
setlocal
title XDB-database Installer
echo.
echo XDB-database installer
echo ----------------------
echo This will sign, install, and launch XDB-database for the current Windows user.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-winui.ps1" -SkipPackage
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo.
  echo Install failed with exit code %EXITCODE%.
  pause
)
exit /b %EXITCODE%
