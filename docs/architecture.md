# Deadlock Vietnamese Localization — Architecture

## 1. Tổng quan

Dự án gồm hai sản phẩm tách biệt:

- **Localization Toolkit** — ứng dụng C# (.NET 10 LTS) chạy trên máy tính, xử lý dữ liệu.
- **Localization Mod** — sản phẩm cuối cùng (VPK) mà Deadlock đọc được.

Ranh giới này cho phép thay đổi cách đóng gói VPK mà không phải viết lại logic dịch thuật.

### Dependency graph

```
DeadlockVN.CLI  (composition root)
   ├── DeadlockVN.Application   (use cases, orchestration)
   └── DeadlockVN.Infrastructure(implementations: parser, validator, file I/O)
            └── DeadlockVN.Core  (entities, interfaces, common helpers)
```

`Application` **không** tham chiếu `Infrastructure`. Composition root (`CLI`) kết nối cả hai.

---

## 2. Định dạng localization của Deadlock

Nguồn: `Deadlock/game/citadel/resource/localization/`, định dạng **Valve KeyValues (KV1)**.

### 2.1. Cấu trúc file

```
"lang"
{
	"Language"		"english"
	"Tokens"
	{
		"Citadel_Ability_bull_charge"		"Shoulder Charge"
		...
	}
}
```

Một số file dùng tên gốc khác, ví dụ `citadel_vdata/accolades/accolades_english.txt` dùng `"accolades.vdata"`.

### 2.2. Encoding

| Nhóm | Encoding |
|---|---|
| 10/11 file | UTF-8 **có BOM** |
| `citadel_vdata/accolades/accolades_english.txt` | UTF-8 **không BOM** |

Khi ghi file output, phải giữ BOM cho các file vốn có BOM (không áp dụng đồng loạt).

### 2.3. Số liệu baseline (build đã kiểm tra)

| File | Entries | Placeholder distinct |
|---|---|---|
| citadel_generated_vo | 78 438 | 0 |
| citadel_main | 5 331 | 625 |
| citadel_heroes | 3 983 | 1 049 |
| citadel_attributes | 2 024 | 241 |
| citadel_mods | 623 | 109 |
| citadel_gc_mod_names | 412 | 0 |
| citadel_gc_hero_names | 299 | 0 |
| citadel_gc | 222 | 4 |
| citadel_vdata/accolades | 62 | 49 |
| citadel_patch_notes | 34 | 0 |
| citadel_seasonal | 29 | 10 |
| **Tổng** | **91 457** | **808** |

Không file nào có localization key trùng lặp.

### 2.4. Những điểm "bẩn" trong định dạng — BẮT BUỘC biết

Đây là các điểm đã kiểm chứng trên dữ liệu thật. Mọi parser/exporter phải xử lý được.

#### a) Nhiều cặp key/value trên cùng một dòng

```
"EvasionWhileSliding_postvalue_label"  "Evasion While Sliding"   "EvasionWhileSliding_postfix"  "%"
```

→ Một dòng có thể chứa **nhiều** cặp key/value.

#### b) Value nằm ở dòng kế tiếp

```
"ability_unicorn_luminousstrike_t2_desc"  "{s:AbilityCooldown}s Cooldown"						
"ability_unicorn_luminousstrike_t3_desc"  "+{s:MagicIncreasePerStack}% Spirit Amp"
```

→ Regex theo dòng sẽ bỏ sót. Phải parse theo luồng token.

#### c) Value có newline thật bên trong

```
"Citadel_PatchNotes_HeroLabs_hero_astro_1" "<b>10/24/2024</b><br>
	<li>Hero Added to Hero Labs</li>"
```

#### d) Key chứa dấu `/`

237 key dạng `"upgrade_burst_fire/modifier_burst_fire_buff"`.

> **Cảnh báo thiết kế:** đây là key **phẳng**, không phải phân cấp. Khi làm translation database (M3),
> **không được dùng `/` làm dấu phân cách path**, nếu không sẽ nhập nhằng key thật với key đường dẫn.

