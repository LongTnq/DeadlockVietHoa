using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Provider dịch qua Ollama (chạy local, không cần API key).
/// <para>
/// Dùng endpoint <c>/api/chat</c> của Ollama. Mặc định <b>không</b> bật
/// <c>format: "json"</c> — tham số này khiến các model nhỏ (3B) trả về
/// object <c>{"0": "..."}</c> thay vì mảng, hoặc lặp token vô hạn và bị
/// Ollama huỷ. Thay vào đó ta yêu cầu JSON bằng prompt rồi tự parse kỹ
/// (xem <see cref="ModelResponseParser"/>).
/// </para>
/// <para>
/// <b>Chiến lược bảo toàn cấu trúc:</b> chuỗi được tách thành các phần
/// "văn bản" và "phần cố định" (token, thẻ HTML) bằng
/// <see cref="TextPartSplitter"/>. Chỉ phần văn bản được gửi cho model;
/// phần cố định được ghép lại nguyên vẹn. Nhờ vậy model <b>không bao giờ</b>
/// có cơ hội xóa thẻ HTML hay tự thêm placeholder giả — hai lỗi hỏng
/// nặng nhất của dịch máy trong game.
/// </para>
/// <para>
/// <b>Vì sao cắt nhỏ lô khi số phần tử lệch:</b> model đôi khi trả về
/// thiếu hoặc thừa phần tử. Nếu giữ nguyên chỉ số, bản dịch của chuỗi
/// này sẽ bị gán nhầm cho chuỗi khác — sai hoàn toàn. Ta cắt đôi lô cho
/// tới khi khớp, và chỉ đánh dấu lỗi khi lô còn 1 chuỗi.
/// </para>
/// </summary>
public sealed class OllamaTranslationProvider : ITranslationProvider
{
    private readonly TranslationProviderConfig _config;
    private readonly HttpClient _httpClient;

    public OllamaTranslationProvider(
        TranslationProviderConfig config,
        HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (httpClient is null)
        {
            // Timeout mặc định của HttpClient là 100 giây — ngắn hơn
            // TimeoutSeconds cấu hình (300s). Nếu không tắt, một lô khó
            // sẽ bị cắt ngang ở giây thứ 100 trước khi provider kịp
            // báo lỗi. Thời gian chờ do CancellationTokenSource nội bộ
            // quyết định.
            _httpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public string Name => $"ollama/{_config.Model}";

    public async Task<bool> IsAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                new Uri($"{_config.Endpoint.TrimEnd('/')}/api/tags"),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts,
        string? context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var results = new List<string>(texts.Count);

        if (texts.Count == 0)
        {
            return results;
        }

        // Tách từng chuỗi thành phần văn bản / phần cố định.
        var splits = new List<List<TextPart>>(texts.Count);

        foreach (string text in texts)
        {
            splits.Add(TextPartSplitter.Split(text));
        }

        // Gom các phần văn bản cần dịch, giữ thứ tự để ghép lại.
        var literals = new List<string>();

        foreach (List<TextPart> parts in splits)
        {
            foreach (TextPart part in parts)
            {
                if (part.Kind != TextPartKind.Literal)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(part.Text))
                {
                    continue;
                }

                literals.Add(part.Text);
            }
        }

        if (literals.Count == 0)
        {
            // Không có gì để dịch -> giữ nguyên bản gốc.
            for (int i = 0; i < texts.Count; i++)
            {
                results.Add(texts[i]);
            }

            return results;
        }

        var translatedLiterals = await TranslateLiteralsAsync(
            literals,
            context,
            cancellationToken);

        // Ghép lại: phần cố định nguyên vẹn, phần văn bản đã dịch.
        int literalCursor = 0;

        for (int i = 0; i < splits.Count; i++)
        {
            var builder = new StringBuilder();

            foreach (TextPart part in splits[i])
            {
                if (part.Kind == TextPartKind.Fixed)
                {
                    builder.Append(part.Text);

                    continue;
                }

                // Phần văn bản rỗng -> giữ nguyên (kể cả khoảng trắng).
                if (string.IsNullOrWhiteSpace(part.Text))
                {
                    builder.Append(part.Text);

                    continue;
                }

                string translated = literalCursor < translatedLiterals.Count
                    ? translatedLiterals[literalCursor]
                    : string.Empty;

                literalCursor++;

                // Giữ nguyên khoảng trắng đầu/cuối của phần văn bản: model
                // thường cắt mất, gây ra "text<span>" dính liền nhau.
                string leading = part.Text[
                    ..(part.Text.Length - part.Text.TrimStart().Length)];
                string trailing = part.Text[(part.Text.TrimEnd().Length)..];

                builder.Append(leading);

                builder.Append(
                    string.IsNullOrWhiteSpace(translated)
                        ? part.Text.Trim()
                        : translated.Trim());

                builder.Append(trailing);
            }

            results.Add(builder.ToString());
        }

        return results;
    }

