namespace DeadlockVN.Core.Entities;

/// <summary>
/// Kết quả vá một file cấu hình của game.
/// </summary>
public sealed class ConfigPatchResult
{
    /// <summary>Đường dẫn file đã vá.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// <c>true</c> nếu file đã được thay đổi; <c>false</c> nếu đã có sẵn
    /// (không làm gì).
    /// </summary>
    public bool Changed { get; set; }

    /// <summary>
    /// <c>true</c> nếu cấu hình đã tồn tại sẵn trong file.
    /// </summary>
    public bool AlreadyPresent { get; set; }

    /// <summary>
    /// Nội dung đã chèn (nếu có).
    /// </summary>
    public string? InsertedContent { get; set; }

    /// <summary>
    /// Bản sao lưu của file gốc trước khi sửa.
    /// </summary>
    public string? BackupPath { get; set; }
}
