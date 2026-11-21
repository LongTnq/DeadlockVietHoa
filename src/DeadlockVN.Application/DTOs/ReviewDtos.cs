using System.Text.Json;
using System.Text.Json.Serialization;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;

namespace DeadlockVN.Application.DTOs;

/// <summary>
/// Một entry được xuất ra để người dịch review.
/// </summary>
public sealed class ReviewItem
{
    /// <summary>Localization key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>File nguồn chứa key.</summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>Nhóm nội dung.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Văn bản tiếng Anh gốc.</summary>
    public string Original { get; set; } = string.Empty;

    /// <summary>Bản dịch tiếng Việt hiện tại.</summary>
    public string Translation { get; set; } = string.Empty;

    /// <summary>Trạng thái hiện tại.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Bản dịch mới do người dịch sửa (để trả lại).</summary>
    public string? NewTranslation { get; set; }

    /// <summary>Trạng thái mới do người dịch đặt (để trả lại).</summary>
    public string? NewStatus { get; set; }
}

/// <summary>
/// File xuất cho người dịch review.
/// </summary>
public sealed class ReviewExportFile
{
    /// <summary>Metadata.</summary>
    public ReviewExportMetadata Metadata { get; set; } = new();

    /// <summary>Các entry.</summary>
    public List<ReviewItem> Items { get; set; } = new();
}

/// <summary>
/// Metadata của file xuất review.
/// </summary>
public sealed class ReviewExportMetadata
{
    /// <summary>Ngôn ngữ đích.</summary>
    public string TargetLanguage { get; set; } = "Vietnamese";

    /// <summary>Thời điểm xuất (UTC).</summary>
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Tên nhóm nội dung (khi xuất theo từng nhóm).</summary>
    public string? Category { get; set; }

    /// <summary>Ghi chú hướng dẫn cho người dịch.</summary>
    public string Instructions { get; set; } =
        "Sửa trường 'newTranslation' nếu bản dịch cần chỉnh. " +
        "Đặt 'newStatus' thành 'Approved' nếu đã duyệt xong, " +
        "hoặc 'Reviewed' nếu vẫn cần xem lại. " +
        "Bỏ trống 'newTranslation' nghĩa là giữ nguyên bản dịch hiện tại.";
}

/// <summary>
/// Kết quả import review.
/// </summary>
public sealed class ReviewImportResult
{
    /// <summary>Số entry đã cập nhật.</summary>
    public int Updated { get; set; }

    /// <summary>Số entry không tìm thấy trong cơ sở dữ liệu.</summary>
    public int NotFound { get; set; }

    /// <summary>Số entry bị bỏ qua (giữ nguyên).</summary>
    public int Skipped { get; set; }

    /// <summary>Số entry lên trạng thái Approved.</summary>
    public int Approved { get; set; }

    /// <summary>Chi tiết lỗi.</summary>
    public List<string> Errors { get; } = new();
}

/// <summary>
/// Trạng thái review dùng cho báo cáo.
/// </summary>
public sealed class ReviewSummary
{
    /// <summary>Tổng số entry.</summary>
    public int Total { get; set; }

    /// <summary>Số entry đã dịch bằng máy (Reviewed).</summary>
    public int MachineTranslated { get; set; }

    /// <summary>Số entry đã được duyệt (Approved).</summary>
    public int Approved { get; set; }

    /// <summary>Số entry chưa dịch (Pending).</summary>
    public int Pending { get; set; }

    /// <summary>Số entry theo từng nhóm.</summary>
    public Dictionary<string, int> ByCategory { get; set; } = new();
}

/// <summary>
/// Serializer JSON dùng chung cho file review.
/// </summary>
public static class ReviewJson
{
    /// <summary>
    /// Tuỳ chọn ghi file — không BOM, camelCase, giữ nguyên ký tự tiếng Việt.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Tuỳ chọn đọc file — không phân biệt hoa thường, cho phép comment.
    /// </summary>
    public static JsonSerializerOptions ReadOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
