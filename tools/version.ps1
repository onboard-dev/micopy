# バージョン番号を設定する(version.bat から呼ばれます)
. (Join-Path $PSScriptRoot 'common.ps1')

$current = Get-AppVersion
while ($true) {
    $v = Ask-Text "新しいバージョン番号を入力してください。`n(例: 1.0.0)`n`n現在のバージョン: $current" 'micopy バージョン設定' $current
    if ($null -eq $v) { Write-Host 'キャンセルしました。'; exit 1 }
    $v = $v.TrimStart('v', 'V')
    $ok = $v -match '^(\d+)\.(\d+)\.(\d+)$'
    if ($ok) { foreach ($i in 1..3) { if ([int64]$Matches[$i] -gt 65534) { $ok = $false } } }
    if ($ok) { break }
    Show-Warn "「$v」は正しい形式ではありません。`n1.0.0 のように「数字.数字.数字」で入力してください。`n(各数字は 0~65534)"
}

# 先頭の 0 を取り除いて正規化(01.2.03 → 1.2.3)
$v = (($v -split '\.') | ForEach-Object { [string][int64]$_ }) -join '.'

$cs = @"
// このファイルは version.bat が自動生成します。手で編集しないでください。
using System.Reflection;

[assembly: AssemblyVersion("$v.0")]
[assembly: AssemblyFileVersion("$v.0")]
[assembly: AssemblyInformationalVersion("$v")]

namespace Micopy
{
    internal static class AppVersion
    {
        public const string Text = "$v";
    }
}
"@
[System.IO.File]::WriteAllText((Join-Path $Root 'src\Version.cs'), $cs.Replace("`r`n", "`n").Replace("`n", "`r`n"), $Utf8Bom)
[System.IO.File]::WriteAllText((Join-Path $Root 'VERSION'), $v, $Utf8NoBom)
Write-Host "バージョンを $v に設定しました。(VERSION / src\Version.cs)"

if (Ask-YesNo "バージョンを $v に設定しました。`n`n続けてビルドしますか?") {
    & cmd /c "`"$Root\build.bat`" nopause"
    if ($LASTEXITCODE -ne 0) { Show-Warn 'ビルドに失敗しました。画面のメッセージを確認してください。'; exit 1 }
    Show-Info "ビルドしました。`nbin\micopy.exe(バージョン $v)"
}
exit 0
