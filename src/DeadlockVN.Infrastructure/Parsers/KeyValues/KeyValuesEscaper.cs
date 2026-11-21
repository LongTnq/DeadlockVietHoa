using System.Text;

namespace DeadlockVN.Infrastructure.Parsers.KeyValues;

/// <summary>
/// Quy tắc escape của định dạng Valve KeyValues (KV1).
/// <para>
/// Đã đối chiếu với 11 file localization gốc của Deadlock:
/// </para>
/// <list type="bullet">
/// <item><description><c>\"</c> — dấu nháy kép (3 700 lần).</description></item>
/// <item><description><c>\\</c> — backslash.</description></item>
/// <item><description>
/// <c>\n</c> — newline. Engine Valve giải mã nó thành newline thật khi nạp
/// file (bằng chứng: 17/29 bản dịch chính thức của game vẫn giữ <c>\n</c>
/// trong file <c>citadel_heroes</c> — nếu không decode, game sẽ hiện chữ
/// <c>"n"</c> trong tooltip).
/// </description></item>
/// </list>
/// <para>
/// Ngoài ra giữ nguyên mọi escape lạ (ví dụ <c>\'</c>) để không làm mất
/// dữ liệu khi round-trip.
/// </para>
/// </summary>
public static class KeyValuesEscaper
{
    /// <summary>
    /// Escape một giá trị để đặt trong ngoặc kép của file KV1.
    /// </summary>
    /// <param name="value">Giá trị đã unescape (nội dung thực của game).</param>
    /// <returns>Giá trị đã escape, chưa bọc ngoặc kép.</returns>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var result = new StringBuilder(value.Length + 8);

        foreach (char current in value)
        {
            switch (current)
            {
                case '\\':
                    result.Append("\\\\");
                    break;

                case '"':
                    result.Append("\\\"");
                    break;

                case '\n':
                    // Engine decode \n thành newline thật -> vẫn hiển thị
                    // đúng như ý định.
                    result.Append("\\n");
                    break;

                case '\r':
                    result.Append("\\r");
                    break;

                case '\t':
                    result.Append("\\t");
                    break;

                default:
                    result.Append(current);
                    break;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Bọc giá trị đã escape trong ngoặc kép.
    /// </summary>
    public static string Quote(string? value)
    {
        return $"\"{Escape(value)}\"";
    }
}
