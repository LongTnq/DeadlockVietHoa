using System.Diagnostics;
using System.Text;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>pack-vpk</c>: đóng gói thư mục staging thành file VPK bằng
/// <c>vpkeditcli.exe</c> (công cụ dòng lệnh đi kèm VPKEdit).
/// </summary>
internal static class PackVpkCommand
{
    private const string DefaultVpkEditCli =
        @"C:\Program Files\VPKEdit\vpkeditcli.exe";

    private const string DefaultStagingDirectory = "workspace/staging/mod";

    private const string DefaultOutputDirectory = "workspace/staging/vpk";

    /// <summary>
    /// Tên file VPK theo quy ước của game: file chỉ mục phải kết thúc bằng
    /// <c>_dir.vpk</c> (ví dụ <c>pak01_dir.vpk</c>). VPKEdit tự sinh file
    /// chunk <c>&lt;tên&gt;_000.vpk</c> khi đóng gói nhiều chunk.
    /// </summary>
    private const string DefaultVpkFileName = "deadlockvn_dir.vpk";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        var options = ParseOptions(args);

        string? stagingDirectory = options.StagingDirectory
            ?? ResolveDefaultStagingDirectory();

        if (stagingDirectory is null || !Directory.Exists(stagingDirectory))
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục staging: {stagingDirectory}\n" +
                "Chạy 'package' trước để tạo.");

            return 1;
        }

        // VPK chỉ nên chứa "resource/localization/...". Thư mục gốc đóng gói
        // được dựng tạm chỉ chứa thư mục "resource" (copy từ staging) để:
        //   1) đường dẫn bên trong VPK là resource/localization/... đúng như
        //      đường dẫn mà game yêu cầu;
        //   2) không đóng gói bản sao fonts.conf (file này phải được vá tại
        //      chỗ trong thư mục game, đóng gói kèm sẽ ghi đè bản game).
        string resourceSource = Path.Combine(stagingDirectory, "resource");

        if (!Directory.Exists(resourceSource))
        {
            Console.Error.WriteLine(
                $"Thư mục staging phải chứa thư mục 'resource': {resourceSource}");

            return 1;
        }

        string? vpkEditCli = options.VpkEditCliPath
            ?? FindVpkEditCli();

        if (vpkEditCli is null)
        {
            Console.Error.WriteLine(
                "Không tìm thấy vpkeditcli.exe.\n" +
                "Cài VPKEdit (https://github.com/WiseMan/vpkedit) hoặc dùng " +
                "--vpkedit <đường dẫn>.");

            return 1;
        }

        string outputDirectory = options.OutputDirectory
            ?? ResolveDefaultOutputDirectory();

        Directory.CreateDirectory(outputDirectory);

        string outputVpk = Path.Combine(
            outputDirectory,
            options.VpkFileName ?? DefaultVpkFileName);

        string packRoot = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn_pack_" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            Directory.CreateDirectory(packRoot);

            // Chỉ đóng gói file .txt — đây là những gì game thực sự đọc.
            // File JSON bên trong staging là dữ liệu nội bộ của công cụ.
            CopyDirectory(
                resourceSource,
                Path.Combine(packRoot, "resource"),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Không tạo được thư mục đóng gói: {ex.Message}");

            return 1;
        }
        try
        {
            Console.WriteLine("=== PACK VPK: đóng gói bộ cài đặt cho mod ===");
            Console.WriteLine($"Staging    : {stagingDirectory}");
            Console.WriteLine($"Pack root  : {packRoot}");
            Console.WriteLine($"VPKEdit CLI: {vpkEditCli}");
            Console.WriteLine($"Đích VPK   : {outputVpk}");
            Console.WriteLine();

            var arguments = new List<string>
            {
                "--no-progress",
                "--type", "vpk",
                "--version", "2",
                "--chunksize", "200",
                "--output", outputVpk,
                packRoot
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = vpkEditCli,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Console.WriteLine("> " + vpkEditCli + " " + string.Join(' ', arguments));
            Console.WriteLine();

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                Console.WriteLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                Console.Error.WriteLine(e.Data);
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine(
                    $"\nVPKEdit trả về mã lỗi {process.ExitCode}.");

                return 1;
            }

            if (!File.Exists(outputVpk))
            {
                Console.Error.WriteLine(
                    $"\nKhông tìm thấy file VPK sau khi đóng gói: {outputVpk}");

                return 1;
            }

            PrintResult(outputVpk, outputDirectory, vpkEditCli);

            return 0;
        }
        finally
        {
            TryDeleteDirectory(packRoot);
        }
    }

    private static void PrintResult(
        string outputVpk,
        string outputDirectory,
        string vpkEditCli)
    {
        var fileInfo = new FileInfo(outputVpk);

        Console.WriteLine();
        Console.WriteLine("=== ĐÃ ĐÓNG GÓI ===");
        Console.WriteLine($"VPK        : {outputVpk}");
        Console.WriteLine($"Kích thước : {fileInfo.Length / 1024.0:F1} KB");
        Console.WriteLine();

        // Xác thực đường dẫn bên trong VPK.
        Console.WriteLine("=== KIỂM TRA ĐƯỜNG DẪN TRONG VPK ===");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = vpkEditCli,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("--file-tree");
            startInfo.ArgumentList.Add(outputVpk);

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return;
            }

            string tree = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            bool hasResource = tree.Contains("resource", StringComparison.Ordinal);
            bool hasLocalization = tree.Contains("localization", StringComparison.Ordinal);
            bool hasVietnamese = tree.Contains("_vietnamese.txt", StringComparison.Ordinal);

            if (hasResource && hasLocalization && hasVietnamese)
            {
                Console.WriteLine(
                    "  ✓ Có resource/localization/*_vietnamese.txt trong VPK.");
            }
            else
            {
                Console.Error.WriteLine(
                    "  ✗ Đường dẫn trong VPK không đúng! " +
                    $"resource={hasResource} localization={hasLocalization} " +
                    $"vietnamese={hasVietnamese}");

                Console.Error.WriteLine(
                    "  Kiểm tra lại cấu trúc thư mục staging.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"  Không kiểm tra được cây VPK: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("=== CÀI ĐẶT ===");
        Console.WriteLine("Copy file VPK vào thư mục addons của game:");
        Console.WriteLine("  <game>/game/citadel/addons/");
        Console.WriteLine();
        Console.WriteLine("Lưu ý: copy CẢ file <tên>_dir.vpk và <tên>_000.vpk");
        Console.WriteLine("nếu VPKEdit sinh ra file chunk riêng.");
        Console.WriteLine();
        Console.WriteLine($"Thư mục output: {outputDirectory}");
    }

    /// <summary>
    /// Chép đệ quy thư mục nguồn sang thư mục đích, chỉ lấy file có phần
    /// mở rộng trong <paramref name="extensions"/>.
    /// <para>
    /// <b>Vì sao cần lọc:</b> thư mục staging còn chứa các file JSON
    /// bản dịch nội bộ (heroes.json, items.json, voice/*.json…) — tổng
    /// cộng hơn 50 MB và <b>không</b> có ích gì cho game. Nếu đóng gói
    /// luôn, VPK mod sẽ thừa hàng chục MB và phải đóng gói lại mỗi khi
    /// bản dịch thay đổi một entry nào đó.
    /// </para>
    /// </summary>
    private static void CopyDirectory(
        string source,
        string destination,
        IReadOnlySet<string> extensions)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            string extension = Path.GetExtension(file);

            if (!extensions.Contains(extension))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)),
                extensions);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Bỏ qua: thư mục tạm trong %TEMP% không quan trọng.
        }
    }

    private static string? FindVpkEditCli()
    {
        var candidates = new[]
        {
            DefaultVpkEditCli,
            @"C:\Program Files (x86)\VPKEdit\vpkeditcli.exe",
            @"D:\Tools\VPKEdit\vpkeditcli.exe",
            @"E:\Tools\VPKEdit\vpkeditcli.exe"
        };

        foreach (string candidate in candidates)
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

        return DefaultStagingDirectory;
    }

    private static string ResolveDefaultOutputDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "staging",
                "vpk");

            if (Directory.Exists(Path.GetDirectoryName(candidate)!))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return DefaultOutputDirectory;
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--staging":
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        options.StagingDirectory = args[++i];
                    }

                    break;

                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        options.OutputDirectory = args[++i];
                    }

                    break;

                case "--name":
                case "-n":
                    if (i + 1 < args.Length)
                    {
                        options.VpkFileName = args[++i];
                    }

                    break;

                case "--vpkedit":
                    if (i + 1 < args.Length)
                    {
                        options.VpkEditCliPath = args[++i];
                    }

                    break;

                default:
                    break;
            }
        }

        return options;
    }

    private sealed class Options
    {
        public string? StagingDirectory { get; set; }

        public string? OutputDirectory { get; set; }

        public string? VpkFileName { get; set; }

        public string? VpkEditCliPath { get; set; }
    }
}
