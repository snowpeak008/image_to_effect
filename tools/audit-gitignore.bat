@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-GitIgnore.ps1"
set RESULT=%ERRORLEVEL%
exit /b %RESULT%
