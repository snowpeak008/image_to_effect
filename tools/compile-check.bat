@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-Unity.ps1" -Mode Compile
set RESULT=%ERRORLEVEL%
exit /b %RESULT%