    /// <summary>
    /// Dịch danh sách phần văn bản. Nếu model trả về sai số lượng phần
    /// tử, lô được chia đôi và thử lại từng nửa.
    /// </summary>
    private async Task<List<string>> TranslateLiteralsAsync(
        IReadOnlyList<string> literals,
        string? context,
        CancellationToken cancellationToken)
    {
        var results = new List<string>(literals.Count);

        if (literals.Count == 0)
        {
            return results;
        }

        // Giới hạn theo CẢ số lượng lẫn tổng số ký tự.
        //
        // <b>Vì sao cần giới hạn ký tự:</b> Ollama chạy model với
        // <c>context_length</c> mặc định là 4096 token. Một lô 25 chuỗi
        // ngắn vừa khít, nhưng 25 chuỗi dài (mô tả kỹ năng có nhiều thẻ
        // HTML) thì prompt + đáp án vượt quá cửa sổ ngữ cảnh — model
        // bị kẹt và không trả lời, lệnh dịch treo im lặng.
        //
        // Ngân sách ký tự cho MỘT lô gửi đi. Ứng với cửa sổ ngữ cảnh
        // 4096 token, đây là mức vừa khít: ~450 token cho prompt,
        // ~450 token cho đáp án, còn dư phân nửa.
        //
        // Cần giữ đủ nhỏ để một lô luôn kịp trả lời trong thời gian chờ
        // — nếu không, lô bị timeout rồi bị chia đôi liên tục, mất
        // hàng chục phút mà không dịch được chuỗi nào.
        const int MaxCharsPerBatch = 1200;

        // Chuỗi dài hơn ngân sách sẽ không bao giờ vừa một lô — phải
        // tách thành câu. Nếu không, lô đó treo tới hết timeout rồi bị
        // chia đôi liên tục, mất hàng chục phút cho một chuỗi.
        //
        // Ngưỡng 600 ký tự không phải ngẫu nhiên: model 7B bắt đầu
        // "tóm tắt thay vì dịch" khi đoạn văn dài hơn khoảng 1.000 ký
        // tự, và cần ~130 giây cho một đoạn dài. Giữ mỗi mảnh ngắn
        // giúp model dịch đúng và nhanh hơn nhiều.
        const int MaxCharsPerLiteral = 600;

        // Tách trước, rồi mới chia lô.
        //
        // Mỗi mảnh nhớ chỉ số của chuỗi gốc nó thuộc về, để ghép lại
        // đúng thứ tự sau khi dịch.
        var pieceTexts = new List<string>();
        var pieceOwners = new List<int>();

        for (int i = 0; i < literals.Count; i++)
        {
            string literal = literals[i];

            if (literal.Length <= MaxCharsPerLiteral)
            {
                pieceTexts.Add(literal);
                pieceOwners.Add(i);

                continue;
            }

            foreach (string part in SentenceSplitter.Split(literal))
            {
                pieceTexts.Add(part);
                pieceOwners.Add(i);
            }
        }

        var pieceTranslations = new List<string>(pieceTexts.Count);

        int size = Math.Min(_config.BatchSize, pieceTexts.Count);

        int offset = 0;

        while (offset < pieceTexts.Count)
        {
            var slice = new List<string>();
            int chars = 0;

            while (slice.Count < size && offset + slice.Count < pieceTexts.Count)
            {
                string candidate = pieceTexts[offset + slice.Count];

                // Luôn lấy ít nhất một chuỗi, kể cả khi nó quá dài —
                // nếu không sẽ vòng lặp vô hạn.
                if (slice.Count > 0 &&
                    chars + candidate.Length > MaxCharsPerBatch)
                {
                    break;
                }

                slice.Add(candidate);
                chars += candidate.Length;
            }

            pieceTranslations.AddRange(
                await TranslateSliceAsync(
                    slice,
                    context,
                    cancellationToken));

            offset += slice.Count;
        }

        // Ghép các mảnh của từng chuỗi gốc lại, theo đúng thứ tự.
        for (int i = 0; i < literals.Count; i++)
        {
            var builder = new StringBuilder();

            for (int p = 0; p < pieceTexts.Count; p++)
            {
                if (pieceOwners[p] != i)
                {
                    continue;
                }

                if (p < pieceTranslations.Count &&
                    !string.IsNullOrEmpty(pieceTranslations[p]))
                {
                    builder.Append(pieceTranslations[p]);
                }
                else
                {
                    // Dịch hỏng -> giữ nguyên phần gốc.
                    builder.Append(pieceTexts[p]);
                }
            }

            results.Add(builder.ToString());
        }

        return results;
    }

