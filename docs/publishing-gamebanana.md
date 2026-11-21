# Hướng dẫn publish mod lên GameBanana

Tài liệu này mô tả cách đưa mod **Deadlock Vietnamese Localization** lên
GameBanana để nó xuất hiện trong **Deadlock Mod Manager (DMM)**.

## 0. Kiến thức nền

### GameBanana có phải nguồn của DMM không?

Có. Theo README của DMM:

> *GameBanana provides the comprehensive mod database and API that makes
> browsing, discovering, and downloading Deadlock mods possible.*

DMM cache catalog GameBanana và tải mod từ đó. Nên **chỉ cần upload lên
GameBanana** là mod sẽ xuất hiện trong `Mods Store` sau vài phút.

### Có cần GitHub không?

Không bắt buộc. GitHub chỉ hữu ích để chia sẻ mã nguồn (repo này là
**private** nên người khác không thấy).

## 1. Cơ chế kỹ thuật của mod

### Game đọc file localization thế nào?

Deadlock đọc file localization ở đường dẫn cố định:

```
game/citadel/resource/localization/<folder>/<file>_<ngôn ngữ>.txt
```

Các file này là **loose file** trên đĩa (không nằm trong VPK của game).

### Thứ tự mount (đọc từ `game/citadel/gameinfo.gi`)

```
SearchPaths
{
    Game_UILanguage   citadel_*LANGUAGE*   <- thắng, ghi đè mọi thứ
    Game_LowViolence  citadel_lv
    Game              citadel/addons      <- VPK của mod (DMM quản lý)
    Game              citadel             <- file gốc của game
    Game              core
}
```

Hai điều quyết định:

1. `citadel_*LANGUAGE*` được mount **đầu tiên**, nên thư mục
   `citadel_vietnamese/` (nếu tồn tại) sẽ thắng mọi thứ khác.
2. Trong cùng một search path, **engine luôn tìm trong VPK trước loose file**.

### Vì sao mod ship file `*_english.txt`?

Vì `citadel/addons` nằm **sau** `citadel_*LANGUAGE*` và **trước** `citadel`.
Nếu người dùng để ngôn ngữ là English thì:

- `citadel_*LANGUAGE*` không resolve (không có `citadel_english/`) → không mount
- `citadel/addons` mount trước `citadel` → VPK của mod thắng loose file

Kết quả: game đọc file `*_english.txt` trong VPK của mod → hiện tiếng Việt,
trong khi `Settings > Language` vẫn ghi "English".

### Vì sao lại có thêm biến thể `*_vietnamese.txt`?

Cho người dùng muốn chọn ngôn ngữ "Tiếng Việt" trong menu game. Biến thể
này yêu cầu:
- `gameinfo.gi` có `"vietnamese" "3"` trong `SupportedLanguages`
- Game khởi chạy với ngôn ngữ vietnamese

Vì `citadel_vietnamese/` được mount **đầu tiên**, biến thể này phải nằm
trong một thư mục được mount trước — đó là lý do biến thể này kèm theo
hướng dẫn cài tay, không phải để DMM quản lý.

## 2. Đóng gói

```powershell
python tools/build_release_dmm.py
```

Kết quả:

```
release/deadlock-vietnamese.zip
├── README.md
├── vpk-english/
│   ├── pak01_dir.vpk
│   └── pak01_000.vpk
└── vpk-vietnamese/
    ├── pak01_dir.vpk
    └── pak01_000.vpk
```

Script tự động:
- Đổi tên `*_vietnamese.txt` → `*_english.txt` (biến thể english)
- Sửa trường `"Language"` cho khớp tên file
- Giữ nguyên BOM UTF-8 (game yêu cầu)
- Đóng gói `panorama/fonts/fonts.conf` với font fallback tiếng Việt
- Điều chỉnh điều kiện `lang` trong `fonts.conf` theo từng biến thể

## 3. Upload lên GameBanana

### Bước 1 — Tài khoản

