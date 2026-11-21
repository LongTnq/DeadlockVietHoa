namespace DeadlockVN.Core.Enums;

/// <summary>
/// Loại kiểm tra validation.
/// </summary>
public enum ValidationRule
{
    /// <summary>Bản dịch rỗng nhưng bản gốc không rỗng.</summary>
    MissingTranslation = 1,

    /// <summary>Bản dịch trùng với bản gốc (chưa dịch).</summary>
    Untranslated = 2,

    /// <summary>Thiếu placeholder trong bản dịch.</summary>
    MissingPlaceholder = 3,

    /// <summary>Thừa placeholder trong bản dịch.</summary>
    ExtraPlaceholder = 4,

    /// <summary>Placeholder bị thay đổi (không còn khớp).</summary>
    MismatchedPlaceholder = 5,

    /// <summary>Số lượng placeholder không khớp.</summary>
    PlaceholderCountMismatch = 6,

    /// <summary>Thẻ HTML mở không đóng (hoặc đóng không mở).</summary>
    UnbalancedHtmlTag = 7,

    /// <summary>Có ký tự tiếng Việt không thể hiển thị.</summary>
    UnsupportedCharacter = 8,

    /// <summary>Bản dịch có khoảng trắng thừa ở đầu/cuối.</summary>
    LeadingOrTrailingWhitespace = 9,

    /// <summary>Localization key bị trùng lặp.</summary>
    DuplicateKey = 10,

    /// <summary>Bản dịch chứa ký tự điều khiển không hợp lệ.</summary>
    ControlCharacter = 11,

    /// <summary>Placeholder printf-style bị thay đổi.</summary>
    PrintfTokenMismatch = 12,

    /// <summary>Bản dịch quá dài so với bản gốc (có thể vỡ layout).</summary>
    ExcessiveLength = 13
}