    /// <summary>
    /// Dịch một lô con. Nếu số phần tử trả về không khớp, chia đôi.
    /// </summary>
    private async Task<List<string>> TranslateSliceAsync(
        IReadOnlyList<string> slice,
        string? context,
        CancellationToken cancellationToken,
        int depth = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await RequestAsync(slice, context, cancellationToken);

        // Model bỏ sót phần tử -> dịch lại đúng phần đó, không cần chia
        // đôi cả lô.
        if (response.ByIndex.Count > 0)
        {
            var filled = await FillMissingAsync(
                slice,
                response,
                context,
                cancellationToken);

            if (filled.Count > 0)
            {
                var rebuilt = new List<string>(slice.Count);

                for (int i = 0; i < slice.Count; i++)
                {
                    if (filled.TryGetValue(i, out string? value))
                    {
                        rebuilt.Add(value);

                        continue;
                    }

                    rebuilt.Add(
                        response.ByIndex.TryGetValue(i, out string? original)
                            ? original
                            : string.Empty);
                }

                return await RetryUntranslatedAsync(
                    slice,
                    rebuilt,
                    context,
                    cancellationToken,
                    depth);
            }
        }

        var translated = AlignResponse(slice, response);

        if (translated.Count == slice.Count)
        {
            return await RetryUntranslatedAsync(
                slice,
                translated,
                context,
                cancellationToken,
                depth);
        }

        // Sai số lượng -> chia đôi, trừ khi đã là 1 chuỗi thì bỏ cuộc.
        if (slice.Count == 1)
        {
            return new List<string> { string.Empty };
        }

        // Giới hạn độ sâu chia để tránh bùng nổ số request khi model
        // hỏng hoàn toàn: 30 -> 15 -> 8 -> 4 -> 2 -> 1 là tối đa 31
        // lời gọi cho một lô, thay vì 59.
        const int MaxDepth = 5;

        if (depth >= MaxDepth)
        {
            // Không chia tiếp được -> đánh dấu các phần tử thiếu là rỗng,
            // service sẽ retry.
            var partial = new List<string>(slice.Count);

            for (int i = 0; i < slice.Count; i++)
            {
                partial.Add(
                    i < translated.Count ? translated[i] : string.Empty);
            }

            return partial;
        }

        int middle = slice.Count / 2;

        var first = await TranslateSliceAsync(
            slice.Take(middle).ToList(),
            context,
            cancellationToken,
            depth + 1);

        var second = await TranslateSliceAsync(
            slice.Skip(middle).ToList(),
            context,
            cancellationToken,
            depth + 1);

        first.AddRange(second);

        return first;
    }

