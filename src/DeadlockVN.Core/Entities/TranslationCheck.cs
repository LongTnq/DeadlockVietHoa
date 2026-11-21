namespace DeadlockVN.Core.Entities;

/// <summary>
/// Kết quả kiểm tra một bản dịch do máy sinh ra.
/// </summary>
public sealed class TranslationCheck
{
    /// <summary>
    /// Bản dịch hợp lệ — không có lỗi nghiêm trọng nào.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Các lỗi nghiêm trọng khiến bản dịch không được lưu.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Các cảnh báo không chặn lưu (ví dụ bản dịch trùng nguyên văn bản gốc).
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Tạo kết quả hợp lệ, không lỗi.
    /// </summary>
    public static TranslationCheck Valid { get; } = new();
}
