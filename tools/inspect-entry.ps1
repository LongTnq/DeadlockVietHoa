$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$keys = @(
  'Citadel_Dynamo_HeroTag_1',
  'Citadel_Priest_HeroTag_3',
  'DamagePerSecondFlown_postvalue_label',
  'citadel_ability_wrecker_garbage_suck'
)
foreach ($k in $keys) {
    $e = $doc.entries | Where-Object { $_.key -eq $k } | Select-Object -First 1
    if ($e) {
        Write-Host ("[{0}] {1}" -f $e.category, $k)
        Write-Host ("   EN: <{0}>" -f $e.originalText)
        Write-Host ("   VI: <{0}>" -f $e.translatedText)
        Write-Host ("   status: {0}" -f $e.status)
    } else {
        Write-Host ("KHONG TIM THAY: {0}" -f $k)
    }
}
