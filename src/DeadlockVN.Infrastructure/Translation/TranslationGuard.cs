using System.Text;
using System.Text.RegularExpressions;

using DeadlockVN.Core.Common;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Validation;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Kiểm tra bản dịch do máy sinh ra.
/// <para>
/// <b>Nguyên tắc:</b> so sánh bản dịch với bản gốc — không áp dụng luật
/// tuyệt đối. Bản gốc của game có nhiều trường hợp "bẩn" hợp lệ (thẻ HTML
/// không cân bằng, thẻ ngữ cảnh trong VO, token không chuẩn) nên check tuyệt
/// đối sẽ tạo báo động giả và bỏ qua những bản dịch hợp lệ.
/// </para>
/// </summary>
public sealed class TranslationGuard : ITranslationGuard
{
    public TranslationCheck Check(string original, string translated)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(translated);

        var check = new TranslationCheck();

        // 1. Rỗng.
        if (string.IsNullOrWhiteSpace(translated))
        {
            // Bản gốc cũng rỗng -> hợp lệ (không có gì để dịch).
            if (string.IsNullOrWhiteSpace(original))
            {
                return check;
            }

            check.Errors.Add("Bản dịch rỗng.");

            return check;
        }

        // 2. Placeholder — phải khớp CHÍNH XÁC cả tập lẫn số lần xuất hiện.
        //
        //    Token plural được so sánh theo tên biến (xem
        //    TextTokenNormalizer) vì phần "one{...} other{...}" bên trong
        //    được phép dịch.
        var originalTokens = CountNormalizedTokens(original);
        var translatedTokens = CountNormalizedTokens(translated);

        foreach (var pair in originalTokens)
        {
            if (!translatedTokens.TryGetValue(pair.Key, out int count))
            {
                check.Errors.Add($"Thiếu placeholder {pair.Key}.");
            }
            else if (count != pair.Value)
            {
                check.Errors.Add(
                    $"Placeholder {pair.Key} xuất hiện {count} lần " +
                    $"trong bản dịch nhưng {pair.Value} lần trong bản gốc.");
            }
        }

        foreach (var pair in translatedTokens)
        {
            if (!originalTokens.ContainsKey(pair.Key))
            {
                check.Errors.Add($"Thừa placeholder {pair.Key}.");
            }
        }

        // 3. Cấu trúc thẻ HTML/Panorama.
        var originalSignature =
            HtmlTagChecker.ComputeStructureSignature(original);

        var translatedSignature =
            HtmlTagChecker.ComputeStructureSignature(translated);

        if (!originalSignature.SequenceEqual(
                translatedSignature,
                StringComparer.OrdinalIgnoreCase))
        {
            check.Errors.Add(
                "Cấu trúc thẻ HTML không khớp. " +
                $"Gốc: [{string.Join(", ", originalSignature)}], " +
                $"bản dịch: [{string.Join(", ", translatedSignature)}].");
        }

        // 4. Ký tự không hỗ trợ.
        var unsupported = VietnameseCharacterChecker
            .FindUnsupportedCharacters(translated);

        if (unsupported.Count > 0)
        {
            check.Warnings.Add(
                "Ký tự có thể không hiển thị được: " +
                string.Join(" ", unsupported.Select(c => $"'{c}' (U+{(int)c:X4})")));
        }

        // 5. Bản dịch trùng nguyên văn bản gốc.
        //
        //    Chuỗi ngắn (dưới 3 từ) thường là tên riêng — giữ nguyên là
        //    đúng, chỉ cảnh báo. Nhưng chuỗi dài bị bỏ nguyên nghĩa là
        //    model đã bỏ sót -> phải báo lỗi để service thử lại, vì nếu
        //    không game sẽ hiển thị tiếng Anh lẫn trong bản dịch.
        if (UntranslatedDetector.IsUntranslated(original, translated))
        {
            check.Errors.Add(
                "Bản dịch chưa được dịch (vẫn là tiếng Anh).");
        }
        else if (string.Equals(
                     original.Trim(),
                     translated.Trim(),
                     StringComparison.Ordinal))
        {
            check.Warnings.Add("Bản dịch trùng nguyên văn bản gốc.");
        }

