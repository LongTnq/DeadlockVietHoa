$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

# Tái hiện logic của RevalidateCommand: bản dịch khớp gốc của entry KHÁC
# trong cùng file, mà gốc của entry này khác -> nghi ngờ lệch.
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
        $flagged += [pscustomobject]@{
            Key = $e.key
            Cat = $e.category
            File = $e.sourceFile
            EN = $e.originalText
            VI = $e.translatedText
            OtherKey = $owner.key
            OtherEN = $owner.originalText
        }
        break
    }
}

Write-Host ("Flagged: {0}" -f $flagged.Count)
Write-Host ""
foreach ($f in $flagged) {
    Write-Host ("[{0}] {1}" -f $f.Cat, $f.Key)
    Write-Host ("   EN : <{0}>" -f $f.EN)
    Write-Host ("   VI : <{0}>" -f $f.VI)
    Write-Host ("   KHAC: {0} -> <{1}>" -f $f.OtherKey, $f.OtherEN)
    Write-Host ""
}
