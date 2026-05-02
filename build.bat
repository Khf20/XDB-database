@echo off
setlocal
set "ROOT=%~dp0"
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo C# compiler not found.
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ ^
  /out:"%ROOT%XDB-database.exe" ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  "%ROOT%XdbDatabase.cs"

exit /b %ERRORLEVEL%