        // 6. Bản dịch có ký tự CJK trong khi bản gốc không có.
        //
        //    Model đôi khi "loãng" và sinh tiếng Trực (ví dụ "L㎜atoria",
        //    "伏击"). Bản gốc có CJK chỉ ở lore Nhật (Yamato) và keybind
        //    Nhật — nếu bản gốc không có CJK mà bản dịch có, đó chắc chắn
        //    là lỗi của model.
        if (ContainsCjk(original) == false &&
            ContainsCjk(translated))
        {
            check.Errors.Add(
                "Bản dịch chứa ký tự CJK (Trung Quốc/Nhật) " +
                "trong khi bản gốc không có.");
        }

        // 6b. Bản dịch là tiếng Pháp.
        //
        //     Model 7B đôi khi "dịch sang tiếng Pháp" ("Inhibiteur",
        //     "Aura Héroïque", "Pierre Teleportation"). Tiếng Việt không
        //     dùng các phụ âm có dấu này, nên đây là lỗi chắc chắn.
        if (LooksLikeFrench(translated) &&
            !LooksLikeFrench(original))
        {
            check.Errors.Add(
                "Bản dịch có dấu tiếng Pháp — model đã dịch sai ngôn ngữ.");
        }

        // 7. Dấu câu bị mất.
        //    Model nhỏ hay nuốt mất dấu ":" hoặc "," (ví dụ "Bounce Pad:
        //    Now deals damage" -> "Bounce Pad Giờ gây sát thương"). Điều
        //    này làm câu trông lạc lõng. Chỉ cảnh báo — không chặn, vì
        //    bản gốc cũng có những trường hợp dấu câu không cân đối.
        foreach (char punctuation in TrackedPunctuation)
        {
            int originalCount = original.Count(c => c == punctuation);
            int translatedCount = translated.Count(c => c == punctuation);

            if (originalCount > translatedCount)
            {
                check.Warnings.Add(
                    $"Bản dịch có thể thiếu dấu '{punctuation}' " +
                    $"(gốc {originalCount}, dịch {translatedCount}).");
            }
        }

        // 8. Bản dịch rỗng trong khi gốc có nội dung -> lỗi.
        if (string.IsNullOrWhiteSpace(original) &&
            !string.IsNullOrWhiteSpace(translated))
        {
            check.Errors.Add("Bản gốc rỗng nhưng bản dịch có nội dung.");
        }

        // 9. Bản dịch có tỷ lệ chữ cái quá cao -> còn sót tiếng Anh.
        //
        //    Bản dịch tiếng Việt luôn chứa ít nhất vài chữ cái có dấu
        //    hoặc từ Hán-Việt. Nếu gần như toàn chữ cái Latin không dấu
        //    và chuỗi dài -> model đã bỏ sót, in ra nguyên tiếng Anh.
        //    Chỉ kiểm tra với chuỗi đủ dài để tránh báo nhầm mã / ký hiệu.
        //
        //    <b>Không áp dụng</b> khi bản dịch giống hệt bản gốc: trường
        //    hợp đó là tên riêng giữ nguyên (hợp lệ), đã được luật #5 xử
        //    lý. Ở đây ta chỉ săn các bản dịch bị CẮT CỤT hoặc thay bằng
        //    nội dung tiếng Anh khác — ví dụ "Spiritual Overflow SOF" bị
        //    đổi thành "Spiral Overflow SOF".
        bool sameAsOriginal = string.Equals(
            original.Trim(),
            translated.Trim(),
            StringComparison.Ordinal);

