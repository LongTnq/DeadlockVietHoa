# Sửa bản dịch thủ công

Hướng dẫn này giải thích **file nào sửa ở đâu** và **quy trình đầy đủ**
để sửa lỗi dịch thuật rồi đưa lên GameBanana.

## Sơ đồ các lớp file

```
workspace/translations/vietnamese.json      ← NGUỒN SỰ THẬT (DB)
        │  export-to-game
        ▼
workspace/staging/localization/*.txt          ← FILE SỬA TAY ĐƯỢC
        │  import-from-staging
        ▼
workspace/translations/vietnamese.json      ← đồng bộ ngược lại DB
        │  package
        ▼
workspace/staging/mod/resource/localization/ ← đóng gói VPK
        │  tools/deploy_lang.py
        ▼
game/citadel_vietnamese/pak01_*.vpk         ← cài vào game
```

## 1. File nên sửa: `workspace/staging/localization/`

Đây là **file bạn nên mở ra sửa**. Định dạng Valve KeyValues — mỗi
entry một dòng, dễ đọc, dễ sửa:

```
workspace\staging\localization\
├── citadel_main\citadel_main_vietnamese.txt          479 KB   ← UI chính
├── citadel_heroes\citadel_heroes_vietnamese.txt      515 KB   ← mô tả hero + lore
├── citadel_attributes\citadel_attributes_vietnamese.txt  140 KB ← thuộc tính
├── citadel_mods\citadel_mods_vietnamese.txt           81 KB
├── citadel_gc\citadel_gc_vietnamese.txt                17 KB
├── citadel_gc_hero_names\...                          11 KB
├── citadel_gc_mod_names\...                           23 KB
├── citadel_patch_notes\...                            19 KB
├── citadel_seasonal\...                                3 KB
├── citadel_generated_vo\...                       5,8 MB  ← VO, CHƯA dịch
└── citadel_vdata\accolades\...                         8 KB
```

Cấu trúc một dòng:

```
		"Cancel"						"Huỷ"
		 └─ key (giữ nguyên)   └─ giá trị tiếng Việt (sửa ở đây)
```

### ⚠️ Chỉ sửa phần giá trị, giữ nguyên phần key

Sửa sai key sẽ làm game không hiển thị chuỗi đó. Giữ nguyên cả
placeholder (`{s:...}`, `{i:...}`) và thẻ HTML (`<span>`, `<br>`) — lệnh
`import-from-staging` sẽ tự phát hiện và từ chối nếu bạn làm hỏng chúng.

### Editor nên dùng

| Editor | Ghi chú |
|---|---|
| VS Code | Mở cả thư mục `staging/localization`, tìm & thay thế toàn cục |
| Notepad++ | Encoding UTF-8, giữ nguyên BOM ở đầu file |
| Sublime Text | Nhanh, "Find in Folder" tiện |

> **Quan trọng:** file phải giữ **UTF-8**. Nếu lưu bằng UTF-16 hoặc
> GBK, tiếng Việt sẽ bị lỗi font trong game.

## 2. Quy trình sửa (3 bước)

### Bước 1 — Sửa file `.txt` trong staging

Mở file bạn muốn sửa bằng editor, sửa giá trị tiếng Việt, lưu lại.

### Bước 2 — Đồng bộ ngược vào DB

```powershell
dotnet run --project src/DeadlockVN.CLI -- import-from-staging
```

Lệnh này:
- đọc lại tất cả file `*_vietnamese.txt` trong staging
- so sánh với DB, **chỉ lấy những entry đã được sửa**
- kiểm tra chất lượng (placeholder, cấu trúc HTML, còn sót tiếng Anh?)
- entry hợp lệ → cập nhật và đánh dấu `Approved`
- entry lỗi → **từ chối** và in ra danh sách để bạn sửa lại

### Bước 3 — Đóng gói lại và cài vào game

```powershell
dotnet run --project src/DeadlockVN.CLI -- package
python tools/deploy_lang.py
```

> ⚠️ **Không chạy `export-to-game` sau khi đã sửa tay** — lệnh đó xuất
> lại từ DB và sẽ **ghi đè** toàn bộ chỉnh sửa của bạn. Luôn đi theo
> thứ tự: sửa `.txt` → `import-from-staging` → `package` → deploy.

## 3. Kiểm tra chất lượng

```powershell
# Kiểm tra toàn bộ DB (placeholder, HTML, tiếng Anh còn sót)
dotnet run --project src/DeadlockVN.CLI -- revalidate

# Xem tiến độ theo nhóm
dotnet run --project src/DeadlockVN.CLI -- report

# Xem các entry đang chờ review
dotnet run --project src/DeadlockVN.CLI -- review --limit 50
```

## 4. Những file KHÔNG nên sửa tay

| File | Lý do |
|---|---|
| `workspace/translations/vietnamese.json` | File DB gộp, 32 MB, 1 entry = 1 dòng JSON dài. Sửa tay rất dễ sai. Dùng `import-from-staging` thay thế. |
| `workspace/translations/ui/ui.json` và các file tách nhóm khác | **Stale** — chúng được sinh ra từ lâu và không còn đồng bộ với DB (hiện chứa 0 entry đã dịch). |
| `workspace/staging/mod/...` | Output sinh tự động, sẽ bị ghi đè mỗi lần chạy `package`. |
| `game/citadel_vietnamese/pak01_*.vpk` | File nhị phân, phải đóng gói lại bằng `package` + `deploy_lang.py`. |
| `game/citadel/resource/localization/*.txt` | Bản sao loose trong game. Steam có thể ghi đè khi verify file. |

