@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop-apache-admin.ps1"
exit /b %ERRORLEVEL%
