namespace DeadlockVN.Core.Entities;

/// <summary>
/// Kết quả đóng gói bộ cài đặt cho mod.
/// </summary>
public sealed class ModPackageResult
{
    /// <summary>Thư mục output.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Số file localization đã ghi.</summary>
    public int LocalizationFileCount { get; set; }

    /// <summary>Tổng số entry đã dịch.</summary>
    public int TranslatedEntries { get; set; }

    /// <summary>Tổng số entry (kể cả chưa dịch).</summary>
    public int TotalEntries { get; set; }

    /// <summary>Các file localization đã ghi, kèm đường dẫn tương đối.</summary>
    public List<string> LocalizationFiles { get; set; } = new();

    /// <summary>Kết quả vá <c>gameinfo.gi</c> (nếu có).</summary>
    public ConfigPatchResult? GameInfoPatch { get; set; }

    /// <summary>Kết quả vá <c>fonts.conf</c> (nếu có).</summary>
    public ConfigPatchResult? FontsPatch { get; set; }

    /// <summary>
    /// Các key trong bản dịch không tồn tại trong file gốc.
    /// </summary>
    public List<string> UnknownKeys { get; set; } = new();

    /// <summary>
    /// Đường dẫn tương đối của <c>fonts.conf</c> trong thư mục output
    /// (để đóng gói cùng VPK nếu cần).
    /// </summary>
    public string? FontsConfRelativePath { get; set; }

    /// <summary>
    /// Các họ font đã phát hiện thiếu glyph tiếng Việt và cần fallback.
    /// <para>
    /// Rỗng nghĩa là không cần vá <c>fonts.conf</c>.
    /// </para>
    /// </summary>
    public List<string> DeficientFontFamilies { get; set; } = new();
}
