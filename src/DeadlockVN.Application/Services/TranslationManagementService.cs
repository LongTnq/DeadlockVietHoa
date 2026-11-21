using DeadlockVN.Application.DTOs;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Application.Services;

/// <summary>
/// Quản lý vòng đời của bộ bản dịch: xuất, nạp, đồng bộ, báo cáo tiến độ.
/// </summary>
public sealed class TranslationManagementService
{
    private readonly ITranslationStore _store;
    private readonly ILocalizationSourceReader _sourceReader;

    public TranslationManagementService(
        ITranslationStore store,
        ILocalizationSourceReader sourceReader)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    /// <summary>
    /// Tạo bộ bản dịch mới từ thư mục nguồn gốc, rồi lưu ra file JSON.
    /// </summary>
    public async Task<ExportResult> CreateAsync(
        string sourceDirectory,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var document = await BuildDocumentAsync(sourceDirectory, cancellationToken);

        await _store.SaveAsync(document, outputPath, cancellationToken);

        return BuildExportResult(document, outputPath);
    }

    /// <summary>
    /// Tạo bộ bản dịch mới và lưu ra nhiều file, tách theo nhóm nội dung.
    /// </summary>
    public async Task<SplitExportResult> CreateSplitAsync(
        string sourceDirectory,
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        var document = await BuildDocumentAsync(sourceDirectory, cancellationToken);

        var files = await SaveSplitAsync(document, rootDirectory, cancellationToken);

        return new SplitExportResult
        {
            ExportedCount = document.Entries.Count,
            TranslatedCount = document.Entries.Count(
                entry => !string.IsNullOrWhiteSpace(entry.TranslatedText)),
            RootDirectory = rootDirectory,
            Files = files
                .Select(path =>
                {
                    string relative = Path
                        .GetRelativePath(rootDirectory, path)
                        .Replace('\\', '/');

                    int separator = relative.IndexOf('/');

                    return new CategoryFileInfo(
                        separator > 0 ? relative[..separator] : relative,
                        relative,
                        path,
                        new FileInfo(path).Length);
                })
                .ToList()
        };
    }

    /// <summary>
    /// Xuất bộ bản dịch hiện tại ra file JSON.
    /// </summary>
    public async Task<ExportResult> ExportAsync(
        string translationsPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var document = await _store.LoadAsync(translationsPath, cancellationToken);

        await _store.SaveAsync(document, outputPath, cancellationToken);

        return BuildExportResult(document, outputPath);
    }

    /// <summary>
    /// Nạp một file bản dịch khác vào bộ bản dịch hiện tại.
    /// <para>
    /// Bản dịch đã tồn tại sẽ <b>không</b> bị ghi đè nếu bộ hiện tại đã có
    /// bản dịch không rỗng — để tránh mất công sức dịch.
    /// </para>
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        string targetPath,
        string sourcePath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var target = await _store.LoadAsync(targetPath, cancellationToken);
        var source = await _store.LoadAsync(sourcePath, cancellationToken);

        var result = MergeEntries(target, source, overwrite);

        await _store.SaveAsync(target, targetPath, cancellationToken);

