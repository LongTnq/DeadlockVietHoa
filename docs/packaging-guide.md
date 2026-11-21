# Hướng dẫn đóng gói & cài đặt mod tiếng Việt (Deadlock)

> Sprint 4 — Ngày 2. Tài liệu này mô tả cách đóng gói bản dịch tiếng Việt
> thành mod cài được cho Deadlock.
>
> **Cập nhật 19/11/2026** — phần 0 và 5 đã được kiểm chứng lại bằng bản
> cài game thật. Xem `docs/manual-editing-guide.md` cho quy trình sửa tay
> + đóng gói hiện hành.

---

## 0. Kết luận quan trọng từ việc khảo sát bản cài game thật

Trước khi đóng gói, cần nắm 5 sự thật đã được kiểm chứng trên bản cài
`E:\Steam\steamapps\common\Deadlock\game\citadel`:

| # | Sự thật | Bằng chứng |
|---|----------|-------------|
| 1 | **File localization nằm LOOSE, không nằm trong VPK** | `pak01_dir.vpk` chứa **0** chuỗi `citadel_main`; toàn bộ 11 file nằm tại `game/citadel/resource/localization/` |
| 2 | Game đọc file loose qua search path `MOD`/`GAME` = `game\citadel\` | `console.log` mục `[Filesystem] Path ID:` liệt kê `MOD "…\game\citadel\"` |
| 3 | `gameinfo.gi` chỉ khai báo **17** ngôn ngữ, **không có** `vietnamese` | Đọc trực tiếp khối `SupportedLanguages` |
| 4 | **Font chính THIẾU 86/176 mã tiếng Việt** | `valveoracle-medium.ttf` thiếu `ơ ư ờ ở ị ọ ộ ụ ữ` (nguyên âm móc) |
| 5 | Game **không có** font "Noto Sans" tổng quát | Chỉ có `notosansjp/kr/sc/tc/thai`; `notosansthai` thiếu 113/113 mã Việt |

### Ảnh hưởng tới thiết kế

- **Cần CẢ VPK lẫn file loose.** File loose là nơi game đọc trực tiếp và
  hoạt động ngay — nhưng **Steam verify sẽ ghi đè** về stub 60 byte.
  VPK trong thư mục `game/citadel_vietnamese/` là cơ chế chính thức của
  Valve cho ngôn ngữ phụ, Steam **không** quản lý thư mục đó nên an toàn.
  Xem `tools/deploy_lang.py` — script làm cả hai.
- **Font fallback là BẮT BUỘC.** `VALVE Oracle` — font dùng cho toàn bộ UI —
  thiếu 15 mã Unicode, trong đó có `ơ` và `ư` (vốn rất phổ biến: *Được*,
  *Hướng*, *Ưu*) cùng 7 tổ hợp `ờ ở ị ọ ộ ụ ữ`. Thiếu chúng sẽ hiện
  ký tự vuông □. Khối fallback trong `fonts.conf` trỏ sang
  `Noto Sans JP` là bắt buộc.
- **Game đã có sẵn file `*_vietnamese.txt` dạng stub rỗng** (60 byte).
  Mod của ta ghi đè các stub này bằng bản dịch đầy đủ.

---

## 1. Cấu trúc mod

```
game/citadel/
├── resource/localization/                    ← SỬA (11 file)
│   ├── citadel_main/citadel_main_vietnamese.txt
│   ├── citadel_heroes/citadel_heroes_vietnamese.txt
│   ├── citadel_attributes/citadel_attributes_vietnamese.txt
│   ├── citadel_gc/citadel_gc_vietnamese.txt
│   ├── citadel_gc_hero_names/citadel_gc_hero_names_vietnamese.txt
│   ├── citadel_gc_mod_names/citadel_gc_mod_names_vietnamese.txt
│   ├── citadel_generated_vo/citadel_generated_vo_vietnamese.txt
│   ├── citadel_mods/citadel_mods_vietnamese.txt
│   ├── citadel_patch_notes/citadel_patch_notes_vietnamese.txt
│   ├── citadel_seasonal/citadel_seasonal_vietnamese.txt
│   └── citadel_vdata/accolades/accolades_vietnamese.txt
│
├── gameinfo.gi                               ← SỬA (1 dòng)
│   └── SupportedLanguages: + "vietnamese" "3"
│
└── panorama/fonts/fonts.conf                ← TUỲ CHỌN
    └── + khối <match> cho vi-vn
```

