using DeadlockVN.Core.Common;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Infrastructure.Validation;

/// <summary>
/// Validator chất lượng bản dịch tiếng Việt cho Deadlock.
/// <para>
/// Nguyên tắc quan trọng: validator <b>so sánh bản dịch với bản gốc</b>,
/// không áp dụng luật tuyệt đối. Lý do: dữ liệu gốc của game có nhiều
/// trường hợp "bẩn" hợp lệ — 139 value có thẻ tự cân bằng
/// (<c>&lt;laughs&gt;</c>, <c>&lt;None&gt;</c>), token không chuẩn
/// (<c>{#}</c>, <c>{%param1%}</c>), v.v. Nếu check tuyệt đối thì sẽ có
/// hàng trăm báo động giả và người dịch sẽ bỏ qua tool.
/// </para>
/// </summary>
public sealed class LocalizationValidator : ILocalizationValidator
{
    /// <summary>
    /// Ngưỡng cảnh báo khi bản dịch dài hơn bản gốc.
    /// Tiếng Việt trung bình dài hơn tiếng Anh khoảng 15-25%, nên 2.0x là
    /// ngưỡng hợp lý để cảnh báo vỡ layout.
    /// </summary>
    private const double ExcessiveLengthRatio = 2.0;

    /// <summary>
    /// Ngưỡng cảnh báo khi bản dịch dài hơn tuyệt đối 120 ký tự.
    /// </summary>
    private const int ExcessiveLengthAbsolute = 120;

    public ValidationResult Validate(IEnumerable<TranslationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var allEntries = entries.ToList();

        var issues = new List<ValidationIssue>();

        // 1. Kiểm tra trùng lặp ở cấp tập bản dịch.
        //
        // Định danh đầy đủ của một entry là cặp (file nguồn, key) chứ không
        // chỉ key: trong game có những key lặp hợp lệ giữa các file khác
        // nhau (ví dụ "Language" nằm trong cả 10 file localization).
        // Chỉ coi là trùng lặp khi cả file nguồn lẫn key đều giống nhau.
        var duplicates = allEntries
            .GroupBy(entry => entry.Id, TranslationIdComparer.Instance)
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            issues.Add(new ValidationIssue
            {
                Key = duplicate.First().Key,
                Rule = ValidationRule.DuplicateKey,
                Severity = ValidationSeverity.Error,
                Message =
                    $"Localization key bị trùng lặp {duplicate.Count()} lần " +
                    $"trong file '{duplicate.First().SourceFile}'.",
                Suggestion = "Mỗi key chỉ được xuất hiện một lần trong cùng file."
            });
        }

        // 2. Kiểm tra từng entry.
        foreach (var entry in allEntries)
        {
            issues.AddRange(ValidateEntry(entry));
        }

