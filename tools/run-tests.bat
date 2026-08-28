@echo off
setlocal
set TEST_PLATFORM=%~1
if "%TEST_PLATFORM%"=="" set TEST_PLATFORM=EditMode
if /I not "%TEST_PLATFORM%"=="EditMode" if /I not "%TEST_PLATFORM%"=="PlayMode" (
  echo Usage: %~nx0 [EditMode^|PlayMode]
  exit /b 64
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-Unity.ps1" -Mode %TEST_PLATFORM%
set RESULT=%ERRORLEVEL%
exit /b %RESULT%
