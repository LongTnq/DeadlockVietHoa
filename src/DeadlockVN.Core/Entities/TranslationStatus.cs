namespace DeadlockVN.Core.Entities;

/// <summary>
/// Trạng thái của một bản dịch trong quy trình review.
/// </summary>
public enum TranslationStatus
{
    /// <summary>Chưa có bản dịch.</summary>
    Pending = 0,

    /// <summary>Đang dịch.</summary>
    Translating = 1,

    /// <summary>Đã dịch xong, chờ review.</summary>
    Reviewed = 2,

    /// <summary>Đã được duyệt, có thể đóng gói.</summary>
    Approved = 3
}