    /// <summary>
    /// Dịch lại riêng những phần tử bị model bỏ sót.
    /// <para>
    /// Khi model trả về JSON đánh số, ta biết chính xác chỉ số nào
    /// thiếu. Thay vì chia đôi cả lô (tốn gấp đôi request), ta chỉ dịch
    /// lại đúng những phần tử đó — mỗi lần một, để model tập trung.
    /// </para>
    /// </summary>
    private async Task<Dictionary<int, string>> FillMissingAsync(
        IReadOnlyList<string> slice,
        ModelResponse response,
        string? context,
        CancellationToken cancellationToken)
    {
        if (response.ByIndex.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var missing = new List<int>();

        for (int i = 0; i < slice.Count; i++)
        {
            if (!response.ByIndex.ContainsKey(i))
            {
                missing.Add(i);
            }
        }

        if (missing.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var filled = new Dictionary<int, string>();

        foreach (int index in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var single = await TranslateSliceAsync(
                new List<string> { slice[index] },
                context,
                cancellationToken,
                0);

            if (single.Count == 1 && !string.IsNullOrWhiteSpace(single[0]))
            {
                filled[index] = single[0];
            }
        }

        return filled;
    }

    /// <summary>
    /// Căn phản hồi của model với lô đã gửi.
    /// <para>
    /// <b>Vì sao cần:</b> model có thể trả về:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>đúng số phần tử — dùng theo thứ tự;</description></item>
    ///   <item><description>
    ///     nhiều hơn / ít hơn (hay gặp: model lặp lại mục) — nếu dùng theo
    ///     thứ tự, bản dịch sẽ bị gán nhầm cho chuỗi khác, tức là sai
    ///     hoàn toàn;
    ///   </description></item>
    ///   <item><description>
    ///     kèm trường "original" — khi đó ta ánh xạ theo văn bản gốc, đáng
    ///     tin cậy hơn chỉ số.
    ///   </description></item>
    /// </list>
    /// <para>
    /// <b>Trường hợp nguy hiểm nhất:</b> model trả về mảng phẳng đúng số
    /// lượng nhưng bỏ sót một chuỗi ở giữa. Khi đó mọi bản dịch từ vị trí
    /// đó trở đi bị lệch — ví dụ <c>Language</c> nhận bản dịch của
    /// "Extended Magazine". Ta phát hiện việc này bằng cách so "anchor":
    /// những chuỗi mà model giữ nguyên (tên riêng) phải khớp vị trí.
    /// Nếu lệch -> cả lô bị loại để dịch lại với lô nhỏ hơn.
    /// </para>
    /// </summary>
    private static List<string> AlignResponse(
        IReadOnlyList<string> slice,
        ModelResponse response)
    {
        var result = new List<string>(slice.Count);

        // Ưu tiên ánh xạ theo chỉ số — biết chính xác phần tử nào thiếu.
        if (response.ByIndex.Count > 0)
        {
            for (int i = 0; i < slice.Count; i++)
            {
                result.Add(
                    response.ByIndex.TryGetValue(i, out string? value)
                        ? value
                        : string.Empty);
            }

            return result;
        }

        // Ưu tiên ánh xạ theo văn bản gốc.
        if (response.ByOriginal.Count > 0)
        {
            foreach (string original in slice)
            {
                result.Add(
                    response.ByOriginal.TryGetValue(original, out string? value)
                        ? value
                        : string.Empty);
            }

            return result;
        }

        // Không có ánh xạ -> dùng theo thứ tự, giới hạn theo độ dài lô.
        foreach (string value in response.Translations)
        {
            if (result.Count >= slice.Count)
            {
                break;
            }

            result.Add(value);
        }

        // Kiểm tra lệch chỉ số bằng "anchor".
        if (IsMisaligned(slice, result))
        {
            return new List<string>();
        }

        return result;
    }

    /// <summary>
    /// Phát hiện trường hợp bản dịch bị lệch chỉ số so với chuỗi gốc.
    /// <para>
    /// <b>Ý tưởng:</b> trong bất kỳ lô nào cũng có ít nhất vài chuỗi mà
    /// model giữ nguyên — tên riêng, mã, ký hiệu đơn vị. Những chuỗi đó
    /// là "mốc" (anchor). Nếu bản dịch tại vị trí <c>i</c> bằng nguyên văn
    /// chuỗi gốc tại vị trí <c>j != i</c>, nghĩa là model đã bỏ sót phần tử
    /// và mọi thứ từ <c>min(i,j)</c> trở đi đang lệch.
    /// </para>
    /// <para>
    /// <b>Độ nhạy:</b> chỉ kiểm tra với chuỗi đủ dài (>= 4 ký tự) để tránh
    /// báo nhầm với những mã ngắn như "OK" hay "1".
    /// </para>
    /// </summary>
    private static bool IsMisaligned(
        IReadOnlyList<string> slice,
        IReadOnlyList<string> translated)
    {
        if (translated.Count != slice.Count)
        {
            return false;
        }

        // Kiểm tra chéo: bản dịch tại vị trí i có trùng với chuỗi gốc ở
        // vị trí khác không? Nếu có -> lệch.
        //
        // Chỉ xét bản dịch ĐỦ DÀI. Bản dịch ngắn trùng với một chuỗi gốc
        // khác là chuyện bình thường (ví dụ "Teamplay" -> "Tactical", còn
        // "Tactical" cũng là nội dung gốc của một entry khác trong cùng
        // file) — không phải dấu hiệu lệch chỉ số.
        const int SuspiciousLength = 12;

        for (int i = 0; i < slice.Count; i++)
        {
            string result = translated[i].Trim();

            if (result.Length < SuspiciousLength)
            {
                continue;
            }

            for (int j = 0; j < i; j++)
            {
                string candidate = slice[j].Trim();

                if (candidate.Length < SuspiciousLength)
                {
                    continue;
                }

                // Hai chuỗi gốc giống nhau -> không phải lệch.
                if (string.Equals(
                        candidate,
                        slice[i].Trim(),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(
                        result,
                        candidate,
                        StringComparison.Ordinal))
                {
                    // Bản dịch của vị trí i trùng với chuỗi gốc ở vị trí
                    // j < i -> model đã bỏ sót phần tử ở giữa và mọi thứ
                    // từ j trở đi đang lệch.
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Thử lại những chuỗi mà model trả về nguyên văn tiếng Anh.
    /// <para>
    /// Model nhỏ hay bỏ sót một đoạn trong lô — nó trả về đúng chuỗi gốc.
    /// Nếu không xử lý, game sẽ hiển thị tiếng Anh lẫn trong bản dịch.
    /// Ta gom các chuỗi đó ra và dịch lại riêng, mỗi lần một (model tập
    /// trung hơn khi lô nhỏ).
    /// </para>
    /// </summary>
    private async Task<List<string>> RetryUntranslatedAsync(
        IReadOnlyList<string> slice,
        IReadOnlyList<string> translated,
        string? context,
        CancellationToken cancellationToken,
        int depth)
    {
        var result = new List<string>(translated);
        var pending = new List<int>();

        for (int i = 0; i < slice.Count; i++)
        {
            if (UntranslatedDetector.IsUntranslated(slice[i], translated[i]))
            {
                pending.Add(i);
            }
        }

        if (pending.Count == 0)
        {
            return result;
        }

        // Giới hạn số vòng thử lại để tránh lặp vô hạn.
        const int MaxRetryRounds = 2;

        if (depth >= MaxRetryRounds)
        {
            return result;
        }

        foreach (int index in pending)
        {
            var single = await TranslateSliceAsync(
                new List<string> { slice[index] },
                context,
                cancellationToken,
                depth + 1);

            if (single.Count == 1 &&
                !UntranslatedDetector.IsUntranslated(slice[index], single[0]))
            {
                result[index] = single[0];
            }
        }

        return result;
    }

    /// <summary>
    /// Gọi Ollama một lần và phân tích kết quả.
    /// </summary>
    private async Task<ModelResponse> RequestAsync(
        IReadOnlyList<string> literals,
        string? context,
        CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest
        {
            Model = _config.Model,
            Stream = false,
            Options = new OllamaOptions
            {
                Temperature = 0.2,
                TopP = 0.9,
                RepeatPenalty = 1.15,
                NumPredict = _config.MaxTokens,
                NumCtx = 4096
            },
            Messages = new List<OllamaMessage>
            {
                new()
                {
                    Role = "system",
                    Content = PromptBuilder.BuildSystemPrompt(context)
                },
                new()
                {
                    Role = "user",
                    Content = PromptBuilder.BuildUserPrompt(literals)
                }
            }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

        try
        {
            using var httpResponse = await _httpClient.PostAsJsonAsync(
                new Uri($"{_config.Endpoint.TrimEnd('/')}/api/chat"),
                request,
                cts.Token);

            httpResponse.EnsureSuccessStatusCode();

            var chatResponse = await httpResponse.Content.ReadFromJsonAsync<OllamaChatResponse>(
                cts.Token);

            string content = chatResponse?.Message?.Content ?? string.Empty;

            return ModelResponseParser.Parse(content);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Hết thời gian chờ -> trả về rỗng. Lớp gọi bên trên sẽ
            // tự chia nhỏ lô để thử lại với phần nhỏ hơn; nếu vẫn hỏng
            // thì service sẽ retry.
            return new ModelResponse();
        }
    }

    // ---------------- wire format ----------------

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }

        [JsonPropertyName("messages")]
        public List<OllamaMessage> Messages { get; set; } = new();
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.2;

        [JsonPropertyName("top_p")]
        public double TopP { get; set; } = 0.9;

        [JsonPropertyName("repeat_penalty")]
        public double RepeatPenalty { get; set; } = 1.15;

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; } = 2048;

        /// <summary>
        /// Cửa sổ ngữ cảnh của model.
        /// <para>
        /// Không nên tăng quá 4096 (mặc định của Ollama). KV cache cho
        /// context lớn chiếm VRAM, đẩy phần model xuống CPU — và khi đó
        /// tốc độ giảm mạnh. Giải pháp đúng cho lô lớn là chia nhỏ lô
        /// theo tổng số ký tự, không phải tăng context.
        /// </para>
        /// </summary>
        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; set; } = 4096;
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }
    }
}
