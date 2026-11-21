$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$entries = $doc.entries
Write-Host "Total: $($entries.Count)"

function Test-Cjk([string]$s) {
    if (-not $s) { return $false }
    foreach ($ch in $s.ToCharArray()) {
        $c = [int]$ch
        if (($c -ge 0x2E80 -and $c -le 0x2FFF) -or ($c -ge 0x3000 -and $c -le 0x30FF) -or
            ($c -ge 0x3100 -and $c -le 0x31FF) -or ($c -ge 0x3200 -and $c -le 0x33FF) -or
            ($c -ge 0x3400 -and $c -le 0x4DBF) -or ($c -ge 0x4E00 -and $c -le 0x9FFF) -or
            ($c -ge 0xA000 -and $c -le 0xA4CF) -or ($c -ge 0xA960 -and $c -le 0xD7FF) -or
            ($c -ge 0xF900 -and $c -le 0xFAFF) -or ($c -ge 0xFF00 -and $c -le 0xFFEF)) { return $true }
    }
    return $false
}

$cjk = @()
$identical = @()
foreach ($e in $entries) {
    if ($e.status -eq 'Pending' -or -not $e.translatedText) { continue }
    if ((Test-Cjk $e.originalText) -eq $false -and (Test-Cjk $e.translatedText)) {
        $cjk += $e
    }
    if ($e.originalText.Trim() -ceq $e.translatedText.Trim()) {
        $identical += $e
    }
}
Write-Host "CJK leaked: $($cjk.Count)"
foreach ($e in $cjk) { Write-Host "  [$($e.category)] $($e.key) => $($e.translatedText)" }
Write-Host "Identical: $($identical.Count)"
foreach ($e in $identical) { Write-Host "  [$($e.category)] $($e.key) => $($e.translatedText)" }
