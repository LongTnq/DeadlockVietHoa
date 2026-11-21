namespace DeadlockVN.Application.DTOs;

/// <summary>
/// Kết quả xuất bộ bản dịch ra nhiều file (tách theo nhóm nội dung).
/// </summary>
public sealed class SplitExportResult
{
    /// <summary>Số entry đã xuất.</summary>
    public int ExportedCount { get; init; }

    /// <summary>Số entry đã có bản dịch (không rỗng).</summary>
    public int TranslatedCount { get; init; }

    /// <summary>Thư mục gốc chứa các file đã tách.</summary>
    public string RootDirectory { get; init; } = string.Empty;

    /// <summary>Danh sách file đã ghi.</summary>
    public IReadOnlyList<CategoryFileInfo> Files { get; init; } = Array.Empty<CategoryFileInfo>();
}

/// <summary>
/// Thông tin một file bản dịch của một nhóm.
/// </summary>
/// <param name="Category">Tên nhóm (thư mục con).</param>
/// <param name="FileName">Tên file.</param>
/// <param name="Path">Đường dẫn đầy đủ.</param>
/// <param name="SizeBytes">Kích thước file (byte).</param>
public sealed record CategoryFileInfo(
    string Category,
    string FileName,
    string Path,
    long SizeBytes);