### `gameinfo.gi` — thay đổi duy nhất

```diff
 		"turkish" "3"
 		"ukrainian" "3"
+		"vietnamese" "3"
 	}
```

> Game đã có sẵn file `*_vietnamese.txt` dạng **stub rỗng** (60 byte, header
> `"Language" "English"`, `Tokens {}` rỗng). Mod của ta **ghi đè** các file
> stub này bằng bản dịch đầy đủ.

---

## 2. Cách 1 — Chép trực tiếp (khuyến nghị)

### 2.1 Sinh gói mod

```bash
dotnet run --project src/DeadlockVN.CLI -- package \
  --source   workspace/source/original \
  --output   workspace/staging/mod \
  --translations workspace/translations \
  --game     "E:\Steam\steamapps\common\Deadlock\game"
```

Lệnh sẽ:

1. Xuất 11 file `*_vietnamese.txt` vào `workspace/staging/mod/resource/localization/`
2. Vá `gameinfo.gi` (tạo `gameinfo.gi.bak`)
3. Vá `fonts.conf` (tạo `fonts.conf.bak`)
4. Copy `fonts.conf` đã vá vào staging

### 2.2 Chép vào game

```powershell
$game = "E:\Steam\steamapps\common\Deadlock\game\citadel"

# Sao lưu file gốc của game trước
Copy-Item "$game\gameinfo.gi" "$game\gameinfo.gi.orig" -ErrorAction SilentlyContinue

# Chép 11 file localization
Copy-Item "workspace\staging\mod\resource\localization\*" `
          "$game\resource\localization\" -Recurse -Force

# Chép fonts.conf đã vá (tuỳ chọn)
Copy-Item "workspace\staging\mod\panorama\fonts\fonts.conf" `
          "$game\panorama\fonts\fonts.conf" -Force
```

> **Quan trọng:** `gameinfo.gi` **không** chép từ staging — nó được vá
> **tại chỗ** bởi lệnh `package` (kèm `.bak`). Nếu bạn chép đè từ staging
> thì mất file `.bak`.

### 2.3 Khởi động game

1. Mở Deadlock
2. Vào **Settings → Language → Tiếng Việt (Vietnamese)**
3. Game sẽ đọc file `*_vietnamese.txt` đã chép

### 2.4 Gỡ mod

```powershell
$game = "E:\Steam\steamapps\common\Deadlock\game\citadel"

# Khôi phục gameinfo.gi
Copy-Item "$game\gameinfo.gi.bak" "$game\gameinfo.gi" -Force

# Khôi phục fonts.conf
Copy-Item "$game\panorama\fonts\fonts.conf.bak" `
          "$game\panorama\fonts\fonts.conf" -Force

# Xoá file localization (game sẽ dùng lại stub rỗng)
Get-ChildItem "$game\resource\localization" -Recurse -Filter "*_vietnamese.txt" |
  ForEach-Object {
    $orig = $_.FullName -replace '_vietnamese\.txt$','_english.txt'
    Copy-Item $orig $_.FullName -Force
  }