        return result;
    }

    /// <summary>
    /// Nạp bản dịch từ một file hoặc thư mục JSON khác vào bộ bản dịch hiện tại
    /// (dạng tách theo nhóm).
    /// </summary>
    public async Task<ImportResult> ImportSplitAsync(
        string targetRoot,
        string sourcePath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var target = await _store.LoadSplitAsync(targetRoot, cancellationToken);

        // Nguồn có thể là một file đơn lẻ (người dịch gửi file) hoặc một
        // thư mục đã tách (giao nhánh).
        var source = Directory.Exists(sourcePath)
            ? await _store.LoadSplitAsync(sourcePath, cancellationToken)
            : await _store.LoadAsync(sourcePath, cancellationToken);

        var result = MergeEntries(target, source, overwrite);

        await SaveSplitAsync(target, targetRoot, cancellationToken);

        return result;
    }

    /// <summary>
    /// Đồng bộ bộ bản dịch với bản gốc mới từ game.
    /// </summary>
    public async Task<TranslationSyncResult> SyncAsync(
        string translationsPath,
        string sourceDirectory,
        CancellationToken cancellationToken = default)
    {
        var document = await _store.LoadAsync(translationsPath, cancellationToken);

        var originals = await _sourceReader.ReadAsync(
            sourceDirectory,
            cancellationToken);

        var result = _store.Sync(document, originals);

        await _store.SaveAsync(document, translationsPath, cancellationToken);

        return result;
    }

    /// <summary>
    /// Đồng bộ bộ bản dịch (dạng tách theo nhóm) với bản gốc mới từ game.
    /// </summary>
    public async Task<TranslationSyncResult> SyncSplitAsync(
        string rootDirectory,
        string sourceDirectory,
        CancellationToken cancellationToken = default)
    {
        var document = await _store.LoadSplitAsync(rootDirectory, cancellationToken);

        var originals = await _sourceReader.ReadAsync(
            sourceDirectory,
            cancellationToken);

        var result = _store.Sync(document, originals);

        await SaveSplitAsync(document, rootDirectory, cancellationToken);

        return result;
    }

    /// <summary>
    /// Tạo báo cáo tiến độ Việt hóa.
    /// </summary>
    public async Task<TranslationReport> BuildReportAsync(
        string translationsPath,
        CancellationToken cancellationToken = default)
    {
        var document = await _store.LoadAsync(translationsPath, cancellationToken);

        return BuildReport(document);
    }

    /// <summary>
    /// Tạo báo cáo tiến độ từ một thư mục bản dịch tách theo nhóm.
    /// </summary>
    public async Task<TranslationReport> BuildSplitReportAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        var document = await _store.LoadSplitAsync(rootDirectory, cancellationToken);

        return BuildReport(document);
    }

    /// <summary>
    /// Tạo báo cáo tiến độ từ document trong bộ nhớ.
    /// </summary>
    public static TranslationReport BuildReport(TranslationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var entries = document.Entries;

        var categories = entries
            .GroupBy(entry => entry.Category)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryProgress
            {
                Name = group.Key.ToString(),
                Total = group.Count(),
                Translated = group.Count(e => !string.IsNullOrWhiteSpace(e.TranslatedText)),
                Approved = group.Count(e => e.Status == TranslationStatus.Approved)
            })
            .ToList();

        return new TranslationReport
        {
            Total = entries.Count,
            Translated = entries.Count(e => !string.IsNullOrWhiteSpace(e.TranslatedText)),
            Approved = entries.Count(e => e.Status == TranslationStatus.Approved),
            Pending = entries.Count(e => e.Status == TranslationStatus.Pending),
            Translating = entries.Count(e => e.Status == TranslationStatus.Translating),
            Reviewed = entries.Count(e => e.Status == TranslationStatus.Reviewed),
            Stale = entries.Count(e => e.IsStale),
            Categories = categories
        };
    }

    private async Task<TranslationDocument> BuildDocumentAsync(
        string sourceDirectory,
        CancellationToken cancellationToken)
    {
        var originals = await _sourceReader.ReadAsync(
            sourceDirectory,
            cancellationToken);

        var document = new TranslationDocument
        {
            SourceLanguage = "English",
            TargetLanguage = "Vietnamese",
            Version = "0.1.0"
        };

        foreach (var original in originals)
        {
            original.OriginalTextSnapshot = original.OriginalText;

            document.Entries.Add(original);
        }

        return document;
    }

    private Task<IReadOnlyList<string>> SaveSplitAsync(
        TranslationDocument document,
        string rootDirectory,
        CancellationToken cancellationToken)
    {
        return _store.SaveSplitAsync(document, rootDirectory, cancellationToken);
    }

    private static ImportResult MergeEntries(
        TranslationDocument target,
        TranslationDocument source,
        bool overwrite)
    {
        var existing = target.Entries.ToDictionary(entry => entry.Id, entry => entry);

        int added = 0;
        int updated = 0;
        int skipped = 0;
        var skippedDetails = new List<string>();

        foreach (var sourceEntry in source.Entries)
        {
            if (existing.TryGetValue(sourceEntry.Id, out var targetEntry))
            {
                bool targetHasTranslation =
                    !string.IsNullOrWhiteSpace(targetEntry.TranslatedText);

                if (targetHasTranslation && !overwrite)
                {
                    skipped++;
                    skippedDetails.Add(
                        $"{sourceEntry.Id} (đã có bản dịch: \"{targetEntry.TranslatedText}\")");

                    continue;
                }

                targetEntry.TranslatedText = sourceEntry.TranslatedText;
                targetEntry.Status = sourceEntry.Status;

                if (string.IsNullOrEmpty(targetEntry.OriginalTextSnapshot))
                {
                    targetEntry.OriginalTextSnapshot = sourceEntry.OriginalText;
                }

                updated++;
            }
            else
            {
                var newEntry = new TranslationEntry
                {
                    Id = sourceEntry.Id,
                    OriginalText = sourceEntry.OriginalText,
                    TranslatedText = sourceEntry.TranslatedText,
                    Status = sourceEntry.Status,
                    Category = sourceEntry.Category,
                    OriginalTextSnapshot = sourceEntry.OriginalTextSnapshot
                };

                target.Entries.Add(newEntry);

                existing[newEntry.Id] = newEntry;

                added++;
            }
        }

        return new ImportResult
        {
            SourceCount = source.Entries.Count,
            AddedCount = added,
            UpdatedCount = updated,
            SkippedCount = skipped,
            Skipped = skippedDetails
        };
    }

    private static ExportResult BuildExportResult(
        TranslationDocument document,
        string outputPath)
    {
        var byCategory = document.Entries
            .GroupBy(entry => entry.Category)
            .ToDictionary(
                group => group.Key.ToString(),
                group => group.Count());

        return new ExportResult
        {
            ExportedCount = document.Entries.Count,
            TranslatedCount = document.Entries.Count(
                entry => !string.IsNullOrWhiteSpace(entry.TranslatedText)),
            OutputPath = outputPath,
            ByCategory = byCategory
        };
    }
}