        if (translated.Length >= 20 && !sameAsOriginal)
        {
            // Chỉ xét phần VĂN BẢN THẬT — bỏ qua placeholder và thẻ
            // HTML. Nếu tính cả token thì chuỗi ngắn như
            // "Purchase {g:citadel_ability_name:...}" bị tính là dài,
            // còn bản dịch "Mua {g:...}" không có chữ cái có dấu nào
            // ("Mua" là từ hợp lệ, chỉ là không dấu) -> báo nhầm.
            string plainTranslated = StripTokensAndTags(translated);
            string plainOriginal = StripTokensAndTags(original);

            int letters = plainTranslated.Count(char.IsLetter);
            int accented = plainTranslated.Count(IsVietnameseLetter);
            int asciiLetters = plainTranslated.Count(
                c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

            double asciiRatio = letters > 0
                ? (double)asciiLetters / letters
                : 0;

            // Phần văn bản thật phải đủ dài (>= 20 ký tự, >= 3 từ) thì
            // mới đáng kiểm tra — nếu không thì "Mua", "Về", "Ổ"…
            // đều sẽ bị báo nhầm.
            int words = CountWords(plainTranslated);

            bool longEnough = plainTranslated.Length >= 20 && words >= 3;

            // > 80% chữ cái không dấu VÀ không có chữ cái tiếng Việt nào.
            if (longEnough && asciiRatio > 0.80 && accented == 0)
            {
                check.Errors.Add(
                    "Bản dịch gần như toàn tiếng Anh " +
                    $"(không có chữ cái tiếng Việt nào).");
            }
        }

        // 10. Ký hiệu quan trọng bị mất.
        //
        //     Model hay "rút gọn" chuỗi và làm rơi ký hiệu có ý nghĩa:
        //     "+{s:X}%" -> ":{s:X}%", "50%" -> "50", "-10" -> "10".
        //     Đây là lỗi thay đổi *ý nghĩa* (số liệu), nên phải chặn.
        //
        //     Riêng dấu '-' chỉ được tính khi đi kèm số ("-10", "-5%"),
        //     vì dấu gạch nối trong tên ("High-Velocity Mag", "Shock-") là
        //     chữ, mất đi cũng không sai nghĩa.
        foreach (char symbol in SignificantSymbols)
        {
            int originalCount = CountSymbol(original, symbol);
            int translatedCount = CountSymbol(translated, symbol);

            if (translatedCount < originalCount)
            {
                check.Errors.Add(
                    $"Bản dịch thiếu ký hiệu '{symbol}' " +
                    $"(gốc {originalCount}, dịch {translatedCount}).");
            }
        }

        // 11. Số trong bản dịch bị mất hoặc đổi.
        //
        //     Cùng lý do với #10: số là dữ liệu, không phải văn phong.
        //     Trích các con số đứng riêng (không tính phần trong token).
        var originalNumbers = ExtractStandaloneNumbers(original);
        var translatedNumbers = ExtractStandaloneNumbers(translated);

        foreach (string number in originalNumbers)
        {
            if (!translatedNumbers.Contains(number))
            {
                check.Errors.Add(
                    $"Bản dịch thiếu số '{number}' có trong bản gốc.");

                break;
            }
        }

        return check;
    }

    /// <summary>
    /// Ký hiệu mang ý nghĩa số học — mất đi là đổi ý nghĩa.
    /// </summary>
    private static readonly char[] SignificantSymbols =
    {
        '+', '-', '=', '%', '×', '÷'
    };

    /// <summary>
    /// Đếm số lần xuất hiện của ký hiệu mang ý nghĩa.
    /// <para>
    /// Dấu '-' chỉ được tính khi đứng ngay trước một chữ số — nếu
    /// không, dấu gạch nối trong tên ("High-Velocity") sẽ bị tính nhầm.
    /// </para>
    /// </summary>
    private static int CountSymbol(string text, char symbol)
    {
        if (symbol != '-')
        {
            return text.Count(c => c == symbol);
        }

        int count = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '-')
            {
                continue;
            }

            bool followedByDigit =
                i + 1 < text.Length && char.IsDigit(text[i + 1]);

            if (followedByDigit)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Trích các con số đứng riêng trong văn bản, bỏ qua phần nằm trong
    /// token <c>{...}</c> và trong thẻ HTML.
    /// </summary>
    private static HashSet<string> ExtractStandaloneNumbers(string text)
    {
        // Bỏ token và thẻ HTML trước.
        string cleaned = TextTokenExtractor.Extract(text).Count > 0
            ? StripTokensAndTags(text)
            : text;

        var numbers = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in StandaloneNumberRegex.Matches(cleaned))
        {
            numbers.Add(match.Value);
        }

