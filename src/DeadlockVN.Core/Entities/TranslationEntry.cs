using DeadlockVN.Core.Enums;

namespace DeadlockVN.Core.Entities;

/// <summary>
/// Một entry bản dịch tương ứng với một localization key của game.
/// </summary>
public sealed class TranslationEntry
{
    /// <summary>
    /// Định danh duy nhất: file nguồn + localization key.
    /// </summary>
    public TranslationId Id { get; set; }

    /// <summary>Localization key (không bao giờ được thay đổi).</summary>
    public string Key => Id.Key;

    /// <summary>
    /// Đường dẫn file gốc chứa key này, tương đối so với thư mục localization.
    /// </summary>
    public string SourceFile => Id.SourceFile;

    /// <summary>Nội dung tiếng Anh gốc, lấy từ file game.</summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>Nội dung tiếng Việt.</summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>Trạng thái review.</summary>
    public TranslationStatus Status { get; set; } = TranslationStatus.Pending;

    /// <summary>Nhóm nội dung, dùng để chia nhỏ công việc dịch.</summary>
    public LocalizationCategory Category { get; set; } = LocalizationCategory.Misc;

    /// <summary>
    /// Nội dung gốc tại thời điểm bản dịch được tạo.
    /// <para>
    /// Dùng để phát hiện game đã đổi nội dung gốc sau đó. Nếu
    /// <see cref="OriginalText"/> khác <see cref="OriginalTextSnapshot"/>,
    /// bản dịch có thể đã lỗi thời và cần review lại.
    /// </para>
    /// </summary>
    public string OriginalTextSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// Bản dịch có cần được review lại vì nội dung gốc đã thay đổi không.
    /// </summary>
    public bool IsStale =>
        !string.IsNullOrEmpty(OriginalTextSnapshot) &&
        !string.Equals(OriginalText, OriginalTextSnapshot, StringComparison.Ordinal);
}
