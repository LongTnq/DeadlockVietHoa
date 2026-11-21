$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

foreach ($k in @('guide_the_map_walkers_header','walker_unit')) {
    $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
    if ($null -eq $e) { Write-Host "KHONG THAY: $k"; continue }
    Write-Host ("{0}  [{1}]" -f $k, $e.status)
    Write-Host ("   EN=<{0}>" -f $e.originalText)
    Write-Host ("   VI=<{0}>" -f $e.translatedText)
    Write-Host ""
}
