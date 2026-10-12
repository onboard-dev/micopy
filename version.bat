@echo off
rem バージョン番号を設定します(ダイアログで 1.0.0 のように入力)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\version.ps1"
echo.
pause