#### e) Comment

- `//` xuống dòng
- `/* ... */` (không thấy trong dữ liệu hiện tại nhưng parser hỗ trợ)

#### f) Escape

| Escape | Số lần | File |
|---|---|---|
| `\"` | 2 398 | citadel_heroes |
| `\"` | 694 | citadel_mods |
| `\n` (literal) | 99 | citadel_main |
| `\'` | 2 | citadel_main |

#### g) Chữ Nhật trong bản gốc

- `citadel_heroes` / `hero_yamato_lore`: lore Nhật (kana + kanji)
- `citadel_main`: các keybind Nhật (`全`, `半`, `角`, `変`, `換`, `無`, kana)

→ Game hiển thị được CJK. **Không được báo động** cho các ký tự này.

---

## 3. Token runtime (placeholder)

Có **3 nhóm** token, tất cả đều phải được giữ nguyên khi dịch.

### 3.1. Braced — `{...}`

| Prefix | Số lần | Ví dụ |
|---|---|---|
| `s` | 440 | `{s:sign}`, `{s:SlowPercent}` |
| `g` | 118 | `{g:citadel_inline_attribute:'Pulling'}` |
| `d` | 88 | `{d:buildNumber}` |
| `i` | 69 | `{i:item_level}` |
| `plural` | 62 | `{num_commends, plural, one{player} other{players}}` |
| `t` | 23 | `{t:s:popular_items_updated_timestamp}` |
| `(none)` | 4 | `{%param1%}`, `{%param2%}`, `{#}` |
| `f` | 4 | `{f:1:damage_meter_duration}` |

**Lưu ý quan trọng:**

- Token plural có thể **chứa token lồng nhau**:
  `{num_damage_rows, plural, one{Top {i:num_damage_rows}} other{x}}`
  → Phải quét cân bằng dấu ngoặc, **không dùng regex đơn giản**.
- `{#}` là token thật (boon number).
- Trong `{g:citadel_inline_attribute:'Pulling'}`, phần `'Pulling'` là **key tra cứu**, KHÔNG phải text hiển thị.
  Game resolve: `Pulling` → key `InlineAttribute_Pulling` → value `"pulling"`.
  → **Người dịch dịch key `InlineAttribute_Pulling`, không dịch inner text.**

### 3.2. Printf — `%s1`, `%s2`, ...

6 token distinct: `%s1` … `%s6`. Chỉ xuất hiện trong `citadel_gc` và `citadel_main`.

### 3.3. Percent-name — `%partner%`, `%param0%`

4 token distinct.

---

## 4. Thẻ HTML / Panorama

Thẻ HTML/Panorama **thật** (đếm trên dữ liệu thật):

| Thẻ | Số lần |
|---|---|
| `span` | 1 476 |
| `br` | 1 029 |
| `li` | 166 |
| `i` | 155 |
| `b` | 76 |
| `panel` | 47 |
| `a` | 1 |

### 4.1. Thẻ ngữ cảnh trong VO — KHÔNG phải HTML

File VO chứa các "thẻ" chú thích ngữ cảnh:

```
<battle cry>
<laughs>
<None>
<in>
<maurice>
```

Đây **không phải HTML**. Chúng có dạng `<từ>` hoặc `<từ từ>`.

> **Quyết định thiết kế:** validator dùng **whitelist thẻ** (`span`, `br`, `b`, `i`, `li`, `panel`, `a`, …),
> không dùng heuristic "thẻ nào có `<>`". Dùng heuristic sẽ tạo hàng trăm báo động giả.

### 4.2. Bản gốc có HTML không cân bằng

Một số value trong bản gốc có thẻ thừa, ví dụ:

```
"citadel_ability_bull_heal_t3_desc"  "<span class=\"highlight\">+{s:Radius}m</span> Radius and <span class=\"highlight\">+{s:DPS}</span> DPS with improved Spirit scaling</span>"
```

→ **Bản gốc có `</span>` thừa.**

