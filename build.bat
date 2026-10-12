@echo off
rem ============================================================
rem  micopy ビルド用バッチ(Windows標準の .NET Framework 4.x の csc.exe を使用)
rem  Visual Studio は不要です。このファイルをダブルクリックすると bin\micopy.exe ができます。
rem  引数に nopause を付けると最後の一時停止をしません(他のバッチから呼ぶとき用)。
rem ============================================================
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist bin mkdir bin

"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:bin\micopy.exe ^
  /win32icon:res\micopy.ico /win32manifest:res\app.manifest ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.Linq.dll /r:System.Xml.dll /r:System.Core.dll ^
  /resource:res\micopy.ico,micopy.ico /resource:res\logo.png,micopy.logo.png ^
  /resource:res\emoji\add.png,micopy.emoji.add.png ^
  /resource:res\emoji\arrow.png,micopy.emoji.arrow.png ^
  /resource:res\emoji\broom.png,micopy.emoji.broom.png ^
  /resource:res\emoji\bulb.png,micopy.emoji.bulb.png ^
  /resource:res\emoji\chart.png,micopy.emoji.chart.png ^
  /resource:res\emoji\check.png,micopy.emoji.check.png ^
  /resource:res\emoji\clock.png,micopy.emoji.clock.png ^
  /resource:res\emoji\cross.png,micopy.emoji.cross.png ^
  /resource:res\emoji\door.png,micopy.emoji.door.png ^
  /resource:res\emoji\edit.png,micopy.emoji.edit.png ^
  /resource:res\emoji\export.png,micopy.emoji.export.png ^
  /resource:res\emoji\file.png,micopy.emoji.file.png ^
  /resource:res\emoji\floppy.png,micopy.emoji.floppy.png ^
  /resource:res\emoji\folder.png,micopy.emoji.folder.png ^
  /resource:res\emoji\hourglass.png,micopy.emoji.hourglass.png ^
  /resource:res\emoji\import.png,micopy.emoji.import.png ^
  /resource:res\emoji\info.png,micopy.emoji.info.png ^
  /resource:res\emoji\memo.png,micopy.emoji.memo.png ^
  /resource:res\emoji\noentry.png,micopy.emoji.noentry.png ^
  /resource:res\emoji\open_folder.png,micopy.emoji.open_folder.png ^
  /resource:res\emoji\package.png,micopy.emoji.package.png ^
  /resource:res\emoji\party.png,micopy.emoji.party.png ^
  /resource:res\emoji\question.png,micopy.emoji.question.png ^
  /resource:res\emoji\rocket.png,micopy.emoji.rocket.png ^
  /resource:res\emoji\search.png,micopy.emoji.search.png ^
  /resource:res\emoji\skip.png,micopy.emoji.skip.png ^
  /resource:res\emoji\sparkles.png,micopy.emoji.sparkles.png ^
  /resource:res\emoji\stop.png,micopy.emoji.stop.png ^
  /resource:res\emoji\trash.png,micopy.emoji.trash.png ^
  /resource:res\emoji\warning.png,micopy.emoji.warning.png ^
  src\*.cs

set ERR=%ERRORLEVEL%
if not "%ERR%"=="0" (
  echo.
  echo ビルドに失敗しました。
) else (
  echo.
  echo ビルド成功: bin\micopy.exe
)
if /i not "%~1"=="nopause" pause
exit /b %ERR%
