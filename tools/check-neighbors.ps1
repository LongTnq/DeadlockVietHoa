$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$groups = @(
  @('TimeWallWidth_label','TimeWallWidth_postvalue_label','TimeWallWidth_postfix',
    'TimeWallHeight_label','TimeWallHeight_postvalue_label','TimeWallHeight_postfix'),
  @('BeamWidth_label','BeamWidth_postvalue_label','BeamWidth_postfix'),
  @('ChargeSpeed_label','ChargeSpeed_postvalue_label','ChargeSpeed_postfix'),
  @('BonusBullets_label','BonusBullets_postvalue_label','BonusBullets_postfix'),
  @('citadel_settings_playsoundinbackground_label','citadel_settings_playsoundinbackground_desc',
    'citadel_settings_showsubtitles','citadel_settings_showsubtitles_desc'),
  @('Citadel_Dynamo_HeroTag_1','Citadel_Dynamo_HeroTag_2','Citadel_Dynamo_HeroTag_3',
    'Citadel_Priest_HeroTag_1','Citadel_Priest_HeroTag_2','Citadel_Priest_HeroTag_3')
)

foreach ($g in $groups) {
    Write-Host "----------------------------------------"
    foreach ($k in $g) {
        $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
        if ($null -eq $e) { Write-Host ("  {0,-52} KHONG THAY" -f $k); continue }
        Write-Host ("  {0,-52} [{1,-9}] EN=<{2}>  VI=<{3}>" -f $k, $e.status, $e.originalText, $e.translatedText)
    }
    Write-Host ""
}
