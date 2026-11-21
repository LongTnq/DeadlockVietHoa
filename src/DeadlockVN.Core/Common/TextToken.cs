namespace DeadlockVN.Core.Common;

/// <summary>
/// Loại token runtime tìm thấy trong chuỗi localization.
/// </summary>
public enum TextTokenKind
{
    /// <summary>
    /// Token trong ngoặc kép: <c>{s:damage}</c>, <c>{g:...}</c>,
    /// <c>{hero}</c>, <c>{%param1%}</c>, hoặc token plural i18n.
    /// </summary>
    Braced = 1,

    /// <summary>
    /// Token printf-style: <c>%s1</c>, <c>%s2</c>.
    /// </summary>
    Printf = 2,

    /// <summary>
    /// Token dạng tên: <c>%partner%</c>, <c>%param0%</c>.
    /// </summary>
    PercentName = 3
}

/// <summary>
/// Một token runtime được trích xuất từ chuỗi localization.
/// </summary>
/// <param name="Kind">Loại token.</param>
/// <param name="Value">Giá trị thô, ví dụ <c>{s:damage}</c>.</param>
public readonly record struct TextToken(TextTokenKind Kind, string Value)
{
    public override string ToString() => Value;
}
