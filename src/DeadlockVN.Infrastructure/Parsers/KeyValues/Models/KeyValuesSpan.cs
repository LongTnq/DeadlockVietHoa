namespace DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

/// <summary>
/// Vị trí của một đoạn văn bản trong file nguồn.
/// <para>
/// Dùng để thay thế <b>tại chỗ</b> trong file gốc thay vì serialize lại toàn bộ
/// cây KeyValues. Nhờ vậy comment, thụt lề, thứ tự escape và mọi chi tiết
/// định dạng của file game được giữ nguyên.
/// </para>
/// </summary>
public readonly record struct KeyValuesSpan(int Start, int Length)
{
    /// <summary>Vị trí kết thúc (exclusive).</summary>
    public int End => Start + Length;

    /// <summary>Span rỗng.</summary>
    public static KeyValuesSpan Empty => new(0, 0);

    public bool IsEmpty => Length == 0;
}
