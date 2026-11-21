$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$pending = @($doc.entries | Where-Object { $_.status -eq 'Pending' -and $_.category -ne 'Voice' -and $_.originalText })
Write-Host ("Pending co noi dich duoc: {0}" -f $pending.Count)

$sys = @"
Bạn là chuyên gia dịch thuật Việt Nam cho trò chơi Deadlock (Valve Source 2).

QUY TẮC BẮT BUỘC:
1. Dịch đúng nghĩa sang tiếng Việt tự nhiên, đúng văn phong game.
2. KHÔNG thêm, KHÔNG bớt, KHÔNG diễn giải — dịch sát nghĩa gốc.
3. KHÔNG thêm giải thích, không thêm dấu ngoặc kép bao quanh.
5. Giữ nguyên khoảng trắng đầu/cuối chuỗi nếu có.
8. TUYỆT ĐỐI KHÔNG tóm tắt. Bản dịch phải có cùng độ dài xấp xỉ với bản gốc.
9. Chỉ trả về bản dịch. TUYỆT ĐỐI KHÔNG trả lời bằng tiếng Anh.
"@

$texts = @($pending | Select-Object -First 8 | ForEach-Object { $_.originalText })
$lines = ''
for ($i = 0; $i -lt $texts.Count; $i++) { $lines += "[$i] " + $texts[$i] + "`n" }
$user = "Dịch các chuỗi dưới đây sang tiếng Việt.`nTrả về DUY NHẤT một JSON object, mỗi khoá là CHỈ SỐ.`nGiá trị của mỗi khoá PHẢI là một chuỗi văn bản đơn giản.`n`n$lines`nJSON:"

$body = @{
    model = 'qwen2.5:7b-instruct-q4_K_M'; stream = $false
    messages = @(@{ role = 'system'; content = $sys }, @{ role = 'user'; content = $user })
    options = @{ temperature = 0.2; top_p = 0.9; repeat_penalty = 1.15; num_ctx = 4096; num_predict = 2048 }
} | ConvertTo-Json -Depth 6

$r = Invoke-RestMethod -Uri 'http://localhost:11434/api/chat' -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 400
$r.message.content | Out-File 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\resp2.txt' -Encoding UTF8
Write-Host "done"
