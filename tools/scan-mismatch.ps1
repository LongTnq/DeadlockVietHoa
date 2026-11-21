$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$entries = @($doc.entries | Where-Object { $_.status -ne 'Pending' -and $_.translatedText })

# Ban do originalText -> danh sach key dang giu no (de tim ban dich bi "copy" nham tu entry khac)
$origMap = @{}
foreach ($e in $entries) {
    $k = $e.originalText.Trim()
    if (-not $origMap.ContainsKey($k)) { $origMap[$k] = New-Object System.Collections.ArrayList }
    [void]$origMap[$k].Add($e.key)
}

$mismatch = New-Object System.Collections.ArrayList
foreach ($e in $entries) {
    $t = $e.translatedText.Trim()
    if ($t -ceq $e.originalText.Trim()) { continue }
    if ($origMap.ContainsKey($t)) {
        [void]$mismatch.Add([pscustomobject]@{
            Key = $e.key
            Original = $e.originalText
            Translated = $e.translatedText
            CopiedFrom = ($origMap[$t] -join ', ')
        })
    }
}
Write-Host "Entries lai bang copy tu entry khac: $($mismatch.Count)"
$mismatch | Format-Table -AutoSize -Wrap
