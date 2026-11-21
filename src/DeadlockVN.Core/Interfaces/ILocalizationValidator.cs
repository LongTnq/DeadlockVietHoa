using DeadlockVN.Core.Entities;

namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Kiểm tra chất lượng một tập bản dịch.
/// </summary>
public interface ILocalizationValidator
{
    /// <summary>
    /// Validate toàn bộ tập bản dịch.
    /// </summary>
    ValidationResult Validate(IEnumerable<TranslationEntry> entries);

    /// <summary>
    /// Validate một entry đơn lẻ.
    /// </summary>
    IReadOnlyList<Entities.ValidationIssue> ValidateEntry(TranslationEntry entry);
}
