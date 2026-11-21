namespace DeadlockVN.Core.Entities;

/// <summary>
/// Metadata của một file bản dịch (một file JSON trong workspace).
/// </summary>
public sealed class TranslationDocument
{
    /// <summary>Ngôn ngữ nguồn.</summary>
    public string SourceLanguage { get; set; } = "English";

    /// <summary>Ngôn ngữ đích.</summary>
    public string TargetLanguage { get; set; } = "Vietnamese";

    /// <summary>Phiên bản bộ bản dịch.</summary>
    public string Version { get; set; } = "0.1.0";

    /// <summary>Thời điểm cập nhật gần nhất (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Các entry bản dịch.</summary>
    public List<TranslationEntry> Entries { get; set; } = new();
}
