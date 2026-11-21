namespace DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

/// <summary>
/// Một token đã được lexer tách ra từ file KeyValues.
/// <para>
/// Chỉ dùng nội bộ trong assembly Infrastructure. Xem
/// <c>InternalsVisibleTo</c> trong <c>DeadlockVN.Infrastructure.csproj</c>.
/// </para>
/// </summary>
internal sealed record KeyValuesToken(
    KeyValuesTokenType Type,
    string Value,
    int Line,
    int Column,
    int Start,
    int Length)
{
    /// <summary>
    /// Vị trí kết thúc (exclusive) của token trong chuỗi nguồn.
    /// </summary>
    public int End => Start + Length;
}
