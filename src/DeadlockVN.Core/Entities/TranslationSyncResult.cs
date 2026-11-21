using DeadlockVN.Core.Entities;

namespace DeadlockVN.Core.Entities;

/// <summary>
/// Kết quả đồng bộ bộ bản dịch với bản gốc mới.
/// </summary>
public sealed class TranslationSyncResult
{
    /// <summary>Các thay đổi đã phát hiện.</summary>
    public IReadOnlyList<TranslationChange> Changes { get; init; } = Array.Empty<TranslationChange>();

    /// <summary>Các key mới xuất hiện trong game.</summary>
    public IReadOnlyList<TranslationChange> Added { get; init; } = Array.Empty<TranslationChange>();

    /// <summary>Các key có nội dung gốc đã thay đổi.</summary>
    public IReadOnlyList<TranslationChange> Modified { get; init; } = Array.Empty<TranslationChange>();

    /// <summary>Các key không còn tồn tại trong game.</summary>
    public IReadOnlyList<TranslationChange> Removed { get; init; } = Array.Empty<TranslationChange>();

    /// <summary>Tổng số entry sau khi đồng bộ.</summary>
    public int TotalEntries { get; init; }
}
