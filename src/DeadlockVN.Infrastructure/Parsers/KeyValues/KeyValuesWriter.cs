using System.Text;

using DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

namespace DeadlockVN.Infrastructure.Parsers.KeyValues;

/// <summary>
/// Ghi cây KeyValues ra file theo định dạng của game.
/// <para>
/// <b>Lưu ý quan trọng:</b> exporter chính <b>không</b> dùng writer này —
/// nó thay thế tại chỗ trên file gốc để giữ nguyên mọi chi tiết định dạng.
/// Writer tồn tại cho các trường hợp cần tạo file mới từ đầu (ví dụ
/// sinh file header cho một ngôn ngữ mới) và làm công cụ kiểm chứng.
/// </para>
/// </summary>
public sealed class KeyValuesWriter
{
    private readonly StringBuilder _builder = new();

    /// <summary>
    /// Chuyển đổi ký tự xuống dòng khi ghi file.
    /// </summary>
    public string NewLine { get; set; } = "\r\n";

    /// <summary>
    /// Ghi một tài liệu KeyValues ra chuỗi.
    /// </summary>
    public string Write(KeyValuesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _builder.Clear();

        WriteNodes(document.Nodes, depth: 0);

        return _builder.ToString();
    }

    private void WriteNodes(IReadOnlyList<KeyValuesNode> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            WriteIndent(depth);

            if (node.IsValue)
            {
                _builder.Append(KeyValuesEscaper.Quote(node.Key));
                _builder.Append(' ');
                _builder.Append(KeyValuesEscaper.Quote(node.Value));
                _builder.Append(NewLine);

                continue;
            }

            _builder.Append(KeyValuesEscaper.Quote(node.Key));
            _builder.Append(NewLine);

            WriteIndent(depth);
            _builder.Append('{');
            _builder.Append(NewLine);

            WriteNodes(node.Children, depth + 1);

            WriteIndent(depth);
            _builder.Append('}');
            _builder.Append(NewLine);
        }
    }

    private void WriteIndent(int depth)
    {
        for (int i = 0; i < depth; i++)
        {
            _builder.Append('\t');
        }
    }
}