        return new ValidationResult
        {
            Issues = issues,
            CheckedCount = allEntries.Count,
            TranslatedCount = allEntries.Count(e => !string.IsNullOrWhiteSpace(e.TranslatedText)),
            ApprovedCount = allEntries.Count(e => e.Status == TranslationStatus.Approved)
        };
    }

    public IReadOnlyList<ValidationIssue> ValidateEntry(TranslationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var issues = new List<ValidationIssue>();

        string original = entry.OriginalText ?? string.Empty;
        string translated = entry.TranslatedText ?? string.Empty;

        // --- Rỗng ---
        if (string.IsNullOrWhiteSpace(translated))
        {
            // Nếu bản gốc cũng rỗng thì không phải lỗi.
            if (!string.IsNullOrWhiteSpace(original))
            {
                issues.Add(new ValidationIssue
                {
                    Key = entry.Key,
                    Rule = ValidationRule.MissingTranslation,
                    Severity = ValidationSeverity.Error,
                    Message = "Chưa có bản dịch.",
                    Suggestion = "Dịch giá trị này hoặc đánh dấu để bỏ qua."
                });
            }

            return issues;
        }

        // --- Trùng nguyên văn bản gốc ---
        if (string.Equals(
            original.Trim(),
            translated.Trim(),
            StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.Untranslated,
                Severity = ValidationSeverity.Warning,
                Message = "Bản dịch trùng nguyên văn bản gốc.",
                Suggestion = "Nếu đây là thuật ngữ giữ nguyên (ví dụ \"Deadlock\"), bỏ qua cảnh báo này."
            });
        }

        // --- Khoảng trắng thừa ---
        if (translated != translated.Trim())
        {
            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.LeadingOrTrailingWhitespace,
                Severity = ValidationSeverity.Warning,
                Message = "Bản dịch có khoảng trắng thừa ở đầu hoặc cuối.",
                Suggestion = "Cắt bỏ khoảng trắng thừa."
            });
        }

        // --- Placeholder ---
        ValidatePlaceholders(entry, original, translated, issues);

        // --- Thẻ HTML ---
        ValidateHtmlTags(entry, original, translated, issues);

        // --- Ký tự ---
        ValidateCharacters(entry, translated, issues);

        // --- Độ dài ---
        ValidateLength(entry, original, translated, issues);

        return issues;
    }

    private static void ValidatePlaceholders(
        TranslationEntry entry,
        string original,
        string translated,
        List<ValidationIssue> issues)
    {
        var originalTokens = TextTokenExtractor.Extract(original);
        var translatedTokens = TextTokenExtractor.Extract(translated);

        var originalCounts = originalTokens
            .GroupBy(t => t.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var translatedCounts = translatedTokens
            .GroupBy(t => t.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // 1. Thiếu token.
        foreach (var token in originalCounts.Keys)
        {
            if (translatedCounts.ContainsKey(token))
            {
                continue;
            }

            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.MissingPlaceholder,
                Severity = ValidationSeverity.Error,
                Message = $"Thiếu placeholder {token} trong bản dịch.",
                Suggestion = $"Giữ nguyên {token} ở vị trí tương ứng trong câu tiếng Việt."
            });
        }

        // 2. Thừa token.
        foreach (var token in translatedCounts.Keys)
        {
            if (originalCounts.ContainsKey(token))
            {
                continue;
            }

            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.ExtraPlaceholder,
                Severity = ValidationSeverity.Error,
                Message = $"Bản dịch có placeholder {token} không tồn tại trong bản gốc.",
                Suggestion = $"Xóa {token} hoặc kiểm tra xem có nhầm với token khác không."
            });
        }

        // 3. Số lượng token khác nhau (cùng tên nhưng khác số lần xuất hiện).
        foreach (var token in originalCounts.Keys.Intersect(translatedCounts.Keys, StringComparer.Ordinal))
        {
            if (originalCounts[token] == translatedCounts[token])
            {
                continue;
            }

            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.PlaceholderCountMismatch,
                Severity = ValidationSeverity.Error,
                Message =
                    $"Placeholder {token} xuất hiện {originalCounts[token]} lần trong bản gốc " +
                    $"nhưng {translatedCounts[token]} lần trong bản dịch.",
                Suggestion = "Giữ nguyên số lần xuất hiện của placeholder."
            });
        }

        // 4. Token printf-style bị đổi tên (ví dụ %s1 -> %s2).
        var originalPrintf = originalTokens
            .Where(t => t.Kind == TextTokenKind.Printf)
            .Select(t => t.Value)
            .ToHashSet(StringComparer.Ordinal);

        var translatedPrintf = translatedTokens
            .Where(t => t.Kind == TextTokenKind.Printf)
            .Select(t => t.Value)
            .ToHashSet(StringComparer.Ordinal);

        if (!originalPrintf.SetEquals(translatedPrintf))
        {
            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.PrintfTokenMismatch,
                Severity = ValidationSeverity.Error,
                Message =
                    $"Token printf-style không khớp. " +
                    $"Bản gốc: [{string.Join(", ", originalPrintf.Order())}] " +
                    $"Bản dịch: [{string.Join(", ", translatedPrintf.Order())}].",
                Suggestion = "Giữ nguyên các token như %s1, %s2 theo đúng thứ tự."
            });
        }
    }

    private static void ValidateHtmlTags(
        TranslationEntry entry,
        string original,
        string translated,
        List<ValidationIssue> issues)
    {
        var originalSignature = HtmlTagChecker.ComputeStructureSignature(original);
        var translatedSignature = HtmlTagChecker.ComputeStructureSignature(translated);

        if (originalSignature.SequenceEqual(translatedSignature, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        issues.Add(new ValidationIssue
        {
            Key = entry.Key,
            Rule = ValidationRule.UnbalancedHtmlTag,
            Severity = ValidationSeverity.Error,
            Message =
                "Cấu trúc thẻ HTML/Panorama không khớp với bản gốc. " +
                $"Bản gốc: [{string.Join(", ", originalSignature)}] " +
                $"Bản dịch: [{string.Join(", ", translatedSignature)}].",
            Suggestion = "Giữ nguyên số lượng và thứ tự thẻ <span>, <br>, <b>, <i> như bản gốc."
        });
    }

    private static void ValidateCharacters(
        TranslationEntry entry,
        string translated,
        List<ValidationIssue> issues)
    {
        var unsupported = VietnameseCharacterChecker.FindUnsupportedCharacters(translated);

        if (unsupported.Count > 0)
        {
            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.UnsupportedCharacter,
                Severity = ValidationSeverity.Warning,
                Message =
                    "Bản dịch chứa ký tự có thể không hiển thị được: " +
                    string.Join(" ", unsupported.Select(c => $"'{c}' (U+{(int)c:X4})")),
                Suggestion =
                    "Kiểm tra font của game có glyph cho các ký tự này, " +
                    "hoặc thay bằng ký tự tương đương."
            });
        }

        // Ký tự điều khiển (trừ \n, \r, \t).
        var controlChars = translated
            .Where(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))
            .Distinct()
            .ToList();

        if (controlChars.Count > 0)
        {
            issues.Add(new ValidationIssue
            {
                Key = entry.Key,
                Rule = ValidationRule.ControlCharacter,
                Severity = ValidationSeverity.Error,
                Message =
                    "Bản dịch chứa ký tự điều khiển không hợp lệ: " +
                    string.Join(" ", controlChars.Select(c => $"U+{(int)c:X4}")),
                Suggestion = "Xóa các ký tự điều khiển này."
            });
        }
    }

    private static void ValidateLength(
        TranslationEntry entry,
        string original,
        string translated,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(original))
        {
            return;
        }

        int originalLength = original.Length;
        int translatedLength = translated.Length;

        double ratio = (double)translatedLength / originalLength;

        if (ratio <= ExcessiveLengthRatio &&
            translatedLength - originalLength <= ExcessiveLengthAbsolute)
        {
            return;
        }

        issues.Add(new ValidationIssue
        {
            Key = entry.Key,
            Rule = ValidationRule.ExcessiveLength,
            Severity = ValidationSeverity.Warning,
            Message =
                $"Bản dịch dài {translatedLength} ký tự so với {originalLength} " +
                $"ký tự của bản gốc ({ratio:F2}x) — có thể làm vỡ UI.",
            Suggestion = "Rút gọn bản dịch hoặc kiểm tra lại xu hướng của UI."
        });
    }
}
