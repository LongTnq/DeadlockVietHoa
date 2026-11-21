# Hướng dẫn dịch tự động

Deadlock có ~91 500 chuỗi localization. Dịch tay toàn bộ là việc hàng
tháng việc — vì vậy dự án có pipeline dịch tự động bằng model ngôn ngữ
chạy **cục bộ** (Ollama), kết hợp duyệt thủ công.

> **Nguyên tắc:** bản dịch máy luôn ở trạng thái `Reviewed` — **chưa** bao
> giờ ở trạng thái `Approved`. Người chơi chỉ nhận bản đã được con người
> duyệt.

---

## 1. Cài đặt Ollama

```powershell
winget install --id Ollama.Ollama -e
```

Khởi động server (Ollama tự chạy nền sau khi cài, nếu chưa):

```powershell
ollama serve
```

Tải model:

```powershell
ollama pull qwen2.5:7b-instruct
```

### Yêu cầu phần cứng

| Model | VRAM | RAM | Ghi chú |
|-------|------|-----|---------|
| `qwen2.5:7b-instruct-q4_K_M` | 4 GB | 16 GB | **Khuyên dùng** — chạy được trên GTX 1650 4GB |
| `qwen2.5:3b-instruct` | ~2 GB | 8 GB | Nhanh hơn, chất lượng kém hơn nhiều |
| `qwen2.5:14b-instruct` | 8 GB+ | 32 GB | Chất lượng tốt hơn nhiều |

> **Lưu ý về model 3B:** đo thực tế trên GTX 1650 cho thấy 3B dịch thiếu
> chính xác (~30% chuỗi bị bỏ nguyên tiếng Anh, có lúc lẫn tiếng Trung).
> Pipeline có cơ chế phát hiện và dịch lại, nhưng tốn thời gian gấp đôi.
> Nên dùng 7B nếu có thể.

Trên máy không có GPU rời, model vẫn chạy nhưng **rất chậm** (~1-2 giây
mỗi chuỗi). Cân nhắc dùng `--batch` nhỏ hơn và chạy qua đêm.

> **Lưu ý về model 7B:** bản `qwen2.5:7b-instruct` mặc định dùng định dạng
> `fp16` (~15 GB) — vượt xa VRAM 4 GB của GTX 1650 và sẽ nhảy sang CPU,
> cực chậm. Hãy tải bản lượng tử thấp:
>
> ```powershell
> ollama pull qwen2.5:7b-instruct-q4_K_M
> ```
>
> Bản này ~4.7 GB, vừa đủ nạp vào VRAM 4 GB.

---

## 2. Chạy dịch tự động

### 2.1 Xem trước khối lượng công việc

```powershell
dotnet run --project src/DeadlockVN.CLI -- auto-translate --dry-run --categories heroes,items,misc
```

```
Số entry cần dịch: 990

  Heroes        308
  Items         619
  Misc           63

Ước tính số lô: 33 (batch 30)
```

### 2.2 Chạy thật

```powershell
dotnet run --project src/DeadlockVN.CLI -- auto-translate --categories heroes,items,misc
```

Tiến độ được in ra sau mỗi lô, và cơ sở dữ liệu được **lưu định kỳ** — nên
dừng bằng `Ctrl+C` không mất công sức.

Chạy lại lệnh sẽ **tiếp tục** từ chỗ dừng: các entry đã dịch (status
`Reviewed`) được bỏ qua, chỉ entry còn `Pending` mới được dịch lại.

### 2.3 Các tuỳ chọn

| Tuỳ chọn | Ý nghĩa |
|----------|---------|
| `--categories <list>` | Giới hạn nhóm nội dung. Mặc định: tất cả **trừ** `voice` |
| `--batch <n>` | Số chuỗi mỗi lô (mặc định 30) |
| `--retries <n>` | Số lần thử lại khi bản dịch hỏng (mặc định 3) |
| `--timeout <n>` | Timeout mỗi lời gọi provider, giây (mặc định 120) |
| `--max-tokens <n>` | Token tối đa mỗi lời gọi (mặc định 2048) |
| `--model <name>` | Model khác |
| `--endpoint <url>` | Ollama ở máy khác trong LAN |
| `--provider <name>` | Provider khác (hiện mới có `ollama`) |
| `--dry-run` | Chỉ báo khối lượng, không gọi provider |
| `--split` | Dùng bộ bản dịch tách theo nhóm |

