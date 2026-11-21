$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

# LUAT A: cung ban goc giong nhau trong cung file nhung ban dich KHAC nhau
# -> bang chung manh manh nhat cua viec lech chi so.
$groups = @{}
foreach ($e in $doc.entries) {
    if ($e.status -eq 'Pending' -or [string]::IsNullOrWhiteSpace($e.translatedText)) { continue }
    $k = $e.sourceFile + '||' + $e.originalText.Trim()
    if (-not $groups.ContainsKey($k)) { $groups[$k] = @() }
    $groups[$k] += $e
}

$hits = @()
foreach ($k in $groups.Keys) {
    $g = $groups[$k]
    $distinct = @($g | ForEach-Object { $_.translatedText.Trim() } | Select-Object -Unique)
    if ($distinct.Count -gt 1) {
        $hits += [pscustomobject]@{
            File = $g[0].sourceFile
            EN = $g[0].originalText
            Cat = $g[0].category
            Trans = ($g | ForEach-Object { "$($_.key)=<$($_.translatedText)>" }) -join ' | '
        }
    }
}

Write-Host ("LUAT A - so nhom co ban dich khac nhau: {0}" -f $hits.Count)
Write-Host ""
foreach ($h in ($hits | Sort-Object Cat, EN)) {
    Write-Host ("[{0}] EN=<{1}>" -f $h.Cat, $h.EN)
    Write-Host ("      {0}" -f $h.Trans)
}