        return numbers;
    }

    /// <summary>
    /// Bỏ mọi token <c>{...}</c> và thẻ HTML khỏi chuỗi.
    /// </summary>
    private static string StripTokensAndTags(string text)
    {
        var builder = new StringBuilder(text.Length);

        int index = 0;

        while (index < text.Length)
        {
            if (text[index] == '{')
            {
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

            if (text[index] == '<')
            {
                int close = text.IndexOf('>', index);

                if (close > 0)
                {
                    index = close + 1;

                    builder.Append(' ');

                    continue;
                }
            }

            builder.Append(text[index]);
            index++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Ký tự chữ cái tiếng Việt (có dấu hoặc là chữ Hán-Việt).
    /// </summary>
    private static bool IsVietnameseLetter(char character)
    {
        return character is
            'à' or 'á' or 'ả' or 'ã' or 'ạ' or 'â' or 'ầ' or 'ấ' or 'ẩ' or 'ẫ' or 'ậ' or
            'ă' or 'ằ' or 'ắ' or 'ẳ' or 'ẵ' or 'ặ' or
            'è' or 'é' or 'ẻ' or 'ẽ' or 'ẹ' or 'ê' or 'ề' or 'ế' or 'ể' or 'ễ' or 'ệ' or
            'ì' or 'í' or 'ỉ' or 'ĩ' or 'ị' or
            'ò' or 'ó' or 'ỏ' or 'õ' or 'ọ' or 'ô' or 'ồ' or 'ố' or 'ổ' or 'ỗ' or 'ộ' or
            'ơ' or 'ờ' or 'ớ' or 'ở' or 'ỡ' or 'ợ' or
            'ù' or 'ú' or 'ủ' or 'ũ' or 'ụ' or 'ư' or 'ừ' or 'ứ' or 'ử' or 'ữ' or 'ự' or
            'ỳ' or 'ý' or 'ỷ' or 'ỹ' or 'ỵ' or
            'đ' or 'Đ' or
            'á' or 'é' or 'í' or 'ó' or 'ú' or 'ý' or
            'Á' or 'É' or 'Í' or 'Ó' or 'Ú' or 'Ý' or
            'ạ' or 'ả' or 'ấ' or 'ầ' or 'ẩ' or 'ẫ' or 'ậ' or
            'ạ' or 'ặ' or 'ặ' or 'ẹ' or 'ệ' or 'ị' or
            'ọ' or 'ỏ' or 'ố' or 'ộ' or 'ớ' or 'ợ' or
            'ụ' or 'ứ' or 'ự' or 'ủ' or 'ũ' or
            'ỵ' or 'ỳ' or 'ỷ' or 'ỹ';
    }

    /// <summary>
    /// Token runtime của Valve — <c>{...}</c> — và thẻ HTML/Panorama.
    /// <para>
    /// Dùng để lấy phần văn bản thật của một chuỗi, bỏ qua mọi token
    /// và thẻ. Nhờ vậy các kiểm tra "còn sót tiếng Anh" không bị nhiễu
    /// bởi tên biến bên trong placeholder (ví dụ "selected_build_name"
    /// trong <c>{s:selected_build_name}</c>).
    /// </para>
    /// </summary>
    private static readonly Regex TokenOrTagRegex = new(
        @"\{[^{}]*\}|</?[A-Za-z][^<>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Regex tách từ (chỉ chữ cái Latin).
    /// </summary>
    private static readonly Regex WordRegex = new(
        @"[A-Za-z]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Đếm số từ trong chuỗi.
    /// </summary>
    private static int CountWords(string text)
    {
        return WordRegex.Matches(text).Count;
    }

    /// <summary>
    /// Các ký tự có dấu <b>chỉ</b> tiếng Pháp dùng, tiếng Việt không có.
    /// <para>
    /// Cần phân biệt rất cẩn thận: tiếng Việt và tiếng Pháp dùng chung
    /// rất nhiều ký tự có dấu (<c>à á â ã è é ê ì í ò ó ô ù ú ý</c>).
    /// Nếu đưa nhầm chúng vào danh sách này thì mọi bản dịch tiếng Việt
    /// hợp lệ đều bị báo nhầm là tiếng Pháp.
    /// </para>
    /// <para>
    /// Danh sách dưới đây chỉ gồm các ký tự mà tiếng Việt <b>không</b>
    /// có: <c>ä å æ ç ë î ï ñ ö ø û ü ÿ œ</c>.
    /// </para>
    /// <para>
    /// <b>Lưu ý:</b> <c>õ</c> (U+00F5) <b>KHÔNG</b> được đưa vào đây.
    /// Tiếng Việt có dùng <c>õ</c> rất phổ biến — "lõi", "nõi", "gõ",
    /// "hõ"… Trước đây <c>õ</c> nằm trong danh sách này nên mọi bản
    /// dịch chứa "lõi" đều bị báo nhầm là "dấu tiếng Pháp" và bị loại.
    /// </para>
    /// </summary>
    private static readonly char[] FrenchAccents =
    {
        'ä', 'å', 'æ', 'ç', 'ë',
        'î', 'ï', 'ñ', 'ö', 'ø',
        'û', 'ü', 'ÿ', 'œ'
    };

    /// <summary>
    /// Chuỗi có trông như tiếng Pháp không.
    /// </summary>
    private static bool LooksLikeFrench(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (char character in text)
        {
            foreach (char accent in FrenchAccents)
            {
                if (character == accent)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Chuỗi có chứa ký tự CJK (Trung Quốc / Nhật Hán) không.
    /// </summary>
    private static bool ContainsCjk(string text)
    {
        foreach (char character in text)
        {
            if (IsCjkCodePoint(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Khoảng mã CJK đầy đủ: Kanji, Hiragana, Katakana, Hangul, Bopomofo,
    /// ký hiệu CJK, chữ tương thích, Kana mở rộng và chữ Latin fullwidth.
    /// </summary>
    private static bool IsCjkCodePoint(char character)
    {
        int code = character;

        return (code >= 0x2E80 && code <= 0x2FFF) ||   // CJK Radicals, Symbols Supplement
               (code >= 0x3000 && code <= 0x30FF) ||   // Symbols & Punctuation, Hiragana, Katakana
               (code >= 0x3100 && code <= 0x31FF) ||   // Bopomofo, Jamo, Kanbun, Strokes
               (code >= 0x3200 && code <= 0x33FF) ||   // Enclosed CJK, Compatibility
               (code >= 0x3400 && code <= 0x4DBF) ||   // Unified Ext A
               (code >= 0x4E00 && code <= 0x9FFF) ||   // Unified Ideographs
               (code >= 0xA000 && code <= 0xA4CF) ||   // Yi Syllables
               (code >= 0xA960 && code <= 0xD7FF) ||   // Hangul Jamo Ext, Hangul Syllables
               (code >= 0xF900 && code <= 0xFAFF) ||   // Compatibility Ideographs
               (code >= 0xFF00 && code <= 0xFFEF);     // Halfwidth/Fullwidth Forms
    }

    /// <summary>
    /// Các dấu câu được theo dõi — dấu câu thường bị model nuốt mất.
    /// </summary>
    private static readonly char[] TrackedPunctuation =
    {
        ':', ';', ',', '.', '!', '?', '-'
    };

    /// <summary>
    /// Số đứng riêng trong văn bản (không tính phần trong token).
    /// </summary>
    private static readonly Regex StandaloneNumberRegex = new(
        @"\d+(?:[.,]\d+)?%?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Đếm số lần xuất hiện của mỗi token, sau khi chuẩn hoá.
    /// </summary>
    private static Dictionary<string, int> CountNormalizedTokens(string text)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var token in TextTokenExtractor.Extract(text))
        {
            string key = TextTokenNormalizer.Normalize(token.Value);

            result.TryGetValue(key, out int count);

            result[key] = count + 1;
        }

        return result;
    }
}