> **Quyết định thiết kế:** validator **so sánh cấu trúc thẻ giữa bản gốc và bản dịch**,
> không kiểm tra cân bằng tuyệt đối. Nếu bản gốc thừa 1 `</span>` thì bản dịch cũng được phép thừa 1 `</span>`.

---

## 5. Validator (Milestone 4)

### 5.1. Nguyên tắc cốt lõi

> **Validator SO SÁNH bản dịch với bản gốc, không áp dụng luật tuyệt đối.**

Lý do: dữ liệu gốc có nhiều trường hợp "bẩn" hợp lệ (thẻ ngữ cảnh, HTML không cân bằng,
tiếng Nhật, token không chuẩn). Check tuyệt đối sẽ tạo hàng trăm báo động giả và
người dịch sẽ bỏ qua tool.

### 5.2. Các rule

| Rule | Severity | Mô tả |
|---|---|---|
| `MissingTranslation` | Error | Bản dịch rỗng, bản gốc không rỗng |
| `Untranslated` | Warning | Bản dịch trùng nguyên văn bản gốc |
| `MissingPlaceholder` | Error | Thiếu token trong bản dịch |
| `ExtraPlaceholder` | Error | Thừa token không có trong bản gốc |
| `PlaceholderCountMismatch` | Error | Cùng token nhưng khác số lần xuất hiện |
| `PrintfTokenMismatch` | Error | Tập token printf-style không khớp |
| `UnbalancedHtmlTag` | Error | Cấu trúc thẻ HTML/Panorama không khớp |
| `UnsupportedCharacter` | Warning | Ký tự ngoài tập hỗ trợ |
| `ControlCharacter` | Error | Ký tự điều khiển không hợp lệ |
| `LeadingOrTrailingWhitespace` | Warning | Khoảng trắng thừa đầu/cuối |
| `DuplicateKey` | Error | Key trùng lặp trong cùng tập |
| `ExcessiveLength` | Warning | Bản dịch > 2.0x hoặc > +120 ký tự so với bản gốc |

### 5.3. Tập ký tự được hỗ trợ

- ASCII
- Latin-1 Supplement (`U+00A0`–`U+00FF`)
- Latin Extended-A/B (`U+0100`–`U+017F`)
- **Latin Extended Additional (`U+1E00`–`U+1EFF`)** — phần lớn dấu tiếng Việt dùng dạng precomposed
- Kana (`U+3040`–`U+30FF`)
- CJK Ideographs (`U+4E00`–`U+9FFF`)
- CJK Symbols & Punctuation (`U+3000`–`U+303F`)
- Halfwidth/Fullwidth Forms (`U+FF66`–`U+FF9F`)
- Một số ký hiệu: `…` `“ ”` `–` `—` `→` `°` `¥` `€` `·` `•` `×` `÷` `±` `≤` `≥` `©` `®` `™` `¡` `¿`

### 5.4. Self-check

```powershell
dotnet run --project src/DeadlockVN.CLI -- validate --self-check
```

Dùng bản gốc tiếng Anh làm "bản dịch" để chứng minh validator **không báo lỗi nghiêm trọng**.
Đây là bài kiểm tra tự tham chiếu — phải PASS trước khi tin validator.

Kết quả hiện tại: **0 Error** trên 91 457 entry.

---

## 6. Translation Management (Milestone 3)

### 6.1. Định danh entry — cặp (file, key)

> **Định danh đầy đủ của một entry là cặp `(SourceFile, Key)`, KHÔNG phải key đơn thuần.**

Lý do: key `Language` xuất hiện ở **10 file localization khác nhau** trong game.
Đếm trên toàn bộ dữ liệu:

| Chỉ số | Giá trị |
|---|---|
| Tổng entry (11 file) | 91 457 |
| Key distinct (bỏ qua file) | 91 448 |
| Key trùng giữa các file | đúng 1 (`Language`, xuất hiện 10 lần) |
| Trùng **cùng file** | 0 |

