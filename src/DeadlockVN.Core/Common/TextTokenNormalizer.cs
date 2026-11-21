namespace DeadlockVN.Core.Common;

/// <summary>
/// Chuẩn hoá token runtime để so sánh giữa bản gốc và bản dịch.
/// <para>
/// <b>Vì sao cần:</b> token plural của Valve có dạng
/// <c>{count, plural, one{Item} other{Items}}</c> — phần văn bản bên
/// trong <c>one{...}</c>/<c>other{...}</c> <b>phải được dịch</b>, nên giá
/// trị literal của token khác nhau giữa bản gốc và bản dịch. Nếu so sánh
/// literal thì mọi bản dịch hợp lệ đều bị báo động sai.
/// </para>
/// <para>
/// Vì vậy token plural được so sánh theo <b>tên biến</b> thay vì toàn bộ
/// chuỗi: <c>{count, plural, ...}</c> và
/// <c>{count, plural, one{món} other{món}}</c> cùng một "chữ ký".
/// </para>
/// </summary>
public static class TextTokenNormalizer
{
    /// <summary>
    /// Chuẩn hoá một token để dùng làm khoá so sánh.
    /// </summary>
    /// <param name="token">Giá trị thô của token.</param>
    /// <returns>
    /// Chuỗi khoá đã chuẩn hoá. Với token plural, trả về
    /// <c>{plural:tên_biến}</c>; các token khác trả về nguyên văn.
    /// </returns>
    public static string Normalize(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return token;
        }

        if (!token.StartsWith('{') || !token.EndsWith('}'))
        {
            return token;
        }

        // Tìm đoạn ", plural," ở cấp ngoài cùng.
        int pluralIndex = IndexOfPluralSeparator(token);

        if (pluralIndex < 0)
        {
            return token;
        }

        // Tên biến nằm giữa "{" và dấu "," đầu tiên.
        int commaIndex = token.IndexOf(',', StringComparison.Ordinal);

        if (commaIndex <= 1)
        {
            return token;
        }

        string variableName = token[1..commaIndex].Trim();

        if (variableName.Length == 0)
        {
            return token;
        }

        return "{plural:" + variableName + "}";
    }

    /// <summary>
    /// Tìm vị trí của dấu phân cách ", plural," trong token.
    /// </summary>
    private static int IndexOfPluralSeparator(string token)
    {
        // Quét từ đầu, bỏ qua ngoặc nhọn lồng nhau (không có trong token
        // plural của game, nhưng để cho an toàn).
        int depth = 0;

        for (int i = 0; i < token.Length; i++)
        {
            char current = token[i];

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

            if (depth != 1)
            {
                continue;
            }

            // Tại đây đang ở cấp ngoài cùng -> tìm "plural".
            //
            // "plural" có 6 ký tự (p-l-u-r-a-l).
            const string PluralWord = "plural";

            if (i + PluralWord.Length > token.Length ||
                !token.AsSpan(i, PluralWord.Length).Equals(
                    PluralWord,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Đảm bảo "plural" là một từ riêng.
            bool precededBySeparator =
                i == 0 || char.IsWhiteSpace(token[i - 1]) || token[i - 1] == ',';

            int after = i + PluralWord.Length;

            bool followedBySeparator =
                after < token.Length &&
                (char.IsWhiteSpace(token[after]) || token[after] == ',');

            if (precededBySeparator && followedBySeparator)
            {
                return i;
            }
        }

        return -1;
    }
}