### 2.4 Tham số khuyên dùng cho máy yếu

Đo thực tế trên GTX 1650 4GB với `qwen2.5:7b-instruct-q4_K_M`:

| Tham số | Giá trị | Lý do |
|---------|---------|--------|
| `--model` | `qwen2.5:7b-instruct-q4_K_M` | Bản Q4 vừa đủ VRAM 4 GB |
| `--batch` | 25 | Lô lớn hơn thì model hay bỏ sót mục |
| `--max-tokens` | 2048 | Chuỗi mô tả kỹ năng khá dài |
| `--timeout` | 120 | Lô có chuỗi dài có thể mất 1-2 phút |

Tốc độ đo được: **~5 token/giây** (model 7B chạy một phần trên CPU do VRAM
4 GB không đủ). Với 990 chuỗi ngắn cần khoảng 25-30 phút.

> **Lưu ý:** lô 25 chuỗi có thể mất 1-2 phút — đó là bình thường, không
> phải treo. Tiến độ được in ra sau **mỗi lô** nên nếu không thấy gì
> thêm trong vài phút thì vẫn đang chạy.

#### Giới hạn cứng về kích thước lô

Provider **không** chỉ giới hạn theo số lượng chuỗi mà còn giới hạn theo
**tổng số ký tự** (`MaxCharsPerBatch = 1200`). Lý do:

- Ollama dùng `context_length` mặc định **4096 token**. Lô 25 chuỗi dài
  (mô tả kỹ năng, lore hero) có thể vượt quá cửa sổ này → model **không
  trả lời**, request treo.
- Chuỗi trên **600 ký tự** được tách thành câu trước khi gửi
  (`SentenceSplitter`), rồi ghép lại sau khi dịch. Model 7B bắt đầu
  **tóm tắt thay vì dịch** khi đoạn văn dài hơn ~1.000 ký tự.
- **Đừng tăng `num_ctx`**: KV cache cho context lớn chiếm VRAM, đẩy phần
  model xuống CPU và tốc độ giảm mạnh. Cách đúng là chia nhỏ lô.

> **Lưu ý:** nếu tiến trình dịch dừng im nhiều phút mà GPU vẫn 100%,
> hãy kiểm tra `ollama ps` — nếu `context_length` là 8192 thì còn sót
> cấu hình cũ. Restart Ollama để reset.

### 2.5 Vì sao mặc định bỏ qua `voice`

Lời thoại cần giữ giọng điệu, nhịp điệu và cách nhân vật nói. Bản dịch máy
gần như luôn hỏng phần này — nên nhóm `voice` bị loại khỏi mặc định, và phải
dịch tay.

---

## 3. Cách pipeline bảo vệ bản dịch

### 3.1 Tách chuỗi thành "văn bản" và "phần cố định"

Đây là **lớp bảo vệ quan trọng nhất** của pipeline.

Chuỗi localization của Deadlock trộn lẫn văn bản tiếng Anh với hai loại
thứ **không được phép dịch**:

- **Token runtime**: `{s:Damage}`, `{g:citadel_inline_attribute:'MoveSpeed'}`,
  `{%param%}`, `{#}`, `%s1`, `%partner%`
- **Thẻ HTML/Panorama**: `<span class="highlight">`, `<br>`, `<b>`, `<li>`,
  `<panel>` (kể cả thẻ ngữ cảnh trong VO như `<laughs>`)

Model ngôn ngữ nhỏ rất dễ phá hỏng những thứ này:

