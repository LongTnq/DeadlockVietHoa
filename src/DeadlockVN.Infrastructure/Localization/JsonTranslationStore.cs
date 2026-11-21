using System.Text.Json;
using System.Text.Json.Serialization;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Infrastructure.Localization;

/// <summary>
/// Lưu/load bộ bản dịch dưới dạng JSON.
/// <para>
/// <b>Định dạng file:</b>
/// </para>
/// <code>
/// {
///   "metadata": {
///     "sourceLanguage": "English",
///     "targetLanguage": "Vietnamese",
///     "version": "0.1.0",
///     "updatedAt": "2026-11-16T10:00:00+00:00"
///   },
///   "entries": [
///     {
///       "key": "upgrade_close_range",
///       "sourceFile": "citadel_gc_mod_names/citadel_gc_mod_names_english.txt",
///       "originalText": "Close Quarters",
///       "translatedText": "Cận Chiến",
///       "status": "Approved",
///       "category": "Items",
///       "originalTextSnapshot": "Close Quarters"
///     }
///   ]
/// }
/// </code>
/// <para>
/// <b>Lưu ý:</b> định danh entry là cặp <c>(sourceFile, key)</c> vì key
/// <c>Language</c> xuất hiện ở 10 file khác nhau.
/// </para>
/// </summary>
public sealed class JsonTranslationStore : ITranslationStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<TranslationDocument> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Không tìm thấy file bản dịch: {path}", path);
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);

        return Deserialize(json);
    }

    public async Task SaveAsync(
        TranslationDocument document,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        document.UpdatedAt = DateTimeOffset.UtcNow;

        var payload = new TranslationDocumentPayload
        {
            Metadata = new TranslationMetadataPayload
            {
                SourceLanguage = document.SourceLanguage,
                TargetLanguage = document.TargetLanguage,
                Version = document.Version,
                UpdatedAt = document.UpdatedAt
            },
            Entries = document.Entries
                .Select(entry => new TranslationEntryPayload
                {
                    Key = entry.Key,
                    SourceFile = entry.SourceFile,
                    OriginalText = entry.OriginalText,
                    TranslatedText = entry.TranslatedText,
                    Status = entry.Status.ToString(),
                    Category = entry.Category.ToString(),
                    OriginalTextSnapshot = string.IsNullOrEmpty(entry.OriginalTextSnapshot)
                        ? entry.OriginalText
                        : entry.OriginalTextSnapshot
                })
                .ToList()
        };

        string json = JsonSerializer.Serialize(payload, WriteOptions);

        // UTF-8 không BOM: file bản dịch là tài liệu nội bộ, không phải file game.
        await File.WriteAllTextAsync(
            path,
            json,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    /// <summary>
    /// Lưu bộ bản dịch ra nhiều file, tách theo nhóm nội dung.
    /// <para>
    /// Một file cho toàn bộ 91 457 entry nặng ~31 MB — quá lớn để quản lý
    /// bằng git và khó cho nhiều người cùng dịch. Tách theo nhóm vừa đúng
    /// với cách chia việc, vừa giữ mỗi file ở kích thước hợp lý.
    /// </para>
    /// </summary>
    /// <param name="document">Bộ bản dịch.</param>
    /// <param name="rootDirectory">Thư mục gốc, ví dụ <c>workspace/translations</c>.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Danh sách file đã ghi.</returns>
    public async Task<IReadOnlyList<string>> SaveSplitAsync(
        TranslationDocument document,
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        Directory.CreateDirectory(rootDirectory);

        var writtenFiles = new List<string>();

        // Danh sách slug hero, dùng để tách nhóm Voice thành file theo hero.
        var heroSlugs = VoicePartitioner.ExtractHeroSlugs(document.Entries);

        foreach (var category in CategoryLayout.All)
        {
            var categoryEntries = document.Entries
                .Where(entry => entry.Category == category)
                .ToList();

            if (categoryEntries.Count == 0)
            {
                continue;
            }

            if (category == LocalizationCategory.Voice)
            {
                // Voice được tách nhỏ thêm theo từng hero.
                var buckets = VoicePartitioner.Group(categoryEntries, heroSlugs);

                foreach (var bucket in buckets.OrderBy(b => b.Key, StringComparer.OrdinalIgnoreCase))
                {
                    string path = Path.Combine(
                        rootDirectory,
                        CategoryLayout.GetFolder(category),
                        $"{bucket.Key}.json");

                    await SaveAsync(
                        CreatePartDocument(document, bucket.Value),
                        path,
                        cancellationToken);

                    writtenFiles.Add(path);
                }

                continue;
            }

            string categoryPath = CategoryLayout.GetPath(rootDirectory, category);

            await SaveAsync(
                CreatePartDocument(document, categoryEntries),
                categoryPath,
                cancellationToken);

            writtenFiles.Add(categoryPath);
        }

        return writtenFiles;
    }

    private static TranslationDocument CreatePartDocument(
        TranslationDocument source,
        List<TranslationEntry> entries)
    {
        return new TranslationDocument
        {
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            Version = source.Version,
            Entries = entries
        };
    }

    /// <summary>
    /// Nạp bộ bản dịch từ nhiều file trong một thư mục (dạng đã tách theo nhóm).
    /// </summary>
    public async Task<TranslationDocument> LoadSplitAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var document = new TranslationDocument();

        if (!Directory.Exists(rootDirectory))
        {
            return document;
        }

        var files = Directory
            .GetFiles(rootDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var part = await LoadAsync(file, cancellationToken);

            document.Entries.AddRange(part.Entries);

            if (part.Version != document.Version)
            {
                document.Version = part.Version;
            }
        }

        document.UpdatedAt = DateTimeOffset.UtcNow;

        return document;
    }

    public TranslationSyncResult Sync(
        TranslationDocument document,
        IEnumerable<TranslationEntry> currentOriginals)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(currentOriginals);

        var existing = document.Entries.ToDictionary(
            entry => entry.Id,
            entry => entry);

        var changes = new List<TranslationChange>();
        var synced = new List<TranslationEntry>();

        var seenIds = new HashSet<TranslationId>();

        foreach (var original in currentOriginals)
        {
            seenIds.Add(original.Id);

            if (existing.TryGetValue(original.Id, out var entry))
            {
                bool contentChanged = !string.Equals(
                    entry.OriginalText,
                    original.OriginalText,
                    StringComparison.Ordinal);

                // Cập nhật nội dung gốc, nhưng giữ nguyên bản dịch.
                entry.OriginalText = original.OriginalText;
                entry.Category = original.Category;

                if (contentChanged)
                {
                    // Nội dung gốc đã thay đổi -> bản dịch có thể lỗi thời.
                    // Hạ trạng thái về Reviewed để bắt buộc người dịch xem lại.
                    if (entry.Status == TranslationStatus.Approved)
                    {
                        entry.Status = TranslationStatus.Reviewed;
                    }

                    changes.Add(new TranslationChange
                    {
                        Id = original.Id,
                        Kind = TranslationChangeKind.Modified,
                        OldOriginalText = entry.OriginalTextSnapshot,
                        NewOriginalText = original.OriginalText,
                        TranslatedText = entry.TranslatedText,
                        Status = entry.Status
                    });
                }

                synced.Add(entry);
            }
            else
            {
                // Key mới -> tạo entry Pending.
                var newEntry = new TranslationEntry
                {
                    Id = original.Id,
                    OriginalText = original.OriginalText,
                    TranslatedText = string.Empty,
                    Status = TranslationStatus.Pending,
                    Category = original.Category,
                    OriginalTextSnapshot = original.OriginalText
                };

                synced.Add(newEntry);

                changes.Add(new TranslationChange
                {
                    Id = original.Id,
                    Kind = TranslationChangeKind.Added,
                    OldOriginalText = null,
                    NewOriginalText = original.OriginalText,
                    TranslatedText = null,
                    Status = TranslationStatus.Pending
                });
            }
        }

        // Key không còn tồn tại trong game.
        foreach (var entry in existing.Values)
        {
            if (seenIds.Contains(entry.Id))
            {
                continue;
            }

            changes.Add(new TranslationChange
            {
                Id = entry.Id,
                Kind = TranslationChangeKind.Removed,
                OldOriginalText = entry.OriginalText,
                NewOriginalText = null,
                TranslatedText = entry.TranslatedText,
                Status = entry.Status
            });
        }

        document.Entries = synced;

        return new TranslationSyncResult
        {
            Changes = changes,
            Added = changes.Where(c => c.Kind == TranslationChangeKind.Added).ToList(),
            Modified = changes.Where(c => c.Kind == TranslationChangeKind.Modified).ToList(),
            Removed = changes.Where(c => c.Kind == TranslationChangeKind.Removed).ToList(),
            TotalEntries = synced.Count
        };
    }

    private static TranslationDocument Deserialize(string json)
    {
        var payload = JsonSerializer.Deserialize<TranslationDocumentPayload>(json, ReadOptions);

        if (payload is null)
        {
            return new TranslationDocument();
        }

        var document = new TranslationDocument
        {
            SourceLanguage = payload.Metadata?.SourceLanguage ?? "English",
            TargetLanguage = payload.Metadata?.TargetLanguage ?? "Vietnamese",
            Version = payload.Metadata?.Version ?? "0.1.0",
            UpdatedAt = payload.Metadata?.UpdatedAt ?? DateTimeOffset.UtcNow
        };

        foreach (var entry in payload.Entries ?? new List<TranslationEntryPayload>())
        {
            var translationEntry = new TranslationEntry
            {
                Id = new TranslationId(entry.SourceFile ?? string.Empty, entry.Key ?? string.Empty),
                OriginalText = entry.OriginalText ?? string.Empty,
                TranslatedText = entry.TranslatedText ?? string.Empty,
                Status = ParseStatus(entry.Status),
                Category = ParseCategory(entry.Category),
                OriginalTextSnapshot = entry.OriginalTextSnapshot ?? entry.OriginalText ?? string.Empty
            };

            document.Entries.Add(translationEntry);
        }

        return document;
    }

    private static TranslationStatus ParseStatus(string? value)
    {
        return Enum.TryParse<TranslationStatus>(value, ignoreCase: true, out var status)
            ? status
            : TranslationStatus.Pending;
    }

    private static LocalizationCategory ParseCategory(string? value)
    {
        return Enum.TryParse<LocalizationCategory>(value, ignoreCase: true, out var category)
            ? category
            : LocalizationCategory.Misc;
    }

    private sealed class TranslationDocumentPayload
    {
        public TranslationMetadataPayload? Metadata { get; set; }

        public List<TranslationEntryPayload>? Entries { get; set; }
    }

    private sealed class TranslationMetadataPayload
    {
        public string? SourceLanguage { get; set; }

        public string? TargetLanguage { get; set; }

        public string? Version { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class TranslationEntryPayload
    {
        public string? Key { get; set; }

        public string? SourceFile { get; set; }

        public string? OriginalText { get; set; }

        public string? TranslatedText { get; set; }

        public string? Status { get; set; }

        public string? Category { get; set; }

        public string? OriginalTextSnapshot { get; set; }
    }
}
