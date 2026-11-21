using System.Text;

using DeadlockVN.Core.Common;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Dựng prompt dịch cho provider.
/// <para>
/// <b>Quan trọng — kiến trúc một tầng:</b> trước khi gửi, chuỗi đã được
/// tách bởi <see cref="TextPartSplitter"/> thành phần văn bản và phần cố
/// định (token, thẻ HTML). Chỉ phần văn bản mới tới đây. Nhờ vậy prompt
/// không còn nhắc "giữ nguyên placeholder" / "giữ nguyên thẻ HTML" —
/// model <b>không bao giờ nhìn thấy</b> những thứ đó, nên không thể làm
/// hỏng. Đây là điều kiện tiên quyết để dịch máy đủ tin cậy: model nhỏ
/// rất dễ xóa thẻ <c>&lt;span&gt;</c> hoặc tự bịa ra placeholder.
/// </para>
/// <para>
/// Vì vậy prompt ở đây ngắn gọn: chỉ yêu cầu dịch đúng nghĩa, giữ nguyên
/// định dạng, trả về JSON. Prompt dài chỉ làm model 3B rối và chậm.
/// </para>
/// </summary>
public static class PromptBuilder
{
    /// <summary>
    /// Chỉ dẫn hệ thống — dùng cho mọi lô.
    /// </summary>
    public static string BuildSystemPrompt(string? context)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "Bạn là chuyên gia dịch thuật Việt Nam cho trò chơi Deadlock (Valve Source 2).");
        builder.AppendLine();
        builder.AppendLine("QUY TẮC BẮT BUỘC:");
        builder.AppendLine(
            "1. Dịch đúng nghĩa sang tiếng Việt tự nhiên, đúng văn phong game.");
        builder.AppendLine(
            "2. KHÔNG thêm, KHÔNG bớt, KHÔNG diễn giải — dịch sát nghĩa gốc.");
        builder.AppendLine(
            "3. KHÔNG thêm giải thích, không thêm dấu ngoặc kép bao quanh.");
        builder.AppendLine(
            "4. Thuật ngữ chuyên ngành dịch theo cách phổ biến trong game Việt Nam. " +
            "Tên riêng, tên hero, tên vật phẩm: giữ nguyên tiếng Anh nếu đã quen thuộc.");
        builder.AppendLine(
            "5. Giữ nguyên khoảng trắng đầu/cuối chuỗi nếu có.");
        builder.AppendLine(
            "6. Nếu chuỗi rỗng hoặc không có gì để dịch, trả về nguyên văn.");
        builder.AppendLine(
            "7. GIỮ NGUYÊN dấu câu ở đầu chuỗi (ví dụ \": \" hoặc \", \") " +
            "và dấu chấm câu ở cuối. Chỉ thay đổi phần chữ, không thay đổi dấu câu.");
        builder.AppendLine(
            "8. TUYỆT ĐỐI KHÔNG tóm tắt, không diễn giải lại, không liệt kê ý chính. " +
            "Bản dịch phải có cùng độ dài xấp xỉ với bản gốc — " +
            "mỗi câu của bản gốc phải có một câu tương ứng trong bản dịch.");
        builder.AppendLine(
            "9. Chỉ trả về bản dịch. TUYỆT ĐỐI KHÔNG trả lời bằng tiếng Anh.");

        if (!string.IsNullOrWhiteSpace(context))
        {
            builder.AppendLine();
            builder.AppendLine($"Ngữ cảnh nhóm nội dung: {context}");
        }

        builder.AppendLine();
        builder.AppendLine("Ví dụ:");
        builder.AppendLine(
            "Gốc: \"Spirit Amp, restores Spirit every 8 seconds\"");
        builder.AppendLine(
            "Dịch: \"Nội lực, hồi Nội lực mỗi 8 giây\"");
        builder.AppendLine(
            "Gốc: \"Warning: reduces damage\"");
        builder.AppendLine(
            "Dịch: \"Cảnh báo: giảm sát thương\"");
        builder.AppendLine(
            "Gốc: \"Now properly deals damage in an area on landing\"");
        builder.AppendLine(
            "Dịch: \"Giờ đã gây sát thương đúng trong một khu vực khi hạ cánh\"");

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Dựng nội dung người dùng cho một lô — danh sách chuỗi cần dịch.
    /// </summary>
    /// <param name="texts">Các chuỗi tiếng Anh.</param>
    /// <returns>
    /// Chuỗi chứa các chuỗi, mỗi chuỗi trên một dòng, đánh số bằng
    /// <c>[0]</c>, <c>[1]</c>… để model ánh xạ lại được.
    /// </returns>
    public static string BuildUserPrompt(IReadOnlyList<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var builder = new StringBuilder();

        builder.AppendLine(
            "Dịch các chuỗi dưới đây sang tiếng Việt.");
        builder.AppendLine(
            "Trả về DUY NHẤT một JSON object, trong đó mỗi khoá là CHỈ SỐ " +
            "của chuỗi (số nguyên, không có dấu gọi) và giá trị là bản dịch.");
        builder.AppendLine(
            "Ví dụ cho 2 chuỗi: {\"0\": \"bản dịch 0\", \"1\": \"bản dịch 1\"}");
        builder.AppendLine(
            "PHẢI có đúng khoá cho mọi chỉ số từ 0 đến N-1 (N là số chuỗi " +
            "được liệt kê). Không thêm khoá nào ngoài phạm vi, không bỏ khoá nào.");
        builder.AppendLine(
            "Nếu một chuỗi không cần dịch (tên riêng), vẫn phải có khoá đó, " +
            "giá trị là nguyên văn chuỗi.");
        builder.AppendLine(
            "Giá trị của mỗi khoá PHẢI là một chuỗi văn bản đơn giản. " +
            "TUYỆT ĐỐI KHÔNG tạo object lồng bên trong giá trị " +
            "(ví dụ KHÔNG được trả về {\"0\": {\"a\": \"...\", \"b\": \"...\"}}).");
        builder.AppendLine();

        for (int i = 0; i < texts.Count; i++)
        {
            builder.AppendLine($"[{i}] {texts[i]}");
        }

        builder.AppendLine();
        builder.AppendLine("JSON:");

        return builder.ToString();
    }

    /// <summary>
    /// Tách phần placeholder ra khỏi chuỗi trước khi gửi đi, để model
    /// không đụng vào chúng.
    /// <para>
    /// <b>Chiến lược:</b> thay mỗi placeholder bằng một mã ngắn gợi nhớ
    /// (<c>__T0__</c>, <c>__T1__</c>…) trước, gửi đi, rồi khôi phục lại
    /// sau khi nhận về. Cách này hiệu quả hơn việc chỉ "dặn" model không
    /// đụng vào placeholder — vì model rất dễ vô tình bỏ dấu ngoặc.
    /// </para>
    /// <para>
    /// <b>Ngoại lệ — token plural:</b> token dạng
    /// <c>{count, plural, one{Item} other{Items}}</c> chỉ được che <b>tên
    /// biến</b>, còn phần văn bản bên trong vẫn phải được dịch. Nếu che
    /// toàn bộ token, bản dịch sẽ mất nội dung "Item"/"Items" và game
    /// hiển thị sai.
    /// </para>
    /// </summary>
    /// <param name="text">Chuỗi gốc.</param>
    /// <param name="masked">Chuỗi đã che placeholder.</param>
    /// <param name="placeholders">
    /// Danh sách placeholder gốc, theo đúng thứ tự xuất hiện.
    /// </param>
    public static string MaskPlaceholders(
        string text,
        out IReadOnlyList<string> placeholders)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = new List<string>();
        var builder = new StringBuilder(text.Length);

        int index = 0;

        while (index < text.Length)
        {
            if (text[index] != '{')
            {
                builder.Append(text[index]);
                index++;
                continue;
            }

            // Quét cân bằng dấu ngoặc — token plural có thể chứa token lồng nhau.
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
                // Không cân bằng -> coi như văn bản thường.
                builder.Append(text[start..]);
                break;
            }

            string candidate = text[start..index];

            // Token plural: chỉ che tên biến, giữ nguyên phần cần dịch.
            if (IsPluralToken(candidate))
            {
                // Giá trị khôi phục chỉ là TÊN BIẾN — vì phần
                // "one{...} other{...}" được phép dịch.
                found.Add(ExtractPluralVariableName(candidate));
                builder.Append(MaskPluralToken(candidate, found.Count - 1));

                continue;
            }

            // Token rỗng "{#}" vẫn là token thật (boon number) -> cũng che.
            found.Add(candidate);
            builder.Append($"__T{found.Count - 1}__");
        }

        // Che token printf-style và percent-name.
        string masked = builder.ToString();

        masked = MaskPattern(masked, @"%[sdfxplu]\d+", found);
        masked = MaskPattern(masked, @"%[A-Za-z_][A-Za-z0-9_]*%", found);

        placeholders = found;

        return masked;
    }

    /// <summary>
    /// Lấy tên biến của token plural (ví dụ <c>count</c> trong
    /// <c>{count, plural, ...}</c>).
    /// </summary>
    private static string ExtractPluralVariableName(string token)
    {
        int commaIndex = token.IndexOf(',', StringComparison.Ordinal);

        if (commaIndex <= 1)
        {
            return token;
        }

        return token[1..commaIndex];
    }

    /// <summary>
    /// Che phần "tên biến" của token plural, giữ nguyên các nhãn và văn bản.
    /// </summary>
    private static string MaskPluralToken(string token, int index)
    {
        // Dạng: {TÊN, plural, one{...} other{...}}
        int firstComma = token.IndexOf(',', StringComparison.Ordinal);

        if (firstComma <= 1)
        {
            return $"__T{index}__";
        }

        return "{" + $"__T{index}__" + token[firstComma..];
    }

    /// <summary>
    /// Token có phải dạng plural của Valve không.
    /// </summary>
    private static bool IsPluralToken(string token)
    {
        int commaIndex = token.IndexOf(',', StringComparison.Ordinal);

        if (commaIndex <= 1)
        {
            return false;
        }

        string rest = token[(commaIndex + 1)..].TrimStart();

        return rest.StartsWith("plural", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Khôi phục các placeholder đã che.
    /// </summary>
    public static string UnmaskPlaceholders(
        string text,
        IReadOnlyList<string> placeholders)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(placeholders);

        string result = text;

        for (int i = placeholders.Count - 1; i >= 0; i--)
        {
            result = result.Replace($"__T{i}__", placeholders[i], StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Che các token khớp mẫu, bỏ qua những token đã nằm trong vùng đã che.
    /// </summary>
    private static string MaskPattern(
        string text,
        string pattern,
        List<string> placeholders)
    {
        var regex = new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        return regex.Replace(text, match =>
        {
            placeholders.Add(match.Value);

            return $"__T{placeholders.Count - 1}__";
        });
    }

    /// <summary>
    /// Kiểm tra nhanh một chuỗi có chứa placeholder hay không.
    /// </summary>
    public static bool HasPlaceholders(string text)
    {
        return TextTokenExtractor.Extract(text).Count > 0;
    }
}
