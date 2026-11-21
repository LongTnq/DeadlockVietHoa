namespace DeadlockVN.Core.Entities;

/// <summary>
/// Kết quả xuất một file KeyValues đã dịch.
/// </summary>
public sealed class LocalizationExportResult
{
    /// <summary>Đường dẫn file đã ghi.</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Tổng số entry có trong file gốc.
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Số entry đã được thay bằng bản dịch tiếng Việt.
    /// </summary>
    public int TranslatedEntries { get; set; }

    /// <summary>
    /// Số entry chưa có bản dịch — giữ nguyên tiếng Anh gốc.
    /// </summary>
    public int UntranslatedEntries { get; set; }

    /// <summary>
    /// Các key trong bản dịch nhưng <b>không tồn tại</b> trong file gốc.
    /// <para>
    /// Đây là lỗi nghiêm trọng: key không tồn tại nghĩa là bản dịch được
    /// tạo cho một phiên bản game khác, hoặc đã nhầm key.
    /// </para>
    /// </summary>
    public List<string> UnknownKeys { get; set; } = new();

    /// <summary>
    /// Các key trong file gốc nhưng không có trong bản dịch.
    /// </summary>
    public List<string> MissingKeys { get; set; } = new();

    /// <summary>
    /// File gốc có BOM UTF-8 hay không (được giữ nguyên khi ghi).
    /// </summary>
    public bool HadByteOrderMark { get; set; }

    /// <summary>
    /// <c>true</c> nếu header <c>"Language"</c> đã được đổi sang ngôn ngữ đích.
    /// </summary>
    public bool LanguageHeaderRewritten { get; set; }

    /// <summary>
    /// <c>true</c> nếu không có key nào lạ (xem <see cref="UnknownKeys"/>).
    /// </summary>
    public bool IsValid => UnknownKeys.Count == 0;
}