| Lỗi thường gặp | Hậu quả trong game |
|-----------------|--------------------|
| Xóa thẻ `<span>` | Mất highlight, mất xuống dòng, vỡ layout |
| Đảo thứ tự thẻ (đóng trước mở) | Render sai, có thể crash panel |
| Tự bịa placeholder (`{s:shock}` từ chữ "shock") | Hiển thị sai số liệu |

Vì vậy pipeline **không gửi thẻ và token cho model**. Thay vào đó
`TextPartSplitter` tách chuỗi thành hai loại phần:

```
Gốc:  Your next <span class="highlight">Melee</span> attack heals you.
       └─ Văn bản ─┘ └────── Phần cố định ──────┘ └─ Văn bản ─┘
Gửi:   ["Your next ", " attack heals you."]     ← model không thấy thẻ nào
Nhận:   ["Lần tới ", " đòn đánh cận chiến hồi phục cho bạn."]
Ghi:   Lần tới <span class="highlight">đòn đánh cận chiến</span> đòn đánh hồi phục cho bạn.
       └────── Phần cố định ghép lại NGUYÊN VẸN ──────┘
```

Bằng cách này model **không bao giờ có cơ hội** xóa thẻ hay bịa token —
không phải "dặn model đừng làm", mà là loại bỏ khả năng làm.

**Cam kết:** ghép lại các phần luôn cho ra đúng chuỗi gốc, từng ký tự
(đã có test `Split_RoundTrip_ReconstructsOriginal`).

**Token plural** được xử lý đặc biệt: khung token và tên biến cố định,
nhưng phần văn bản bên trong nhánh (`one{...}`/`other{...}`) vẫn được dịch:

```
Gốc:  {num_kills, plural, one{kill} other{kills}}
Gửi:   ["kill", "kills"]     ← chỉ phần chữ
Ghi:  {num_kills, plural, one{hạ gục} other{hạ gục}}
```

### 3.2 Guard — kiểm tra bản dịch

Mọi bản dịch phải qua `TranslationGuard` trước khi được ghi. Guard kiểm tra:

| Kiểm tra | Hậu quả khi fail |
|----------|------------------|
| Placeholder thiếu / thừa / đổi tên | ❌ Chặn — thử lại |
| Placeholder xuất hiện sai số lần | ❌ Chặn — thử lại |
| Cấu trúc thẻ HTML vỡ | ❌ Chặn — thử lại |
| Bản dịch rỗng | ❌ Chặn — thử lại |
| Bản dịch vẫn là tiếng Anh (bị bỏ sót) | ❌ Chặn — thử lại |
| Bản dịch gần như toàn tiếng Anh (không có chữ cái tiếng Việt nào) | ❌ Chặn — thử lại |
| Chứa ký tự CJK (Trung Quốc/Nhật) trong khi bản gốc không có | ❌ Chặn — thử lại |
| Dịch nhầm sang tiếng Pháp | ❌ Chặn — thử lại |
| Mất ký hiệu số học (`+`, `-`, `%`, `=`) | ❌ Chặn — thử lại |
| Mất số có trong bản gốc | ❌ Chặn — thử lại |
| Ký tự không hiển thị được | ⚠️ Cảnh báo |
| Bản dịch trùng nguyên văn (ngắn) | ⚠️ Cảnh báo |
| Mất dấu câu | ⚠️ Cảnh báo |

**Bản dịch hỏng không bao giờ được ghi vào cơ sở dữ liệu.** Nó vẫn ở trạng
thái `Pending` để dịch lại sau. Ghi bản dịch hỏng vào DB sẽ làm game hiển
thị sai — mất số liệu, vỡ layout.

> **Về luật "tiếng Pháp":** tiếng Việt và tiếng Pháp dùng chung rất nhiều
> ký tự có dấu (`à á â ã è é ê ì í ò ó ô ù ú ý`). Vì vậy danh sách ký tự
> chỉ gồm những ký tự **chỉ** tiếng Pháp có: `ä å æ ç ë î ï ñ õ ö ø û ü ÿ œ`.
> Đưa nhầm `â` hay `ô` vào danh sách sẽ khiến mọi bản dịch tiếng Việt hợp
> lệ đều bị báo nhầm.

