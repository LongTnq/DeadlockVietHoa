using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;

namespace DeadlockVN.Core.Entities;

/// <summary>
/// Kết quả validate của một tập bản dịch.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>Danh sách vấn đề phát hiện được.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();

    /// <summary>Tổng số entry đã kiểm tra.</summary>
    public int CheckedCount { get; init; }

    /// <summary>Số entry đã có bản dịch (không rỗng).</summary>
    public int TranslatedCount { get; init; }

    /// <summary>Số entry đã được duyệt.</summary>
    public int ApprovedCount { get; init; }

    /// <summary>Có lỗi mức <see cref="ValidationSeverity.Error"/> hay không.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error);

    /// <summary>Có cảnh báo hay không.</summary>
    public bool HasWarnings => Issues.Any(i => i.Severity == ValidationSeverity.Warning);

    /// <summary>Có thể đóng gói VPK hay không (không có lỗi Error).</summary>
    public bool CanBuild => !HasErrors;

    /// <summary>Các issue theo từng rule.</summary>
    public IReadOnlyDictionary<ValidationRule, int> IssuesByRule =>
        Issues
            .GroupBy(i => i.Rule)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

    /// <summary>Các issue theo từng mức độ.</summary>
    public IReadOnlyDictionary<ValidationSeverity, int> IssuesBySeverity =>
        Issues
            .GroupBy(i => i.Severity)
            .ToDictionary(
                group => group.Key,
                group => group.Count());
}
