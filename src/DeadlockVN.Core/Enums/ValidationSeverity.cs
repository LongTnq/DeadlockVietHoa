namespace DeadlockVN.Core.Enums;

/// <summary>
/// Mức độ nghiêm trọng của một lỗi validation.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Chỉ là thông tin, không chặn build.</summary>
    Info = 0,

    /// <summary>Cảnh báo, nên xử lý nhưng không bắt buộc.</summary>
    Warning = 1,

    /// <summary>Lỗi, chặn build VPK.</summary>
    Error = 2
}
