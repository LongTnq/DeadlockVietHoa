$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$out  = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\quality-report.txt'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$entries = @($doc.entries | Where-Object { $_.status -ne 'Pending' -and $_.translatedText })

# French-specific accented letters (code points) - not used in Vietnamese.
$frenchCodes = @(0xE0,0xE1,0xE2,0xE3,0xE4,0xE5,0xE6,0xE7,0xE8,0xE9,0xEA,0xEB,
                 0xEC,0xED,0xEE,0xEF,0xF1,0xF2,0xF3,0xF4,0xF5,0xF6,0xF8,
                 0xF9,0xFA,0xFB,0xFC,0xFD,0xFF,0x153,0x152)
# Vietnamese-specific accented letters (code points).
$viCodes = @(0x102,0x103,0x106,0x107,0x110,0x111,0x114,0x115,0x118,0x119,0x1EA0,0x1EA1)

$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s) }

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

$issues = New-Object System.Collections.ArrayList
foreach ($e in $entries) {
    $o = $e.originalText.Trim(); $t = $e.translatedText.Trim()
    $reasons = New-Object System.Collections.ArrayList

    if (-not (Test-Cjk $e.originalText) -and (Test-Cjk $e.translatedText)) { [void]$reasons.Add('CJK') }

    $hasFr = $false; $hasVi = $false
    foreach ($ch in $t.ToCharArray()) {
        $c = [int]$ch
        if ($frenchCodes -contains $c) { $hasFr = $true }
        if ($viCodes -contains $c) { $hasVi = $true }
    }
    if ($hasFr -and -not $hasVi) { [void]$reasons.Add('French') }

    if ($t.Length -ge 20) {
        $letters = ([regex]::Matches($t, '[A-Za-z]')).Count
        $ratio = $letters / [double]$t.Length
        if ($ratio -gt 0.78) { [void]$reasons.Add('ResidualEnglish') }
    }

    if ($o.Length -ge 10) {
        if ($t.Length -lt ($o.Length * 0.55) -or $t.Length -gt ($o.Length * 2.3)) { [void]$reasons.Add('LengthRatio') }
    }

    if ($reasons.Count -gt 0) {
        [void]$issues.Add([pscustomobject]@{ Key=$e.key; Cat=$e.category; Reason=($reasons -join ','); Orig=$o; Trans=$t })
    }
}

W "Tong entry da dich: $($entries.Count)"
W "Entry co van de: $($issues.Count)"
W ""
W "=== Theo ly do ==="
foreach ($g in ($issues | Group-Object Reason | Sort-Object Count -Descending)) { W ("{0,-22} {1}" -f $g.Name, $g.Count) }
W ""
W "=== Theo nhom ==="
foreach ($g in ($issues | Group-Object Cat | Sort-Object Count -Descending)) { W ("{0,-12} {1}" -f $g.Name, $g.Count) }
W ""
W "=== Chi tiet ==="
foreach ($i in $issues) {
    W ("[{0}] {1}" -f $i.Reason, $i.Key)
    W ("   EN: {0}" -f $i.Orig)
    W ("   VI: {0}" -f $i.Trans)
}
[System.IO.File]::WriteAllText($out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Bao cao: $out  (issues=$($issues.Count) / $($entries.Count))"
