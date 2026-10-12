@echo off
rem GitHub (onboard-dev/micopy) へコミット&プッシュします
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\commit.ps1"
echo.
pause