> **Về luật "mất ký hiệu":** dấu `-` chỉ được tính khi đi kèm số (`-10`).
> Dấu gạch nối trong tên ("High-Velocity Mag") là chữ, mất đi không sai
> nghĩa nên không tính.

### 3.3 Phát hiện bản dịch bị bỏ sót

Model nhỏ hay bỏ qua một đoạn trong lô — trả về đúng chuỗi gốc. Nếu không
xử lý, game sẽ hiển thị tiếng Anh lẫn trong bản dịch.

`UntranslatedDetector` nhận dạng trường hợp này bằng hai điều kiện:

1. Bản dịch giống hệt bản gốc
2. Bản gốc trông như **câu tiếng Anh**: có từ chức năng (`your`, `the`,
   `deals`…) **và** có ít nhất 2 từ viết thường

Điều kiện thứ hai giải quyết mâu thuẫn: `"Hogarth the Fabulous"` có "the"
nhưng là tên riêng hợp lệ, còn `"Your bullets build up to a Silence"` là
câu tiếng Anh bị bỏ dịch.

Khi phát hiện, provider **dịch lại riêng** chuỗi đó (mỗi lần một — model
tập trung hơn khi lô nhỏ).

Ngoài ra còn một dạng sót khác: bản dịch **khác** bản gốc nhưng gần như toàn
tiếng Anh (model rút gọn câu). Guard phát hiện qua tỷ lệ chữ cái Latin
không dấu > 80% **và** không có chữ cái tiếng Việt nào trong chuỗi.

### 3.4 Vòng lặp thử lại

```
gửi lô → guard → entry lỗi bị đưa sang lô tiếp theo → tối đa 3 lần
```

Sau 3 lần vẫn lỗi → entry giữ nguyên `Pending`, được ghi log để người
dịch xử lý tay.

### 3.5 Xử lý lệch chỉ số — lỗi nguy hiểm nhất của dịch máy

> **Đây là lỗi làm hỏng bản dịch nặng nhất, và là lý do prompt phải yêu
> cầu JSON đánh số thay vì mảng phẳng.**

Khi model trả về **mảng phẳng** và bỏ sót một phần tử ở giữa, ta không biết
phần tử nào bị thiếu. Nếu vẫn ánh xạ theo vị trí, **mọi bản dịch từ vị trí
đó trở đi sẽ bị gán nhầm**.

Ví dụ thực tế đã gặp với batch đầu tiên:

```
Gốc:  [0] "English"        [1] "Extended Magazine"   [2] "Ammo Scavenger"
Dịch:  [0] "Hộp Ammunition XM EM Ext Mag"   ← lấy nhầm của [1]
       [1] "Hộp Ammunition Nâng Cấp"        ← lấy nhầm của [2]
       [2] "Bàn Tay Lượm Ammunition"
```

Kết quả: key `Language` hiển thị "Hộp Ammunition XM EM Ext Mag" trong game.

**Cơ chế xử lý hiện tại (3 lớp):**

1. **Prompt yêu cầu JSON đánh số.** Model phải trả về
   `{"0": "...", "1": "...", "2": "..."}`. Vậy ta biết chính xác chỉ số
   nào bị thiếu, không cần đoán.

2. **Dịch lại đúng phần thiếu.** `FillMissingAsync` gọi lại provider cho
   *chỉ* những chỉ số bị thiếu, mỗi lần một chuỗi — rẻ hơn nhiều so với
   chia đôi cả lô.

3. **Phát hiện lệch bằng "anchor".** Nếu model vẫn trả về mảng phẳng,
   provider so chéo: bản dịch tại vị trí `i` có trùng với chuỗi gốc ở vị trí
   `j < i` không? Nếu có → lô bị loại để chia nhỏ dịch lại.

