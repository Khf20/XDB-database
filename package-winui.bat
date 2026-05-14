@echo off
setlocal
set "ROOT=%~dp0"
dotnet publish "%ROOT%src\XDBDatabase.WinUI\XDBDatabase.WinUI.csproj" -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false -p:AppxBundle=Always -p:AppxBundlePlatforms=x64
exit /b %ERRORLEVEL%
