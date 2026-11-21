$ErrorActionPreference = 'Stop'

# Tái hiện đúng prompt mà provider dùng, nhưng với model 3B,
# trên 10 chuỗi UI thật (đã bỏ placeholder — giống TextPartSplitter).

$texts = @(
  ' TOTAL SOULS',
  'Choose an ability to imbue',
  'Copy to clipboard',
  'Did not participate.',
  ' of ',
  ' Remaining',
  'Lanes will be assigned based on the skill of all players in the match.',
  'Lane assignments will prioritize keeping your party together.'
)

$sys = @"
Bạn là chuyên gia dịch thuật Việt Nam cho trò chơi Deadlock (Valve Source 2).

QUY TẮC BẮT BUỘC:
1. Dịch đúng nghĩa sang tiếng Việt tự nhiên, đúng văn phong game.
2. KHÔNG thêm, KHÔNG bớt, KHÔNG diễn giải — dịch sát nghĩa gốc.
3. KHÔNG thêm giải thích, không thêm dấu ngoặc kép bao quanh.
4. Thuật ngữ chuyên ngành dịch theo cách phổ biến trong game Việt Nam. Tên riêng, tên hero, tên vật phẩm: giữ nguyên tiếng Anh nếu đã quen thuộc.
5. Giữ nguyên khoảng trắng đầu/cuối chuỗi nếu có.
6. Nếu chuỗi rỗng hoặc không có gì để dịch, trả về nguyên văn.
7. GIỮ NGUYÊN dấu câu ở đầu chuỗi và dấu chấm câu ở cuối. Chỉ thay đổi phần chữ, không thay đổi dấu câu.
8. TUYỆT ĐỐI KHÔNG tóm tắt, không diễn giải lại, không liệt kê ý chính. Bản dịch phải có cùng độ dài xấp xỉ với bản gốc.
9. Chỉ trả về bản dịch. TUYỆT ĐỐI KHÔNG trả lời bằng tiếng Anh.

Ví dụ:
Gốc: "Spirit Amp, restores Spirit every 8 seconds"
Dịch: "Nội lực, hồi Nội lực mỗi 8 giây"
Gốc: "Warning: reduces damage"
Dịch: "Cảnh báo: giảm sát thương"
"@

$lines = ''
for ($i = 0; $i -lt $texts.Count; $i++) {
    $lines += "[$i] " + $texts[$i] + "`n"
}

$user = @"
Dịch các chuỗi dưới đây sang tiếng Việt.
Trả về DUY NHẤT một JSON object, trong đó mỗi khoá là CHỈ SỐ của chuỗi (số nguyên, không có dấu gọi) và giá trị là bản dịch.
Ví dụ cho 2 chuỗi: {"0": "bản dịch 0", "1": "bản dịch 1"}
PHẢI có đúng khoá cho mọi chỉ số từ 0 đến N-1 (N là số chuỗi được liệt kê). Không thêm khoá nào ngoài phạm vi, không bỏ khoá nào.
Nếu một chuỗi không cần dịch (tên riêng), vẫn phải có khoá đó, giá trị là nguyên văn chuỗi.

$lines
JSON:
"@

foreach ($model in @('qwen2.5:3b-instruct', 'qwen2.5:7b-instruct-q4_K_M')) {
    $body = @{
        model = $model
        stream = $false
        messages = @(
            @{ role = 'system'; content = $sys },
            @{ role = 'user'; content = $user }
        )
        options = @{ temperature = 0.2; top_p = 0.9; repeat_penalty = 1.15; num_ctx = 4096; num_predict = 2048 }
    } | ConvertTo-Json -Depth 6

    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-RestMethod -Uri 'http://localhost:11434/api/chat' -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 400
        $sw.Stop()
        Write-Host ("=== {0}  ({1:N1}s, eval={2}) ===" -f $model, $sw.Elapsed.TotalSeconds, $r.eval_count)
        Write-Host $r.message.content
        Write-Host ""
    } catch {
        $sw.Stop()
        Write-Host ("=== {0} FAILED ({1:N1}s) ===" -f $model, $sw.Elapsed.TotalSeconds)
    }
}
