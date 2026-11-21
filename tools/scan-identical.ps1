$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$entries = $doc.entries | Where-Object { $_.status -ne 'Pending' -and $_.translatedText }
$identical = $entries | Where-Object { $_.originalText.Trim() -ceq $_.translatedText.Trim() }
Write-Host "Total translated: $($entries.Count)  Identical: $($identical.Count)"
$identical | Group-Object category | Format-Table Name, Count -AutoSize
Write-Host "--- by sourceFile ---"
$identical | Group-Object sourceFile | Sort-Object Count -Descending | Format-Table Count, Name -AutoSize
Write-Host "--- samples (non-hero-name) ---"
$identical | Where-Object { $_.key -notmatch '^hero_[a-z_]+(:n|_search:n|_sort:n)$' } | Select-Object -First 40 key, originalText | Format-Table -AutoSize -Wrap
