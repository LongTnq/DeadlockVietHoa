using System.Text;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Parsers.KeyValues;
using DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

namespace DeadlockVN.Infrastructure.Exporting;

/// <summary>
/// Xuất bộ bản dịch ra file KeyValues mà game đọc được.
/// <para>
/// <b>Cách hoạt động:</b>
/// </para>
/// <list type="number">
/// <item><description>
/// Đọc file gốc, ghi nhớ có BOM hay không.
/// </description></item>
/// <item><description>
/// Parse file gốc thành AST kèm <i>offset</i> của từng value.
/// </description></item>
/// <item><description>
/// Gom tất cả thay thế (value đã dịch + header <c>"Language"</c>) thành
/// <b>một danh sách</b>, rồi áp dụng từ cuối về đầu.
/// </description></item>
/// <item><description>
/// Ghi lại file với BOM giống file gốc.
/// </description></item>
/// </list>
/// <para>
/// Nhờ thay tại chỗ, mọi thứ ngoài value (comment, thụt lề, thứ tự escape,
/// xuống dòng thật trong value) được giữ nguyên tuyệt đối — điều mà
/// serialize lại cây không thể đảm bảo.
/// </para>
/// </summary>
public sealed class LocalizationExporter : ILocalizationExporter
{
    /// <summary>
    /// Khóa header chứa tên ngôn ngữ.
    /// </summary>
    private const string LanguageKey = "Language";

    public async Task<LocalizationExportResult> ExportAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyCollection<TranslationEntry> entries,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(entries);

        byte[] bytes = await File.ReadAllBytesAsync(
            sourcePath,
            cancellationToken);

        // Đọc byte thô thay vì File.ReadAllTextAsync vì hàm đó tự nhận diện
        // encoding và **bỏ BOM**, khiến ta không xác định được file gốc có
        // BOM hay không (điều cần giữ nguyên khi ghi lại).
        bool hasBom =
            bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;

        // Lexer tự bỏ BOM; ta làm việc trên chuỗi đã bỏ BOM để offset khớp.
        string content = new UTF8Encoding(false).GetString(
            hasBom ? bytes[3..] : bytes);

        var parser = new KeyValuesParser();
        var document = parser.Parse(content);

        // Gom bản dịch theo key.
        var translated = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.TranslatedText))
            {
                translated[entry.Key] = entry.TranslatedText;
            }
        }

        // Tìm key "Language" ở cấp gốc (header của file).
        var languageSpan = FindRootLanguageSpan(document);

        var replacements = new List<Replacement>();
        var originalKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in document.Nodes)
        {
            CollectReplacements(
                node,
                replacements,
                originalKeys,
                translated,
                languageSpan);
        }

        var result = new LocalizationExportResult
        {
            OutputPath = outputPath,
            TotalEntries = originalKeys.Count,
            HadByteOrderMark = hasBom
        };

        foreach (var key in translated.Keys)
        {
            // Header "Language" ở cấp gốc là metadata, không phải nội
            // dịch — nó được ghi đè bằng tên ngôn ngữ đích. Vì vậy không
            // được báo là "key không tồn tại trong file gốc".
            if (string.Equals(key, LanguageKey, StringComparison.Ordinal) &&
                languageSpan is { } rootHeader &&
                !rootHeader.IsEmpty)
            {
                continue;
            }

            if (!originalKeys.Contains(key))
            {
                result.UnknownKeys.Add(key);
            }
        }

        foreach (var key in originalKeys)
        {
            if (!translated.ContainsKey(key))
            {
                result.MissingKeys.Add(key);
            }
        }

        result.TranslatedEntries = replacements.Count(x => x.IsTranslation);
        result.UntranslatedEntries = result.TotalEntries - result.TranslatedEntries;

        // Áp dụng header ngôn ngữ đích.
        if (!string.IsNullOrWhiteSpace(targetLanguage) &&
            languageSpan is { } headerSpan &&
            !headerSpan.IsEmpty)
        {
            replacements.Add(new Replacement(
                headerSpan,
                KeyValuesEscaper.Quote(targetLanguage),
                IsTranslation: false));

            result.LanguageHeaderRewritten = true;
        }

        string output = ApplyReplacements(content, replacements);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Ghi UTF-8, giữ nguyên trạng thái BOM của file gốc.
        byte[] outputBytes = new UTF8Encoding(false).GetBytes(output);

        if (hasBom)
        {
            var withBom = new byte[outputBytes.Length + 3];

            withBom[0] = 0xEF;
            withBom[1] = 0xBB;
            withBom[2] = 0xBF;

            Buffer.BlockCopy(outputBytes, 0, withBom, 3, outputBytes.Length);

            outputBytes = withBom;
        }

        await File.WriteAllBytesAsync(
            outputPath,
            outputBytes,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Tìm span của key <c>"Language"</c> nằm ở cấp gốc của file (ngay dưới
    /// node gốc, thường là <c>"lang"</c>).
    /// <para>
    /// Chỉ xét node con đầu tiên của node gốc — để không nhầm với các key
    /// <c>Language</c> sâu hơn (ví dụ <c>citadel_settings_language</c>).
    /// </para>
    /// </summary>
    private static KeyValuesSpan? FindRootLanguageSpan(KeyValuesDocument document)
    {
        foreach (var node in document.Nodes)
        {
            if (!node.IsObject)
            {
                continue;
            }

            foreach (var child in node.Children)
            {
                if (child.IsValue &&
                    string.Equals(child.Key, LanguageKey, StringComparison.Ordinal))
                {
                    return child.ValueSpan;
                }
            }
        }

        return null;
    }

    private static void CollectReplacements(
        KeyValuesNode node,
        List<Replacement> replacements,
        HashSet<string> originalKeys,
        Dictionary<string, string> translated,
        KeyValuesSpan? languageSpan)
    {
        if (node.IsValue)
        {
            // Header "Language" là metadata, không phải nội dung cần dịch
            // -> không tính vào tổng số entry và không báo thiếu bản dịch.
            bool isLanguageHeader =
                languageSpan is { } header &&
                header.Start == node.ValueSpan.Start;

            if (isLanguageHeader)
            {
                return;
            }

            originalKeys.Add(node.Key);

            if (translated.TryGetValue(node.Key, out string? text))
            {
                replacements.Add(new Replacement(
                    node.ValueSpan,
                    KeyValuesEscaper.Quote(text),
                    IsTranslation: true));
            }

            return;
        }

        foreach (var child in node.Children)
        {
            CollectReplacements(
                child,
                replacements,
                originalKeys,
                translated,
                languageSpan);
        }
    }

    /// <summary>
    /// Thay các đoạn offset trong chuỗi gốc. Các đoạn được xử lý từ cuối về
    /// đầu để không phải chỉnh lại offset của các đoạn còn lại.
    /// </summary>
    private static string ApplyReplacements(
        string content,
        List<Replacement> replacements)
    {
        if (replacements.Count == 0)
        {
            return content;
        }

        var ordered = replacements
            .OrderBy(r => r.Span.Start)
            .ToList();

        var result = content;

        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var replacement = ordered[i];

            result = result.Remove(replacement.Span.Start, replacement.Span.Length)
                           .Insert(replacement.Span.Start, replacement.Text);
        }

        return result;
    }

    /// <summary>
    /// Một lần thay thế tại chỗ trên chuỗi gốc.
    /// </summary>
    private readonly record struct Replacement(
        KeyValuesSpan Span,
        string Text,
        bool IsTranslation);
}
