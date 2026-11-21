using DeadlockVN.Application.DTOs;
using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Các lệnh quản lý bản dịch: export, import, sync, report.
/// </summary>
internal static class TranslationCommand
{
    private const string DefaultOriginalDirectory = "workspace/source/original";

    private const string DefaultTranslationsFile = "workspace/translations/vietnamese.json";

    private const string DefaultTranslationsDirectory = "workspace/translations";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();

            return 1;
        }

        string subCommand = args[0].ToLowerInvariant();

        var options = ParseOptions(args[1..]);

        switch (subCommand)
        {
            case "export":
            case "create":
                return await ExportAsync(options);

            case "import":
                return await ImportAsync(options);

            case "sync":
                return await SyncAsync(options);

            case "report":
                return await ReportAsync(options);

            default:
                Console.Error.WriteLine($"Unknown sub-command: {subCommand}");

                PrintUsage();

                return 1;
        }
    }

    // ---------------- export / create ----------------

    private static async Task<int> ExportAsync(CommandOptions options)
    {
        string? sourceDirectory = options.SourceDirectory
            ?? ResolveDefaultOriginalDirectory();

        if (sourceDirectory is null)
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục '{DefaultOriginalDirectory}'. " +
                "Dùng --source <dir> để chỉ định.");

            return 1;
        }

        var service = CreateService();

        Console.WriteLine("=== EXPORT: tạo bộ bản dịch từ bản gốc ===");
        Console.WriteLine($"Nguồn : {sourceDirectory}");
        Console.WriteLine();

        // Chế độ tách theo nhóm (mặc định): mỗi nhóm một file trong thư mục con.
        if (options.Split)
        {
            string rootDirectory = options.OutputPath ?? DefaultTranslationsDirectory;

            Console.WriteLine($"Đích  : {rootDirectory} (tách theo nhóm)");
            Console.WriteLine();

            var splitResult = await service.CreateSplitAsync(sourceDirectory, rootDirectory);

            Console.WriteLine($"Entry đã xuất  : {splitResult.ExportedCount}");
            Console.WriteLine($"Đã có bản dịch: {splitResult.TranslatedCount}");
            Console.WriteLine();
            Console.WriteLine($"{"Nhóm",-12} {"File",-28} {"Kích thước",12}");
            Console.WriteLine(new string('-', 56));

            foreach (var file in splitResult.Files.OrderByDescending(f => f.SizeBytes))
            {
                Console.WriteLine(
                    $"{file.Category,-12} {file.FileName,-28} " +
                    $"{FormatSize(file.SizeBytes),12}");
            }

            Console.WriteLine();
            Console.WriteLine($"Tổng file: {splitResult.Files.Count}");

            return 0;
        }

        string outputPath = options.OutputPath ?? DefaultTranslationsFile;

        Console.WriteLine($"Đích  : {outputPath}");
        Console.WriteLine();

        ExportResult result = await service.CreateAsync(sourceDirectory, outputPath);

        PrintExportResult(result);

        return 0;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / 1024.0 / 1024.0:F2} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes} B";
    }

    private static void PrintExportResult(ExportResult result)
    {
        Console.WriteLine($"Entry đã xuất : {result.ExportedCount}");
        Console.WriteLine($"Đã có bản dịch: {result.TranslatedCount}");
        Console.WriteLine($"File          : {result.OutputPath}");
        Console.WriteLine();

        if (result.ByCategory.Count > 0)
        {
            Console.WriteLine("Phân bố theo nhóm:");

            foreach (var category in result.ByCategory.OrderByDescending(c => c.Value))
            {
                Console.WriteLine($"  {category.Key,-10} {category.Value}");
            }

            Console.WriteLine();
        }
    }

    // ---------------- import ----------------

    private static async Task<int> ImportAsync(CommandOptions options)
    {
        if (options.InputPath is null)
        {
            Console.Error.WriteLine(
                "Thiếu file bản dịch nguồn. Dùng: import <file> [--target <file>] [--overwrite]");

            return 1;
        }

        var service = CreateService();

        Console.WriteLine("=== IMPORT: nạp bản dịch ===");
        Console.WriteLine($"Nguồn : {options.InputPath}");
        Console.WriteLine();

        ImportResult result;

        if (options.Split)
        {
            string targetRoot = options.OutputPath ?? DefaultTranslationsDirectory;

            Console.WriteLine($"Đích  : {targetRoot} (tách theo nhóm)");
            Console.WriteLine();

            result = await service.ImportSplitAsync(
                targetRoot,
                options.InputPath,
                options.Overwrite);
        }
        else
        {
            string targetPath = options.OutputPath ?? DefaultTranslationsFile;

            Console.WriteLine($"Đích  : {targetPath}");
            Console.WriteLine();

            result = await service.ImportAsync(
                targetPath,
                options.InputPath,
                options.Overwrite);
        }

        Console.WriteLine($"Entry trong file nguồn : {result.SourceCount}");
        Console.WriteLine($"Thêm mới               : {result.AddedCount}");
        Console.WriteLine($"Cập nhật              : {result.UpdatedCount}");
        Console.WriteLine($"Bỏ qua                : {result.SkippedCount}");
        Console.WriteLine();

        if (result.Skipped.Count > 0)
        {
            Console.WriteLine("Chi tiết các entry bị bỏ qua (tối đa 20):");

            foreach (string skipped in result.Skipped.Take(20))
            {
                Console.WriteLine($"  {skipped}");
            }

            if (result.Skipped.Count > 20)
            {
                Console.WriteLine($"  ... và {result.Skipped.Count - 20} entry khác.");
            }
        }

        return 0;
    }

    // ---------------- sync ----------------

    private static async Task<int> SyncAsync(CommandOptions options)
    {
        string? sourceDirectory = options.SourceDirectory
            ?? ResolveDefaultOriginalDirectory();

        if (sourceDirectory is null)
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục '{DefaultOriginalDirectory}'. " +
                "Dùng --source <dir> để chỉ định.");

            return 1;
        }

        var service = CreateService();

        Console.WriteLine("=== SYNC: đồng bộ với bản gốc mới ===");
        Console.WriteLine($"Nguồn : {sourceDirectory}");
        Console.WriteLine();

        TranslationSyncResult result;

        if (options.Split)
        {
            string targetRoot = options.OutputPath ?? DefaultTranslationsDirectory;

            Console.WriteLine($"Đích  : {targetRoot} (tách theo nhóm)");
            Console.WriteLine();

            result = await service.SyncSplitAsync(targetRoot, sourceDirectory);
        }
        else
        {
            string translationsPath = options.OutputPath ?? DefaultTranslationsFile;

            Console.WriteLine($"Đích  : {translationsPath}");
            Console.WriteLine();

            result = await service.SyncAsync(translationsPath, sourceDirectory);
        }

        Console.WriteLine($"Tổng entry sau sync : {result.TotalEntries}");
        Console.WriteLine($"Key mới             : {result.Added.Count}");
        Console.WriteLine($"Nội dung gốc đổi   : {result.Modified.Count}");
        Console.WriteLine($"Key đã bị xoá       : {result.Removed.Count}");
        Console.WriteLine();

        if (result.Added.Count > 0)
        {
            Console.WriteLine("Key mới (tối đa 20):");

            foreach (var change in result.Added.Take(20))
            {
                Console.WriteLine($"  + {change.Id}");
                Console.WriteLine($"      {Truncate(change.NewOriginalText ?? string.Empty, 90)}");
            }

            if (result.Added.Count > 20)
            {
                Console.WriteLine($"  ... và {result.Added.Count - 20} key khác.");
            }

            Console.WriteLine();
        }

        if (result.Modified.Count > 0)
        {
            Console.WriteLine("Nội dung gốc đã thay đổi — cần review lại (tối đa 20):");

            foreach (var change in result.Modified.Take(20))
            {
                Console.WriteLine($"  ~ {change.Id}");
                Console.WriteLine($"      cũ: {Truncate(change.OldOriginalText ?? string.Empty, 80)}");
                Console.WriteLine($"      mới: {Truncate(change.NewOriginalText ?? string.Empty, 80)}");
            }

            if (result.Modified.Count > 20)
            {
                Console.WriteLine($"  ... và {result.Modified.Count - 20} key khác.");
            }

            Console.WriteLine();
        }

        if (result.Removed.Count > 0)
        {
            Console.WriteLine("Key đã bị xoá khỏi game (tối đa 20):");

            foreach (var change in result.Removed.Take(20))
            {
                Console.WriteLine($"  - {change.Id}");
            }

            if (result.Removed.Count > 20)
            {
                Console.WriteLine($"  ... và {result.Removed.Count - 20} key khác.");
            }

            Console.WriteLine();
        }

        return 0;
    }

    // ---------------- report ----------------

    private static async Task<int> ReportAsync(CommandOptions options)
    {
        var service = CreateService();

        TranslationReport report;
        string displayPath;

        if (options.Split)
        {
            string rootDirectory = options.InputPath ?? DefaultTranslationsDirectory;

            if (!Directory.Exists(rootDirectory))
            {
                Console.Error.WriteLine(
                    $"Không tìm thấy thư mục bản dịch: {rootDirectory}\n" +
                    "Chạy 'export' trước để tạo.");

                return 1;
            }

            displayPath = rootDirectory;
            report = await service.BuildSplitReportAsync(rootDirectory);
        }
        else
        {
            string translationsPath = options.InputPath ?? DefaultTranslationsFile;

            if (!File.Exists(translationsPath))
            {
                Console.Error.WriteLine(
                    $"Không tìm thấy file bản dịch: {translationsPath}\n" +
                    "Chạy 'export' trước để tạo.");

                return 1;
            }

            displayPath = translationsPath;
            report = await service.BuildReportAsync(translationsPath);
        }

        Console.WriteLine("=== BÁO CÁO TIẾN ĐỘ VIỆT HÓA ===");
        Console.WriteLine($"File: {displayPath}");
        Console.WriteLine();
        Console.WriteLine($"Tổng entry    : {report.Total}");
        Console.WriteLine($"Đã có bản dịch: {report.Translated} ({report.TranslatedPercent:F2}%)");
        Console.WriteLine($"Đã được duyệt : {report.Approved} ({report.ApprovedPercent:F2}%)");
        Console.WriteLine($"Chờ dịch      : {report.Pending}");
        Console.WriteLine($"Đang dịch     : {report.Translating}");
        Console.WriteLine($"Chờ review    : {report.Reviewed}");
        Console.WriteLine($"Cần review lại: {report.Stale}");
        Console.WriteLine();

        Console.WriteLine($"{"Nhóm",-12} {"Tổng",8} {"Đã dịch",10} {"Đã duyệt",10}  Tiến độ");
        Console.WriteLine(new string('-', 62));

        foreach (var category in report.Categories.OrderByDescending(c => c.Total))
        {
            string bar = BuildProgressBar(category.TranslatedPercent);

            Console.WriteLine(
                $"{category.Name,-12} {category.Total,8} {category.Translated,10} " +
                $"{category.Approved,10}  {bar} {category.TranslatedPercent:F1}%");
        }

        return 0;
    }

    private static string BuildProgressBar(double percent, int width = 20)
    {
        int filled = (int)Math.Round(percent / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);

        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }

    // ---------------- helpers ----------------

    private static TranslationManagementService CreateService()
    {
        var classifier = new LocalizationClassifier();
        var reader = new LocalizationSourceReader(classifier);
        var store = new JsonTranslationStore();

        return new TranslationManagementService(store, reader);
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

    private static string Truncate(string value, int maxLength)
    {
        string singleLine = value.ReplaceLineEndings(" ");

        return singleLine.Length <= maxLength
            ? singleLine
            : singleLine[..maxLength] + "...";
    }

    private static CommandOptions ParseOptions(string[] args)
    {
        var options = new CommandOptions();

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

                case "--target":
                case "-t":
                    if (i + 1 < args.Length)
                    {
                        options.OutputPath = args[++i];
                    }

                    break;

                case "--overwrite":
                    options.Overwrite = true;

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

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Quản lý bản dịch:

          export  [--source <dir>] [--output <dir-or-file>] [--split]
                  Tạo bộ bản dịch mới từ bản gốc tiếng Anh.

          import  <file-or-dir> [--target <file-or-dir>] [--overwrite] [--split]
                  Nạp bản dịch từ file/thư mục JSON khác vào bộ bản dịch hiện tại.
                  Bản dịch đã có sẽ được giữ nguyên trừ khi có --overwrite.

          sync    [--source <dir>] [--output <file-or-dir>] [--split]
                  Đồng bộ bộ bản dịch với bản gốc mới từ game.

          report  [<file-or-dir>] [--split]
                  Báo cáo tiến độ Việt hóa.

        Chế độ tách theo nhóm (--split):
          Mỗi nhóm nội dung nằm ở một thư mục con:
            workspace/translations/items/items.json
            workspace/translations/heroes/heroes.json
            workspace/translations/abilities/abilities.json
            workspace/translations/ui/ui.json
            workspace/translations/voice/voice.json
            workspace/translations/misc/misc.json

        Mặc định:
          source : workspace/source/original
          target : workspace/translations/vietnamese.json
        """);
    }

    private sealed class CommandOptions
    {
        public string? InputPath { get; set; }

        public string? OutputPath { get; set; }

        public string? SourceDirectory { get; set; }

        public bool Overwrite { get; set; }

        public bool Split { get; set; }
    }
}
