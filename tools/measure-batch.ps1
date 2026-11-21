$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$cats = @('Abilities','Ui')
$pending = @($doc.entries | Where-Object {
    $_.status -eq 'Pending' -and $cats -contains $_.category -and $_.originalText
})

# Lấy 25 entry đầu tiên của nhóm citadel_heroes
$group = @($pending | Where-Object { $_.sourceFile -like 'citadel_heroes*' } | Select-Object -First 25)

$texts = @($group | ForEach-Object { $_.originalText })
$totalChars = ($texts | Measure-Object -Property Length -Sum).Sum
Write-Host ("25 entry, total {0} chars, max {1}" -f $totalChars, ($texts | Measure-Object -Property Length -Maximum).Maximum)

$json = ($texts | ForEach-Object { @{ n = $_ } } | ConvertTo-Json -Depth 4 -Compress)
Write-Host "payload length: $($json.Length)"
