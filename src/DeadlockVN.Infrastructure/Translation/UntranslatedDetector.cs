using System.Text.RegularExpressions;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Phát hiện bản dịch mà model đã bỏ qua (trả về nguyên văn tiếng Anh).
/// <para>
/// <b>Vì sao cần:</b> model nhỏ (3B) hay bỏ sót một đoạn trong lô — nó
/// trả về đúng chuỗi gốc cho phần đó. Kết quả: game hiển thị tiếng Anh
/// lẫn trong bản dịch. Guard hiện chỉ kiểm tra placeholder và thẻ HTML nên
/// không bắt được lỗi này.
/// </para>
/// <para>
/// <b>Tiêu chí nhận dạng (2 điều kiện phải cùng đúng):</b>
/// </para>
/// <list type="number">
///   <item><description>
///     Bản dịch giống hệt bản gốc (sau khi cắt khoảng trắng).
///   </description></item>
///   <item><description>
///     Bản gốc có <b>từ chức năng tiếng Anh</b> (như "your", "the",
///     "deals") và có <b>từ thường</b> (chữ thường) — tức là trông như một
///     câu tiếng Anh chứ không phải tên riêng.
///   </description></item>
/// </list>
/// <para>
/// Điều kiện thứ hai giải quyết mâu thuẫn cố hữu: tên hero kiểu
/// "Hogarth the Fabulous" có chứa "the" nhưng lại là tên riêng hợp lệ,
/// còn "Your bullets build up to a Silence" là câu tiếng Anh bị bỏ dịch.
/// </para>
/// </summary>
public static class UntranslatedDetector
{
    /// <summary>
    /// Token runtime của Valve — <c>{...}</c> — và thẻ HTML/Panorama.
    /// <para>
    /// <b>Vì sao phải loại bỏ trước khi phân tích:</b> nhiều entry của
    /// Deadlock <b>không chứa chữ nào để dịch</b>, chỉ là placeholder:
    /// <c>{s:selected_build_name}</c>, <c>{d:refresh_rate} Hz</c>,
    /// <c>BuildID: {i:r:selected_hero_build_id}</c>. Nếu đếm từ trong
    /// cả placeholder thì các từ "selected", "build", "refresh"… bị hiểu
    /// là từ chức năng tiếng Anh và entry hợp lệ bị báo nhầm là "chưa
    /// dịch".
    /// </para>
    /// </summary>
    private static readonly Regex TagRegex = new(
        @"</?[A-Za-z][^<>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Từ chức năng tiếng Anh.
    /// <para>
    /// Chỉ gồm những từ chắc chắn không xuất hiện trong tên riêng của
    /// game, hoặc nếu có thì phải kèm một từ thường khác — nhờ vậy tên
    /// hero như "Hogarth the Fabulous" không bị báo nhầm.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> EnglishFunctionWords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Trạng ngữ.
        "the", "a", "an", "and", "or", "but", "if", "then", "than",
        "when", "while", "for", "from", "with", "without", "into", "onto",
        "over", "under", "through", "during", "before", "after",
        "each", "every", "all", "any", "some", "none", "not", "no",
        "you", "your", "yours", "their", "them", "they", "his", "her",
        "its", "this", "that", "these", "those", "there", "here",
        "what", "which", "who", "will", "would", "can", "could",
        "should", "shall", "may", "might", "must", "has", "have", "had",
        "been", "be", "is", "are", "was", "were", "do", "does", "did",
        "to", "of", "in", "on", "at", "by", "as", "up", "down", "out",
        "off", "so", "also", "more", "less", "most", "least", "much",
        "many", "only", "just", "now", "still", "already", "again",
        "very", "too", "about", "against", "between", "both", "other",
        "another", "such", "own", "same", "while", "per", "into",

        // Động từ hay gặp trong mô tả kỹ năng.
        "deals", "gain", "gains", "applies", "increase", "increases",
        "reduce", "reduces", "restore", "restores", "heals", "healing",
        "blocks", "chance", "build", "builds", "prevent", "prevents",
        "grants", "gives", "gives", "reduces", "adds", "removes",
        "makes", "causes", "allows", "lets", "makes", "dealing",
        "firing", "fires", "hitting", "hits", "shooting", "shoots",
        "channelling", "channeling", "interrupts", "accumulate",
        "accumulating", "refreshes", "refresh", "charges", "stacking",
        "charges", "passes", "landing", "landed", "hitting"
    };

