using System.Text;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;
using DeadlockVN.Infrastructure.Parsers.KeyValues;
using DeadlockVN.Infrastructure.Parsers.KeyValues.Models;
using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>import-from-staging</c>: đọc lại các file <c>*_vietnamese.txt</c>
/// đã export ra staging, so sánh với cơ sở dữ liệu bản dịch và cập nhật
/// những bản dịch đã được sửa tay.
/// <para>
/// <b>Vì sao cần:</b> file KeyValues (KeyValues) rất dễ sửa tay — mỗi
/// entry một dòng, thấy ngay giá trị tiếng Việt. Nhưng nếu sửa trực
/// tiếp file staging rồi chạy lại <c>export-to-game</c> thì toàn bộ thay
/// đổi sẽ bị ghi đè. Lệnh này giải quyết đúng vấn đề đó: đọc ngược
/// từ staging về database.
/// </para>
/// <para>
/// <b>Quy trình đầy đủ:</b>
/// <list type="number">
///   <item><description>Sửa file <c>*_vietnamese.txt</c> trong staging.</description></item>
///   <item><description>Chạy <c>import-from-staging</c> để đồng bộ về DB.</description></item>
///   <item><description>Chạy <c>package</c> + <c>deploy_lang.py</c> để đưa vào game.</description></item>
/// </list>
/// </para>
/// </summary>
internal static class ImportFromStagingCommand
{
    private const string DefaultStaging = "workspace/staging/localization";

    private const string DefaultDatabase = "workspace/translations/vietnamese.json";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        var options = ParseOptions(args);

        if (options.ShowHelp)
        {
            PrintUsage();

            return 0;
        }

        string stagingDirectory = options.Staging ?? ResolveDefaultStaging();
        string databasePath = options.DatabasePath ?? ResolveDefaultDatabase();

