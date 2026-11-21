using DeadlockVN.Application.DTOs;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Application.Services;

/// <summary>
/// Xuất bộ bản dịch ra thư mục staging theo đúng cấu trúc file của game.
/// <para>
/// Mỗi file nguồn <c>&lt;tên&gt;_english.txt</c> sẽ tạo ra file
/// <c>&lt;tên&gt;_vietnamese.txt</c> ở cùng đường dẫn tương đối, sẵn sàng
/// đóng gói VPK.
/// </para>
/// </summary>
public sealed class GameLocalizationExportService
{
    private readonly ITranslationStore _store;
    private readonly ILocalizationExporter _exporter;

    public GameLocalizationExportService(
        ITranslationStore store,
        ILocalizationExporter exporter)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    /// <summary>
    /// Xuất từ một file bản dịch JSON.
    /// </summary>
    public async Task<GameExportResult> ExportAsync(
        string sourceDirectory,
        string translationsPath,
        string outputDirectory,
        string targetLanguage = "vietnamese",
        CancellationToken cancellationToken = default)
    {
        var document = await _store.LoadAsync(translationsPath, cancellationToken);

        return await ExportCoreAsync(
            document,
            sourceDirectory,
            outputDirectory,
            targetLanguage,
            cancellationToken);
    }

    /// <summary>
    /// Xuất từ bộ bản dịch đã tách theo nhóm.
    /// </summary>
    public async Task<GameExportResult> ExportSplitAsync(
        string sourceDirectory,
        string translationsDirectory,
        string outputDirectory,
        string targetLanguage = "vietnamese",
        CancellationToken cancellationToken = default)
    {
        var document = await _store.LoadSplitAsync(translationsDirectory, cancellationToken);

        return await ExportCoreAsync(
            document,
            sourceDirectory,
            outputDirectory,
            targetLanguage,
            cancellationToken);
    }

    private async Task<GameExportResult> ExportCoreAsync(
        TranslationDocument document,
        string sourceDirectory,
        string outputDirectory,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Không tìm thấy thư mục nguồn: {sourceDirectory}");
        }

        // Gom entry theo file nguồn.
        var entriesByFile = document.Entries
            .GroupBy(entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<TranslationEntry>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var result = new GameExportResult
        {
            OutputDirectory = outputDirectory,
            TargetLanguage = targetLanguage
        };

        var sourceFiles = Directory
            .GetFiles(sourceDirectory, "*_english.txt", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path
                .GetRelativePath(sourceDirectory, sourceFile)
                .Replace('\\', '/');

            // Đổi hậu tố _english.txt -> _vietnamese.txt
            string outputRelativePath = relativePath.Replace(
                "_english.txt",
                $"_{targetLanguage}.txt",
                StringComparison.OrdinalIgnoreCase);

            string outputPath = Path.Combine(
                outputDirectory,
                outputRelativePath.Replace('/', Path.DirectorySeparatorChar));

            entriesByFile.TryGetValue(relativePath, out var entries);

            var fileResult = await _exporter.ExportAsync(
                sourceFile,
                outputPath,
                entries ?? Array.Empty<TranslationEntry>(),
                targetLanguage,
                cancellationToken);

            result.TotalEntries += fileResult.TotalEntries;
            result.TranslatedEntries += fileResult.TranslatedEntries;
            result.UntranslatedEntries += fileResult.UntranslatedEntries;
            result.FileCount++;

            result.UnknownKeys.AddRange(
                fileResult.UnknownKeys.Select(key => $"{relativePath}#{key}"));

            result.Files.Add(new GameExportFileInfo
            {
                SourceFile = relativePath,
                OutputFile = outputRelativePath,
                TotalEntries = fileResult.TotalEntries,
                TranslatedEntries = fileResult.TranslatedEntries,
                HadByteOrderMark = fileResult.HadByteOrderMark
            });
        }

        // Key có trong bản dịch nhưng không tồn tại trong bất kỳ file nguồn nào.
        foreach (var group in entriesByFile)
        {
            string sourcePath = Path.Combine(
                sourceDirectory,
                group.Key.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(sourcePath))
            {
                continue;
            }

            foreach (var entry in group.Value)
            {
                if (!string.IsNullOrWhiteSpace(entry.TranslatedText))
                {
                    result.UnknownKeys.Add($"{group.Key}#{entry.Key}");
                }
            }
        }

        return result;
    }
}
