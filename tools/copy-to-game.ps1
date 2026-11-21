$ErrorActionPreference = 'Stop'
$stg  = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\staging\mod\resource\localization'
$game = 'E:\Steam\steamapps\common\Deadlock\game\citadel\resource\localization'

$files = Get-ChildItem $stg -Recurse -Filter '*_vietnamese.txt'
Write-Host ("Copy {0} file sang game..." -f $files.Count)

foreach ($f in $files) {
    $rel = $f.FullName.Substring($stg.Length + 1)
    $dst = Join-Path $game $rel

    $dstDir = Split-Path $dst -Parent
    if (-not (Test-Path $dstDir)) {
        New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
    }

    # Backup file cu neu ton tai
    if (Test-Path $dst) {
        $bak = "$dst.bak"
        if (-not (Test-Path $bak)) {
            Copy-Item $dst $bak -Force
        }
    }

    Copy-Item $f.FullName $dst -Force
    Write-Host ("  OK  {0}" -f $rel)
}

Write-Host ""
Write-Host "=== Kiem tra ket qua ==="
foreach ($f in $files) {
    $rel = $f.FullName.Substring($stg.Length + 1)
    $dst = Join-Path $game $rel
    $b = [System.IO.File]::ReadAllBytes($dst)
    $t = [System.Text.Encoding]::UTF8.GetString($b)
    $n = ([regex]::Matches($t, '[\u00C0-\u1EF9]')).Count
    Write-Host ("  {0,-52} {1,10:N0} bytes  {2,6} ky tu co dau" -f $rel, $b.Length, $n)
}
