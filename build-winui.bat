@echo off
setlocal
set "ROOT=%~dp0"
dotnet build "%ROOT%src\XDBDatabase.WinUI\XDBDatabase.WinUI.csproj" -c Debug -p:Platform=x64
exit /b %ERRORLEVEL%
