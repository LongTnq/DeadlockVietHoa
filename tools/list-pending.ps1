$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$pending = @($doc.entries | Where-Object { $_.status -eq 'Pending' -and $_.category -ne 'Voice' -and $_.originalText })
Write-Host ("Pending: {0}" -f $pending.Count)
Write-Host ""
$pending | Sort-Object { $_.originalText.Length } | Select-Object -First 30 | ForEach-Object {
    Write-Output ("[{0}] len={1} {2}" -f $_.category, $_.originalText.Length, $_.key)
    Write-Output ("    {0}" -f $_.originalText)
}
