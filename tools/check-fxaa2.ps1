$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$groups = @(
  @('citadel_settings_fxaa','citadel_settings_antialiasing'),
  @('guide_the_map_walkers_header','Citadel_Hud_KillFeedWalker')
)

foreach ($g in $groups) {
    Write-Host "----------------------------------------"
    foreach ($k in $g) {
        $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
        if ($null -eq $e) { Write-Host ("  {0,-50} KHONG THAY" -f $k); continue }
        Write-Host ("  {0,-50} [{1,-9}]" -f $k, $e.status)
        Write-Host ("      EN=<{0}>" -f $e.originalText)
        Write-Host ("      VI=<{0}>" -f $e.translatedText)
    }
    Write-Host ""
}