        if (!Directory.Exists(stagingDirectory))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục staging: {stagingDirectory}\n" +
                "Chạy 'deadlockvn export-to-game' trước để tạo.");

            return 1;
        }

        if (!File.Exists(databasePath))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy file bản dịch: {databasePath}");

            return 1;
        }

        Console.WriteLine("=== IMPORT TỪ STAGING: đọc lại bản dịch đã sửa tay ===");
        Console.WriteLine($"Staging   : {stagingDirectory}");
        Console.WriteLine($"Cơ sở dữ liệu: {databasePath}");
        Console.WriteLine();

        var store = new JsonTranslationStore();

        var document = await store.LoadAsync(databasePath);

        // Nhóm entry theo file nguồn.
        var entriesByFile = document.Entries
            .GroupBy(entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var parser = new KeyValuesParser();

        int updated = 0;
        int unchanged = 0;
        int missing = 0;
        int invalid = 0;

        var problems = new List<string>();
        var changedFiles = new List<string>();

        var files = Directory
            .GetFiles(
                stagingDirectory,
                "*_vietnamese.txt",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string file in files)
        {
            string relativePath = Path
                .GetRelativePath(stagingDirectory, file)
                .Replace('\\', '/');

            // "citadel_main_vietnamese.txt" -> "citadel_main_english.txt"
            string sourceFile = relativePath.Replace(
                "_vietnamese.txt",
                "_english.txt",
                StringComparison.OrdinalIgnoreCase);

            if (!entriesByFile.TryGetValue(sourceFile, out var entries))
            {
                Console.WriteLine(
                    $"  BỎ QUA (không có trong DB): {relativePath}");

                missing++;

                continue;
            }

            string source = await File.ReadAllTextAsync(file);

            KeyValuesDocument parsed;

            try
            {
                parsed = parser.Parse(source);
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine(
                    $"  LỖI cú pháp trong {relativePath}: {ex.Message}");

                invalid++;

                continue;
            }

            var staged = new Dictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var entry in parsed.EnumerateEntries())
            {
                staged[entry.Key] = entry.Value;
            }

            int fileUpdated = 0;

            foreach (TranslationEntry entry in entries)
            {
                // Chỉ xét entry ĐÃ CÓ bản dịch trong DB. Entry chưa dịch
                // (đặc biệt toàn bộ nhóm Voice — 78.438 entry giữ nguyên
                // tiếng Anh trong file staging) không phải thay đổi, và
                // nếu đưa qua guard thì sẽ bị từ chối hàng loạt.
                if (string.IsNullOrEmpty(entry.TranslatedText))
                {
                    continue;
                }

                if (!staged.TryGetValue(entry.Key, out string? value) ||
                    value is null)
                {
                    continue;
                }

                if (string.Equals(
                        entry.TranslatedText,
                        value,
                        StringComparison.Ordinal))
                {
                    unchanged++;

                    continue;
                }

                // Kiểm tra chất lượng bản dịch vừa sửa.
                var check = new TranslationGuard().Check(entry.OriginalText, value);

                if (check.Errors.Count > 0)
                {
                    invalid++;

                    problems.Add(
                        $"{relativePath}#{entry.Key}: " +
                        string.Join(" ", check.Errors));

                    continue;
                }

                entry.TranslatedText = value;
                entry.Status = TranslationStatus.Approved;

                updated++;
                fileUpdated++;
            }

            if (fileUpdated > 0)
            {
                changedFiles.Add($"{relativePath} ({fileUpdated} entry)");
                Console.WriteLine(
                    $"  CẬP NHẬT {relativePath}: {fileUpdated} entry");
            }
        }

        Console.WriteLine();

        if (updated > 0)
        {
            await store.SaveAsync(document, databasePath);
        }

        Console.WriteLine("=== KẾT QUẢ ===");
        Console.WriteLine($"Đã cập nhật  : {updated}");
        Console.WriteLine($"Không đổi : {unchanged}");
        Console.WriteLine($"Bị từ chối (lỗi chất lượng): {invalid}");
        Console.WriteLine($"Bỏ qua (file lạ)  : {missing}");

        if (problems.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("=== BẢN DỊCH BỊ TỪ CHỐI ===");

            foreach (string problem in problems.Take(30))
            {
                Console.WriteLine("  " + problem);
            }

            if (problems.Count > 30)
            {
                Console.WriteLine($"  ... và {problems.Count - 30} vấn đề khác.");
            }
        }

        if (updated > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Bước tiếp theo — đóng gói và đưa vào game:");
            Console.WriteLine(
                "  dotnet run --project src/DeadlockVN.CLI -- package");
            Console.WriteLine(
                "  python tools/deploy_lang.py");
        }

        return invalid > 0 ? 2 : 0;
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--staging":
                    if (i + 1 < args.Length)
                    {
                        options.Staging = args[++i];
                    }

                    break;

                case "--db":
                case "--database":
                    if (i + 1 < args.Length)
                    {
                        options.DatabasePath = args[++i];
                    }

                    break;

                case "-h":
                case "--help":
                    options.ShowHelp = true;

                    break;

                default:
                    break;
            }
        }

        return options;
    }

    private static string ResolveDefaultStaging()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "staging",
                "localization");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return DefaultStaging;
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

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Đọc lại bản dịch đã sửa tay từ các file *_vietnamese.txt trong
        staging và cập nhật ngược vào cơ sở dữ liệu.

        Usage:
          deadlockvn import-from-staging [options]

        Options:
          --staging <dir>   Thư mục staging (mặc định workspace/staging/localization)
          --db <file>       File bản dịch (mặc định workspace/translations/vietnamese.json)
          -h, --help        Hiện trợ giúp

        Ví dụ:
          # 1. Sửa file .txt trong staging/localization bằng trình soạn thảo
          # 2. Đồng bộ ngược vào database
          deadlockvn import-from-staging

          # 3. Đóng gói lại và cài vào game
          deadlockvn package
          python tools/deploy_lang.py
        """);
    }

    private sealed class Options
    {
        public string? Staging { get; set; }

        public string? DatabasePath { get; set; }

        public bool ShowHelp { get; set; }
    }
}