Hệ quả trực tiếp:
- Bộ bản dịch phải định danh entry bằng cặp (file, key).
- Rule `DuplicateKey` của validator so sánh theo **cặp (file, key)**, không theo key.

`SourceFile` luôn dùng dấu `/` và là đường dẫn **tương đối** so với thư mục nguồn
(vd. `citadel_main/citadel_main_english.txt`) → không phụ thuộc hệ điều hành.

### 6.2. Phân nhóm nội dung

| Nhóm | Entries | Nguồn |
|---|---|---|
| `Voice` | 78 438 | `citadel_generated_vo` |
| `Ui` | 8 046 | `citadel_attributes`, `citadel_main`, `citadel_gc`, `citadel_mods`, `citadel_vdata` |
| `Abilities` | 3 983 | `citadel_heroes` |
| `Items` | 619 | `citadel_gc_mod_names`, `citadel_mods` |
| `Heroes` | 308 | `citadel_gc_hero_names` |
| `Misc` | 63 | `citadel_patch_notes`, `citadel_seasonal` |

### 6.3. Cấu trúc lưu trữ — tách theo nhóm, VO tách theo hero

Một file JSON gộp toàn bộ 91 457 entry nặng **~31 MB** — không quản lý được bằng git,
và không chia được việc cho nhiều người.

> **Quyết định thiết kế:** lưu **tách theo nhóm**, và nhóm `Voice` tách **tiếp theo hero**.

```
workspace/translations/
├── abilities/abilities.json        1.44 MB   3 983 entry
├── heroes/heroes.json              88.1 KB     308 entry
├── items/items.json                246.2 KB    619 entry
├── misc/misc.json                  46.8 KB      63 entry
├── ui/ui.json                      2.47 MB   8 046 entry
└── voice/
    ├── _common.json                1.07 MB   2 688 entry  (không thuộc hero nào)
    ├── artist.json                 690.5 KB  ~2 000 entry
    ├── gigawatt.json               683.9 KB
    └── ... (44 hero khác)
```

Tổng: **50 file**, mỗi file 0.3–2.5 MB → git diff được, một hero = một đơn vị
công việc dịch tự nhiên.

**Cách xác định hero của entry VO:** key VO có dạng `<hero>_<...>` (vd `gigawatt_kill_01`).
Danh sách hero lấy từ nhóm `Heroes` qua key `hero_<slug>:n`. Khớp theo **tiền tố dài nhất**
để tránh nhầm giữa các tên có chung tiền tố (`artist` vs `artillery`), và yêu cầu
có dấu `_` ngay sau để `haze` không khớp `hazelnut`.

Kết quả khớp: 75 750 / 78 438 entry VO (96.6%) thuộc hero; 2 688 entry còn lại
(announcer, `Language`, …) vào `_common`.

### 6.4. Vòng đời của một entry

```csharp
Pending  ──(người dịch bắt đầu)──▶  Translating
                                        │
                                   (nội dung gốc đổi)
                                        ▼
Pending  ──(game update)──▶  Reviewed  ──(duyệt)──▶  Approved
```

Mỗi entry lưu 2 bản nội dung gốc:

| Trường | Ý nghĩa |
|---|---|
| `originalText` | Nội dung gốc **hiện tại** (cập nhật mỗi lần sync) |
| `originalTextSnapshot` | Nội dung gốc tại lúc bản dịch được tạo |

`IsStale = (originalText != originalTextSnapshot)` → bản dịch có thể đã lỗi thời.

### 6.5. Đồng bộ với game update

```powershell
dotnet run --project src/DeadlockVN.CLI -- sync --split
```

| Kết quả | Hành động |
|---|---|
| `Added` | Tạo entry `Pending` mới |
| `Modified` | Cập nhật `originalText`; nếu đang `Approved` → hạ xuống `Reviewed` (bắt buộc review lại) |
| `Removed` | Báo cáo key không còn trong game (không xoá khỏi bộ dịch) |

### 6.6. Import — không mất công sức dịch

```powershell
dotnet run --project src/DeadlockVN.CLI -- import <file-or-dir> --target workspace/translations --split
```

