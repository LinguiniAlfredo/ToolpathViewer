@echo off
setlocal enabledelayedexpansion

REM Forward all arguments to build.ps1 with execution policy bypass
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
