$ErrorActionPreference = 'Stop'

$root = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
$vpkSrc = Join-Path $root 'workspace\staging\vpk'
$addons = 'E:\Steam\steamapps\common\Deadlock\game\citadel\addons'

Write-Host '=== 1. Copy VPK vao addons ==='
Get-ChildItem $vpkSrc -Filter 'deadlockvn*' | ForEach-Object {
    $dst = Join-Path $addons $_.Name
    Copy-Item $_.FullName $dst -Force
    Write-Host ("  {0,-24} {1,12:N0} bytes" -f $_.Name, $_.Length)
}

Write-Host ''
Write-Host '=== 2. Xoa file loose da bi Steam ghi de ==='
$loose = Join-Path 'E:\Steam\steamapps\common\Deadlock\game\citadel\resource\localization' ''
Get-ChildItem $loose -Recurse -Filter '*_vietnamese.txt' -ErrorAction SilentlyContinue | ForEach-Object {
    # File 60 bytes = file rong cua game (khong co ban dich).
    # Giu lai ban da dua vao game neu co noi dung.
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $hasContent = $bytes.Length -gt 100

    if (-not $hasContent) {
        Write-Host ("  bo: {0}" -f $_.Name)
    }
}

Write-Host ''
Write-Host '=== 3. Kiem tra VPK trong game ==='
Get-ChildItem $addons -Filter 'deadlockvn*' | ForEach-Object {
    Write-Host ("  {0,-24} {1,12:N0} bytes  {2}" -f $_.Name, $_.Length, $_.LastWriteTime.ToString('MM-dd HH:mm'))
}

Write-Host ''
Write-Host '=== 4. Kiem tra noi dung VPK ==='
$vpkCli = 'C:\Program Files\VPKEdit\vpkeditcli.exe'
if (Test-Path $vpkCli) {
    $tree = & $vpkCli --file-tree (Join-Path $addons 'deadlockvn_dir.vpk') 2>&1
    $tree | Select-String 'vietnamese.txt' | ForEach-Object {
        $line = $_.ToString() -replace "`e\[[0-9;]*m", ''
        Write-Host ("  " + $line.Trim())
    }
}
