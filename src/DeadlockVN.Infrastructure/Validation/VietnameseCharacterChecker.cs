using DeadlockVN.Core.Common;

namespace DeadlockVN.Infrastructure.Validation;

/// <summary>
/// Kiểm tra ký tự có thể hiển thị được trong game Deadlock.
/// <para>
/// Font của Deadlock có thể thiếu glyph. Vì chưa xác minh được font thực tế,
/// validator chỉ kiểm tra theo bảng mã Unicode: mọi ký tự trong bản dịch
/// phải thuộc tập ký tự mà game đang dùng. Ký tự ngoài tập đó sẽ bị báo động.
/// </para>
/// <para>
/// <b>Lưu ý về CJK:</b> bản gốc của game chứa tiếng Nhật hợp lệ (lore
/// Yamato, các keybind Nhật: 全/半/角/変/換/無, kana). Vì game hiển thị được
/// CJK nên CJK được coi là ký tự được hỗ trợ — tuyệt đối không báo động,
/// nếu không sẽ chặn việc giữ nguyên lore Nhật khi Việt hóa.
/// </para>
/// </summary>
public static class VietnameseCharacterChecker
{
    /// <summary>
    /// Các ký tự thường gặp trong bản gốc tiếng Anh mà vẫn được phép giữ nguyên.
    /// </summary>
    private static readonly HashSet<char> AllowedExtra = new()
    {
        '…', // …
        '‘', '’', // ' '
        '“', '”', // " "
        '–', '—', // – —
        '→', '←', '↑', '↓', // arrows
        '°', '¥', '€', '$', '£', // symbols
        '·', '•', // bullets
        '×', '÷', // math
        '±', '≤', '≥',
        '©', '®', '™',
        '¡', '¿'
    };

    /// <summary>
    /// Kiểm tra một chuỗi có chứa ký tự không được hỗ trợ hay không.
    /// </summary>
    /// <returns>Danh sách ký tự lỗi (rỗng nếu hợp lệ).</returns>
    public static IReadOnlyList<char> FindUnsupportedCharacters(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<char>();
        }

        var result = new List<char>();

        foreach (char character in text)
        {
            if (IsSupported(character))
            {
                continue;
            }

            if (!result.Contains(character))
            {
                result.Add(character);
            }
        }

        return result;
    }

    /// <summary>
    /// Ký tự có được hỗ trợ hay không.
    /// </summary>
    public static bool IsSupported(char character)
    {
        // ASCII.
        if (character is >= ' ' and <= '~')
        {
            return true;
        }

        // Xuống dòng, tab, và các ký tự điều khiển khác được xử lý riêng.
        if (character is '\n' or '\r' or '\t')
        {
            return true;
        }

        // Latin-1 Supplement.
        if (character is >= ' ' and <= 'ÿ')
        {
            return true;
        }

        // Latin Extended-A/B phần lớn phủ tiếng Việt + các ngôn ngữ châu Âu.
        if (character is >= 'Ā' and <= 'ſ')
        {
            return true;
        }

        // Latin Extended Additional (U+1E00–U+1EFF).
        // <b>Quan trọng:</b> đây là nơi chứa phần lớn dấu tiếng Việt dùng
        // dạng precomposed — ắ ằ ẵ ặ ế ề ể ệ ớ ờ ở ợ ứ ừ ử ự ị ọ ủ.
        // Thiếu dải này thì mọi bản dịch tiếng Việt đều bị báo động sai.
        if (character is >= 'Ḁ' and <= 'ỿ')
        {
            return true;
        }

        // Một số ký tự bổ sung hay gặp.
        if (character is >= 'ƒ' and <= '˅')
        {
            return true;
        }

        // Kana (hiragana + katakana) — bản gốc có keybind Nhật.
        if (character is >= '぀' and <= 'ヿ')
        {
            return true;
        }

        // Kana mở rộng + dấu thanh.
        if (character is >= 'ㇰ' and <= 'ㇿ')
        {
            return true;
        }

        // CJK Unified Ideographs — lore Nhật (Yamato) và keybind 全/半/角/変/換/無.
        if (character is >= '一' and <= '鿿')
        {
            return true;
        }

        // CJK Symbols and Punctuation (、。「」…).
        if (character is >= '　' and <= '〿')
        {
            return true;
        }

        // Halfwidth/Fullwidth Forms.
        if (character is >= 'ｦ' and <= 'ﾟ')
        {
            return true;
        }

        // CJK Compatibility Forms.
        if (character is >= '豈' and <= '﫿')
        {
            return true;
        }

        return AllowedExtra.Contains(character);
    }
}
