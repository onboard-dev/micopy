# GitHub に新しいリリース(タグ+micopy.exe)を登録する(release.bat から呼ばれます)
. (Join-Path $PSScriptRoot 'common.ps1')
Require-Command 'git' "Git for Windows をインストールしてください。`n(例: winget install Git.Git)"
Require-Command 'gh'  "GitHub CLI をインストールしてください。`n(例: winget install GitHub.cli)`n`nインストール後、コマンドプロンプトで一度だけ`n  gh auth login`nを実行して GitHub にログインしてください。"
Set-Location $Root

try {
    & gh auth status > $null 2>&1
    if ($LASTEXITCODE -ne 0) {
        Show-Warn "GitHub CLI にログインしていません。`n`nコマンドプロンプトで`n  gh auth login`nを実行してから、もう一度お試しください。"
        exit 1
    }
    if (-not (Test-Path (Join-Path $Root '.git'))) {
        Show-Warn 'まだ GitHub に登録されていません。先に commit.bat を実行してください。'
        exit 1
    }

    $version = Get-AppVersion
    $tag = "v$version"
    $branch = Get-CurrentBranch

    # ---------- 同じタグがすでにないか ----------
    & gh release view $tag --repo $RepoSlug > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        Show-Warn "リリース $tag はすでにあります。`n`n先に version.bat でバージョン番号を上げてください。"
        exit 1
    }
    $remoteTag = & git.exe ls-remote --tags origin "refs/tags/$tag"
    if ($remoteTag) {
        Show-Warn "タグ $tag はすでに GitHub にあります。`n`n先に version.bat でバージョン番号を上げてください。"
        exit 1
    }

    if (-not (Ask-YesNo "micopy $tag をリリースします。`n`n・ビルドする`n・コミットしていない変更があればコミット&プッシュする`n・GitHub にタグ $tag とリリースを作り、micopy.exe を登録する`n`nよろしいですか?")) {
        Write-Host 'キャンセルしました。'; exit 1
    }

    # ---------- ビルド ----------
    Write-Host 'ビルドしています…'
    & cmd /c "`"$Root\build.bat`" nopause"
    if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました。' }
    $exe = Join-Path $Root 'bin\micopy.exe'
    $fileVer = (Get-Item $exe).VersionInfo.FileVersion
    if ($fileVer -ne "$version.0") {
        throw "micopy.exe のバージョン ($fileVer) が VERSION ($version) と一致しません。"
    }

    # ---------- 未コミット・未プッシュの変更 ----------
    $dirty = @(& git.exe status --porcelain)
    & git.exe fetch -q origin
    $ahead = 0
    & git.exe rev-parse --verify -q "origin/$branch" > $null
    if ($LASTEXITCODE -eq 0) { $ahead = [int](& git.exe rev-list --count "origin/$branch..HEAD") } else { $ahead = 1 }
    if ($dirty.Count -gt 0 -or $ahead -gt 0) {
        Write-Host 'コミット&プッシュします…'
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'commit.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'コミット&プッシュが完了しなかったため、リリースを中止しました。' }
    }

    # ---------- リリースノート ----------
    $notes = Ask-Text "リリースの説明を入力してください。(1行)`n`n空欄のまま OK を押すと「micopy $tag」になります。`n変更点の一覧は自動で追加されます。" "micopy $tag リリース" ''
    if ($null -eq $notes) { $notes = "micopy $tag" }
    $notesFile = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($notesFile, $notes, $Utf8NoBom)

    # ---------- リリース作成(タグも同時に作られます) ----------
    Write-Host "GitHub にリリース $tag を作成しています…"
    try {
        & gh release create $tag "$exe#micopy.exe ($tag)" --repo $RepoSlug --target $branch --title "micopy $tag" --notes-file $notesFile --generate-notes
        if ($LASTEXITCODE -ne 0) { throw 'リリースの作成に失敗しました。' }
    }
    finally { Remove-Item $notesFile -ErrorAction SilentlyContinue }

    & git.exe fetch -q --tags origin
    $url = "https://github.com/$RepoSlug/releases/tag/$tag"
    Write-Host ''
    Write-Host "完了しました: $url"
    if (Ask-YesNo "リリース $tag を作成しました。`n`nブラウザで開きますか?") { Start-Process $url }
    exit 0
}
catch {
    Write-Host ''
    Write-Host "エラー: $($_.Exception.Message)" -ForegroundColor Red
    Show-Warn "リリースに失敗しました。`n`n$($_.Exception.Message)"
    exit 1
}
