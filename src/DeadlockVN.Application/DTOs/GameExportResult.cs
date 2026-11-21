namespace DeadlockVN.Application.DTOs;

/// <summary>
/// Kết quả xuất toàn bộ bộ bản dịch ra thư mục staging (dạng file game).
/// </summary>
public sealed class GameExportResult
{
    /// <summary>Thư mục staging đã ghi.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Tổng số entry gốc trong các file nguồn.</summary>
    public int TotalEntries { get; set; }

    /// <summary>Số entry đã được dịch và ghi ra file.</summary>
    public int TranslatedEntries { get; set; }

    /// <summary>Số entry chưa dịch (giữ nguyên tiếng Anh).</summary>
    public int UntranslatedEntries { get; set; }

    /// <summary>Số file đã ghi.</summary>
    public int FileCount { get; set; }

    /// <summary>Ngôn ngữ đích đã ghi vào header.</summary>
    public string TargetLanguage { get; set; } = "vietnamese";

    /// <summary>
    /// Các key trong bản dịch không tồn tại trong file gốc — lỗi nghiêm trọng,
    /// thường do dịch nhầm phiên bản game.
    /// </summary>
    public List<string> UnknownKeys { get; set; } = new();

    /// <summary>Chi tiết từng file đã xuất.</summary>
    public List<GameExportFileInfo> Files { get; set; } = new();
}

/// <summary>
/// Thông tin một file đã xuất ra thư mục staging.
/// </summary>
public sealed class GameExportFileInfo
{
    /// <summary>Đường dẫn file nguồn (tiếng Anh).</summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>Đường dẫn file đã xuất.</summary>
    public string OutputFile { get; set; } = string.Empty;

    /// <summary>Số entry trong file.</summary>
    public int TotalEntries { get; set; }

    /// <summary>Số entry đã dịch.</summary>
    public int TranslatedEntries { get; set; }

    /// <summary>File gốc có BOM UTF-8 hay không.</summary>
    public bool HadByteOrderMark { get; set; }
}
