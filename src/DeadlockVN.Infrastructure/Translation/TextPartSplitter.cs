using System.Text;
using System.Text.RegularExpressions;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Loại của một phần trong chuỗi localization.
/// </summary>
public enum TextPartKind
{
    /// <summary>
    /// Văn bản thuần — được gửi cho model dịch.
    /// </summary>
    Literal,

    /// <summary>
    /// Token runtime, thẻ HTML/Panorama, hoặc thẻ ngữ cảnh — giữ nguyên
    /// tuyệt đối, không bao giờ gửi cho model.
    /// </summary>
    Fixed
}

/// <summary>
/// Một phần của chuỗi localization sau khi tách.
/// </summary>
public readonly record struct TextPart(TextPartKind Kind, string Text);

/// <summary>
/// Tách chuỗi localization thành các phần "văn bản" và "phần cố định".
/// <para>
/// <b>Vì sao cần tách:</b> model ngôn ngữ nhỏ (3B) rất dễ:
/// </para>
/// <list type="bullet">
///   <item><description>
///     xóa thẻ HTML (<c>&lt;span&gt;</c> biến mất trong bản dịch → mất
///     highlight, mất xuống dòng),
///   </description></item>
///   <item><description>
///     tự thêm placeholder giả (<c>{s:shock}</c> từ chữ "shock"),
///   </description></item>
///   <item><description>
///     đảo thứ tự thẻ (đóng trước mở → vỡ layout).
///   </description></item>
/// </list>
/// <para>
/// Nếu ta chỉ che placeholder (biện pháp của <c>PromptBuilder</c>) thì
/// thẻ HTML vẫn nằm trong văn bản gửi đi và model có thể phá hỏng.
/// Cách chắc chắn 100% là <b>không cho model nhìn thấy thẻ nào cả</b>:
/// tách thẻ ra, chỉ dịch phần chữ, rồi ghép lại đúng thứ tự ban đầu.
/// </para>
/// <para>
/// <b>Token plural</b> được xử lý đặc biệt: phần văn bản bên trong
/// (<c>one{...} other{...}</c>) vẫn phải được dịch, nên chỉ khung token
/// và tên biến được giữ cố định.
/// </para>
/// <para>
/// <b>Cam kết quan trọng:</b> ghép lại các phần luôn cho ra đúng chuỗi
/// gốc, từng ký tự. Không có khoảng trắng nào bị thêm/bớt.
/// </para>
/// </summary>
public static class TextPartSplitter
{
    /// <summary>
    /// Thẻ HTML/Panorama — mọi thẻ dạng <c>&lt;tag ...&gt;</c> đều được
    /// coi là phần cố định, kể cả thẻ ngữ cảnh trong VO
    /// (<c>&lt;laughs&gt;</c>, <c>&lt;None&gt;</c>) vì những thẻ đó cũng
    /// không được dịch.
    /// </summary>
    private static readonly Regex TagRegex = new(
        @"</?[A-Za-z][^<>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Token kiểu printf (<c>%s1</c>).
    /// </summary>
    private static readonly Regex PrintfRegex = new(
        @"%[sdfxplu]\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Token kiểu tên phần trăm (<c>%partner%</c>).
    /// </summary>
    private static readonly Regex PercentNameRegex = new(
        @"%[A-Za-z_][A-Za-z0-9_]*%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Tách chuỗi thành danh sách phần.
    /// </summary>
    /// <param name="text">Chuỗi gốc.</param>
    public static List<TextPart> Split(string? text)
    {
        var parts = new List<TextPart>();

        if (string.IsNullOrEmpty(text))
        {
            return parts;
        }

        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0)
            {
                return;
            }

            parts.Add(new TextPart(TextPartKind.Literal, literal.ToString()));

            literal.Clear();
        }

        int index = 0;

        while (index < text.Length)
        {
            char current = text[index];

            // 1. Token braced — quét cân bằng ngoặc.
            if (current == '{')
            {
                if (TryReadBalanced(text, index, out string token, out int tokenEnd))
                {
                    FlushLiteral();

                    AppendToken(parts, token);

                    index = tokenEnd;

                    continue;
                }

                literal.Append(current);
                index++;

                continue;
            }

            // 2. Thẻ HTML/Panorama.
            if (current == '<')
            {
                Match match = TagRegex.Match(text, index);

                if (match.Success && match.Index == index)
                {
                    FlushLiteral();

                    parts.Add(new TextPart(TextPartKind.Fixed, match.Value));

                    index = match.Index + match.Length;

                    continue;
                }

                literal.Append(current);
                index++;

                continue;
            }

            // 3. Token printf và percent-name.
            if (current == '%')
            {
                Match match = PrintfRegex.Match(text, index);

                if (!match.Success || match.Index != index)
                {
                    match = PercentNameRegex.Match(text, index);
                }

                if (match.Success && match.Index == index)
                {
                    FlushLiteral();

                    parts.Add(new TextPart(TextPartKind.Fixed, match.Value));

                    index = match.Index + match.Length;

                    continue;
                }

                literal.Append(current);
                index++;

                continue;
            }

            literal.Append(current);
            index++;
        }

        FlushLiteral();

        return parts;
    }

    /// <summary>
    /// Thêm các phần cho một token braced.
    /// <para>
    /// Token thường → một phần cố định. Token plural → khung token, tên
    /// biến và nhãn nhánh (<c>one{</c>, <c>other{</c>) cố định; phần văn
    /// bản bên trong được tách tiếp để dịch.
    /// </para>
    /// <para>
    /// Mọi mảnh cố định được lấy <b>nguyên văn</b> từ chuỗi gốc (verbatim)
    /// nên ghép lại luôn cho ra đúng bản gốc.
    /// </para>
    /// </summary>
    private static void AppendToken(List<TextPart> parts, string token)
    {
        if (!TryAppendPlural(parts, token))
        {
            parts.Add(new TextPart(TextPartKind.Fixed, token));
        }
    }

    /// <summary>
    /// Thử xử lý token plural, trả về <c>true</c> nếu thành công.
    /// <para>
    /// <b>Cấu trúc:</b> <c>{tên, plural, one{...} other{...}}</c>.
    /// </para>
    /// </summary>
    private static bool TryAppendPlural(List<TextPart> parts, string token)
    {
        if (token.Length < 3 || token[0] != '{' || token[^1] != '}')
        {
            return false;
        }

        // Tên biến: tới dấu phẩy đầu tiên.
        int comma = token.IndexOf(',', StringComparison.Ordinal);

        if (comma <= 1)
        {
            return false;
        }

        string name = token[1..comma].Trim();

        if (name.Length == 0)
        {
            return false;
        }

        // Từ sau dấu phẩy phải chứa từ khóa "plural".
        int keyword = token.IndexOf(
            "plural",
            comma + 1,
            StringComparison.OrdinalIgnoreCase);

        if (keyword < 0)
        {
            return false;
        }

        int keywordEnd = keyword + "plural".Length;

        if (keywordEnd >= token.Length || token[keywordEnd] != ',')
        {
            return false;
        }

        // Phần cố định: "{tên, plural," — lấy nguyên văn.
        int frameEnd = keywordEnd + 1;

        // Nuốt khoảng trắng sau dấu phẩy vào phần cố định.
        while (frameEnd < token.Length && char.IsWhiteSpace(token[frameEnd]))
        {
            frameEnd++;
        }

        parts.Add(new TextPart(
            TextPartKind.Fixed,
            token[..frameEnd]));

        int index = frameEnd;

        // Các nhánh "one{...} other{...}".
        while (true)
        {
            int open = token.IndexOf('{', index);

            if (open < 0)
            {
                return false;
            }

            // Nhãn nhánh + dấu "{" — nguyên văn.
            parts.Add(new TextPart(
                TextPartKind.Fixed,
                token[index..(open + 1)]));

            // Quét nội dung nhánh tới dấu "}" tương ứng.
            if (!TryReadBranchBody(token, open + 1, out string body, out int bodyEnd))
            {
                return false;
            }

            // Nội dung nhánh được tách tiếp (có thể chứa token lồng).
            parts.AddRange(Split(body));

            // Dấu "}" đóng nhánh.
            parts.Add(new TextPart(TextPartKind.Fixed, "}"));

            index = bodyEnd + 1;

            // Dấu "}" đóng cả token -> hết nhánh.
            if (index < token.Length && token[index] == '}')
            {
                break;
            }

            // Dấu phẩy phân cách nhánh (Valve dùng cả dấu phẩy lẫn
            // khoảng trắng) — giữ nguyên văn.
            int separatorEnd = index;

            if (separatorEnd < token.Length && token[separatorEnd] == ',')
            {
                separatorEnd++;
            }

            while (separatorEnd < token.Length && char.IsWhiteSpace(token[separatorEnd]))
            {
                separatorEnd++;
            }

            if (separatorEnd == index)
            {
                // Không có dấu phân cách -> token hỏng, coi như token thường.
                return false;
            }

            parts.Add(new TextPart(
                TextPartKind.Fixed,
                token[index..separatorEnd]));

            index = separatorEnd;
        }

        // Dấu "}" đóng token.
        parts.Add(new TextPart(TextPartKind.Fixed, "}"));

        return true;
    }

    /// <summary>
    /// Đọc nội dung một nhánh token plural, từ vị trí
    /// <paramref name="start"/> tới dấu "}" đóng nhánh.
    /// <para>
    /// Token lồng như <c>{#}</c> được đếm cân bằng bình thường — mỗi
    /// <c>{}</c> đều tự cân bằng nên không ảnh hưởng tới việc tìm dấu
    /// "}" đóng nhánh.
    /// </para>
    /// </summary>
    private static bool TryReadBranchBody(
        string text,
        int start,
        out string body,
        out int end)
    {
        body = string.Empty;
        end = start;

        int depth = 1;
        int index = start;

        while (index < text.Length)
        {
            char current = text[index];

            if (current == '{')
            {
                depth++;
                index++;

                continue;
            }

            if (current == '}')
            {
                depth--;

                if (depth == 0)
                {
                    body = text[start..index];
                    end = index;

                    return true;
                }

                index++;

                continue;
            }

            index++;
        }

        return false;
    }

    /// <summary>
    /// Thử đọc một token braced cân bằng từ vị trí <paramref name="start"/>.
    /// </summary>
    private static bool TryReadBalanced(
        string text,
        int start,
        out string token,
        out int end)
    {
        token = string.Empty;
        end = start;

        int depth = 0;

        for (int i = start; i < text.Length; i++)
        {
            char current = text[i];

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;

                if (depth == 0)
                {
                    token = text[start..(i + 1)];
                    end = i + 1;

                    return true;
                }
            }
        }

        return false;
    }
}