```

---

## 3. Cách 2 — Đóng gói VPK (tùy chọn)

Dùng khi muốn phát hành mod dạng **file `.vpk`** để chia sẻ, hoặc dùng
**Deadlock Mod Manager (DMM)**.

### 3.1 Vì sao vẫn khả dụng

VPK được mount qua search path `GAME citadel/addons/` (DMM quản lý) hoặc
bất kỳ thư mục `Game` nào trong `gameinfo.gi`. Source 2 tìm file theo thứ tự
ưu tiên search path, nên file trong VPK sẽ **ghi đè** file loose.

### 3.2 Tạo VPK bằng VPKEdit

1. Mở **VPKEdit** (`C:\Program Files\VPKEdit\vpkedit.exe`)
2. **File → Create New VPK…**
3. **Add Folder…** → chọn `workspace\staging\mod\resource`
   - ⚠ Chọn đúng thư mục `resource` để đường dẫn trong VPK là
     `resource/localization/...` (không phải `mod/resource/localization/...`)
4. **Save VPK…** → đặt tên `deadlockvn.vpk`
5. Copy vào `game\citadel\addons\`

### 3.3 Kiểm tra đường dẫn trong VPK

Mở VPKEdit, mở `deadlockvn.vpk`, xác nhận cây thư mục:

```
resource/
└── localization/
    ├── citadel_main/
    │   └── citadel_main_vietnamese.txt
    ├── citadel_heroes/
    │   └── citadel_heroes_vietnamese.txt
    └── ... (11 thư mục)
```

> **Sai dấu là game không đọc file.** Đường dẫn phải là
> `resource/localization/<nhóm>/<tên>_vietnamese.txt`.

### 3.4 Với Deadlock Mod Manager

DMM chỉ cần một thư mục mod trong `content/citadel/<tên-mod>/`:

```
content/citadel/deadlockvn/
├── resource/localization/...
└── mod.vdf          ← manifest DMM
```

---

## 4. Kiểm chứng sau khi cài

### 4.1 Kiểm tra file

```powershell
$game = "E:\Steam\steamapps\common\Deadlock\game\citadel"

Get-ChildItem "$game\resource\localization" -Recurse -Filter "*_vietnamese.txt" |
  Select-Object Name, Length
