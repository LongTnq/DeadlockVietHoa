using System.Diagnostics;

using DeadlockVN.Core.Common;
using DeadlockVN.Infrastructure.Parsers.KeyValues;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>parse</c>: parse file/thư mục localization tiếng Anh và in thống kê.
/// </summary>
internal static class ParseCommand
{
    private const string DefaultOriginalDirectory = "workspace/source/original";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        string? target = args.Length > 0
            ? args[0]
            : ResolveDefaultTarget();

        if (target is null)
        {
            Console.Error.WriteLine(
                $"Không tìm thấy thư mục mặc định '{DefaultOriginalDirectory}'. " +
                "Hãy truyền đường dẫn file hoặc thư mục cần parse.");

            return 1;
        }

        var fullPath = Path.GetFullPath(target);

        if (Directory.Exists(fullPath))
        {
            return await ParseDirectoryAsync(fullPath);
        }

        if (File.Exists(fullPath))
        {
            return await ParseFileAsync(fullPath, verbose: true);
        }

        Console.Error.WriteLine($"Không tìm thấy: {fullPath}");

        return 1;
    }

    private static string? ResolveDefaultTarget()
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

    private static async Task<int> ParseDirectoryAsync(string directory)
    {
        var files = Directory
            .GetFiles(directory, "*_english.txt", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            Console.Error.WriteLine(
                $"Không tìm thấy file *_english.txt trong: {directory}");

            return 1;
        }

        Console.WriteLine($"Thư mục: {directory}");
        Console.WriteLine($"Số file: {files.Count}");
        Console.WriteLine();

        int totalEntries = 0;
        int totalPlaceholders = 0;
        int failedFiles = 0;
        var allPlaceholders = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var result = await ParseFileCoreAsync(file);

            if (!result.Success)
            {
                failedFiles++;
                Console.WriteLine(
                    $"[FAIL] {RelativePath(directory, file)} -> {result.Error}");
                continue;
            }

            totalEntries += result.EntryCount;
            totalPlaceholders += result.PlaceholderCount;

            foreach (string placeholder in result.Placeholders)
            {
                allPlaceholders.Add(placeholder);
            }

            Console.WriteLine(
                $"[OK]   {RelativePath(directory, file),-58} " +
                $"entries={result.EntryCount,-7} " +
                $"placeholders={result.PlaceholderCount,-6} " +
                $"{result.Elapsed.TotalMilliseconds,8:F1} ms");
        }

        Console.WriteLine();

        if (allPlaceholders.Count > 0)
        {
            Console.WriteLine("Placeholder theo loại:");

            var byKind = allPlaceholders
                .GroupBy(PlaceholderExtractor.GetKind)
                .OrderByDescending(x => x.Count());

            foreach (var group in byKind)
            {
                Console.WriteLine(
                    $"  {group.Key,-10} {group.Count(),-6} " +
                    $"ví dụ: {Truncate(string.Join(", ", group.Take(3)), 80)}");
            }

            Console.WriteLine();
        }

        Console.WriteLine($"Tổng entries      : {totalEntries}");
        Console.WriteLine($"Placeholder distinct: {allPlaceholders.Count}");
        Console.WriteLine($"File lỗi          : {failedFiles}");

        return failedFiles == 0 ? 0 : 1;
    }

    private static async Task<int> ParseFileAsync(string file, bool verbose)
    {
        var result = await ParseFileCoreAsync(file);

        if (!result.Success)
        {
            Console.Error.WriteLine($"[FAIL] {file} -> {result.Error}");

            return 1;
        }

        if (!verbose)
        {
            Console.WriteLine(
                $"[OK] {file} entries={result.EntryCount} " +
                $"placeholders={result.PlaceholderCount}");

            return 0;
        }

        Console.WriteLine($"File: {file}");
        Console.WriteLine($"Root nodes: {result.RootNodeCount}");
        Console.WriteLine($"Entries: {result.EntryCount}");
        Console.WriteLine($"Placeholder: {result.PlaceholderCount}");
        Console.WriteLine($"Thời gian: {result.Elapsed.TotalMilliseconds:F1} ms");
        Console.WriteLine();

        Console.WriteLine("Sample entries (10 đầu):");

        foreach (var entry in result.SampleEntries)
        {
            Console.WriteLine($"  {entry.Path}");
            Console.WriteLine($"      = {Truncate(entry.Value, 100)}");
        }

        if (result.Placeholders.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Placeholder tìm thấy:");

            foreach (string placeholder in result.Placeholders.Take(30))
            {
                Console.WriteLine($"  {placeholder}");
            }

            if (result.Placeholders.Count > 30)
            {
                Console.WriteLine(
                    $"  ... và {result.Placeholders.Count - 30} placeholder khác.");
            }
        }

        return 0;
    }

    private static async Task<ParseFileResult> ParseFileCoreAsync(string file)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            string source = await File.ReadAllTextAsync(file);

            var parser = new KeyValuesParser();

            var document = parser.Parse(source);

            var entries = document.EnumerateEntries().ToList();

            var placeholders = new List<string>();

            foreach (var entry in entries)
            {
                placeholders.AddRange(
                    PlaceholderExtractor.ExtractDistinct(entry.Value));
            }

            stopwatch.Stop();

            return new ParseFileResult(
                Success: true,
                Error: string.Empty,
                EntryCount: entries.Count,
                RootNodeCount: document.Nodes.Count,
                PlaceholderCount: placeholders.Count,
                Placeholders: placeholders.Distinct(StringComparer.Ordinal).ToList(),
                SampleEntries: entries
                    .Take(10)
                    .Select(entry => (entry.Path, entry.Value))
                    .ToList(),
                Elapsed: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new ParseFileResult(
                Success: false,
                Error: ex.Message,
                EntryCount: 0,
                RootNodeCount: 0,
                PlaceholderCount: 0,
                Placeholders: Array.Empty<string>(),
                SampleEntries: Array.Empty<(string Path, string Value)>(),
                Elapsed: stopwatch.Elapsed);
        }
    }

    private static string RelativePath(string root, string file)
    {
        return Path.GetRelativePath(root, file);
    }

    private static string Truncate(string value, int maxLength)
    {
        string singleLine = value.ReplaceLineEndings(" ");

        return singleLine.Length <= maxLength
            ? singleLine
            : singleLine[..maxLength] + "...";
    }

    private sealed record ParseFileResult(
        bool Success,
        string Error,
        int EntryCount,
        int RootNodeCount,
        int PlaceholderCount,
        IReadOnlyList<string> Placeholders,
        IReadOnlyList<(string Path, string Value)> SampleEntries,
        TimeSpan Elapsed);
}
