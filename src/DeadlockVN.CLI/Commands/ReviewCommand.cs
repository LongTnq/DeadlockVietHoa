using DeadlockVN.Application.DTOs;
using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Các lệnh review: <c>review</c>, <c>review-export</c>,
/// <c>review-import</c>, <c>approve</c>.
/// </summary>
internal static class ReviewCommand
{
    private const string DefaultDatabase = "workspace/translations/vietnamese.json";

    public static Task<int> ExecuteAsync(string[] args) => DispatchAsync(args);

    private static async Task<int> DispatchAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();

            return 0;
        }

        string subCommand = args[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "review":
                return await RunReviewAsync(args[1..]);

            case "review-export":
                return await RunExportAsync(args[1..]);

            case "review-import":
                return await RunImportAsync(args[1..]);

            case "approve":
                return await RunApproveAsync(args[1..]);

            case "-h":
            case "--help":
            case "help":
                PrintUsage();

                return 0;

            default:
                Console.Error.WriteLine(
                    $"Lệnh review không hợp lệ: {subCommand}");

                PrintUsage();

                return 1;
        }
    }

    /// <summary>
    /// <c>review</c> — in ra danh sách entry chờ review để người dịch đọc
    /// trực tiếp trên terminal.
    /// </summary>
    private static async Task<int> RunReviewAsync(string[] args)
    {
        var options = ParseOptions(args);

        var (document, databasePath) = await LoadDocumentAsync(options);

        var service = new ReviewService();

        var categories = ParseCategories(options.Categories);

        var export = service.BuildExport(
            document,
            categories,
            options.All,
            options.Limit);

        if (export.Items.Count == 0)
        {
            Console.WriteLine(
                "Không có entry nào cần review " +
                "(mọi entry đã Approved hoặc chưa được dịch).");

            return 0;
        }

        Console.WriteLine("=== REVIEW: các entry chờ duyệt ===");
        Console.WriteLine(
            $"Cơ sở dữ liệu: {databasePath}  |  " +
            $"Số entry: {export.Items.Count}");
        Console.WriteLine();

        string? currentCategory = null;

        foreach (var item in export.Items)
        {
            if (item.Category != currentCategory)
            {
                currentCategory = item.Category;

                Console.WriteLine($"--- {currentCategory} ---");
            }

            Console.WriteLine($"[{item.Key}]  ({item.SourceFile})");
            Console.WriteLine($"  EN: {item.Original}");
            Console.WriteLine($"  VI: {item.Translation}");
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// <c>review-export</c> — xuất entry ra file JSON để người khác sửa.
    /// </summary>
    private static async Task<int> RunExportAsync(string[] args)
    {
        var options = ParseOptions(args);

        var (document, databasePath) = await LoadDocumentAsync(options);

        var service = new ReviewService();

        var categories = ParseCategories(options.Categories);

        var export = service.BuildExport(
            document,
            categories,
            options.All,
            options.Limit);

        string outputPath = options.OutputPath
            ?? Path.Combine(
                "workspace",
                "review",
                $"review-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        await ReviewService.WriteExportAsync(export, outputPath);

        Console.WriteLine("=== REVIEW EXPORT ===");
        Console.WriteLine($"Cơ sở dữ liệu : {databasePath}");
        Console.WriteLine($"Số entry      : {export.Items.Count}");
        Console.WriteLine($"File xuất     : {outputPath}");
        Console.WriteLine();
        Console.WriteLine(
            "Người dịch sửa trường 'newTranslation' và 'newStatus' trong file này, " +
            "rồi chạy:");
        Console.WriteLine($"  deadlockvn review-import \"{outputPath}\"");

        return 0;
    }

    /// <summary>
    /// <c>review-import</c> — nhập lại các chỉnh sửa.
    /// </summary>
    private static async Task<int> RunImportAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "Thiếu đường dẫn file review cần nhập.\n" +
                "Ví dụ: deadlockvn review-import workspace/review/review-xxx.json");

            return 1;
        }

        string importPath = args[0];

        var options = ParseOptions(args[1..]);

        if (!File.Exists(importPath))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy file review: {importPath}");

            return 1;
        }

        var (document, databasePath) = await LoadDocumentAsync(options);

        var imported = await ReviewService.ReadExportAsync(importPath);

        var service = new ReviewService();

        var result = service.ApplyImport(document, imported);

        if (result.Updated > 0)
        {
            await SaveDocumentAsync(document, databasePath, options);
        }

        Console.WriteLine("=== REVIEW IMPORT ===");
        Console.WriteLine($"File nhập   : {importPath}");
        Console.WriteLine($"Cập nhật    : {result.Updated}");
        Console.WriteLine($"Đã duyệt    : {result.Approved}");
        Console.WriteLine($"Không tìm thấy: {result.NotFound}");
        Console.WriteLine($"Bỏ qua      : {result.Skipped}");

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("=== LỖI ===");

            foreach (string error in result.Errors.Take(20))
            {
                Console.WriteLine("  " + error);
            }
        }

        return result.Errors.Count > 0 ? 1 : 0;
    }

    /// <summary>
    /// <c>approve</c> — duyệt hàng loạt entry đã review.
    /// <para>
    /// Dùng sau khi người dịch đã xem qua file review-export và sửa
    /// trực tiếp trong cơ sở dữ liệu (chứ không dùng review-import).
    /// </para>
    /// </summary>
    private static async Task<int> RunApproveAsync(string[] args)
    {
        var options = ParseOptions(args);

        var (document, databasePath) = await LoadDocumentAsync(options);

        var categories = ParseCategories(options.Categories);

        var targets = document.Entries
            .Where(entry =>
            {
                if (categories is not null && categories.Count > 0 &&
                    !categories.Contains(entry.Category) &&
                    !categories.Contains(LocalizationCategory.All))
                {
                    return false;
                }

                return entry.Status == TranslationStatus.Reviewed &&
                       !string.IsNullOrWhiteSpace(entry.TranslatedText);
            })
            .ToList();

        if (targets.Count == 0)
        {
            Console.WriteLine("Không có entry nào ở trạng thái Reviewed.");

            return 0;
        }

        if (!options.Yes)
        {
            Console.WriteLine(
                $"Sẽ duyệt {targets.Count} entry. " +
                "Thêm --yes nếu chắc chắn.");

            Console.WriteLine("Danh sách nhóm nội dung:");

            foreach (var group in targets.GroupBy(entry => entry.Category))
            {
                Console.WriteLine($"  {group.Key,-10} {group.Count(),6}");
            }

            return 1;
        }

        foreach (var entry in targets)
        {
            entry.Status = TranslationStatus.Approved;
            entry.OriginalTextSnapshot = entry.OriginalText;
        }

        await SaveDocumentAsync(document, databasePath, options);

        Console.WriteLine("=== APPROVE ===");
        Console.WriteLine($"Đã duyệt: {targets.Count} entry");
        Console.WriteLine($"Lưu tại : {databasePath}");

        return 0;
    }

    private static async Task<(TranslationDocument Document, string Path)>
        LoadDocumentAsync(Options options)
    {
        string path = options.DatabasePath ?? ResolveDefaultDatabase();

        var store = new JsonTranslationStore();

        TranslationDocument document;

        if (options.Split)
        {
            string root = options.SplitDirectory ?? ResolveDefaultSplitDirectory();

            document = await store.LoadSplitAsync(root);
        }
        else
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Không tìm thấy file bản dịch: {path}", path);
            }

            document = await store.LoadAsync(path);
        }

        return (document, path);
    }

    private static async Task SaveDocumentAsync(
        TranslationDocument document,
        string path,
        Options options)
    {
        var store = new JsonTranslationStore();

        if (options.Split)
        {
            await store.SaveSplitAsync(
                document,
                options.SplitDirectory ?? ResolveDefaultSplitDirectory());

            return;
        }

        await store.SaveAsync(document, path);
    }

    private static IReadOnlyCollection<LocalizationCategory>? ParseCategories(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var categories = new List<LocalizationCategory>();

        foreach (string part in value.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(part, ignoreCase: true, out LocalizationCategory category))
            {
                categories.Add(category);

                continue;
            }

            switch (part.ToLowerInvariant())
            {
                case "item":
                    categories.Add(LocalizationCategory.Items);
                    break;
                case "hero":
                    categories.Add(LocalizationCategory.Heroes);
                    break;
                case "ability":
                    categories.Add(LocalizationCategory.Abilities);
                    break;
                case "vo":
                case "voice":
                    categories.Add(LocalizationCategory.Voice);
                    break;
                default:
                    Console.Error.WriteLine(
                        $"Không hiểu nhóm nội dung: {part}");

                    return null;
            }
        }

        return categories.Count > 0 ? categories : null;
    }

    private static string ResolveDefaultDatabase()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "translations",
                "vietnamese.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return DefaultDatabase;
    }

    private static string ResolveDefaultSplitDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "translations");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return "workspace/translations";
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db":
                case "--database":
                    if (i + 1 < args.Length)
                    {
                        options.DatabasePath = args[++i];
                    }

                    break;

                case "--split":
                    options.Split = true;

                    break;

                case "--split-dir":
                    if (i + 1 < args.Length)
                    {
                        options.Split = true;
                        options.SplitDirectory = args[++i];
                    }

                    break;

                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        options.OutputPath = args[++i];
                    }

                    break;

                case "--categories":
                    if (i + 1 < args.Length)
                    {
                        options.Categories = args[++i];
                    }

                    break;

                case "--limit":
                    if (i + 1 < args.Length &&
                        int.TryParse(args[++i], out int limit))
                    {
                        options.Limit = limit;
                    }

                    break;

                case "--all":
                    options.All = true;

                    break;

                case "--yes":
                case "-y":
                    options.Yes = true;

                    break;

                default:
                    break;
            }
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Công cụ review bản dịch.

        Usage:
          deadlockvn review         [--db <file>] [--categories <list>] [--all] [--limit <n>]
          deadlockvn review-export  [--db <file>] [--output <file>] [--categories <list>]
                                   [--all] [--limit <n>]
          deadlockvn review-import  <file> [--db <file>]
          deadlockvn approve        [--db <file>] [--categories <list>] --yes

        Options:
          --db <file>          File bản dịch (mặc định workspace/translations/vietnamese.json)
          --split              Dùng bộ bản dịch tách theo nhóm
          --split-dir <dir>    Thư mục chứa bộ bản dịch tách theo nhóm
          --output <file>      File xuất cho review
          --categories <list>  Lọc theo nhóm: heroes,items,abilities,ui,misc,voice
          --limit <n>          Giới hạn số entry
          --all                Bao gồm cả entry đã Approved (mặc định chỉ Reviewed)
          --yes, -y            Xác nhận thao tác ghi

        Quy trình:
          1. deadlockvn auto-translate --categories heroes,items,misc
             -> bản dịch máy ở trạng thái Reviewed
          2. deadlockvn review-export -o workspace/review/abc.json
             -> xuất ra file để người dịch sửa
          3. deadlockvn review-import workspace/review/abc.json
             -> nhập lại các chỉnh sửa, tự động duyệt nếu đặt newStatus
          4. deadlockvn approve --categories ui --yes
             -> duyệt hàng loạt phần còn lại

        Ví dụ:
          deadlockvn review --categories heroes --limit 20
          deadlockvn review-export -o workspace/review/ui.json --categories ui
          deadlockvn review-import workspace/review/ui.json
        """);
    }

    private sealed class Options
    {
        public string? DatabasePath { get; set; }

        public bool Split { get; set; }

        public string? SplitDirectory { get; set; }

        public string? OutputPath { get; set; }

        public string? Categories { get; set; }

        public int Limit { get; set; }

        public bool All { get; set; }

        public bool Yes { get; set; }
    }
}
