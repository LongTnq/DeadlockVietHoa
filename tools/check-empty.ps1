$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$keys = @('ability_ice_dome_desc','hero_gigawatt_lore','hero_astro_lore','Citadel_PatchNotes_HeroLabs_hero_wrecker_2')
foreach ($k in $keys) {
    $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
    Write-Host ("{0,-45} status={1,-10} viLen={2}" -f $k, $e.status, ($e.translatedText | Out-String).Trim().Length)
}

Write-Host ""
Write-Host "=== Dem entries Translated nhung VI rong ==="
$bad = @($doc.entries | Where-Object {
    $_.status -eq 'Translated' -or $_.status -eq 'Reviewed' -or $_.status -eq 'Approved'
} | Where-Object { [string]::IsNullOrWhiteSpace($_.translatedText) })
Write-Host ("So entry rong: {0}" -f $bad.Count)
$bad | Select-Object -First 10 | ForEach-Object { Write-Host ("  [{0}] {1} ({2})" -f $_.category, $_.key, $_.sourceFile) }
