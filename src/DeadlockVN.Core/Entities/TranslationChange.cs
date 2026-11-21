using DeadlockVN.Core.Enums;

namespace DeadlockVN.Core.Entities;

/// <summary>
/// Một thay đổi được phát hiện khi so sánh bản dịch hiện tại với bản gốc mới.
/// </summary>
public sealed class TranslationChange
{
    /// <summary>Định danh entry.</summary>
    public TranslationId Id { get; init; }

    /// <summary>Loại thay đổi.</summary>
    public TranslationChangeKind Kind { get; init; }

    /// <summary>Nội dung gốc cũ (null nếu key mới).</summary>
    public string? OldOriginalText { get; init; }

    /// <summary>Nội dung gốc mới (null nếu key đã bị xoá).</summary>
    public string? NewOriginalText { get; init; }

    /// <summary>Bản dịch hiện có (null nếu key mới hoặc đã bị xoá).</summary>
    public string? TranslatedText { get; init; }

    /// <summary>Trạng thái hiện tại của bản dịch.</summary>
    public TranslationStatus Status { get; init; }

    public override string ToString() => $"{Kind}: {Id}";
}
