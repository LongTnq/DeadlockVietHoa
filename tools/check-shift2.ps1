$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

function Normalize-Plural([string]$s) {
    $t = $s.Trim().ToLowerInvariant()
    if ($t.EndsWith('ies')) { return $t.Substring(0, $t.Length - 3) + 'y' }
    if ($t.EndsWith('es'))  { return $t.Substring(0, $t.Length - 2) }
    if ($t.EndsWith('s'))   { return $t.Substring(0, $t.Length - 1) }
    return $t
}

$byFile = @{}
foreach ($e in $doc.entries) {
    if ($e.status -eq 'Pending' -or [string]::IsNullOrWhiteSpace($e.translatedText)) { continue }
    $f = $e.sourceFile
    if (-not $byFile.ContainsKey($f)) { $byFile[$f] = @{} }
    $o = $e.originalText.Trim()
    if (-not $byFile[$f].ContainsKey($o)) { $byFile[$f][$o] = @() }
    $byFile[$f][$o] += $e
}

$flagged = @()
foreach ($e in $doc.entries) {
    if ($e.status -eq 'Pending' -or [string]::IsNullOrWhiteSpace($e.translatedText)) { continue }
    $f = $e.sourceFile
    if (-not $byFile.ContainsKey($f)) { continue }
    $t = $e.translatedText.Trim()
    if (-not $byFile[$f].ContainsKey($t)) { continue }
    foreach ($owner in $byFile[$f][$t]) {
        if ($owner.key -eq $e.key) { continue }
        if ($owner.originalText.Trim() -eq $e.originalText.Trim()) { continue }
        if ($owner.translatedText.Trim() -eq $t) { continue }
        # BO SUNG: bo qua truong hop so/it hoac khac biet nho (Word vs Words)
        if ((Normalize-Plural $owner.originalText) -eq (Normalize-Plural $e.originalText)) { continue }
        $flagged += [pscustomobject]@{
            Key = $e.key; Cat = $e.category; EN = $e.originalText; VI = $e.translatedText
            OtherKey = $owner.key; OtherEN = $owner.originalText; OtherVI = $owner.translatedText
        }
        break
    }
}

Write-Host ("Flagged voi luat bo so/it: {0}" -f $flagged.Count)
Write-Host ""
foreach ($x in $flagged) {
    Write-Host ("[{0}] {1}" -f $x.Cat, $x.Key)
    Write-Host ("   EN=<{0}>  VI=<{1}>" -f $x.EN, $x.VI)
    Write-Host ("   KHAC: {0} EN=<{1}> VI=<{2}>" -f $x.OtherKey, $x.OtherEN, $x.OtherVI)
    Write-Host ""
}
