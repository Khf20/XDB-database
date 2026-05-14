@echo off
set "XDB_ROOT=%~dp0"
set "PATH=%XDB_ROOT%stack\php;%XDB_ROOT%stack\mariadb\bin;%XDB_ROOT%stack\apache\bin;%PATH%"
cd /d "%XDB_ROOT%www"
title XDB Portable Shell
echo XDB Portable Shell
echo ------------------
echo php, mysql, and Apache tools are available in this terminal session.
echo.