```

Mỗi file phải **> 1 KB** (stub rỗng là 60 byte).

### 4.2 Kiểm tra `gameinfo.gi`

```powershell
Select-String -Path "$game\gameinfo.gi" -Pattern "vietnamese"
```

Phải có: `		"vietnamese" "3"`

### 4.3 Kiểm tra trong game

Mở `game/citadel/console.log` sau khi vào game, tìm:

```
[Filesystem] MOD   "…\game\citadel\"
```

### 4.4 Kiểm tra font (nếu thấy ký tự vuông □)

Font `VALVE Oracle` phủ đủ tiếng Việt, nên ký tự vuông chỉ xuất hiện nếu:

- Bản dịch chứa ký tự ngoài BMP (emoji, chữ Hán, Nôm)
- Hoặc font bị thiếu trong bản cài

Xử lý: thêm khối fallback trong `fonts.conf` (xem mục 5).

---

## 5. Font fallback

**Bắt buộc** — các font chính của game thiếu mã Unicode tiếng Việt.

### 5.1 Font có sẵn trong game

Đo thật bằng `FontCoverageChecker` trên bảng `cmap` của file font trong
bản cài game (bộ 176 mã tiếng Việt):

| Họ font | File đại diện | Phủ tiếng Việt | Ghi chú |
|-----------|-----------------|-----------------|---------|
| `VALVE Oracle` | `valveoracle-medium.ttf` | ❌ 90/176 (thiếu **86**) | **Font chính — thiếu toàn bộ nguyên âm móc `ơ ư ờ ở ị ọ ộ ụ ữ`** |
| `VALVE Pulp` | `valvepulp-bold.ttf` | ❌ 90/176 (thiếu **86**) | Font tiêu đề — thiếu y hệt VALVE Oracle |
| `Retail Text Demo` | `retailtextdemo-bold.otf` | ❌ 45/176 (thiếu **131**) | Font chữ nghịch cảnh |
| `Retail Demo` | `retaildemo-bold.otf` | ❌ 45/176 (thiếu **131**) | Font chữ nghịch cảnh |
| `Jost` | `jost-black.ttf` | ❌ 86/176 (thiếu **90**) | *Không có trong `fonts.conf` → không cần vá* |
| `Noto Sans JP` | `notosansjp-regular.ttf` | ✅ 176/176 | **Fallback khuyến nghị** |
| `Noto Sans KR` | `notosanskr-regular.ttf` | ✅ 176/176 | Dùng được |
| `Noto Sans SC` | `notosanssc-regular.ttf` | ✅ 176/176 | Dùng được |
| `Noto Sans TC` | `notosanstc-regular.ttf` | ✅ 176/176 | Dùng được |
| `Noto Sans Thai` | `notosansthai-regular.ttf` | ❌ 0/176 | Chỉ dùng cho Thái |
| `Hahmlet` | `hahmlet-medium.ttf` | ✅ 176/176 | Dùng được |
| `Radiance` | `radiance-regular.otf` | ✅ 176/176 | Dùng được |
| `Reaver` | `reaver-regular.otf` | ✅ 176/176 | Dùng được |

→ **Không có "Noto Sans" tổng quát** trong game.

> **ĐÃ ĐỔI (19/11/2026):** đo thực tế trên bản cài thật cho thấy
> `notosansjp` cũng thiếu `ơ` và `ữ` (2/176 mã). Font fallback hiện
> dùng **Arial + Segoe UI** của Windows — đo thủ công bằng
> `tools/check_font_coverage.py`: cả hai phủ **173/173** ký tự Latin mở
> rộng mà bản dịch dùng, không thiếu mã nào.

### 5.2 Mã mà `VALVE Oracle` / `VALVE Pulp` thiếu (86 mã)

Toàn bộ **nguyên âm móc** (`ơ ư`) và mọi tổ hợp dấu của chúng:

```
ơ ư            (U+01A1, U+01B0)
ờ ở ị ọ ộ ụ ữ  (U+1EE1, U+1EF3, U+1EDF, U+1EE3, U+1ECD, U+1EEF, U+1EF1)
Ơ Ư Ờ Ở Ị Ọ Ộ Ụ Ữ  (bản hoa)
```

`Retail Text Demo` và `Retail Demo` thiếu **131 mã** — nhiều hơn vì ngoài
nguyên âm móc, chúng còn thiếu cả các tổ hợp `ế ề ể ệ ớ ờ ở ợ ứ ừ ử ự`
và nhiều ký tự Latin mở rộng khác.

### 5.3 Khối fallback mẫu

Mod phải sinh **một khối `<match>` cho mỗi họ font thiếu glyph**:

```xml
	<!-- Deadlock Vietnamese Localization: font fallback -->
	<match>
		<test name="family">
			<string>VALVEOracle</string>
		</test>
		<test name="lang">
			<string>vi-vn</string>
		</test>
		<edit name="family" mode="append_last" binding="strong">
			<string>Noto Sans JP</string>
		</edit>
	</match>
	<match>
		<test name="family" compare="contains">
			<string>VALVEPulp</string>
		</test>
		<test name="lang">
			<string>vi-vn</string>
		</test>
		<edit name="family" mode="append_last" binding="strong">
			<string>Noto Sans JP</string>
		</edit>
	</match>
```

> `mode="append_last"` (không phải `assign`) để giữ font gốc làm font
> chính và chỉ bổ sung `Noto Sans JP` cho ký tự còn thiếu.

> `compare="contains"` là bắt buộc với `VALVEPulp` và `Retail Text Demo`:
> trong `fonts.conf` của game, các weight được khai báo dạng
> `VALVEPulp-Bold`, `RetailTextDemo-Regular`… nên khớp chính xác tên họ sẽ
> bỏ sót. Game cũng dùng `compare="contains"` cho chính các họ font này
> (xem khối `ja-jp` / `ko-kr` / `zh-cn` trong file gốc).

### 5.4 Kiểm tra phủ glyph

Lệnh `package` tự động kiểm tra **mọi họ font thiếu glyph** và **chỉ** vá
`fonts.conf` cho những họ đó. Bạn cũng có thể kiểm tra thủ công:

```powershell
pip install fonttools
python -c "
from fontTools.ttLib import TTFont
f = TTFont(r'…\game\citadel\panorama\fonts\valveoracle-medium.ttf')
cmap = set()
for t in f['cmap'].tables: cmap |= set(t.cmap.keys())
need = [0x01A1, 0x01B0, 0x1EE1, 0x1EF3, 0x1EDF, 0x1EE3, 0x1ECD, 0x1EEF, 0x1EF1]
print('thieu:', [hex(c) for c in need if c not in cmap])
"
```

---

## 6. Quy trình đầy đủ (từ bản dịch đến mod)

### 6a. Sửa bản dịch (xem `manual-editing-guide.md` để chi tiết)

```powershell
# 1. Sửa tay các file .txt trong staging (mỗi entry 1 dòng, dễ đọc)
#    workspace/staging/localization/citadel_main/citadel_main_vietnamese.txt