- Bản dịch đã có (không rỗng) → **giữ nguyên**, ghi vào danh sách `Skipped`.
- Bản dịch còn trống → điền từ file nguồn.
- Key chưa tồn tại → thêm mới.
- `--overwrite` → ghi đè cả bản dịch đã có.

Nguồn có thể là **một file** (người dịch gửi file) hoặc **một thư mục** (giao nhánh).

### 6.7. Định dạng file bản dịch

```json
{
  "metadata": {
    "sourceLanguage": "English",
    "targetLanguage": "Vietnamese",
    "version": "0.1.0",
    "updatedAt": "2026-11-16T10:32:46.0004554+00:00"
  },
  "entries": [
    {
      "key": "upgrade_close_range",
      "sourceFile": "citadel_gc_mod_names/citadel_gc_mod_names_english.txt",
      "originalText": "Close Quarters",
      "translatedText": "Cận Chiến",
      "status": "Approved",
      "category": "Items",
      "originalTextSnapshot": "Close Quarters"
    }
  ]
}
```

- camelCase, UTF-8 **không BOM** (đây là tài liệu nội bộ, không phải file game).
- `metadata` + `entries` → tương thích ngược với `validate` (đọc được cả mảng thuần).

### 6.8. CLI

```
deadlockvn export  [--source <dir>] [--output <dir-or-file>] [--split]
deadlockvn import  <file-or-dir> [--target <file-or-dir>] [--overwrite] [--split]
deadlockvn sync    [--source <dir>] [--output <file-or-dir>] [--split]
deadlockvn report  [<file-or-dir>] [--split]
deadlockvn validate <file-or-dir>
```

`validate` nhận cả file lẫn thư mục.

---

## 7. Localization Exporter (Milestone 5)

### 7.1. Nguyên tắc cốt lõi — chỉ thay VALUE, tại chỗ

> **Không serialize lại cây KeyValues. Thay đúng đoạn byte của value trong file gốc.**

Lý do: file game có **comment**, **thụt lề tab**, **thứ tự escape**, **BOM**, và
**newline thật bên trong value**. Serialize lại sẽ mất hoặc làm lệch những thứ này,
dẫn tới file không giống bản gốc và rất khó truy vết khi game không nhận.

Cơ chế:

1. Đọc **byte thô** file gốc (không dùng `File.ReadAllTextAsync` — hàm này
   **tự strip BOM**, làm mất thông tin BOM cần giữ).
2. Lexer ghi lại `Start`/`Length` (offset) của **mỗi token** trong chuỗi nguồn.
3. Parser gom offset đó cho **từng value** (`KeyValuesSpan`).
4. Gom tất cả thay thế (value đã dịch + header `Language`) thành **một danh sách**,
   áp dụng **từ cuối về đầu** để không lệch offset.
5. Ghi lại byte, **giữ nguyên trạng thái BOM** của file gốc.

### 7.2. Kết quả kiểm chứng trên dữ liệu thật

| Kiểm tra | Kết quả |
|---|---|
| Round-trip 11 file gốc (không dịch) | **byte-identical 11/11** |
| BOM giữ nguyên | 11/11 (10 file có BOM, `accolades` không) |
| Xuống dòng CRLF giữ nguyên | 11/11 |
| Chỉ thay đúng value cần dịch | 3 dòng thay đổi (1 header + 2 bản dịch mẫu) |

### 7.3. Quy tắc escape của KV1

Đối chiếu với 11 file gốc + 29 bản dịch chính thức của game:

| Escape | Ý nghĩa | Số lần trong file gốc |
|---|---|---|
| `\"` | dấu nháy kép | 3 700 |
| `\\` | backslash | 0 |
| `\n` | **newline thật** | 88 value (2 file) |
| `\'` | escape lạ, giữ nguyên | 2 |