Ngoài ra, lệnh `revalidate` phát hiện lệch chỉ số **sau khi đã lưu**: nếu
bản dịch của entry này trùng với *nội dung gốc* của một entry khác trong
cùng file, entry đó bị báo là lỗi.

### 3.6 Chia nhỏ lô khi model trả về sai số lượng

Nếu model trả về số phần tử **không khớp** với số chuỗi gửi đi, việc ánh
xạ chỉ số sẽ lệch — bản dịch của chuỗi này bị gán nhầm cho chuỗi khác,
tức là sai hoàn toàn.

Provider xử lý bằng cách **chia đôi lô** cho tới khi khớp (giới hạn độ
sâu 5, tức tối đa 31 lời gọi cho một lô 30 chuỗi). Nếu lô còn 1 chuỗi mà
vẫn lệch → đánh dấu lỗi để service retry.

---

## 3.7 Tham số Ollama quan trọng

Provider **không** bật `format: "json"` của Ollama. Tham số này khiến
model 3B trả về object `{"0": "..."}` thay vì mảng JSON, hoặc lặp token vô
hạn và bị Ollama huỷ với lỗi `token repeat limit reached`.

Thay vào đó provider yêu cầu JSON bằng prompt rồi tự parse. `ModelResponseParser`
hỗ trợ:

- Mảng chuỗi: `["a", "b"]`
- **Mảng object**: `[{"original": "...", "translated": "..."}]` — khi có trường
  `original`, provider **ánh xạ theo văn bản gốc** thay vì theo chỉ số.
- **Object đánh số**: `{"0": "a", "1": "b"}` — đây là **định dạng provider
  yêu cầu** trong prompt. Nhờ vậy biết chính xác chỉ số nào bị thiếu.
- Object có mảng lồng: `{"translations": [...]}`
- Markdown fence: ```` ```json [...] ``` ````

Tham số đang dùng:

| Tham số | Giá trị | Lý do |
|---------|---------|--------|
| `temperature` | 0.2 | Ổn định, tránh sáng tạo quá mức |
| `top_p` | 0.9 | Mặc định hợp lý |
| `repeat_penalty` | 1.15 | Giảm lặp token |
| `num_predict` | 2048 (`--max-tokens`) | Giới hạn độ dài sinh — quá lớn thì model lặp |

---

## 4. Thêm provider khác (Claude API, OpenAI…)

Kiến trúc dựa trên interface `ITranslationProvider`, nên thêm provider mới
chỉ cần:

1. Tạo class mới implement `ITranslationProvider` trong
   `src/DeadlockVN.Infrastructure/Translation/`.
2. Thêm nhánh `case "tên-provider":` trong `AutoTranslateCommand`.

Interface:

```csharp
public interface ITranslationProvider
{
    string Name { get; }

    Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts,
        string? context,
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
```

**Quy tắc bắt buộc:** provider **không** được tự sửa placeholder hay thẻ
HTML. Việc đó là của `TranslationGuard` — nếu provider sửa, không thể phát
hiện bản dịch hỏng.

---

## 5. Quy trình đầy đủ

```
1. auto-translate          → bản dịch máy (status: Reviewed)
2. revalidate              → kiểm tra lại bản dịch đã lưu, hạ entry hỏng
3. review-export           → file JSON cho người dịch
4. người dịch sửa file
5. review-import           → nhập lại, tự đánh dấu Approved nếu có
6. approve --yes           → duyệt hàng loạt phần còn lại
7. validate                → kiểm tra chất lượng
8. export-to-game --split  → sinh file *_vietnamese.txt
9. package --game ...      → đóng gói + vá fonts.conf
10. pack-vpk               → đóng gói VPK
11. copy file vào game/citadel/resource/localization/
```

### 5.1 `revalidate` — kiểm tra lại bản dịch đã lưu

Bộ luật kiểm tra mạnh dần theo thời gian (thêm phát hiện CJK, thêm phát
hiện dịch nhầm sang tiếng Pháp, thêm phát hiện lệch chỉ số…). Những entry
đã dịch bằng bộ luật cũ có thể vi phạm bộ luật mới. Lệnh này tìm và hạ
chúng về `Pending` để dịch lại:

