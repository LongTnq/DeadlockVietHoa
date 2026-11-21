# Deploy bản dịch vào game — chạy 1 lệnh, xong.

$ErrorActionPreference = 'Stop'

$root    = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
$gameDir = 'E:\Steam\steamapps\common\Deadlock\game\citadel'
$addons  = Join-Path $gameDir 'addons'

Set-Location $root

Write-Host '=============================================='
Write-Host ' DEPLOY BAN DICH TIENG VIET VAO GAME'
Write-Host '=============================================='
Write-Host ''

# --- 1. Export ra staging -------------------------------------------------
Write-Host '[1/4] Export bao dich ra thu muc staging...'
dotnet run --project src/DeadlockVN.CLI --no-build -- export-to-game `
    --translations workspace/translations/vietnamese.json `
    --source workspace/source/original `
    --output workspace/staging/mod/resource/localization `
    --language vietnamese | Select-String 'Tổng entry|Đã dịch|Chưa dịch'

if ($LASTEXITCODE -ne 0) { throw 'Export that bai.' }
Write-Host ''

# --- 2. Pack VPK ----------------------------------------------------------
Write-Host '[2/4] Dong goi VPK mod...'
Remove-Item 'workspace\staging\vpk\deadlockvn*' -Force -ErrorAction SilentlyContinue

dotnet run --project src/DeadlockVN.CLI --no-build -- pack-vpk `
    --staging workspace/staging/mod `
    --output workspace/staging/vpk `
    --name deadlockvn_dir.vpk | Select-String 'Kích thước|✓|✗'

if ($LASTEXITCODE -ne 0) { throw 'Dong goi VPK that bai.' }
Write-Host ''

# --- 3. Cai VPK vao addons ------------------------------------------------
Write-Host '[3/4] Cai VPK vao thu muc addons...'
Get-ChildItem 'workspace\staging\vpk' -Filter 'deadlockvn*' | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $addons $_.Name) -Force
    Write-Host ("  {0,-24} {1,12:N0} bytes" -f $_.Name, $_.Length)
}

# Xoa file .txt rong con lai trong thu muc game (Steam hay tao lai).
# File rong se ghi de VPK mod -> phai xoa de VPK duoc dung.
$loose = Join-Path $gameDir 'resource\localization'
$removed = 0

Get-ChildItem $loose -Recurse -Filter '*_vietnamese.txt' -ErrorAction SilentlyContinue |
    ForEach-Object {
        if ((Get-Item $_.FullName).Length -le 100) {
            Remove-Item $_.FullName -Force
            $removed++
        }
    }

if ($removed -gt 0) {
    Write-Host "  Da xoa $removed file .txt rong (de VPK mod duoc uu tien)."
}
Write-Host ''

# --- 4. Patch fonts.conf --------------------------------------------------
Write-Host '[4/4] Cap nhat font fallback cho tieng Viet...'
$env:PYTHONIOENCODING = 'utf-8'
python tools/patch_fonts.py
Write-Host ''

# --- Kiem tra --------------------------------------------------------------
Write-Host '=============================================='
Write-Host ' KET QUA'
Write-Host '=============================================='

$chunk = Join-Path $addons 'deadlockvn_000.vpk'
if (Test-Path $chunk) {
    $text = python tools/vpk_scan_content.py $chunk 2>&1 | Out-String
    $count = [regex]::Match($text, 'ky tu co dau: ([\d,]+)').Groups[1].Value
    Write-Host "VPK chua: $count ky tu co dau tieng Viet."
}

Write-Host ''
Write-Host 'Bay gio vao game va kiem tra.'
