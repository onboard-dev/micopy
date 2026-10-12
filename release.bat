@echo off
rem GitHub に新しいリリース(タグ+micopy.exe)を登録します。GitHub CLI (gh) が必要です
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\release.ps1"
echo.
pause
