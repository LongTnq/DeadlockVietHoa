using DeadlockVN.Application.DTOs;
using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Infrastructure.Exporting;
using DeadlockVN.Infrastructure.FileSystem;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>package</c>: đóng gói bộ bản dịch thành bộ cài đặt cho mod,
/// sẵn sàng đóng gói VPK bằng VPKEdit.
/// </summary>
internal static class PackageCommand
{
    private const string DefaultOriginalDirectory = "workspace/source/original";

    private const string DefaultTranslationsDirectory = "workspace/translations";

    private const string DefaultOutputDirectory = "workspace/staging/mod";

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

        string translationsDirectory =
            options.TranslationsDirectory ?? DefaultTranslationsDirectory;

        if (!Directory.Exists(translationsDirectory))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục bản dịch: {translationsDirectory}\n" +
                "Chạy 'export --split' trước để tạo.");

            return 1;
        }

        string outputDirectory = options.OutputDirectory ?? DefaultOutputDirectory;

        Console.WriteLine("=== PACKAGE: đóng gói bộ cài đặt cho mod ===");
        Console.WriteLine($"Nguồn       : {sourceDirectory}");
        Console.WriteLine($"Bản dịch    : {translationsDirectory}");
        Console.WriteLine($"Đích        : {outputDirectory}");

        if (options.GameDirectory is not null)
        {
            Console.WriteLine($"Game dir    : {options.GameDirectory}");
        }

        Console.WriteLine();

        var config = new ModPackageConfig
        {
            SourceDirectory = sourceDirectory,
            TranslationsDirectory = translationsDirectory,
            OutputDirectory = outputDirectory,
            GameDirectory = options.GameDirectory ?? string.Empty,
            TargetLanguage = options.TargetLanguage,
            Split = options.Split,
            PatchGameInfo = options.PatchGameInfo && options.GameDirectory is not null,
            PatchFonts = options.PatchFonts && options.GameDirectory is not null
        };

        var service = new ModPackageService(
            new JsonTranslationStore(),
            new LocalizationExporter(),
            new GameConfigPatcher());

        ModPackageResult result = await service.BuildAsync(config);

        Console.WriteLine($"File localization : {result.LocalizationFileCount}");
        Console.WriteLine($"Tổng entry        : {result.TotalEntries}");
        Console.WriteLine($"Đã dịch           : {result.TranslatedEntries}");
        Console.WriteLine();

        if (result.GameInfoPatch is not null)
        {
            PrintPatch("gameinfo.gi", result.GameInfoPatch);
        }

        if (result.FontsPatch is not null)
        {
            PrintPatch("fonts.conf", result.FontsPatch);
        }

        if (result.DeficientFontFamilies.Count > 0)
        {
            Console.WriteLine(
                "[font] Họ font thiếu glyph tiếng Việt (đã thêm fallback):");

            foreach (string family in result.DeficientFontFamilies)
            {
                Console.WriteLine($"       - {family}");
            }

            Console.WriteLine();
        }

        if (result.UnknownKeys.Count > 0)
        {
            Console.Error.WriteLine(
                $"[CẢNH BÁO] {result.UnknownKeys.Count} key trong bản dịch " +
                "không tồn tại trong file gốc:");

            foreach (string key in result.UnknownKeys.Take(20))
            {
                Console.Error.WriteLine($"  {key}");
            }

            Console.Error.WriteLine();

            return 1;
        }

        PrintVpkInstructions(outputDirectory);

        return 0;
    }

    private static void PrintPatch(string name, ConfigPatchResult patch)
    {
        if (patch.AlreadyPresent)
        {
            Console.WriteLine($"[{name}] Đã có sẵn — không cần vá.");
        }
        else if (patch.Changed)
        {
            Console.WriteLine($"[{name}] ĐÃ VÁ: {patch.FilePath}");

            if (patch.BackupPath is not null)
            {
                Console.WriteLine($"         Bản sao lưu: {patch.BackupPath}");
            }

            if (patch.InsertedContent is not null)
            {
                Console.WriteLine(
                    $"         Đã thêm  : {patch.InsertedContent.Trim()}");
            }
        }
        else
        {
            Console.WriteLine($"[{name}] Không tìm thấy khối cần vá.");
        }

        Console.WriteLine();
    }

    private static void PrintVpkInstructions(string outputDirectory)
    {
        Console.WriteLine("=== HƯỚNG DẪN ĐÓNG GÓI VPK ===");
        Console.WriteLine();
        Console.WriteLine("Bước 1 — Mở VPKEdit, tạo VPK mới:");
        Console.WriteLine("        File > Create New VPK");
        Console.WriteLine();
        Console.WriteLine("Bước 2 — Thêm thư mục localization (giữ nguyên cấu trúc):");
        Console.WriteLine($"        {Path.Combine(outputDirectory, "citadel_main")}");
        Console.WriteLine($"        {Path.Combine(outputDirectory, "citadel_heroes")}");
        Console.WriteLine($"        ... (tất cả {11} thư mục con)");
        Console.WriteLine();
        Console.WriteLine("        ⚠ Đường dẫn trong VPK phải là:");
        Console.WriteLine("          resource/localization/citadel_main/citadel_main_vietnamese.txt");
        Console.WriteLine();
        Console.WriteLine("Bước 3 — Lưu VPK vào:");
        Console.WriteLine("          <game>/game/citadel/addons/");
        Console.WriteLine();
        Console.WriteLine("Bước 4 — Khởi động lại game, chọn ngôn ngữ Tiếng Việt.");
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
                        options.OutputDirectory = args[++i];
                    }

                    break;

                case "--translations":
                case "-t":
                    if (i + 1 < args.Length)
                    {
                        options.TranslationsDirectory = args[++i];
                    }

                    break;

                case "--game":
                case "-g":
                    if (i + 1 < args.Length)
                    {
                        options.GameDirectory = args[++i];
                    }

                    break;

                case "--language":
                case "-l":
                    if (i + 1 < args.Length)
                    {
                        options.TargetLanguage = args[++i];
                    }

                    break;

                case "--no-patch-gameinfo":
                    options.PatchGameInfo = false;
                    break;

                case "--no-patch-fonts":
                    options.PatchFonts = false;
                    break;

                case "--split":
                    options.Split = true;
                    break;

                default:
                    break;
            }
        }

        return options;
    }

    private sealed class Options
    {
        public string? SourceDirectory { get; set; }

        public string? OutputDirectory { get; set; }

        public string? TranslationsDirectory { get; set; }

        public string? GameDirectory { get; set; }

        public string TargetLanguage { get; set; } = "vietnamese";

        public bool PatchGameInfo { get; set; } = true;

        public bool PatchFonts { get; set; } = true;

        public bool Split { get; set; } = true;
    }
}
