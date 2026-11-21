using System.Text.RegularExpressions;

namespace DeadlockVN.Core.Common;

/// <summary>
/// Trích xuất toàn bộ token runtime có trong chuỗi localization của Deadlock.
/// <para>
/// Dữ liệu thật trong <c>citadel_*_english.txt</c> cho thấy có 3 nhóm token:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Braced</b> — <c>{s:damage}</c>, <c>{g:citadel_inline_attribute:'Pulling'}</c>,
///     <c>{d:buildNumber}</c>, <c>{i:item_level}</c>, <c>{t:...}</c>,
///     <c>{f:1:duration}</c>, <c>{hero}</c>, <c>{%param1%}</c>, và token plural
///     i18n <c>{x, plural, one{...} other{...}}</c>.
///   </description></item>
///   <item><description>
///     <b>Printf</b> — <c>%s1</c>, <c>%s2</c>, ... (định dạng chuỗi cũ của game).
///   </description></item>
///   <item><description>
///     <b>PercentName</b> — <c>%partner%</c>, <c>%param0%</c>.
///   </description></item>
/// </list>
/// <para>
/// Token braced có thể chứa token braced khác (token plural lồng ngoặc), nên
/// không thể dùng regex đơn giản — phải quét cân bằng dấu ngoặc.
/// </para>
/// </summary>
public static partial class TextTokenExtractor
{
    /// <summary>
    /// Trích xuất tất cả token (kể cả trùng lặp), theo thứ tự xuất hiện.
    /// </summary>
    public static IReadOnlyList<TextToken> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<TextToken>();
        }

        var result = new List<TextToken>();

        // 1. Token braced — quét cân bằng ngoặc.
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
                // Dấu ngoặc không cân bằng -> coi như không có token.
                break;
            }

            string candidate = text[start..index];

            // "{#}" là token rỗng, không mang ý nghĩa -> bỏ qua.
            if (candidate.Length > 2)
            {
                result.Add(new TextToken(TextTokenKind.Braced, candidate));
            }
        }

        // 2. Token printf và percent-name — chỉ trên phần chưa bị token braced chiếm.
        string masked = MaskBracedTokens(text);

        foreach (Match match in PrintfTokenRegex().Matches(masked))
        {
            result.Add(new TextToken(TextTokenKind.Printf, match.Value));
        }

        foreach (Match match in PercentNameRegex().Matches(masked))
        {
            result.Add(new TextToken(TextTokenKind.PercentName, match.Value));
        }

        return result;
    }

    /// <summary>
    /// Trả về danh sách token duy nhất (distinct), giữ thứ tự xuất hiện.
    /// </summary>
    public static IReadOnlyList<TextToken> ExtractDistinct(string? text)
    {
        return Extract(text)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Thay thế mọi token braced bằng khoảng trắng, giữ nguyên độ dài chuỗi
    /// để các offset không bị lệch. Dùng để tránh đếm trùng token.
    /// </summary>
    private static string MaskBracedTokens(string text)
    {
        var buffer = text.ToCharArray();

        int index = 0;

        while (index < buffer.Length)
        {
            if (buffer[index] != '{')
            {
                index++;
                continue;
            }

            int depth = 0;
            int start = index;

            while (index < buffer.Length)
            {
                char current = buffer[index];

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
                break;
            }

            for (int i = start; i < index; i++)
            {
                buffer[i] = ' ';
            }
        }

        return new string(buffer);
    }

    [GeneratedRegex(@"%[sdfxplu]\d+", RegexOptions.CultureInvariant)]
    private static partial Regex PrintfTokenRegex();

    [GeneratedRegex(@"%[A-Za-z_][A-Za-z0-9_]*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentNameRegex();
}
