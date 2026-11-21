$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$raw = Get-Content $path -Raw -Encoding UTF8
$doc = $raw | ConvertFrom-Json

$keys = @(
  'citadel_settings_playsoundinbackground_desc',
  'citadel_settings_camera_search'
)

foreach ($k in $keys) {
    $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
    if ($null -eq $e) { Write-Host "KHONG THAY: $k"; continue }
    Write-Host ("RESET {0}: [{1}] VI=<{2}>" -f $k, $e.status, $e.translatedText)
    $e.status = 'Pending'
    $e.translatedText = ''
}

$json = $doc | ConvertTo-Json -Depth 20 -Compress
[System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Host "Da luu."
