using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Parsers.KeyValues;

namespace DeadlockVN.Infrastructure.Localization;

/// <summary>
/// Đọc nội dung gốc tiếng Anh từ thư mục localization đã extract.
/// <para>
/// File nguồn được định danh bằng đường dẫn tương đối (dấu <c>/</c>) để
/// tài liệu bản dịch không phụ thuộc hệ điều hành.
/// </para>
/// </summary>
public sealed class LocalizationSourceReader : ILocalizationSourceReader
{
    private readonly ILocalizationClassifier _classifier;

    public LocalizationSourceReader(ILocalizationClassifier classifier)
    {
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
    }

    public async Task<IReadOnlyList<TranslationEntry>> ReadAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Không tìm thấy thư mục: {directory}");
        }

        var files = Directory
            .GetFiles(directory, "*_english.txt", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<TranslationEntry>();
        var parser = new KeyValuesParser();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string source = await File.ReadAllTextAsync(file, cancellationToken);

            var document = parser.Parse(source);

            string relativePath = Path
                .GetRelativePath(directory, file)
                .Replace('\\', '/');

            foreach (var entry in document.EnumerateEntries())
            {
                var translationEntry = new TranslationEntry
                {
                    Id = new TranslationId(relativePath, entry.Key),
                    OriginalText = entry.Value,
                    TranslatedText = string.Empty,
                    Status = TranslationStatus.Pending
                };

                translationEntry.Category = _classifier.Classify(translationEntry);

                result.Add(translationEntry);
            }
        }

        return result;
    }
}