# 2. Đồng bộ ngược vào DB (tự kiểm tra placeholder/HTML, đánh dấu Approved)
dotnet run --project src/DeadlockVN.CLI -- import-from-staging

# 3. Kiểm tra chất lượng
dotnet run --project src/DeadlockVN.CLI -- revalidate
```

### 6b. Đóng gói và cài vào game

```powershell
# 4. Đóng gói (xuất 11 file .txt + vá gameinfo.gi + fonts.conf)
dotnet run --project src/DeadlockVN.CLI -- package \
  --source workspace/source/original \
  --output workspace/staging/mod \
  --translations workspace/translations \
  --game "E:\Steam\steamapps\common\Deadlock\game"

# 5. Đóng gói VPK + chép vào game (cả VPK lẫn file loose)
python tools/deploy_lang.py

# 6. Chạy game, chọn ngôn ngữ Tiếng Việt
```

> ⚠️ **Thứ tự quan trọng.** Không chạy `export-to-game` *sau* khi đã sửa
> tay — lệnh đó xuất lại từ DB và sẽ **ghi đè** chỉnh sửa của bạn.
> Luôn đi: sửa `.txt` → `import-from-staging` → `package` → `deploy_lang.py`.

### 6c. Đóng gói bản phát hành cho GameBanana

```powershell
# Gom thư mục mod + script cài đặt vào 1 file ZIP
Compress-Archive -Path "release\deadlock-vietnamese\*" \
                  -DestinationPath "release\deadlock-vietnamese.zip" -Force
```

Cấu trúc gói phát hành:

```
deadlock-vietnamese/
├── citadel_vietnamese/          ← chép vào game/ (không Steam đụng)
│   ├── gameinfo.gi
│   ├── pak01_dir.vpk
│   └── pak01_000.vpk
├── install.bat                   ← tự động vá gameinfo.gi + fonts.conf
├── uninstall.bat                 ← gỡ mod, khôi phục file gốc
└── README.md                     ← hướng dẫn cài đặt
```

---

## 7. Xử lý sự cố

| Triệu chứng | Nguyên nhân | Cách sửa |
|--------------|-------------|----------|
| Menu không hiện "Tiếng Việt" | `gameinfo.gi` chưa vá | Chạy lại `package` với `--game` |
| Chọn được ngôn ngữ nhưng text vẫn tiếng Anh | File `*_vietnamese.txt` còn là stub 60 B | Chép lại file từ staging |
| Text tiếng Việt hiện ký tự vuông □ | Thiếu glyph | Thêm font fallback (mục 5) |
| Game không khởi động sau khi vá `gameinfo.gi` | File hỏng | Khôi phục `gameinfo.gi.bak` |
| VPK không được đọc | Sai đường dẫn trong VPK | Xem mục 3.3 |
| Báo "key không tồn tại" khi đóng gói | Key lệch với file game | Chạy `sync` để cập nhật danh sách key |

---

## 8. Ghi chú về hình ảnh UI

Game có **hình riêng cho ~16 ngôn ngữ** trong VPK (ví dụ
`killing_blow_thai_png`, `npe_heroes3_ukrainian_png`). **Không có bản tiếng
Việt** → các hình này sẽ tự động fallback về bản tiếng Anh.

Đây là hạn chế của bản dịch **chỉ phần chữ**, không phải lỗi. Nếu muốn
tiếng Việt hoàn toàn, cần làm thêm 1 sprint vẽ lại ~48 hình.
