$ErrorActionPreference = 'Stop'
$root = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

# 1. Kiem tra DB co tieng Viet that khong
$dbPath = Join-Path $root 'workspace\translations\vietnamese.json'
$bytes = [System.IO.File]::ReadAllBytes($dbPath)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)

Write-Host "=== DB ==="
Write-Host ("Size: {0:N0} bytes" -f $bytes.Length)
Write-Host ("BOM : {0}" -f ($bytes[0] -eq 0xEF))

$vi = ([regex]::Matches($text, '[\u00C0-\u1EF9]')).Count
Write-Host ("So ky tu co dau: {0:N0}" -f $vi)

# Lay mau viet co dau
$m = [regex]::Matches($text, '"translatedText"\s*:\s*"([^"]*[\u00C0-\u1EF9][^"]*)"')
Write-Host ("So ban dich co dau: {0:N0}" -f $m.Count)
Write-Host "--- 5 mau ---"
for ($i = 0; $i -lt [Math]::Min(5, $m.Count); $i++) {
    Write-Host ("  {0}" -f $m[$i].Groups[1].Value)
}

Write-Host ""
Write-Host "=== EXPORT (staging) ==="
$stg = Join-Path $root 'workspace\staging\mod\resource\localization'
Get-ChildItem $stg -Recurse -Filter '*vietnamese.txt' | ForEach-Object {
    $b = [System.IO.File]::ReadAllBytes($_.FullName)
    $t = [System.Text.Encoding]::UTF8.GetString($b)
    $n = ([regex]::Matches($t, '[\u00C0-\u1EF9]')).Count
    Write-Host ("  {0,-58} {1,10:N0} bytes  {2,6} ky tu co dau  {3}" -f $_.Name, $b.Length, $n, $_.LastWriteTime.ToString('MM-dd HH:mm'))
}
