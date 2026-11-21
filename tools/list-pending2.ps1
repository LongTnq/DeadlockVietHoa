$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$pending = @($doc.entries | Where-Object {
    $_.status -eq 'Pending' -and $_.category -ne 'Voice' -and -not [string]::IsNullOrWhiteSpace($_.originalText)
})

foreach ($e in ($pending | Sort-Object { $_.originalText.Length })) {
    Write-Host ("[{0}] {1} (len={2})" -f $e.category, $e.key, $e.originalText.Length)
    Write-Host ("   {0}" -f $e.originalText)
}
