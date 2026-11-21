# Chọn API key nào? Hướng dẫn chi tiết

Tóm tắt: **OpenRouter** là lựa chọn đã dùng và đã kiểm chứng cho dự án này.
Groq và OpenAI cũng hoạt động tốt.

## Xếp hạng đề xuất

### 1. OpenRouter — đã dùng và kiểm chứng ✅

| | |
|---|---|
| Link lấy key | https://openrouter.ai/keys |
| Model | `google/gemini-2.5-flash` |
| Chi phí | Rất rẻ — toàn bộ ~13.000 entry chỉ tốn **~$0.15** |
| Tốc độ | 1,6 giây/lô 40 chuỗi — nhanh gấp ~2000× Ollama local |
| Chất lượng tiếng Việt | Rất tốt — giữ nguyên placeholder, không trộn tiếng lạ |

Cách lấy:
1. Mở https://openrouter.ai/keys
2. Đăng ký (Google/GitHub), vào **Credits** → nạp tiền nếu muốn dùng model trả phí
3. Bấm **Create Key** → copy (bắt đầu bằng `sk-or-`)

> Endpoint: `https://openrouter.ai/api/v1` (phải có `/v1`).
> Liệt kê model: `https://openrouter.ai/api/v1/models`

### 2. Groq — miễn phí, rất nhanh

| | |
|---|---|
| Link lấy key | https://console.groq.com/keys |
| Model | `llama-3.3-70b-versatile` |
| Chi phí | **Miễn phí** (có giới hạn tốc độ, đủ dùng) |
| Chất lượng tiếng Việt | Tốt |

Cách lấy:
1. Mở https://console.groq.com/keys
2. Đăng ký bằng Google/GitHub (miễn phí, không cần thẻ)
3. Bấm **Create API Key** → copy key (bắt đầu bằng `gsk_`)

### 3. OpenAI — chất lượng cao nhất nhưng tốn tiền

| | |
|---|---|
| Link lấy key | https://platform.openai.com/api-keys |
| Model | `gpt-4o-mini` |
| Chi phí | ~$0.15 / 1M input token — toàn bộ game ~13.000 entry ≈ **< $0.50** |

Cách lấy:
1. Mở https://platform.openai.com/api-keys
2. **Phải nạp tiền trước** (OpenAI không cho dùng miễn phí API)
3. Bấm **Create new secret key** → copy (bắt đầu bằng `sk-`)

### 4. Google Gemini — miễn phí, nhiều quota

| | |
|---|---|
| Link lấy key | https://aistudio.google.com/app/apikey |
| Model | `gemini-2.0-flash` |
| Chi phí | Miễn phí (rất hào phóng) |

Cách lấy:
1. Mở https://aistudio.google.com/app/apikey
2. Đăng ký Google, bấm **Create API key**

## Lưu key an toàn — file `.env`

**Không** gửi key trong chat hay truyền bằng `--api-key` trên dòng lệnh
(key sẽ nằm trong lịch sử PowerShell). Thay vào đó, tạo file `.env` ở
thư mục gốc dự án:

```
DEADLOCKVN_API_KEY=sk-or-v1-abc123...
```

CLI tự đọc file này khi chạy (xem `src/DeadlockVN.CLI/DotEnv.cs`).
File đã có trong `.gitignore` nên **không bao giờ** bị commit lên Git.

Biến đã có sẵn trong môi trường **không bị** `.env` ghi đè — để đổi
key tạm thời:

```powershell
$env:DEADLOCKVN_API_KEY = "key-khac"
```

## Sau khi có key

```powershell
# Xem trước cần dịch bao nhiêu (không tốn phí)
dotnet run --project src/DeadlockVN.CLI -- auto-translate --dry-run

# Dịch
dotnet run --project src/DeadlockVN.CLI -- auto-translate --provider openrouter --model google/gemini-2.5-flash --batch 40

# Kiểm tra chất lượng
dotnet run --project src/DeadlockVN.CLI -- revalidate

# Đóng gói + cài vào game
dotnet run --project src/DeadlockVN.CLI -- export-to-game
dotnet run --project src/DeadlockVN.CLI -- package
python tools/deploy_lang.py
```

## Tuyệt đối không

- Đừng gửi key trong ảnh chụp màn hình
- Đừng commit key vào git (`.env` đã được `.gitignore` loại trừ)
- Key này nên giới hạn quota nếu được (Groq/OpenRouter có tuỳ chọn này)
- **Nên thu hồi key nếu đã lộ trong chat** — vào trang quản lý key của
  provider và tạo key mới
