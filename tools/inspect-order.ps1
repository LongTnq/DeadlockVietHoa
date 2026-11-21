$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$cats = @('Abilities','Ui')
$pending = @($doc.entries | Where-Object {
    $_.status -eq 'Pending' -and $cats -contains $_.category -and $_.originalText
})

Write-Host "Pending (Abilities+Ui): $($pending.Count)"
Write-Host ""
Write-Host "First 15 pending in document order:"
$i = 0
foreach ($e in $pending) {
    if ($i -ge 15) { break }
    Write-Host ("[{0}] {1} len={2} key={3}" -f $i, $e.sourceFile, $e.originalText.Length, $e.key)
    $i++
}

Write-Host ""
Write-Host "Pending grouped by sourceFile (first-appearance order):"
$seen = @{}
$order = @()
foreach ($e in $pending) {
    if (-not $seen.ContainsKey($e.sourceFile)) {
        $seen[$e.sourceFile] = 0
        $order += $e.sourceFile
    }
    $seen[$e.sourceFile]++
}
$i = 0
foreach ($f in $order) {
    if ($i -ge 6) { break }
    Write-Host ("  {0,-50} {1}" -f $f, $seen[$f])
    $i++
}
