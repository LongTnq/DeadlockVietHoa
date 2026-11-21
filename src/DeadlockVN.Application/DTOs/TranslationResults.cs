using DeadlockVN.Core.Entities;

namespace DeadlockVN.Application.DTOs;

/// <summary>
/// Kết quả thao tác xuất bộ bản dịch ra file JSON.
/// </summary>
public sealed class ExportResult
{
    /// <summary>Số entry đã xuất.</summary>
    public int ExportedCount { get; init; }

    /// <summary>Số entry đã có bản dịch (không rỗng).</summary>
    public int TranslatedCount { get; init; }

    /// <summary>Đường dẫn file đã ghi.</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>Số entry theo từng nhóm nội dung.</summary>
    public IReadOnlyDictionary<string, int> ByCategory { get; init; } =
        new Dictionary<string, int>();
}

/// <summary>
/// Kết quả thao tác nạp bản dịch vào bộ bản dịch hiện tại.
/// </summary>
public sealed class ImportResult
{
    /// <summary>Số entry trong file nguồn.</summary>
    public int SourceCount { get; init; }

    /// <summary>Số entry mới được thêm vào.</summary>
    public int AddedCount { get; init; }

    /// <summary>Số entry được cập nhật (bản dịch đã tồn tại).</summary>
    public int UpdatedCount { get; init; }

    /// <summary>Số entry bị bỏ qua (đã có bản dịch tốt hơn).</summary>
    public int SkippedCount { get; init; }

    /// <summary>Danh sách key bị bỏ qua kèm lý do.</summary>
    public IReadOnlyList<string> Skipped { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Báo cáo tiến độ Việt hóa.
/// </summary>
public sealed class TranslationReport
{
    /// <summary>Tổng số entry.</summary>
    public int Total { get; init; }

    /// <summary>Số entry đã có bản dịch.</summary>
    public int Translated { get; init; }

    /// <summary>Số entry đã được duyệt.</summary>
    public int Approved { get; init; }

    /// <summary>Số entry chưa dịch.</summary>
    public int Pending { get; init; }

    /// <summary>Số entry đang dịch.</summary>
    public int Translating { get; init; }

    /// <summary>Số entry đã dịch nhưng chưa review.</summary>
    public int Reviewed { get; init; }

    /// <summary>Số entry có bản dịch lỗi thời (nội dung gốc đã đổi).</summary>
    public int Stale { get; init; }

    /// <summary>Phần trăm đã dịch (0-100).</summary>
    public double TranslatedPercent => Total == 0 ? 0 : Translated * 100.0 / Total;

    /// <summary>Phần trăm đã duyệt (0-100).</summary>
    public double ApprovedPercent => Total == 0 ? 0 : Approved * 100.0 / Total;

    /// <summary>Thống kê theo từng nhóm nội dung.</summary>
    public IReadOnlyList<CategoryProgress> Categories { get; init; } =
        Array.Empty<CategoryProgress>();
}

/// <summary>
/// Tiến độ của một nhóm nội dung.
/// </summary>
public sealed class CategoryProgress
{
    /// <summary>Tên nhóm.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Tổng số entry.</summary>
    public int Total { get; init; }

    /// <summary>Số entry đã có bản dịch.</summary>
    public int Translated { get; init; }

    /// <summary>Số entry đã được duyệt.</summary>
    public int Approved { get; init; }

    /// <summary>Phần trăm đã dịch (0-100).</summary>
    public double TranslatedPercent => Total == 0 ? 0 : Translated * 100.0 / Total;
}
