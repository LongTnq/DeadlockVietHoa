$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$keys = @(
  'DuringAmbush_conditional',
  'ability_werewolf_passive_desc',
  'hero_magician_playstyle'
)

foreach ($k in $keys) {
    $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
    if ($null -eq $e) { Write-Host "KHONG THAY: $k"; continue }
    Write-Host ("=== {0} [{1}]" -f $k, $e.status)
    Write-Host ("EN: {0}" -f $e.originalText)
    Write-Host ("VI: {0}" -f $e.translatedText)
    Write-Host ""
}

$pending = @($doc.entries | Where-Object { $_.status -eq 'Pending' -and $_.category -ne 'Voice' })
Write-Host ("Pending (khong Voice): {0}" -f $pending.Count)
$pending | Group-Object category | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Name, $_.Count) }
