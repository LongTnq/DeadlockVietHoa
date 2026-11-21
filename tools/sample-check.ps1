$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$keys = @(
  'ability_ice_dome_desc',
  'hero_gigawatt_lore',
  'hero_astro_lore',
  'Citadel_PatchNotes_HeroLabs_hero_wrecker_2'
)

foreach ($k in $keys) {
    $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
    if ($null -eq $e) { Write-Host "KHONG THAY: $k"; continue }
    Write-Host ("=== [{0}] {1}  ({2} -> {3} ký tự)" -f $e.category, $e.key, $e.originalText.Length, $e.translatedText.Length)
    $en = $e.originalText
    $vi = $e.translatedText
    Write-Host ("EN: " + $en.Substring(0, [Math]::Min(220, $en.Length)))
    Write-Host ("VI: " + $vi.Substring(0, [Math]::Min(220, $vi.Length)))
    Write-Host ""
}
