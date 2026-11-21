namespace DeadlockVN.Core.Entities;

/// <summary>
/// So sánh định danh entry theo cặp (file nguồn, key).
/// </summary>
public sealed class TranslationIdComparer : IEqualityComparer<TranslationId>
{
    /// <summary>
    /// Bản dùng chung (thread-safe, không có trạng thái).
    /// </summary>
    public static readonly TranslationIdComparer Instance = new();

    public bool Equals(TranslationId x, TranslationId y)
    {
        return string.Equals(x.SourceFile, y.SourceFile, StringComparison.Ordinal) &&
               string.Equals(x.Key, y.Key, StringComparison.Ordinal);
    }

    public int GetHashCode(TranslationId obj)
    {
        return HashCode.Combine(
            obj.SourceFile is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.SourceFile),
            obj.Key is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.Key));
    }
}
