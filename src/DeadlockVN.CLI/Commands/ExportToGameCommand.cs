using DeadlockVN.Application.DTOs;
using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Exporting;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>export-to-game</c>: xuất bộ bản dịch ra thư mục staging theo đúng
/// cấu trúc file của game, sẵn sàng đóng gói VPK.
/// </summary>
internal static class ExportToGameCommand
{
    private const string DefaultOriginalDirectory = "workspace/source/original";

    private const string DefaultTranslationsFile = "workspace/translations/vietnamese.json";

    private const string DefaultTranslationsDirectory = "workspace/translations";

    private const string DefaultOutputDirectory = "workspace/staging/localization";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        var options = ParseOptions(args);

        string? sourceDirectory = options.SourceDirectory
            ?? ResolveDefaultOriginalDirectory();

        if (sourceDirectory is null)
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục '{DefaultOriginalDirectory}'. " +
                "Dùng --source <dir> để chỉ định.");

            return 1;
        }

        string outputDirectory = options.OutputPath ?? DefaultOutputDirectory;

        Console.WriteLine("=== EXPORT-TO-GAME: xuất bản dịch ra thư mục staging ===");
        Console.WriteLine($"Nguồn : {sourceDirectory}");
        Console.WriteLine($"Đích  : {outputDirectory}");
        Console.WriteLine();

        var service = new GameLocalizationExportService(
            new JsonTranslationStore(),
            new LocalizationExporter());

        GameExportResult result;

        if (options.Split)
        {
            string translationsDirectory =
                options.InputPath ?? DefaultTranslationsDirectory;

            result = await service.ExportSplitAsync(
                sourceDirectory,
                translationsDirectory,
                outputDirectory,
                options.TargetLanguage);
        }
        else
        {
            string translationsPath =
                options.InputPath ?? DefaultTranslationsFile;

            if (!File.Exists(translationsPath))
            {
                Console.Error.WriteLine(
                    $"Không tìm thấy file bản dịch: {translationsPath}\n" +
                    "Chạy 'export' trước để tạo.");

                return 1;
            }

            result = await service.ExportAsync(
                sourceDirectory,
                translationsPath,
                outputDirectory,
                options.TargetLanguage);
        }

        Console.WriteLine($"{"File",-56} {"Entry",8} {"Đã dịch",10}");
        Console.WriteLine(new string('-', 78));

        foreach (var file in result.Files)
        {
            Console.WriteLine(
                $"{file.OutputFile,-56} {file.TotalEntries,8} {file.TranslatedEntries,10}");
        }

        Console.WriteLine();
        Console.WriteLine($"Tổng file      : {result.FileCount}");
        Console.WriteLine($"Tổng entry     : {result.TotalEntries}");
        Console.WriteLine($"Đã dịch        : {result.TranslatedEntries}");
        Console.WriteLine($"Chưa dịch      : {result.UntranslatedEntries}");
        Console.WriteLine($"Ngôn ngữ đích  : {result.TargetLanguage}");
        Console.WriteLine();

        if (result.UnknownKeys.Count > 0)
        {
            Console.Error.WriteLine(
                $"[CẢNH BÁO] {result.UnknownKeys.Count} key trong bản dịch " +
                "không tồn tại trong file gốc:");

            foreach (string key in result.UnknownKeys.Take(20))
            {
                Console.Error.WriteLine($"  {key}");
            }

            if (result.UnknownKeys.Count > 20)
            {
                Console.Error.WriteLine(
                    $"  ... và {result.UnknownKeys.Count - 20} key khác.");
            }

            Console.Error.WriteLine();

            return 1;
        }

        Console.WriteLine(
            "Bước tiếp theo: đóng gói thư mục staging bằng VPKEdit " +
            "(Milestone 6).");

        return 0;
    }

    private static string? ResolveDefaultOriginalDirectory()
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

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            switch (arg)
            {
                case "--source":
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        options.SourceDirectory = args[++i];
                    }

                    break;

                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        options.OutputPath = args[++i];
                    }

                    break;

                case "--translations":
                case "-t":
                    if (i + 1 < args.Length)
                    {
                        options.InputPath = args[++i];
                    }

                    break;

                case "--language":
                case "-l":
                    if (i + 1 < args.Length)
                    {
                        options.TargetLanguage = args[++i];
                    }

                    break;

                case "--split":
                    options.Split = true;
                    break;

                default:
                    if (!arg.StartsWith("--", StringComparison.Ordinal) &&
                        options.InputPath is null)
                    {
                        options.InputPath = arg;
                    }

                    break;
            }
        }

        return options;
    }

    private sealed class Options
    {
        public string? InputPath { get; set; }

        public string? OutputPath { get; set; }

        public string? SourceDirectory { get; set; }

        public string TargetLanguage { get; set; } = "vietnamese";

        public bool Split { get; set; }
    }
}