## 5. Chuẩn bị để đẩy lên GameBanana

Cấu trúc mod nên phát hành:

```
deadlock-vietnamese/
├── citadel_vietnamese/
│   ├── gameinfo.gi
│   ├── pak01_dir.vpk
│   └── pak01_000.vpk
├── README.md                     ← hướng dẫn cài đặt
└── install.bat                   ← script tự động chép + vá gameinfo.gi + fonts.conf
```

### Những gì người dùng khác cần làm

1. Giải nén vào thư mục `game/` của Deadlock
   (`Steam\steamapps\common\Deadlock\game\`)
2. Chạy `install.bat` — script sẽ:
   - thêm `"vietnamese" "3"` vào `SupportedLanguages` trong `citadel/gameinfo.gi`
   - thêm font fallback (Arial + Segoe UI) vào `citadel/panorama/fonts/fonts.conf`
3. Mở game → Settings → Language → Tiếng Việt

### Script cài đặt mẫu

```bat
@echo off
REM === Deadlock Vietnamese Localization - Cai dat ===
setlocal

set GAME=%~dp0..\..\..\Steam\steamapps\common\Deadlock\game
if not exist "%GAME%\citadel\gameinfo.gi" (
    echo [LOI] Khong tim thay thu muc game.
    echo Hay sua duong dan GAME trong install.bat.
    pause
    exit /b 1
)

REM 1. Them "vietnamese" vao SupportedLanguages
findstr /c:"\"vietnamese\"" "%GAME%\citadel\gameinfo.gi" >nul
if errorlevel 1 (
    echo [1/2] Dang them tieng Viet vao SupportedLanguages...
    powershell -NoProfile -Command ^
      "$p='%GAME%\citadel\gameinfo.gi';$t=[IO.File]::ReadAllText($p);" ^
      "$i=$t.IndexOf('SupportedLanguages');" ^
      "$d=0;for($k=$i;$k -lt $t.Length;$k++){if($t[$k] -eq '{'){$d++}elseif($t[$k] -eq '}'){$d--;if($d -eq 0){break}}};" ^
      "[IO.File]::WriteAllText($p,$t.Insert($i+18,\"`t`t`"vietnamese`" `"3`"`r`n"))"
) else (
    echo [1/2] Tieng Viet da co trong SupportedLanguages.
)

REM 2. Font fallback
if not exist "%GAME%\citadel\panorama\fonts\fonts.conf" (
    echo [LOI] Khong tim thay fonts.conf
) else (
    findstr /c:"Arial" "%GAME%\citadel\panorama\fonts\fonts.conf" >nul
    if errorlevel 1 (
        echo [2/2] Dang them font fallback...
        REM ... chen khoi Arial + Segoe UI ...
    ) else (
        echo [2/2] Font fallback da co.
    )
)

echo.
echo HOAN TAT. Mo game va chon Settings ^> Language ^> Tieng Viet.
pause
```

## 6. Kiểm tra trước khi phát hành

```powershell
# 1. DB sạch
dotnet run --project src/DeadlockVN.CLI -- revalidate
#    -> "Entry hỗng: 0"

# 2. Đóng gói sạch
dotnet run --project src/DeadlockVN.CLI -- package
python tools/deploy_lang.py

# 3. Kiểm tra VPK có đúng nội dung
& 'C:\Program Files\VPKEdit\vpkeditcli.exe' --no-progress --file-tree `
    "E:\Steam\steamapps\common\Deadlock\game\citadel_vietnamese\pak01_dir.vpk"

# 4. Chơi thử — mở game, đổi sang Tiếng Việt, chơi 1 ván
```

## Mẹo tìm lỗi dịch nhanh

Tìm các chuỗi còn sót tiếng Anh trong file đã export:

```powershell
# Tìm từ tiếng Anh phổ biến trong bản dịch
Select-String -Path "workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt" `
              -Pattern '"(the|your|Deal|Damage|Health|Cooldown|Level|Points)"'
```

Hoặc dùng công cụ có sẵn:

```powershell
dotnet run --project src/DeadlockVN.CLI -- review --limit 100
```

## Thuật ngữ — bảng tra nhanh

| Tiếng Anh | Tiếng Việt (đang dùng) |
|---|---|
| Health | Máu |
| Health Regen | Hồi máu |
| Damage | Sát thương |
| Cooldown | Thời gian hồi chiêu |
| Stamina | Thể lực |
| Spirit | Nội lực |
| Soul | Linh hồn |
| Ultimate | Tuyệt kỹ |
| Ability | Kỹ năng |
| Item | Vật phẩm |
| Stack | Cộng dồn |
| Chance | Tỉ lệ / Cơ hội |
| Bonus | Thưởng |
| Lifesteal | Hút máu |
| Slow | Làm chậm |
| Stun | Choáng |
| Silence | Phong tấn |
| Slow projectile | Đạn làm chậm |
| Melee | Cận chiến |
| Ranged | Tầm xa |
| Kill | Hạ gục / Tiêu diệt |
| Death | Cái chết |
| Respawn | Hồi sinh |
| Level | Cấp |
| Upgrade | Nâng cấp |
| Buy / Purchase | Mua |
| Sell | Bán |
| Use | Sử dụng |
| Equip | Trang bị |
| Hero | Hero |
| Trooper | Lính |
| Warden | Trưởng canh |
| Patron | Bảo trợ |
| Amber | Hổ phách |
| Guiding Light | Ánh dẫn đường |
| Kill / Assist | Hạ gục / Hỗ trợ |
