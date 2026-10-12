# GitHub リポジトリへコミット&プッシュする(commit.bat から呼ばれます)
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'privacy.ps1')
Require-Command 'git' "Git for Windows をインストールしてください。`n(例: winget install Git.Git)"
Set-Location $Root

try {
    # ---------- 初回: リポジトリの準備 ----------
    if (-not (Test-Path (Join-Path $Root '.git'))) {
        Write-Host "初回のため、Git リポジトリを準備します: $RepoUrl"
        Run-Git init -q
        Run-Git remote add origin $RepoUrl
        Run-Git fetch origin

        # GitHub 側の既定ブランチ(main など)を調べる
        $remoteBranch = $null
        foreach ($line in (& git.exe ls-remote --symref origin HEAD)) {
            if ($line -match '^ref:\s+refs/heads/(\S+)\s+HEAD') { $remoteBranch = $Matches[1] }
        }
        if (-not $remoteBranch) { $remoteBranch = 'main' }
        Run-Git symbolic-ref HEAD "refs/heads/$remoteBranch"

        # GitHub 側にすでにコミット(LICENSE など)があれば、その上に積む
        & git.exe rev-parse --verify -q "origin/$remoteBranch" > $null
        if ($LASTEXITCODE -eq 0) {
            Run-Git reset -q "origin/$remoteBranch"
            # GitHub 側にだけあるファイルは手元に復元する(手元のファイルは上書きしない)
            foreach ($f in (& git.exe ls-files --deleted)) {
                if ($f) { Run-Git checkout -- "$f" }
            }
            Run-Git branch -q --set-upstream-to "origin/$remoteBranch"
        }
    }

    & git.exe config core.quotepath false   # 日本語ファイル名をそのまま表示
    & git.exe config core.safecrlf false    # 改行コード(LF→CRLF)変換の警告を表示しない

    # ---------- 名前とメールアドレス(コミットに記録されます) ----------
    $name = (& git.exe config user.name)
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = Ask-Text "コミットに記録する名前を入力してください。`n(GitHub のユーザー名など)" 'micopy コミット' ''
        if ($null -eq $name) { Write-Host 'キャンセルしました。'; exit 1 }
        Run-Git config user.name "$name"
    }
    $mail = (& git.exe config user.email)
    if ([string]::IsNullOrWhiteSpace($mail)) {
        $mail = Ask-Text "コミットに記録するメールアドレスを入力してください。`n(公開したくない場合は GitHub の noreply アドレス:`n  ID+ユーザー名@users.noreply.github.com)" 'micopy コミット' ''
        if ($null -eq $mail) { Write-Host 'キャンセルしました。'; exit 1 }
        Run-Git config user.email "$mail"
    }

    $branch = Get-CurrentBranch
    $version = Get-AppVersion

    # ---------- コミット ----------
    Run-Git add -A
    $changes = @(& git.exe diff --cached --name-status)
    if ($changes.Count -gt 0) {
        # 個人情報チェック(問題があればコミットしない)
        if (-not (Test-PrivacyBeforeCommit)) {
            Run-Git reset -q
            Write-Host 'コミットを中止しました。内容を修正してから、もう一度実行してください。'
            exit 1
        }
        Write-Host ''
        Write-Host '変更されたファイル:'
        $changes | ForEach-Object { Write-Host "  $_" }
        Write-Host ''
        $list = ($changes | Select-Object -First 15) -join "`n"
        if ($changes.Count -gt 15) { $list += "`n…ほか $($changes.Count - 15) 件" }
        $msg = Ask-Text "コミットメッセージを入力してください。`n`n$list" 'micopy コミット&プッシュ' "v$version 更新"
        if ($null -eq $msg) {
            Run-Git reset -q
            Write-Host 'キャンセルしました。'
            exit 1
        }
        $tmp = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($tmp, $msg, $Utf8NoBom)
        try { Run-Git commit -q -F $tmp } finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
        Write-Host "コミットしました: $msg"
    }
    else {
        Write-Host 'コミットする変更はありません。'
    }

    # ---------- プッシュ ----------
    & git.exe rev-parse --verify -q HEAD > $null
    if ($LASTEXITCODE -ne 0) { Show-Info 'まだコミットがありません。'; exit 0 }

    Write-Host "GitHub へプッシュしています ($branch)…"
    & git.exe push -u origin $branch
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'プッシュできなかったため、GitHub 側の変更を取り込んでから再試行します…'
        Run-Git pull --rebase origin $branch
        Run-Git push -u origin $branch
    }
    Write-Host ''
    Write-Host "完了しました: https://github.com/$RepoSlug"
    exit 0
}
catch {
    Write-Host ''
    Write-Host "エラー: $($_.Exception.Message)" -ForegroundColor Red
    Show-Warn "コミット&プッシュに失敗しました。`n`n$($_.Exception.Message)`n`n画面のメッセージを確認してください。"
    exit 1
}