Vào [gamebanana.com](https://gamebanana.com), đăng ký tài khoản miễn phí.

### Bước 2 — Tạo submission

1. Vào `https://gamebanana.com/mods` → chọn game **Deadlock**.
2. Bấm **Submit a new mod**.
3. Điền các trường:

| Trường | Nội dung |
|---|---|
| **Title** | `Deadlock - Vietnamese Localization` |
| **Category** | Mods → *Localization / UI* (chọn category sát nhất) |
| **Homepage** | (để trống hoặc link repo GitHub) |
| **Description** | xem mẫu bên dưới |

**Mẫu description:**

```markdown
Bản dịch tiếng Việt cho game Deadlock (Valve).

## Đã dịch

| Phần | Trạng thái |
|---|---|
| Giao diện (UI) | ~99% |
| Mô tả hero + lore | ~100% |
| Kỹ năng (abilities) | ~100% |
| Vật phẩm (items) | 100% |
| Chỉ số (attributes) | 100% |
| Giọng nói (voice lines) | Chưa dịch |

## Cách dùng

Cài qua **Deadlock Mod Manager** → Mods Store → Download Mod → Enable →
Launch modded.

Hoặc cài tay: giải nén, copy 2 file `.vpk` vào
`Steam\steamapps\common\Deadlock\game\citadel\addons\`, rồi thêm dòng
`Game citadel/addons` vào `gameinfo.gi` (trước `Game citadel`).

Gói có 2 biến thể:
- `vpk-english/` — giữ ngôn ngữ English trong game, nội dung hiện tiếng Việt
- `vpk-vietnamese/` — chọn "Tiếng Việt" trong Settings > Language

## Ghi chú

- Không sửa file gốc của game.
- Bản dịch nguồn mở, có thể tự chỉnh sửa và đóng góp.
```

### Bước 3 — Files

1. Vào mục **Files** của submission.
2. Upload `release/deadlock-vietnamese.zip`.
3. **Đặt tên file**: `Deadlock Vietnamese Localization v1.0.zip`
4. **Loại file**: chọn `Mod` / `Archive`.
5. Nếu GameBanana hiện nút **"Deadlock Mod Manager 1-click install"** →
   bật lên. Đây là nút mà DMM bắt tay vào.

> **Quan trọng**: ZIP phải chứa `.vpk` ở **trong thư mục con** hoặc ở gốc —
> DMM giải nén rồi tự tìm mọi file `.vpk` bên trong và copy vào
> `citadel/addons/`. Xem source `vpk_manager/operations.rs`:
> `collect_vpks_from_dir()` quét đệ quy mọi `.vpk`.

### Bước 4 — Screenshots

GameBanana **bắt buộc tối thiểu 2 ảnh chụp trong game** (rule `R-DL.839`).
Nếu không có, submission sẽ bị withhold.

Cần chụp:
1. Hub / menu chính hiển thị tiếng Việt.
2. Một màn hình khác (VD: shop, hero select, hoặc pause menu).

> **Lưu ý về rule `R-DL.847` (AI-generated content)**: bản dịch của mod này
> được tạo với sự hỗ trợ của AI. Cần **khai báo trung thực** trong description
> rằng bản dịch có sự hỗ trợ của AI. Rule này cấm AI-generated *asset*
> (render, texture, model, ảnh chụp giả), không cấm AI hỗ trợ dịch thuật
> khi được khai báo.

### Bước 5 — Submit

Bấm **Submit**. Thường duyệt trong vài giờ đến 1–2 ngày.

## 4. Sau khi được duyệt

Mod sẽ xuất hiện trong `Deadlock Mod Manager → Mods Store`. Người dùng:

1. Tìm mod trong Mods Store.
2. **Download Mod** → chọn biến thể.
3. **Mods Library** → **Enable**.
4. **Launch modded**.

## 5. Cập nhật mod

Khi có bản dịch mới:

```powershell
python tools/build_release_dmm.py
```

Sau đó vào GameBanana → submission của bạn → **Files** → upload bản mới.
GameBanana cho phép nhiều file; DMM sẽ hiện bản mới trong phần update.

## 6. Xử lý sự cố

| Hiện tượng | Nguyên nhân | Cách sửa |
|---|---|---|
| Mod không hiện trong Mods Store | Chưa được duyệt, hoặc catalog chưa refresh | Đợi duyệt; trong DMM kiểm tra nút refresh catalog |
| Cài xong game không đổi | `gameinfo.gi` chưa có `Game citadel/addons` | DMM tự thêm khi bấm **Launch modded** lần đầu |
| Vẫn thấy tiếng Anh | Đang bật biến thể `vpk-english` nhưng game ở ngôn ngữ khác | Chọn "English" trong Settings > Language |
| Chữ hiện ô vuông □ | Thiếu font | Bản mới đã kèm `fonts.conf`; cần khởi động lại game |
| Steam verify ghi đè mất | `gameinfo.gi` bị Steam khôi phục | Bấm **Launch modded** lại trong DMM |

## 7. Ghi chú kỹ thuật cho người đóng góp

### Vì sao không dùng loose file `.txt`?

DMM chỉ quản lý file `.vpk`. Source code
`apps/desktop/src-tauri/src/mod_manager/vpk_manager/naming.rs`:

```rust
static ENABLED_VPK_PATTERN: LazyLock = LazyLock::new(|| {
    Regex::new(r"^pak(\d+)_dir\.vpk$").expect("enabled VPK regex must be valid")
});
```

Loose file không được hỗ trợ — nên file `.txt` phải nằm trong VPK.

### Vì sao biến thể `vpk-english` đặt tên file là `pak01_dir.vpk`?

DMM tự cấp số `pakNN` khi enable, không giữ tên trong ZIP. Tên trong ZIP
chỉ để dễ nhận biết. Regex `^pak(\d+)_dir\.vpk$` yêu cầu **2 chữ số**,
nên `pak01` hợp lệ, `pak1` thì không.

### Tại sao không dùng `citadel_vietnamese/`?

Vì `Game_UILanguage citadel_*LANGUAGE*` mount **đầu tiên**, thư mục đó
thắng mọi thứ trong `addons/`. Nếu Valve phát hành bản tiếng Việt chính
thức, mod sẽ bị đè. Đặt trong `addons/` tránh được xung đột này.
