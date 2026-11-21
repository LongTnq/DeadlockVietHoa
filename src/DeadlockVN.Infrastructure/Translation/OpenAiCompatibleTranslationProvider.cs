using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Provider dịch qua API tương thích OpenAI.
/// <para>
/// Dùng được với hầu hết dịch vụ LLM trả lời theo chuẩn
/// <c>POST /v1/chat/completions</c>:
/// </para>
/// <list type="bullet">
///   <item><description>OpenAI — <c>gpt-4o-mini</c></description></item>
///   <item><description>Groq — <c>llama-3.3-70b-versatile</c></description></item>
///   <item><description>OpenRouter — mọi model</description></item>
///   <item><description>
///     Google Gemini qua endpoint tương thích OpenAI của Google
///   </description></item>
/// </list>
/// <para>
/// <b>Vì sao cần provider này:</b> model 7B chạy local trên GPU 4 GB
/// đạt tốc độ ~2,6 token/s và thường trộn tiếng Trung Quốc vào giữa câu
/// tiếng Việt. API cloud xử lý nhanh hơn hàng chục lần và chất lượng
/// ổn định hơn nhiều cho tiếng Việt.
/// </para>
/// <para>
/// <b>Khác biệt so với Ollama:</b> API cloud có cửa sổ ngữ cảnh lớn
/// (16K-128K) nên <b>không</b> cần tách câu bằng
/// <see cref="SentenceSplitter"/> hay giới hạn số ký tự mỗi lô. Lô
/// lớn hơn nhiều, ít request hơn, tổng thời gian thấp hơn nhiều.
/// </para>
/// <para>
/// <b>API key:</b> đọc từ biến môi trường
/// <c>DEADLOCKVN_API_KEY</c> (không ghi vào mã nguồn hay file cấu
/// hình), hoặc truyền qua tham số <c>--api-key</c>.
/// </para>
/// </summary>
public sealed class OpenAiCompatibleTranslationProvider : ITranslationProvider
{
    /// <summary>
    /// Tên biến môi trường chứa API key.
    /// </summary>
    public const string ApiKeyEnvironmentVariable = "DEADLOCKVN_API_KEY";

    private readonly TranslationProviderConfig _config;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAiCompatibleTranslationProvider(
        TranslationProviderConfig config,
        string? apiKey = null,
        HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _apiKey = apiKey
            ?? Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable)
            ?? string.Empty;

        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public string Name => $"{_config.Provider}/{_config.Model}";

