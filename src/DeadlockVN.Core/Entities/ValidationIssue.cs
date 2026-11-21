using DeadlockVN.Core.Enums;

namespace DeadlockVN.Core.Entities;

/// <summary>
/// Một vấn đề được phát hiện khi validate một bản dịch.
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>Localization key liên quan (rỗng nếu lỗi ở cấp file).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Loại kiểm tra đã vi phạm.</summary>
    public ValidationRule Rule { get; init; }

    /// <summary>Mức độ nghiêm trọng.</summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>Thông điệp mô tả vấn đề.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gợi ý cách sửa.</summary>
    public string? Suggestion { get; init; }

    public override string ToString()
    {
        return $"[{Severity}] {Rule} @ {Key}: {Message}";
    }
}
