namespace DeadlockVN.Core.Common;

/// <summary>
/// Trích xuất các placeholder (token runtime) có trong chuỗi localization
/// của Deadlock.
/// <para>
/// Dữ liệu thật trong <c>citadel_*_english.txt</c> cho thấy có nhiều kiểu
/// token khác nhau, không chỉ <c>{s:...}</c>:
/// </para>
/// <list type="bullet">
///   <item><description><c>{s:sign}</c>, <c>{s:SlowPercent}</c> — số/string.</description></item>
///   <item><description><c>{g:citadel_inline_attribute:'Pulling'}</c> — global.</description></item>
///   <item><description><c>{d:buildNumber}</c>, <c>{i:item_level}</c>, <c>{t:...}</c>, <c>{f:1:damage_meter_duration}</c>.</description></item>
///   <item><description><c>{hero}</c>, <c>{kill}</c>, <c>{Soul}</c> — token không có dấu <c>:</c>.</description></item>
///   <item><description><c>{%param1%}</c> — token dạng tham số.</description></item>
///   <item><description><c>{stat_value, plural, one{kill} other{kills}}</c> — có dấu phẩy và dấu ngoặc lồng.</description></item>
/// </list>
/// <para>
/// Vì vậy không thể dùng regex đơn giản; hàm này quét cân bằng dấu ngoặc.
/// </para>
/// </summary>
public static class PlaceholderExtractor
{
    /// <summary>
    /// Trả về danh sách placeholder tìm thấy trong <paramref name="text"/>,
    /// theo thứ tự xuất hiện, có thể trùng lặp.
    /// </summary>
    public static IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();

        int index = 0;

        while (index < text.Length)
        {
            if (text[index] != '{')
            {
                index++;
                continue;
            }

            int depth = 0;
            int start = index;

            while (index < text.Length)
            {
                char current = text[index];

                if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        index++;
                        break;
                    }
                }

                index++;
            }

            if (depth != 0)
            {
                // Dấu ngoặc không cân bằng -> coi như không có placeholder.
                break;
            }

            result.Add(text[start..index]);
        }

        return result;
    }

    /// <summary>
    /// Trả về danh sách placeholder duy nhất (distinct), giữ thứ tự xuất hiện.
    /// </summary>
    public static IReadOnlyList<string> ExtractDistinct(string? text)
    {
        return Extract(text)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Phân loại placeholder theo tiền tố trước dấu <c>:</c>.
    /// <para>
    /// Ví dụ: <c>{s:sign}</c> -> <c>s</c>; <c>{hero}</c> -> <c>(none)</c>;
    /// <c>{num_damage_rows, plural, one{a} other{b}}</c> -> <c>plural</c>.
    /// </para>
    /// <para>
    /// Chỉ dấu <c>:</c> ở cấp ngoài cùng mới được tính, vì token plural
    /// chứa ngoặc lồng (ví dụ <c>{num_damage_rows, plural, one{Top {i:x}}}</c>).
    /// </para>
    /// </summary>
    public static string GetKind(string placeholder)
    {
        string inner = placeholder.Length >= 2
            ? placeholder[1..^1]
            : string.Empty;

        int depth = 0;

        for (int index = 0; index < inner.Length; index++)
        {
            char current = inner[index];

            if (current == '{')
            {
                depth++;
                continue;
            }

            if (current == '}')
            {
                depth--;
                continue;
            }

            if (current == ':' && depth == 0)
            {
                return inner[..index];
            }
        }

        // Token dạng plural của i18n không có tiền tố dạng {x:...}.
        if (inner.Contains(", plural,", StringComparison.Ordinal))
        {
            return "plural";
        }

        return "(none)";
    }
}
