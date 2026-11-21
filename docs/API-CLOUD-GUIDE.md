# Dùng API cloud để dịch nhanh

## Vì sao cần

Ollama local trên GTX 1650 (4 GB VRAM) đạt **2,6 token/s** với `qwen2.5:7b-instruct-q4_K_M`,
và model hay trộn tiếng Trung Quốc vào giữa câu tiếng Việt:

```
"Khu vực sẽ được phân配基于技能分配给所有参赛玩家。"
```

API cloud xử lý nhanh hơn hàng ch�ục lần và chất lượng ổn định hơn nhiều.

## Cách dùng

### 1. Đặt API key

**Cách khuyến nghị — file `.env` (không lọt vào lịch sử shell):**

Tạo file `.env` ở thư mục gốc dự án:

```
DEADLOCKVN_API_KEY=sk-or-v1-abc123...
```

CLI tự đọc file này khi chạy (xem `src/DeadlockVN.CLI/DotEnv.cs`).
File đã có trong `.gitignore` nên **không bao giờ** bị commit lên Git.

**Cách thay thế — biến môi trường:**

```powershell
$env:DEADLOCKVN_API_KEY = "sk-abc123..."
```

> Biến đã có sẵn trong môi trường **không bị** `.env` ghi đè — nên bạn
> vẫn có thể ghi đè tạm thời bằng `$env:DEADLOCKVN_API_KEY = "..."`
> khi cần đổi key.

Các provider được hỗ trợ (đều dùng chuẩn `POST /v1/chat/completions`):

| `--provider` | Endpoint mặc định | Model mặc định |
|---|---|---|
| `openai` | `https://api.openai.com/v1` | `gpt-4o-mini` |
| `groq` | `https://api.groq.com/openai/v1` | `llama-3.3-70b-versatile` |
| `openrouter` | `https://openrouter.ai/api/v1` | `google/gemini-2.5-flash` |
| `gemini` | `https://generativelanguage.googleapis.com/v1beta/openai/v1` | `gemini-2.0-flash` |

> **Lưu ý về endpoint:** tất cả endpoint đều là URL **đầy đủ** tới nơi
> chứa `chat/completions`. Provider tự nối `/chat/completions` và
> `/models` vào cuối — không tự thêm `/v1`. Nếu truyền endpoint thiếu
> `/v1` sẽ nhận lỗi 404 từ server.

### 2. Chạy dịch

```powershell
# Xem trước cần dịch bao nhiêu (không gọi API, không tốn phí)
dotnet run --project src/DeadlockVN.CLI -- auto-translate --dry-run

# Dịch tất cả nhóm còn thiếu
dotnet run --project src/DeadlockVN.CLI -- auto-translate --provider groq --batch 40

# Chỉ dịch nhóm cụ thể
dotnet run --project src/DeadlockVN.CLI -- auto-translate --provider groq --categories abilities,ui --batch 40
```

Có thể truyền key trực tiếp nếu không muốn dùng file `.env`:

```powershell
dotnet run --project src/DeadlockVN.CLI -- auto-translate --provider openai --api-key "sk-..."
```

> ⚠️ Cách này **không an toàn**: key sẽ nằm trong lịch sử PowerShell và
> có thể xuất hiện trong ảnh chụp màn hình. Ưu tiên file `.env`.

## Sau khi dịch xong — đóng gói vào game

**Một lệnh duy nhất:**

```powershell
powershell -NoProfile -File tools/deploy.ps1
```

Lệnh này làm đủ 4 việc: export → đóng gói VPK → cài vào addons → vá
`fonts.conf`.

### Chi tiết từng bước (nếu cần chạy riêng)

```powershell
# 1. Export ra thư mục staging
dotnet run --project src/DeadlockVN.CLI -- export-to-game `
    --translations workspace/translations/vietnamese.json `
    --source workspace/source/original `
    --output workspace/staging/mod/resource/localization `
    --language vietnamese

# 2. Đóng gói thành VPK mod
dotnet run --project src/DeadlockVN.CLI -- pack-vpk `
    --staging workspace/staging/mod `
    --output workspace/staging/vpk `
    --name deadlockvn_dir.vpk

# 3. Cài vào thư mục addons + vá font
powershell -NoProfile -File tools/install-vpk.ps1
python tools/patch_fonts.py
```

> **LƯU Ý:** dùng `export-to-game`, **KHÔNG** dùng `export`.
> Lệnh `export` ghi đè file bản dịch và làm mất toàn bộ công sức đã dịch.

### Tại sao phải dùng VPK chứ không chép file `.txt` rời?

**Steam ghi đè file `.txt` rời trong thư mục game.** Quan sát thực tế:
file đã chép lúc 18:37 (477 KB, đầy tiếng Việt) đã bị Steam ghi đè về
60 bytes (rỗng) lúc 19:09. Tương tự, `fonts.conf` cũng bị ghi đè và mất
phần fallback tiếng Việt.

VPK nằm trong `game/citadel/addons/` nằm ngoài phạm vi Steam quản lý,
nên **không bị ghi đè**. Đây là cách deploy bền vững.

Dấu hiệu Steam đã ghi đè: file `.txt` trong
`game/citadel/resource/localization/` có dung lượng ~60 byte.

Vì `fonts.conf` vẫn bị Steam ghi đè, cần chạy lại
`python tools/patch_fonts.py` sau mỗi lần game cập nhật.


## Kiểm tra nhanh

```powershell
# Số lượng đã dịch
dotnet run --project src/DeadlockVN.CLI -- report --db workspace/translations/vietnamese.json

# Kiểm tra lỗi (placeholder lệch, ký tự lạ)
dotnet run --project src/DeadlockVN.CLI -- revalidate --db workspace/translations/vietnamese.json

# VPK đã cài có thực sự chứa tiếng Việt không
python tools/vpk_scan_content.py "E:\Steam\steamapps\common\Deadlock\game\citadel\addons\deadlockvn_000.vpk"
```

## Font tiếng Việt

Tất cả font của game đều **thiếu glyph `ơ` (U+1ED)** và `ữ` (U+1EE6)`.
Đã thêm `Arial` + `Segoe UI` (Windows, hỗ trợ đầy đủ) vào cuối chuỗi fallback
trong `panorama/fonts/fonts.conf`. Fontconfig chỉ dùng chúng cho ký tự thiếu nên
phần lớn văn bản vẫn giữ font gốc của game.

Nếu vẫn thấy lỗi font, kiểm tra bằng:

```powershell
python tools/font_audit.py
```

## Đọc file bản dịch mà thấy "ký tự lộn xộn"

Đó là **PowerShell console hiển thị sai encoding**, không phải file hỏng.
File là UTF-8 hợp lệ, có BOM giống hệt file gốc của game.

Kiểm tra đúng cách:

```powershell
$env:PYTHONIOENCODING="utf-8"
python tools/verify_game.py
```

Hoặc mở bằng VS Code / Notepad (tự nhận UTF-8).
