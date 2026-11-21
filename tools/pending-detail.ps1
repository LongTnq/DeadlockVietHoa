$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$pending = @($doc.entries | Where-Object { $_.status -eq 'Pending' -and $_.category -ne 'Voice' })

$withText    = @($pending | Where-Object { -not [string]::IsNullOrWhiteSpace($_.originalText) })
$withoutText = @($pending | Where-Object { [string]::IsNullOrWhiteSpace($_.originalText) })

Write-Host ("Pending total (non-Voice): {0}" -f $pending.Count)
Write-Host ("  co noi dich duoc : {0}" -f $withText.Count)
Write-Host ("  rong (bo qua)    : {0}" -f $withoutText.Count)
Write-Host ""
Write-Host "Theo nhom (co noi dich duoc):"
$withText | Group-Object category | ForEach-Object { Write-Host ("  {0,-12} {1}" -f $_.Name, $_.Count) }
Write-Host ""
Write-Host "Theo file:"
$withText | Group-Object sourceFile | ForEach-Object { Write-Host ("  {0,-52} {1}" -f $_.Name, $_.Count) }
Write-Host ""
Write-Host "Mau 10:"
$withText | Select-Object -First 10 | ForEach-Object {
    Write-Host ("  [{0}] {1} len={2}" -f $_.category, $_.key, $_.originalText.Length)
}
