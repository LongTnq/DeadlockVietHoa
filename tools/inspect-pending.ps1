$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$pending = @($doc.entries | Where-Object { $_.status -eq 'Pending' })
Write-Host "Pending total: $($pending.Count)"

$long = @($pending | Where-Object { $_.originalText -and $_.originalText.Length -gt 400 } | Sort-Object { $_.originalText.Length } -Descending)
Write-Host "Pending with original > 400 chars: $($long.Count)"
Write-Host ""
foreach ($e in ($long | Select-Object -First 12)) {
    Write-Host ("[{0}] len={1} key={2}" -f $e.category, $e.originalText.Length, $e.key)
    Write-Host ("   " + $e.originalText.Substring(0, [Math]::Min(150, $e.originalText.Length)))
    Write-Host ""
}

$byCat = $pending | Group-Object category | Sort-Object Count -Descending
Write-Host "Pending by category:"
foreach ($g in $byCat) { Write-Host ("  {0,-12} {1}" -f $g.Name, $g.Count) }
