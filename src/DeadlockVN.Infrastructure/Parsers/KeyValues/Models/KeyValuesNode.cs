namespace DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

/// <summary>
/// Một node trong cây KeyValues: hoặc là cặp key/value, hoặc là một
/// object chứa các node con.
/// </summary>
public sealed class KeyValuesNode
{
    /// <summary>Khóa (localization key). Không bao giờ được thay đổi khi dịch.</summary>
    public string Key { get; }

    /// <summary>Giá trị, hoặc <c>null</c> nếu node là object (có <c>{ }</c>).</summary>
    public string? Value { get; }

    /// <summary>Các node con (rỗng với node dạng key/value).</summary>
    public IReadOnlyList<KeyValuesNode> Children { get; }

    /// <summary>
    /// Vị trí của <b>giá trị thô</b> (bao gồm dấu ngoặc kép và escape) trong
    /// file nguồn. Chỉ có giá trị khi node là cặp key/value.
    /// <para>
    /// Dùng để thay thế tại chỗ khi xuất bản dịch, giữ nguyên phần còn lại
    /// của file.
    /// </para>
    /// </summary>
    public KeyValuesSpan ValueSpan { get; }

    /// <summary><c>true</c> nếu node là object (có <c>{ }</c>).</summary>
    public bool IsObject => Value is null;

    /// <summary><c>true</c> nếu node là cặp key/value.</summary>
    public bool IsValue => Value is not null;

    public KeyValuesNode(
        string key,
        string? value,
        IReadOnlyList<KeyValuesNode>? children = null,
        KeyValuesSpan valueSpan = default)
    {
        Key = key;
        Value = value;
        Children = children ?? Array.Empty<KeyValuesNode>();
        ValueSpan = valueSpan;
    }
}
