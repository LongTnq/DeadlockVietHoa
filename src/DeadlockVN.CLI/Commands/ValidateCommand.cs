using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;
using DeadlockVN.Infrastructure.Parsers.KeyValues;
using DeadlockVN.Infrastructure.Validation;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>validate</c>: kiểm tra chất lượng bản dịch tiếng Việt.
/// <para>
/// Chế độ <c>--self-check</c>: dùng bản gốc tiếng Anh làm "bản dịch" để
/// kiểm tra xem validator có báo động giả hay không. Đây là "bài kiểm tra
/// tự tham chiếu" — kết quả PASS nghĩa là validator đủ nhạy để dùng.
/// </para>
/// </summary>
internal static class ValidateCommand
{
    private const string DefaultOriginalDirectory = "workspace/source/original";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        bool selfCheck = args.Contains("--self-check", StringComparer.OrdinalIgnoreCase);

        string? translationsPath = null;

        foreach (string arg in args)
        {
            if (arg == "--self-check")
            {
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                translationsPath = arg;
            }
        }

        if (selfCheck)
        {
            return await RunSelfCheckAsync();
        }

        if (translationsPath is null)
        {
            Console.Error.WriteLine(
                "Thiếu đường dẫn file bản dịch JSON hoặc cờ --self-check.\n" +
                "Ví dụ:\n" +
                "  deadlockvn validate workspace/translations/vietnamese.json\n" +
                "  deadlockvn validate --self-check");

            return 1;
        }

        return await RunValidateAsync(translationsPath);
    }

    /// <summary>
    /// Tự kiểm tra: dùng bản gốc tiếng Anh làm bản dịch, validator phải
    /// không báo lỗi nghiêm trọng nào.
    /// </summary>
    private static async Task<int> RunSelfCheckAsync()
    {
        string? originalDirectory = ResolveOriginalDirectory();

        if (originalDirectory is null)
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục '{DefaultOriginalDirectory}'.");

            return 1;
        }

        var files = Directory
            .GetFiles(originalDirectory, "*_english.txt", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var validator = new LocalizationValidator();
        var parser = new KeyValuesParser();

        var allIssues = new List<ValidationIssue>();
        int entryCount = 0;

        foreach (var file in files)
        {
            string source = await File.ReadAllTextAsync(file);

            var document = parser.Parse(source);

            string relativePath = Path
                .GetRelativePath(originalDirectory, file)
                .Replace('\\', '/');

            var entries = document.EnumerateEntries()
                .Select(e => new TranslationEntry
                {
                    Id = new TranslationId(relativePath, e.Key),
                    OriginalText = e.Value,
                    TranslatedText = e.Value
                })
                .ToList();

            entryCount += entries.Count;

            var result = validator.Validate(entries);

            allIssues.AddRange(result.Issues);
        }

        Console.WriteLine("=== SELF-CHECK: dùng bản gốc tiếng Anh làm bản dịch ===");
        Console.WriteLine($"File kiểm tra : {files.Count}");
        Console.WriteLine($"Entry kiểm tra: {entryCount}");
        Console.WriteLine();

        var errors = allIssues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
        var warnings = allIssues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();

        Console.WriteLine($"Lỗi (Error)     : {errors.Count}");
        Console.WriteLine($"Cảnh báo (Warning): {warnings.Count}");
        Console.WriteLine();

        if (allIssues.Count > 0)
        {
            Console.WriteLine("Phân bố theo rule:");

            foreach (var group in allIssues.GroupBy(i => i.Rule).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {group.Key,-26} {group.Count()}");
            }

            Console.WriteLine();
            Console.WriteLine("Mẫu chi tiết (tối đa 15):");

            foreach (var issue in allIssues.Take(15))
            {
                Console.WriteLine($"  {issue}");
            }

            Console.WriteLine();
        }

        if (errors.Count > 0)
        {
            Console.WriteLine(
                "KẾT LUẬN: FAIL — validator báo lỗi trên chính bản gốc " +
                "(báo động giả). Cần sửa validator trước khi dùng.");

            return 1;
        }

        Console.WriteLine(
            "KẾT LUẬN: PASS — validator không báo lỗi nghiêm trọng nào trên bản gốc. " +
            "Có thể dùng để kiểm tra bản dịch tiếng Việt.");

        return 0;
    }

    private static async Task<int> RunValidateAsync(string translationsPath)
    {
        var fullPath = Path.GetFullPath(translationsPath);

        if (Directory.Exists(fullPath))
        {
            return await RunValidateDirectoryAsync(fullPath);
        }

        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"Không tìm thấy file: {fullPath}");

            return 1;
        }

        var store = new JsonTranslationStore();

        TranslationDocument document;

        try
        {
            document = await store.LoadAsync(fullPath);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"Lỗi đọc file bản dịch: {ex.Message}");

            return 1;
        }

        return ValidateDocument(fullPath, document);
    }

    /// <summary>
    /// Validate một thư mục bản dịch tách theo nhóm.
    /// </summary>
    private static async Task<int> RunValidateDirectoryAsync(string directory)
    {
        var store = new JsonTranslationStore();

        TranslationDocument document;

        try
        {
            document = await store.LoadSplitAsync(directory);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"Lỗi đọc thư mục bản dịch: {ex.Message}");

            return 1;
        }

        return ValidateDocument(directory, document);
    }

    private static int ValidateDocument(string displayPath, TranslationDocument document)
    {
        var validator = new LocalizationValidator();

        var result = validator.Validate(document.Entries);

        Console.WriteLine($"File: {displayPath}");
        Console.WriteLine($"Entry kiểm tra    : {result.CheckedCount}");
        Console.WriteLine($"Đã có bản dịch    : {result.TranslatedCount}");
        Console.WriteLine($"Đã được duyệt    : {result.ApprovedCount}");
        Console.WriteLine($"Lỗi (Error)      : {result.IssuesBySeverity.GetValueOrDefault(ValidationSeverity.Error)}");
        Console.WriteLine($"Cảnh báo (Warning): {result.IssuesBySeverity.GetValueOrDefault(ValidationSeverity.Warning)}");
        Console.WriteLine();

        if (result.Issues.Count > 0)
        {
            Console.WriteLine("Phân bố theo rule:");

            foreach (var group in result.Issues.GroupBy(i => i.Rule).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {group.Key,-26} {group.Count()}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Chi tiết (tối đa 50):");

        foreach (var issue in result.Issues.Take(50))
        {
            Console.WriteLine($"  {issue}");

            if (!string.IsNullOrEmpty(issue.Suggestion))
            {
                Console.WriteLine($"      -> {issue.Suggestion}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(result.CanBuild
            ? "KẾT LUẬN: PASS — có thể đóng gói VPK."
            : "KẾT LUẬN: FAIL — có lỗi nghiêm trọng, chưa được đóng gói VPK.");

        return result.CanBuild ? 0 : 1;
    }

    private static string? ResolveOriginalDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "source",
                "original");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
