$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$cats = @('Abilities','Ui')
$pending = @($doc.entries | Where-Object {
    $_.status -eq 'Pending' -and $cats -contains $_.category -and $_.originalText
})

$totalChars = ($pending | ForEach-Object { $_.originalText.Length } | Measure-Object -Sum).Sum
Write-Host ("Pending entries: {0}" -f $pending.Count)
Write-Host ("Total source chars: {0:N0}" -f $totalChars)

$long = @($pending | Where-Object { $_.originalText.Length -gt 600 })
$short = @($pending | Where-Object { $_.originalText.Length -le 600 })
$shortChars = ($short | ForEach-Object { $_.originalText.Length } | Measure-Object -Sum).Sum

Write-Host ("  short (<=600): {0} entries, {1:N0} chars" -f $short.Count, $shortChars)
Write-Host ("  long  (>600): {0} entries, {1:N0} chars" -f $long.Count, ($long | ForEach-Object { $_.originalText.Length } | Measure-Object -Sum).Sum)
