namespace DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

/// <summary>
/// Kết quả parse của một file KeyValues.
/// </summary>
public sealed class KeyValuesDocument
{
    /// <summary>Các node ở cấp gốc của file.</summary>
    public IReadOnlyList<KeyValuesNode> Nodes { get; }

    public KeyValuesDocument(
        IReadOnlyList<KeyValuesNode> nodes)
    {
        Nodes = nodes;
    }

    /// <summary>
    /// Duyệt tất cả cặp key/value trong tài liệu, theo thứ tự xuất hiện
    /// trong file, với đường dẫn đầy đủ (ví dụ <c>lang/Tokens/upgrade_x</c>).
    /// </summary>
    public IEnumerable<(string Path, string Key, string Value)> EnumerateEntries()
    {
        return EnumerateEntries(Nodes, string.Empty);
    }

    private static IEnumerable<(string Path, string Key, string Value)> EnumerateEntries(
        IReadOnlyList<KeyValuesNode> nodes,
        string prefix)
    {
        foreach (var node in nodes)
        {
            string path = prefix.Length == 0
                ? node.Key
                : $"{prefix}/{node.Key}";

            if (node.IsValue)
            {
                yield return (path, node.Key, node.Value!);
            }
            else
            {
                foreach (var entry in EnumerateEntries(node.Children, path))
                {
                    yield return entry;
                }
            }
        }
    }
}
