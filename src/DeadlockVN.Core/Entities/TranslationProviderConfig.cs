namespace DeadlockVN.Core.Entities;

/// <summary>
/// Cấu hình của một provider dịch tự động.
/// </summary>
public sealed class TranslationProviderConfig
{
    /// <summary>
    /// Tên provider: <c>ollama</c>, <c>anthropic</c>, <c>openai</c>…
    /// </summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>
    /// Tên model, ví dụ <c>qwen2.5:7b-instruct</c>.
    /// </summary>
    public string Model { get; set; } = "qwen2.5:7b-instruct";

    /// <summary>
    /// Địa chỉ endpoint của provider.
    /// <para>
    /// Với Ollama là <c>http://localhost:11434</c>. Cũng dùng được để trỏ
    /// sang proxy hoặc instance Ollama trên máy khác trong mạng nội bộ.
    /// </para>
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Số chuỗi trong mỗi lô gửi đi.
    /// <para>
    /// Model 7B chạy trên CPU/GPU yếu thì lô lớn sẽ chậm và dễ "quên"
    /// placeholder. Giá trị an toàn: 20-40.
    /// </para>
    /// </summary>
    public int BatchSize { get; set; } = 30;

    /// <summary>
    /// Số lần thử lại khi bản dịch không qua guard.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Thời gian chờ tối đa cho một lời gọi provider (giây).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Số token tối đa model được phép sinh cho một lô.
    /// <para>
    /// Giá trị quá lớn khiến model 3B lặp token vô hạn và bị Ollama huỷ
    /// ("token repeat limit reached"). Giá trị quá nhỏ khiến JSON bị cắt
    /// cụt và không parse được.
    /// </para>
    /// </summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Tên model có hỗ trợ tiếng Việt tốt, dùng để chọn model mặc định.
    /// </summary>
    public static string DefaultModelFor(string provider) => provider switch
    {
        "ollama" => "qwen2.5:7b-instruct",
        _ => "gpt-4o-mini"
    };
}
