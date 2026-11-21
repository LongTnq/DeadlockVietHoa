$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$cats = @('Abilities','Ui')
$pending = @($doc.entries | Where-Object {
    $_.status -eq 'Pending' -and $cats -contains $_.category -and $_.originalText
})

# 10 chuỗi ngắn đầu tiên trong citadel_main
$sample = @($pending | Where-Object { $_.sourceFile -like 'citadel_main*' -and $_.originalText.Length -le 120 } | Select-Object -First 10)

foreach ($e in $sample) {
    Write-Host ("[{0}] {1}" -f $e.key, $e.originalText)
}
