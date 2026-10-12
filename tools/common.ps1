# micopy 開発用スクリプトの共通部分(各 .ps1 から読み込みます)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName Microsoft.VisualBasic
Add-Type -AssemblyName System.Windows.Forms

$Root      = Split-Path -Parent $PSScriptRoot
$RepoOwner = 'onboard-dev'
$RepoName  = 'micopy'
$RepoSlug  = "$RepoOwner/$RepoName"
$RepoUrl   = "https://github.com/$RepoSlug.git"
$Utf8NoBom = New-Object System.Text.UTF8Encoding $false
$Utf8Bom   = New-Object System.Text.UTF8Encoding $true

function Ask-Text([string]$message, [string]$title, [string]$default) {
    # キャンセル時は $null
    $r = [Microsoft.VisualBasic.Interaction]::InputBox($message, $title, $default)
    if ([string]::IsNullOrWhiteSpace($r)) { return $null }
    return $r.Trim()
}

function Show-Info([string]$message) {
    [void][System.Windows.Forms.MessageBox]::Show($message, 'micopy', 'OK', 'Information')
}
function Show-Warn([string]$message) {
    [void][System.Windows.Forms.MessageBox]::Show($message, 'micopy', 'OK', 'Warning')
}
function Ask-YesNo([string]$message) {
    return [System.Windows.Forms.MessageBox]::Show($message, 'micopy', 'YesNo', 'Question') -eq 'Yes'
}

function Get-AppVersion {
    $f = Join-Path $Root 'VERSION'
    if (Test-Path $f) { return ([System.IO.File]::ReadAllText($f)).Trim() }
    return '1.0.0'
}

function Require-Command([string]$name, [string]$installHint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Show-Warn "$name が見つかりません。`n`n$installHint"
        exit 1
    }
}

# git を実行し、失敗したら例外にする
function Run-Git {
    & git.exe @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') が失敗しました (終了コード $LASTEXITCODE)" }
}

function Get-CurrentBranch {
    $b = (& git symbolic-ref --short HEAD 2>$null)
    if ([string]::IsNullOrWhiteSpace($b)) { return 'main' }
    return $b.Trim()
}