```powershell
# Chỉ báo cáo
dotnet run --project src/DeadlockVN.CLI -- revalidate

# Hạ entry hỏng về Pending để dịch lại
dotnet run --project src/DeadlockVN.CLI -- revalidate --fix

# Chỉ nhóm cụ thể
dotnet run --project src/DeadlockVN.CLI -- revalidate --categories heroes,items --fix
```

Ngoài guard, `revalidate` còn phát hiện **lệch chỉ số**: nếu bản dịch của
một entry trùng với *nội dung gốc* của một entry khác trong cùng file, đó
gần như chắc chắn là dấu hiệu lô dịch bị lệch.

### 5.2 Cài vào game — copy trực tiếp, không cần VPK

> **Phát hiện quan trọng:** file localization của Deadlock **không** nằm
> trong VPK. Chúng nằm trực tiếp trong thư mục
> `game/citadel/resource/localization/<tên>/<tên>_<ngôn_ngữ>.txt`.
> Game tìm file theo mẫu `citadel_*LANGUAGE*` (xem `gameinfo.gi`).

Nên cách cài đơn giản và chắc chắn nhất là copy thẳng 11 file:

```powershell
$src = "workspace\staging\mod\resource\localization"
$dst = "E:\Steam\steamapps\common\Deadlock\game\citadel\resource\localization"

Get-ChildItem $src -Recurse -Filter "*_vietnamese.txt" | ForEach-Object {
    $rel = $_.FullName.Substring((Resolve-Path $src).Path.Length + 1)
    $target = Join-Path $dst $rel
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item $_.FullName $target -Force
}
```

Lệnh `pack-vpk` vẫn hữu ích nếu bạn muốn phát hành mod cho người khác —
đặt VPK vào `game/citadel/addons/`.

---

## 6. Xử lý sự cố

| Triệu chứng | Nguyên nhân | Cách xử lý |
|-------------|-------------|------------|
| `Không kết nối được provider` | Ollama chưa chạy | `ollama serve` |
| `model not found` | Chưa tải model | `ollama pull qwen2.5:7b-instruct-q4_K_M` |
| `token repeat limit reached` | Model lặp — thường do model quá nhỏ hoặc prompt rối | Dùng model 7B, giảm `--max-tokens` |
| Timeout | Model quá chậm (CPU) | Tăng `--timeout`, giảm `--batch` |
| Treo im lặng, GPU vẫn 100% | Lô vượt `context_length` 4096 của Ollama | Restart Ollama, kiểm tra `ollama ps` (context phải là 4096) |
| Model trả về tiếng Anh thay vì dịch | Đoạn văn quá dài (>1.000 ký tự) | Provider đã tách câu tự động; nếu vẫn lỗi, `revalidate --fix` rồi dịch lại |
| Model tóm tắt thay vì dịch | Đoạn văn dài | Prompt đã có luật chống tóm tắt; tách câu ở 600 ký tự |
| Nhiều entry bị "Thất bại" | Model làm hỏng placeholder | Tăng `--retries`, giảm `--batch` |
| Bản dịch chất lượng kém | Model 3B hạn chế | Dùng model 7B/14B, hoặc dịch tay nhóm quan trọng |
| Lẫn tiếng Trung trong bản dịch | Model 3B bị "loãng" | Dùng model 7B, hoặc `revalidate --fix` rồi dịch lại |
| Bản dịch là tiếng Pháp | Model 7B trả về nhầm ngôn ngữ | `revalidate --fix` rồi dịch lại (guard đã chặn) |
| Bản dịch bị lệch (nhận nhầm của entry khác) | Model bỏ sót phần tử trong lô | Dùng prompt JSON đánh số + `revalidate --fix` |
| Ký tự hiển thị thành □ | Thiếu font fallback | Chạy lại `package --game ...` (đã vá 4 họ font) |
