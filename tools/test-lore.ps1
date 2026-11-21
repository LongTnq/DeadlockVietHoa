$ErrorActionPreference = 'Stop'
$path = 'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
$doc = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json

$key = 'hero_necro_lore'
$e = $doc.entries | Where-Object { $_.key -eq $key } | Select-Object -First 1

$sys = @"
Bạn là chuyên gia dịch thuật Việt Nam cho trò chơi Deadlock (Valve Source 2).

QUY TẮC BẮT BUỘC:
1. Dịch đúng nghĩa sang tiếng Việt tự nhiên, đúng văn phong game.
2. KHÔNG thêm, KHÔNG bớt, KHÔNG diễn giải — dịch sát nghĩa gốc.
3. KHÔNG thêm giải thích, không thêm dấu ngoặc kép bao quanh.
5. Giữ nguyên khoảng trắng đầu/cuối chuỗi nếu có.
8. TUYỆT ĐỐI KHÔNG tóm tắt. Bản dịch phải có cùng độ dài xấp xỉ với bản gốc.
9. Chỉ trả về bản dịch tiếng Việt. TUYỆT ĐỐI KHÔNG trả lời bằng tiếng Anh, tiếng Pháp hay tiếng Trung.
"@

foreach ($model in @('qwen2.5:3b-instruct', 'qwen2.5:7b-instruct-q4_K_M')) {
    $user = "Dịch chuỗi sau sang tiếng Việt.`n`n" + $e.originalText + "`n`nChỉ trả về bản dịch."
    $body = @{
        model = $model; stream = $false
        messages = @(@{ role = 'system'; content = $sys }, @{ role = 'user'; content = $user })
        options = @{ temperature = 0.2; num_ctx = 4096; num_predict = 2048 }
    } | ConvertTo-Json -Depth 6

    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-RestMethod -Uri 'http://localhost:11434/api/chat' -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 400
        $sw.Stop()
        $out = $r.message.content
        $hasCjk = $out -match '[一-鿿]'
        $hasFr = $out -match '[äåæçëîïñõöøûüÿœ]'
        Write-Host ("=== {0}  {1:N1}s  CJK={2}  French={3}  len={4}" -f $model, $sw.Elapsed.TotalSeconds, $hasCjk, $hasFr, $out.Length)
        Write-Host $out.Substring(0, [Math]::Min(400, $out.Length))
    } catch {
        $sw.Stop()
        Write-Host ("=== {0} FAILED {1:N1}s" -f $model, $sw.Elapsed.TotalSeconds)
    }
    Write-Host ""
}
