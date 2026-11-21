using System.Text;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>install</c>: cài bản dịch vào thư mục game.
/// <para>
/// Có hai chế độ:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Loose</b> (mặc định): chép 11 file <c>*_vietnamese.txt</c> vào
/// <c>&lt;game&gt;/game/citadel/resource/localization/</c>, ghi đè các file
/// stub 58–66 B mà game đã ship sẵn. Đây là cách đơn giản và chắc chắn
/// nhất vì search path <c>Mod citadel</c> được mount sau
/// <c>Game citadel/addons</c>.
/// </description></item>
/// <item><description>
/// <b>VPK</b>: đóng gói thành <c>&lt;name&gt;_dir.vpk</c> rồi chép vào
/// <c>&lt;game&gt;/game/citadel/addons/</c>.
/// </description></item>
/// </list>
/// </summary>
internal static class InstallCommand
{
    private const string DefaultGameDirectory =
        @"E:\Steam\steamapps\common\Deadlock\game";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        var options = ParseOptions(args);

        string gameDirectory = options.GameDirectory ?? DefaultGameDirectory;

        if (!Directory.Exists(gameDirectory))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục game: {gameDirectory}\n" +
                "Dùng --game <đường dẫn tới thư mục 'game'>.");

            return 1;
        }

        string citadelDirectory = Path.Combine(gameDirectory, "citadel");

        if (!Directory.Exists(citadelDirectory))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục 'citadel' trong: {gameDirectory}");

            return 1;
        }

        string? stagingDirectory = options.StagingDirectory
            ?? ResolveDefaultStagingDirectory();

        if (stagingDirectory is null ||
            !Directory.Exists(stagingDirectory))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục staging: {stagingDirectory}\n" +
                "Chạy 'package' trước để tạo.");

            return 1;
        }

        Console.WriteLine("=== CÀI ĐẶT BẢN DỊCH TIẾNG VIỆT ===");
        Console.WriteLine($"Game    : {gameDirectory}");
        Console.WriteLine($"Staging : {stagingDirectory}");
        Console.WriteLine();

        // 1. Vá gameinfo.gi — bắt buộc để game hiện "Tiếng Việt" trong menu.
        bool gameInfoPatched = await PatchGameInfoAsync(
            citadelDirectory,
            options.TargetLanguage);

        // 2. Vá fonts.conf — chỉ khi font chính thiếu glyph tiếng Việt.
        bool fontsPatched = await PatchFontsAsync(
            citadelDirectory,
            options.TargetLanguage);

        // 3. Chép file localization.
        int copied;
        long totalBytes;

        if (options.Mode == InstallMode.Loose)
        {
            (copied, totalBytes) = await CopyLooseFiles(
                stagingDirectory,
                citadelDirectory,
                options.TargetLanguage);
        }
        else
        {
            return await InstallVpkAsync(
                stagingDirectory,
                citadelDirectory,
                options);
        }

        Console.WriteLine();
        Console.WriteLine("=== KẾT QUẢ ===");
        Console.WriteLine($"File localization đã chép : {copied}");
        Console.WriteLine($"Tổng dung lượng            : {totalBytes / 1024.0:F1} KB");
        Console.WriteLine($"gameinfo.gi                : {(gameInfoPatched ? "đã vá" : "không cần vá")}");
        Console.WriteLine($"fonts.conf                 : {(fontsPatched ? "đã vá" : "không cần vá")}");

        if (copied == 0)
        {
            Console.Error.WriteLine(
                "\nKHÔNG chép được file nào — kiểm tra lại thư mục staging.");

            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("=== BƯỚC TIẾP THEO ===");
        Console.WriteLine("1. Mở game Deadlock.");
        Console.WriteLine("2. Vào Settings > Language, chọn 'Tiếng Việt'.");
        Console.WriteLine("3. Khởi động lại game nếu ngôn ngữ chưa đổi.");

        return 0;
    }

    /// <summary>
    /// Chép các file <c>*_vietnamese.txt</c> từ staging vào thư mục
    /// localization của game, ghi đè file stub.
    /// </summary>
    private static async Task<(int Count, long Bytes)> CopyLooseFiles(
        string stagingDirectory,
        string citadelDirectory,
        string targetLanguage)
    {
        long totalBytes = 0;

        string sourceRoot = Path.Combine(
            stagingDirectory,
            "resource",
            "localization");

        if (!Directory.Exists(sourceRoot))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy: {sourceRoot}");

            return (0, 0);
        }

        string targetRoot = Path.Combine(
            citadelDirectory,
            "resource",
            "localization");

        int copied = 0;

        var files = Directory
            .GetFiles(sourceRoot, $"*_{targetLanguage}.txt", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine("--- Chép file localization ---");

        foreach (string source in files)
        {
            string relative = Path.GetRelativePath(sourceRoot, source);

            string destination = Path.Combine(
                targetRoot,
                relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(destination)!);

            // Sao lưu file gốc của game trước lần ghi đè đầu tiên.
            if (File.Exists(destination) && !File.Exists(destination + ".bak"))
            {
                File.Copy(destination, destination + ".bak", overwrite: false);
            }

            byte[] bytes = await File.ReadAllBytesAsync(source);

            await File.WriteAllBytesAsync(destination, bytes);

            totalBytes += bytes.Length;

            copied++;
        }

        return (copied, totalBytes);
    }

    private static async Task<bool> PatchGameInfoAsync(
        string citadelDirectory,
        string targetLanguage)
    {
        string gameInfoPath = Path.Combine(citadelDirectory, "gameinfo.gi");

        if (!File.Exists(gameInfoPath))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy: {gameInfoPath}");

            return false;
        }

        var patcher = new DeadlockVN.Infrastructure.FileSystem.GameConfigPatcher();

        try
        {
            var result = await patcher.PatchSupportedLanguagesAsync(
                gameInfoPath,
                targetLanguage);

            if (result.AlreadyPresent)
            {
                Console.WriteLine(
                    $"gameinfo.gi: ngôn ngữ '{targetLanguage}' đã có sẵn.");

                return false;
            }

            if (result.Changed)
            {
                Console.WriteLine(
                    $"gameinfo.gi: đã chèn {result.InsertedContent}");
                Console.WriteLine($"  sao lưu: {result.BackupPath}");

                return true;
            }

            Console.Error.WriteLine(
                "gameinfo.gi: không tìm thấy khối SupportedLanguages để chèn.");

            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"gameinfo.gi: lỗi - {ex.Message}");

            return false;
        }
    }

    private static async Task<bool> PatchFontsAsync(
        string citadelDirectory,
        string targetLanguage)
    {
        string fontsConfPath = Path.Combine(
            citadelDirectory,
            "panorama",
            "fonts",
            "fonts.conf");

        if (!File.Exists(fontsConfPath))
        {
            Console.Error.WriteLine($"Không tìm thấy: {fontsConfPath}");

            return false;
        }

        string primaryFont = Path.Combine(
            citadelDirectory,
            "panorama",
            "fonts",
            "valveoracle-medium.ttf");

        if (File.Exists(primaryFont) &&
            DeadlockVN.Infrastructure.FileSystem.FontCoverageChecker
                .CoversVietnamese(primaryFont))
        {
            Console.WriteLine(
                "fonts.conf: font chính đã phủ đủ glyph tiếng Việt, không cần vá.");

            return false;
        }

        var patcher = new DeadlockVN.Infrastructure.FileSystem.GameConfigPatcher();

        try
        {
            var config = new DeadlockVN.Core.Entities.FontFallbackConfig
            {
                LanguageCode = "vi-vn"
            };

            var result = await patcher.PatchFontFallbackAsync(
                fontsConfPath,
                config);

            if (result.AlreadyPresent)
            {
                Console.WriteLine("fonts.conf: khối fallback đã có sẵn.");

                return false;
            }

            if (result.Changed)
            {
                Console.WriteLine("fonts.conf: đã thêm khối fallback 'Noto Sans JP'.");
                Console.WriteLine($"  sao lưu: {result.BackupPath}");

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"fonts.conf: lỗi - {ex.Message}");

            return false;
        }
    }

    private static async Task<int> InstallVpkAsync(
        string stagingDirectory,
        string citadelDirectory,
        Options options)
    {
        string? vpkEditCli = FindVpkEditCli();

        if (vpkEditCli is null)
        {
            Console.Error.WriteLine(
                "Không tìm thấy vpkeditcli.exe — dùng chế độ loose.");

            return 1;
        }

        string addonsDirectory = Path.Combine(citadelDirectory, "addons");

        Directory.CreateDirectory(addonsDirectory);

        string baseName = options.VpkName ?? "deadlockvn";
        string outputVpk = Path.Combine(
            addonsDirectory,
            $"{baseName}_dir.vpk");

        Console.WriteLine($"--- Đóng gói VPK vào {addonsDirectory} ---");

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = vpkEditCli,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in new[]
        {
            "--no-progress",
            "--type", "vpk",
            "--version", "2",
            "--chunksize", "200",
            "--output", outputVpk,
            stagingDirectory
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)!;

        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || !File.Exists(outputVpk))
        {
            Console.Error.WriteLine("Đóng gói VPK thất bại:");
            Console.Error.WriteLine(output.ToString());

            return 1;
        }

        Console.WriteLine($"Đã tạo: {outputVpk}");

        return 0;
    }

    private static string? FindVpkEditCli()
    {
        foreach (string candidate in new[]
        {
            @"C:\Program Files\VPKEdit\vpkeditcli.exe",
            @"C:\Program Files (x86)\VPKEdit\vpkeditcli.exe"
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveDefaultStagingDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "staging",
                "mod");

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
            switch (args[i])
            {
                case "--game":
                case "-g":
                    if (i + 1 < args.Length)
                    {
                        options.GameDirectory = args[++i];
                    }

                    break;

                case "--staging":
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        options.StagingDirectory = args[++i];
                    }

                    break;

                case "--language":
                case "-l":
                    if (i + 1 < args.Length)
                    {
                        options.TargetLanguage = args[++i];
                    }

                    break;

                case "--vpk":
                    options.Mode = InstallMode.Vpk;
                    break;

                case "--name":
                case "-n":
                    if (i + 1 < args.Length)
                    {
                        options.VpkName = args[++i];
                    }

                    break;

                default:
                    break;
            }
        }

        return options;
    }

    private enum InstallMode
    {
        Loose,
        Vpk
    }

    private sealed class Options
    {
        public string? GameDirectory { get; set; }

        public string? StagingDirectory { get; set; }

        public string TargetLanguage { get; set; } = "vietnamese";

        public InstallMode Mode { get; set; } = InstallMode.Loose;

        public string? VpkName { get; set; }
    }
}