> **Engine Valve giải mã `\n` thành newline thật khi nạp file.**
> Bằng chứng: **17/29** bản dịch chính thức của game vẫn giữ `\n` trong
> `citadel_heroes` — nếu engine không decode, game sẽ hiển thị chữ `"n"`
> trong tooltip. Vì vậy lexer **decode** `\n` → newline, và escaper **encode**
> newline → `\n` khi ghi.

### 7.4. Header `Language`

Chỉ key `Language` **ở cấp gốc** (ngay dưới node gốc, thường là `"lang"`) được
đổi sang ngôn ngữ đích. Key `Language` sâu hơn (ví dụ `citadel_settings_language`)
và các key trùng tên khác **không bị đụng tới**.

Header này là **metadata**, không phải nội dung cần dịch → không tính vào
`TotalEntries` / `MissingKeys`.

### 7.5. Guard key lạ

Key có trong bản dịch nhưng **không tồn tại** trong file gốc → báo `UnknownKeys`
và CLI trả mã lỗi `1`. Đây là dấu hiệu dịch nhầm phiên bản game.

### 7.6. CLI

```powershell
dotnet run --project src/DeadlockVN.CLI -- export-to-game `
    --source  workspace/source/original `
    --output  workspace/staging/localization `
    --split
```

Kết quả: 11 file `*_vietnamese.txt` giữ nguyên cấu trúc thư mục của game,
sẵn sàng đóng gói VPK bằng VPKEdit (Milestone 6).

---

## 8. Mod Packaging & Game Config Patching (Milestone 6 — Sprint 4 Ngày 2)

### 8.1. Sự thật về bản cài game — quyết định thiết kế

Khảo sát bản cài thật tại `E:\Steam\steamapps\common\Deadlock\game\citadel`
đã xác nhận 5 điều quyết định toàn bộ thiết kế packaging:

| # | Sự thật | Bằng chứng |
|---|----------|-------------|
| 1 | **File localization nằm LOOSE, không nằm trong VPK** | `pak01_dir.vpk` chứa **0** chuỗi `citadel_main`; 11 file nằm tại `game/citadel/resource/localization/` |
| 2 | Game đọc file loose qua search path `MOD`/`GAME` = `game\citadel\` | `console.log` → `[Filesystem] MOD "…\game\citadel\"` |
| 3 | `gameinfo.gi` khai báo **17** ngôn ngữ, **không có** `vietnamese` | Đọc khối `SupportedLanguages` |
| 4 | **Font chính thiếu 86/176 mã tiếng Việt** | `valveoracle-medium.ttf` thiếu `ơ ư ờ ở ị ọ ộ ụ ữ` |
| 5 | Game **không có** font "Noto Sans" tổng quát | Chỉ có `notosansjp/kr/sc/tc/thai` |

**Hệ quả:**

- Chép file rời là đủ — đóng gói VPK **không bắt buộc** (vẫn làm được vì
  VPK được mount ở `GAME` search path).
- Font fallback là **bắt buộc**, trỏ `Noto Sans JP` (phủ 176/176 mã).
- Game **đã có sẵn** `*_vietnamese.txt` dạng stub rỗng (60 byte) → mod ghi
  đè các stub này.

### 8.2. Nguyên tắc vá file cấu hình

> **Chỉ CHÈN dòng mới. Không sửa, không xoá dòng cũ. Luôn tạo `.bak` trước.**

`gameinfo.gi` và `fonts.conf` của game dùng **CRLF** và **không BOM** — vá
cũng phải giữ nguyên đặc tính này.

| File | Thay đổi |
|---|---|
| `gameinfo.gi` | +1 dòng `"vietnamese" "3"` trong khối `SupportedLanguages` |
| `fonts.conf` | +12 dòng: 1 khối `<match>` fallback `vi-vn` → `Noto Sans JP` |

Patcher tự phát hiện: thụt lề (tab), ký tự phân tách key/value (space
hoặc tab), ký tự xuống dòng (CRLF hoặc LF), BOM (có/không).

### 8.3. Font coverage checker

`FontCoverageChecker` đọc bảng `cmap` của font TrueType/OpenType để kiểm tra
phủ glyph — **big-endian**, hỗ trợ format 0/4/6/12.