    /// <summary>
    /// Tách câu thành các từ (chỉ dùng chữ cái).
    /// </summary>
    private static readonly Regex WordRegex = new(
        @"[A-Za-z]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Số từ viết thường tối thiểu để coi là câu tiếng Anh.
    /// <para>
    /// Tên riêng trong game viết theo kiểu Title Case ("Hogarth the
    /// Fabulous") nên chỉ có tối đa một từ viết thường. Câu tiếng Anh
    /// thật thì luôn có ít nhất hai ("Your bullets build up…").
    /// </para>
    /// </summary>
    private const int MinLowercaseWords = 2;

    /// <summary>
    /// Bản dịch có phải bản "chưa dịch" không.
    /// </summary>
    public static bool IsUntranslated(string original, string translated)
    {
        if (string.IsNullOrWhiteSpace(original) ||
            string.IsNullOrWhiteSpace(translated))
        {
            return false;
        }

        if (!string.Equals(
                original.Trim(),
                translated.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        // Giống hệt -> kiểm tra xem có trông như câu tiếng Anh không.
        return LooksLikeEnglishSentence(original);
    }

    /// <summary>
    /// Bỏ qua token runtime và thẻ HTML, chỉ giữ lại phần văn bản thật.
    /// <para>
    /// Token được quét bằng cách cân bằng dấu ngoặc chứ không dùng
    /// regex — vì token plural của Valve có token lồng bên trong:
    /// <c>{hit_count, plural, one{ } other{}}</c>. Regex đơn giản sẽ
    /// không khớp hết, để lại "plural", "one", "other" lọt vào phần
    /// văn bản và bị hiểu nhầm là từ tiếng Anh.
    /// </para>
    /// </summary>
    private static string StripTokensAndTags(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);

        int index = 0;

        while (index < text.Length)
        {
            char current = text[index];

            if (current == '{')
            {
                // Quét cân bằng ngoặc.
                int depth = 0;

                while (index < text.Length)
                {
                    if (text[index] == '{')
                    {
                        depth++;
                    }
                    else if (text[index] == '}')
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

                builder.Append(' ');

                continue;
            }

            if (current == '<')
            {
                Match match = TagRegex.Match(text, index);

                if (match.Success && match.Index == index)
                {
                    index = match.Index + match.Length;

                    builder.Append(' ');

                    continue;
                }
            }

            builder.Append(current);
            index++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Chuỗi có trông như một câu tiếng Anh (thay vì tên riêng) không.
    /// <para>
    /// Điều kiện: có <b>từ chức năng</b> và có ít nhất
    /// <see cref="MinLowercaseWords"/> từ viết thường (không viết hoa).
    /// Nhờ vậy "Hogarth the Fabulous" không bị coi là câu, còn
    /// "Your bullets build up to a Silence" thì có.
    /// </para>
    /// </summary>
    public static bool LooksLikeEnglishSentence(string text)
    {
        // Chỉ phân tích phần văn bản thật — bỏ qua placeholder/thẻ HTML.
        string plain = StripTokensAndTags(text);

        bool hasFunctionWord = false;
        int lowercaseWords = 0;

        foreach (Match match in WordRegex.Matches(plain))
        {
            string word = match.Value;

            if (word.Length > 0 && char.IsLower(word[0]))
            {
                lowercaseWords++;
            }

            if (EnglishFunctionWords.Contains(word))
            {
                hasFunctionWord = true;
            }
        }

        return hasFunctionWord && lowercaseWords >= MinLowercaseWords;
    }
}