    public async Task<bool> IsAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri($"{_config.Endpoint.TrimEnd('/')}/models"));
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(
                request,
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

        // Tách từng chuỗi thành phần văn bản / phần cố định (token, thẻ
        // HTML) để model không bao giờ chạm vào cấu trúc.
        var splits = new List<List<TextPart>>(texts.Count);
        var literals = new List<string>();

        foreach (string text in texts)
        {
            var parts = TextPartSplitter.Split(text);

            splits.Add(parts);

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

                if (string.IsNullOrWhiteSpace(part.Text))
                {
                    builder.Append(part.Text);

                    continue;
                }

                string translated = literalCursor < translatedLiterals.Count
                    ? translatedLiterals[literalCursor]
                    : string.Empty;

                literalCursor++;

                // Giữ nguyên khoảng trắng đầu/cuối của phần văn bản.
                string leading = part.Text[
                    ..(part.Text.Length - part.Text.TrimStart().Length)];
                string trailing = part.Text[(part.Text.TrimEnd().Length)..];

                builder.Append(leading);
                builder.Append(translated.Trim());
                builder.Append(trailing);
            }

            results.Add(builder.ToString());
        }

        return results;
    }

    /// <summary>
    /// Dịch danh sách phần văn bản.
    /// <para>
    /// Khác với Ollama, API cloud có cửa sổ ngữ cảnh lớn nên:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>không cần tách câu;</description></item>
    ///   <item><description>
    ///     giới hạn theo số ký tự cũng không còn cần thiết, nhưng vẫn giữ
    ///     một mức trần để tránh vượt giới hạn output của model.
    ///   </description></item>
    /// </list>
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

        int size = Math.Min(_config.BatchSize, literals.Count);

        int offset = 0;

        while (offset < literals.Count)
        {
            var slice = literals
                .Skip(offset)
                .Take(size)
                .ToList();

            results.AddRange(
                await TranslateSliceAsync(
                    slice,
                    context,
                    cancellationToken));

            offset += slice.Count;
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

        if (slice.Count == 1)
        {
            return new List<string> { string.Empty };
        }

        const int MaxDepth = 5;

        if (depth >= MaxDepth)
        {
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
    /// </summary>
    private static List<string> AlignResponse(
        IReadOnlyList<string> slice,
        ModelResponse response)
    {
        var result = new List<string>(slice.Count);

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

        foreach (string value in response.Translations)
        {
            if (result.Count >= slice.Count)
            {
                break;
            }

            result.Add(value);
        }

        return result;
    }

    /// <summary>
    /// Thử lại những chuỗi mà model trả về nguyên văn tiếng Anh.
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
            if (i >= result.Count)
            {
                break;
            }

            if (UntranslatedDetector.IsUntranslated(slice[i], result[i]))
            {
                pending.Add(i);
            }
        }

        if (pending.Count == 0)
        {
            return result;
        }

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
    /// Gọi API một lần và phân tích kết quả.
    /// </summary>
    private async Task<ModelResponse> RequestAsync(
        IReadOnlyList<string> literals,
        string? context,
        CancellationToken cancellationToken)
    {
        var request = new ChatCompletionRequest
        {
            Model = _config.Model,
            Temperature = 0.2,
            TopP = 0.9,
            MaxTokens = _config.MaxTokens,
            Messages = new List<ChatMessage>
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
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(
                    $"{_config.Endpoint.TrimEnd('/')}/chat/completions"));

            message.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            message.Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            using var httpResponse = await _httpClient.SendAsync(
                message,
                cts.Token);

            if (!httpResponse.IsSuccessStatusCode)
            {
                string body = await httpResponse.Content.ReadAsStringAsync(
                    cts.Token);

                throw new InvalidOperationException(
                    $"API trả về {(int)httpResponse.StatusCode}: " +
                    body[..Math.Min(500, body.Length)]);
            }

            var chatResponse = await httpResponse.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(cts.Token);

            string content =
                chatResponse?.Choices?.FirstOrDefault()?.Message?.Content
                ?? string.Empty;

            DumpResponse(literals, content);

            return ModelResponseParser.Parse(content);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ModelResponse();
        }
    }

    // ---------------- wire format ----------------

    /// <summary>
    /// Ghi lại phản hồi thô ra file khi biến môi trường
    /// <c>DEADLOCKVN_DEBUG_DUMP</c> được đặt (dùng để chẩn đoán).
    /// </summary>
    private static void DumpResponse(
        IReadOnlyList<string> literals,
        string content)
    {
        string? path =
            Environment.GetEnvironmentVariable("DEADLOCKVN_DEBUG_DUMP");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);

            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.WriteLine("=== REQUEST ===");
            for (int i = 0; i < literals.Count; i++)
            {
                writer.WriteLine($"[{i}] {literals[i]}");
            }

            writer.WriteLine("=== RESPONSE ===");
            writer.WriteLine(content);
            writer.WriteLine();
        }
        catch (IOException)
        {
            // Chẩn đoán không được làm hỏng quá trình dịch.
        }
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.2;

        [JsonPropertyName("top_p")]
        public double TopP { get; set; } = 0.9;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 2048;

        /// <summary>
        /// Ép model trả về JSON.
        /// <para>
        /// API cloud hỗ trợ
        /// <c>response_format: { "type": "json_object" }</c> — nhờ vậy
        /// không còn phụ thuộc vào việc model "tự nhớ" định dạng JSON.
        /// </para>
        /// </summary>
        [JsonPropertyName("response_format")]
        public JsonResponseFormat? ResponseFormat { get; set; } = new();
    }

    private sealed class JsonResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }
}