Bộ ký tự tiếng Việt đầy đủ: **176 mã** (29 chữ thường + 29 chữ hoa +
59 + 59 tổ hợp nguyên âm có dấu).

Lệnh `package` **tự động kiểm tra mọi họ font** khai báo trong
`FontFallbackConfig.BaseFontFamilies` trước khi vá `fonts.conf`, và **chỉ
vá cho họ font thực sự thiếu glyph**. Đo thật trên bản cài game:

| Họ font | Phủ | Thiếu |
|---------|-----|-------|
| `VALVEOracle` | 90/176 | **86** |
| `VALVEPulp` | 90/176 | **86** |
| `Retail Text Demo` | 45/176 | **131** |
| `Retail Demo` | 45/176 | **131** |
| `Noto Sans JP/KR/SC/TC`, `Hahmlet`, `Radiance`, `Reaver` | 176/176 | 0 |

Điều này tránh sửa file game khi không cần thiết, đồng thời bảo đảm mọi
họ font thiếu glyph đều được vá (không chỉ font chính).

### 8.4. Cấu trúc gói mod

```
workspace/staging/mod/
└── resource/localization/
    ├── citadel_main/citadel_main_vietnamese.txt
    ├── ... (11 thư mục)
```

Tiền tố `resource/localization/` là **bắt buộc** — đây chính là đường dẫn
trong VPK. Chọn sai → game không đọc file.

### 8.5. CLI

```powershell
dotnet run --project src/DeadlockVN.CLI -- package `
    --source       workspace/source/original `
    --output       workspace/staging/mod `
    --translations workspace/translations `
    --game         "E:\Steam\steamapps\common\Deadlock\game"
```

| Cờ | Tác dụng |
|---|---|
| `--game <dir>` | Bật vá `gameinfo.gi` + `fonts.conf` (trỏ tới thư mục `game`) |
| `--no-patch-gameinfo` | Bỏ qua vá `gameinfo.gi` |
| `--no-patch-fonts` | Bỏ qua vá `fonts.conf` |
| `--language <name>` | Đổi tên ngôn ngữ (mặc định `vietnamese`) |

Chi tiết đóng gói VPK bằng VPKEdit / DMM: xem phần *Đóng gói VPK* trong
tài liệu trên, hoặc chạy `dotnet run --project src/DeadlockVN.CLI -- package`.

---

## 8. Nguyên tắc kỹ thuật đã chốt

1. Không sửa file gốc của Deadlock.
2. Không thay đổi localization key.
3. Luôn lưu bản tiếng Anh gốc.
4. Không để mất placeholder.
5. Không build VPK từ dữ liệu chưa được validate.
6. Mỗi bản phát hành phải có version.
7. Mỗi lần game update phải có quy trình diff.
8. Định danh entry là cặp `(file, key)`, không phải key đơn thuần.
9. Import không bao giờ ghi đè bản dịch đã có (trừ khi có `--overwrite`).
10. Xuất ra file game chỉ thay **value**, tại chỗ — không serialize lại cây.
11. Giữ nguyên BOM của file gốc khi xuất.
12. Vá file cấu hình game chỉ **chèn dòng mới**, không sửa/xoá dòng cũ.
13. Luôn tạo `.bak` trước khi sửa file cấu hình của game.
14. Chỉ vá `fonts.conf` khi font chính **thực sự thiếu** glyph.
15. Đường dẫn trong gói mod phải là `resource/localization/...` — sai là game không đọc.

## 9. Nguồn tham khảo

- [Change item names and other text — Divine Skins Wiki](https://wiki.divineskins.gg/en/docs/deadlock/ui/change-text)
  - Xác nhận: chỉ đổi **value**, giữ nguyên **key** và **cấu trúc file**.
  - Mỗi item có 2 key: `upgrade_<name>` (tên) và `upgrade_<name>_search` (từ khoá tìm kiếm).
  - File text trong VPK **không cần compile**, chỉ cần đúng đường dẫn.
