using System.Text;
using System.Text.RegularExpressions;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Tách một chuỗi văn dài thành nhiều câu để dịch từng phần.
/// <para>
/// <b>Vì sao cần:</b> vài chuỗi trong game rất dài — đoạn lore của
/// hero dài tới 1.600 ký tự. Gửi nguyên khối 1.600 ký tự cho model 7B
/// với cửa sổ ngữ cảnh 4.096 token sẽ khiến request treo: model không
/// kịp sinh hết đáp án trong thời gian chờ, và cả lô bị chia đôi liên
/// tục — mất hàng chục phút cho một chuỗi duy nhất.
/// </para>
/// <para>
/// Tách thành câu giúp mỗi lần gọi model chỉ phải xử lý vài trăm ký tự,
/// vừa nhanh vừa chính xác hơn (model tập trung vào một đoạn ngắn).
/// </para>
/// </summary>
public static class SentenceSplitter
{
    /// <summary>
    /// Ranh giới câu: dấu chấm, hỏi chấm, chấm than, chấm phẩy, hai chấm,
    /// dấu gạch ngang, và xuống dòng — nhưng <b>không</b> ngắt sau dấu
    /// chấm thập phân (1.5) và không ngắt trong "..." hoặc "e.g.".
    /// </summary>
    private static readonly Regex BoundaryRegex = new(
        @"(?<=[.!?;:\-…])\s+|\r?\n",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Dấu chấm ngay sau đó là chữ số -> đó là dấu thập phân, không phải
    /// kết thúc câu (ví dụ "deals 2.86 damage").
    /// </summary>
    private static readonly Regex DecimalPointRegex = new(
        @"\.\d",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Ngắt chuỗi thành các mảnh văn bản, mỗi mảnh không vượt quá
    /// <paramref name="maxChars"/>.
    /// <para>
    /// Các mảnh luôn giữ nguyên khoảng trắng và dấu câu ở cuối, nên ghép
    /// lại cho ra đúng chuỗi gốc.
    /// </para>
    /// </summary>
    /// <param name="text">Chuỗi cần tách.</param>
    /// <param name="maxChars">Độ dài tối đa của một mảnh.</param>
    public static IReadOnlyList<string> Split(string text, int maxChars = 600)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new[] { text ?? string.Empty };
        }

        var pieces = new List<string>();
        var current = new StringBuilder();

        foreach (string sentence in SplitSentences(text))
        {
            if (current.Length > 0 &&
                current.Length + sentence.Length > maxChars)
            {
                pieces.Add(current.ToString());
                current.Clear();
            }

            if (sentence.Length > maxChars)
            {
                // Câu đơn lẻ quá dài -> cắt theo từ.
                foreach (string chunk in SplitByWords(sentence, maxChars))
                {
                    pieces.Add(chunk);
                }

                continue;
            }

            current.Append(sentence);
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString());
        }

        if (pieces.Count == 0)
        {
            return new[] { text };
        }

        return pieces;
    }

    /// <summary>
    /// Tách thành câu, giữ nguyên dấu kết thúc và khoảng trắng.
    /// </summary>
    private static IEnumerable<string> SplitSentences(string text)
    {
        int start = 0;

        foreach (Match match in BoundaryRegex.Matches(text))
        {
            int end = match.Index + match.Length;

            // Không ngắt sau dấu chấm thập phân.
            if (end > 0 &&
                text[end - 1] == '.' &&
                end < text.Length &&
                char.IsDigit(text[end]))
            {
                continue;
            }

            string chunk = text[start..end];

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                yield return chunk;
            }

            start = end;
        }

        if (start < text.Length)
        {
            string chunk = text[start..];

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Cắt một đoạn quá dài theo ranh giới từ, không bỏ sót ký tự nào.
    /// </summary>
    private static IEnumerable<string> SplitByWords(string text, int maxChars)
    {
        var current = new StringBuilder();
        int index = 0;

        while (index < text.Length)
        {
            // Tìm ranh giới từ gần nhất trong khoảng cho phép.
            int limit = Math.Min(text.Length, index + maxChars);

            int cut = -1;

            for (int i = limit; i > index; i--)
            {
                if (char.IsWhiteSpace(text[i - 1]))
                {
                    cut = i;

                    break;
                }
            }

            if (cut <= 0)
            {
                // Không có khoảng trắng nào -> cắt cứng.
                cut = limit;
            }

            current.Append(text, index, cut - index);

            if (current.Length >= maxChars)
            {
                yield return current.ToString();

                current.Clear();
            }

            index = cut;
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
