# コミット前の個人情報チェック(commit.ps1 から読み込みます)
#
# ステージされた変更(追加・変更された行)とファイル名を調べ、
# 個人情報や秘密情報らしきものが見つかったら一覧を表示して確認します。
#
#   tools\privacy-words.txt … 自分の氏名・学校名・住所など、絶対に公開したくない言葉(1行に1つ)
#                              ※ .gitignore 済みなので GitHub には上がりません
#   tools\privacy-allow.txt … 問題ないと分かっている文字列(1行に1つ)。一致したものは報告しません

function Get-WordList([string]$path) {
    if (-not (Test-Path $path)) { return @() }
    return @([System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8) |
        ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
}

function Initialize-PrivacyWords([string]$path) {
    if (Test-Path $path) { return }
    $text = @"
# 公開したくない言葉を 1 行に 1 つずつ書いてください(# で始まる行は無視されます)
# 例:
#   山田 太郎
#   ○○県○○市○○町
# このファイルは .gitignore に入っているので GitHub には上がりません。
"@
    [System.IO.File]::WriteAllText($path, $text.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object System.Text.UTF8Encoding $true))
}

function Test-PrivacyBeforeCommit {
    $wordsFile = Join-Path $Root 'tools\privacy-words.txt'
    $allowFile = Join-Path $Root 'tools\privacy-allow.txt'
    Initialize-PrivacyWords $wordsFile
    $allow = Get-WordList $allowFile

    # ---- 検査ルール(名前, 正規表現) ----
    $rules = New-Object System.Collections.ArrayList
    [void]$rules.Add(@('メールアドレス', '[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}'))
    [void]$rules.Add(@('電話番号', '(?<![\d\-])0\d{1,4}-\d{1,4}-\d{4}(?![\d\-])'))
    [void]$rules.Add(@('携帯電話番号', '(?<!\d)0[789]0\d{8}(?!\d)'))
    [void]$rules.Add(@('郵便番号', '〒\s*\d{3}-?\d{4}'))
    [void]$rules.Add(@('ユーザーフォルダのパス', '(?i)[A-Z]:[\\/]+Users[\\/]+(?!Public\b|Default\b|<|%|\$|USERNAME\b|ユーザー名|x\\)[^\\/\s"''<>|]+'))
    [void]$rules.Add(@('GitHub トークン', '(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}'))
    [void]$rules.Add(@('AWS キー', 'AKIA[0-9A-Z]{16}'))
    [void]$rules.Add(@('秘密鍵', '-----BEGIN [A-Z ]*PRIVATE KEY-----'))
    [void]$rules.Add(@('パスワードらしき記述', '(?i)(password|passwd|pwd|パスワード)\s*[:=]\s*\S{4,}'))
    [void]$rules.Add(@('ローカルIPアドレス', '(?<![\d.])192\.168\.\d{1,3}\.\d{1,3}(?![\d.])'))

    # このPCのユーザー名・コンピューター名、privacy-words.txt の言葉はそのまま検索
    $literals = New-Object System.Collections.ArrayList
    foreach ($w in @($env:USERNAME, $env:COMPUTERNAME)) {
        if ($w -and $w.Length -ge 3) { [void]$literals.Add(@("このPCの名前($w)", $w)) }
    }
    foreach ($w in (Get-WordList $wordsFile)) { [void]$literals.Add(@('公開したくない言葉', $w)) }

    $findings = New-Object System.Collections.ArrayList
    function Add-Finding($file, $line, $kind, $text) {
        foreach ($a in $allow) { if ($text -like "*$a*") { return } }
        if ($kind -eq 'メールアドレス' -and $text -match '(?i)(@users\.noreply\.github\.com|@example\.(com|jp|org|net))$') { return }
        $loc = if ($line -gt 0) { "${file}:$line" } else { $file }
        [void]$findings.Add("[$kind] $loc  →  $text")
    }
    function Check-Text($file, $line, $text) {
        foreach ($r in $rules) {
            foreach ($m in [regex]::Matches($text, $r[1])) { Add-Finding $file $line $r[0] $m.Value }
        }
        foreach ($l in $literals) {
            if ($text.IndexOf($l[1], [StringComparison]::OrdinalIgnoreCase) -ge 0) { Add-Finding $file $line $l[0] $l[1] }
        }
    }

    # ---- ファイル名・ファイルの種類 ----
    $images = New-Object System.Collections.ArrayList
    foreach ($row in (& git.exe diff --cached --name-status --no-renames)) {
        $parts = $row -split "`t"
        if ($parts.Count -lt 2) { continue }
        $status = $parts[0]; $path = $parts[1]
        if ($status -eq 'D') { continue }
        Check-Text $path 0 $path
        $name = [System.IO.Path]::GetFileName($path).ToLowerInvariant()
        if ($name -match '\.(pfx|p12|pem|key|kdbx|ppk)$' -or $name -eq '.env' -or $name -match '^id_(rsa|ed25519|ecdsa)') {
            Add-Finding $path 0 '秘密情報のファイル' $name
        }
        if ($name -eq 'settings.xml' -or $name -match '^micopy_設定_.*\.xml$' -or $name -match '\.log$') {
            Add-Finding $path 0 '個人の設定・ログのファイル' $name
        }
        if ($name -match '\.(png|jpe?g|gif|bmp|webp|tiff?|heic)$') {
            [void]$images.Add($path)   # 中身の検査対象外にする
        }
    }

    # ---- 追加・変更された行 ----
    $file = $null; $lineNo = 0
    foreach ($l in (& git.exe -c core.quotepath=false diff --cached -U0 --no-color --no-ext-diff)) {
        if ($l.StartsWith('+++ ')) {
            $file = if ($l -eq '+++ /dev/null') { $null } else { $l.Substring(4) -replace '^b/', '' }
            continue
        }
        if ($l -match '^@@ -\d+(?:,\d+)? \+(\d+)') { $lineNo = [int]$Matches[1]; continue }
        if ($file -and $l.StartsWith('+')) {
            if ($file -eq 'tools/privacy-allow.txt') { $lineNo++; continue }
            # バイナリ(画像など)の中身は検査しない
            if ($images -notcontains $file -and $file -notmatch '\.(ico|exe|dll|zip|pdf)$') {
                Check-Text $file $lineNo $l.Substring(1)
            }
            $lineNo++
        }
    }

    # ---- 結果 ----
    $ok = $true
    if ($findings.Count -gt 0) {
        $unique = @($findings | Select-Object -Unique)
        Write-Host ''
        Write-Host "個人情報・秘密情報の可能性があるものが $($unique.Count) 件見つかりました:" -ForegroundColor Yellow
        $unique | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        $show = ($unique | Select-Object -First 15) -join "`n"
        if ($unique.Count -gt 15) { $show += "`n…ほか $($unique.Count - 15) 件(コンソール画面を参照)" }
        $msg = "⚠ 個人情報・秘密情報の可能性があるものが見つかりました。`n`n$show`n`n" +
               "問題ないものは tools\privacy-allow.txt に書くと、次回から報告されません。`n`n" +
               "このままコミットしますか?(通常は「いいえ」を選んで修正してください)"
        $r = [System.Windows.Forms.MessageBox]::Show($msg, 'micopy 個人情報チェック', 'YesNo', 'Warning', 'Button2')
        if ($r -ne 'Yes') { $ok = $false }
    }
    # アプリに埋め込むアイコン・絵文字(res\)以外の画像は、写り込みを確認してもらう
    $photos = @($images | Where-Object { $_ -notmatch '^res/' })
    if ($ok -and $photos.Count -gt 0) {
        $list = ($photos | Select-Object -First 10) -join "`n"
        $msg = "画像ファイルが追加・変更されています。`n`n$list`n`n" +
               "画像の中身は自動で検査できません。`n名前・パス・メールアドレスなどが写っていないか確認しましたか?"
        $r = [System.Windows.Forms.MessageBox]::Show($msg, 'micopy 個人情報チェック', 'YesNo', 'Question', 'Button2')
        if ($r -ne 'Yes') { $ok = $false }
    }
    if ($ok) {
        $email = (& git.exe config user.email)
        if ($email -and $email -notmatch '(?i)@users\.noreply\.github\.com$') {
            $flag = Join-Path $Root '.git\micopy-email-confirmed'
            $confirmed = (Test-Path $flag) -and (([System.IO.File]::ReadAllText($flag)).Trim() -eq $email)
            if (-not $confirmed) {
                $msg = "コミットにはメールアドレス「$email」が記録され、GitHub 上で誰でも見られます。`n`n" +
                       "公開したくない場合は「いいえ」を選び、GitHub の noreply アドレス`n(ID+ユーザー名@users.noreply.github.com)に変更してください:`n" +
                       "  git config user.email ○○@users.noreply.github.com`n`nこのアドレスのままコミットしますか?"
                $r = [System.Windows.Forms.MessageBox]::Show($msg, 'micopy 個人情報チェック', 'YesNo', 'Question', 'Button2')
                if ($r -eq 'Yes') { [System.IO.File]::WriteAllText($flag, $email) } else { $ok = $false }
            }
        }
    }
    if ($ok) { Write-Host '個人情報チェック: OK' -ForegroundColor Green }
    return $ok
}
