using System.Text.Json;

using DeadlockVN.Application.DTOs;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;

namespace DeadlockVN.Application.Services;

/// <summary>
/// Công cụ review thủ công: xuất entry ra file để người dịch sửa, rồi nhập
/// lại các chỉnh sửa.
/// <para>
/// <b>Vì sao cần tách xuất/nhập:</b> bản dịch máy sinh cần con người
/// duyệt. Nếu người dịch sửa trực tiếp file JSON chung thì dễ đụng nhau khi
/// nhiều người cùng làm, và dễ vô tình làm hỏng cấu trúc file. Xuất ra file
/// riêng rồi nhập lại là cách an toàn, đồng thời cho phép chia nhỏ công
/// việc theo nhóm nội dung.
/// </para>
/// </summary>
public sealed class ReviewService
{
    /// <summary>
    /// Xuất các entry cần review ra một file JSON.
    /// </summary>
    /// <param name="document">Bộ bản dịch.</param>
    /// <param name="categories">Lọc theo nhóm nội dung (null = tất cả).</param>
    /// <param name="onlyMachineTranslated">
    /// Chỉ xuất entry ở trạng thái <c>Reviewed</c> (dịch bằng máy) —
    /// đây là nhóm cần người kiểm tra gấp nhất.
    /// </param>
    /// <param name="limit">Giới hạn số entry (0 = không giới hạn).</param>
    public ReviewExportFile BuildExport(
        TranslationDocument document,
        IReadOnlyCollection<LocalizationCategory>? categories = null,
        bool onlyMachineTranslated = true,
        int limit = 0)
    {
        ArgumentNullException.ThrowIfNull(document);

        var export = new ReviewExportFile();

        foreach (var entry in document.Entries)
        {
            if (categories is not null && categories.Count > 0 &&
                !categories.Contains(entry.Category) &&
                !categories.Contains(LocalizationCategory.All))
            {
                continue;
            }

            if (onlyMachineTranslated &&
                entry.Status != TranslationStatus.Reviewed)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.TranslatedText))
            {
                continue;
            }

            export.Items.Add(new ReviewItem
            {
                Key = entry.Key,
                SourceFile = entry.SourceFile,
                Category = entry.Category.ToString(),
                Original = entry.OriginalText,
                Translation = entry.TranslatedText,
                Status = entry.Status.ToString()
            });

            if (limit > 0 && export.Items.Count >= limit)
            {
                break;
            }
        }

        return export;
    }

    /// <summary>
    /// Nhập lại các chỉnh sửa từ file review.
    /// <para>
    /// Chỉ những entry có <c>newTranslation</c> hoặc <c>newStatus</c> mới
    /// được cập nhật — các entry khác giữ nguyên.
    /// </para>
    /// </summary>
    public ReviewImportResult ApplyImport(
        TranslationDocument document,
        ReviewExportFile imported)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(imported);

        var result = new ReviewImportResult();

        var index = document.Entries
            .ToDictionary(entry => entry.Id);

        foreach (var item in imported.Items)
        {
            var id = new TranslationId(
                item.SourceFile ?? string.Empty,
                item.Key ?? string.Empty);

            if (!index.TryGetValue(id, out var entry))
            {
                result.NotFound++;

                continue;
            }

            bool hasTranslation =
                !string.IsNullOrWhiteSpace(item.NewTranslation);

            bool hasStatus =
                !string.IsNullOrWhiteSpace(item.NewStatus);

            if (!hasTranslation && !hasStatus)
            {
                result.Skipped++;

                continue;
            }

            if (hasTranslation)
            {
                entry.TranslatedText = item.NewTranslation!;
                entry.OriginalTextSnapshot = entry.OriginalText;
            }

            if (hasStatus)
            {
                if (!Enum.TryParse(
                        item.NewStatus,
                        ignoreCase: true,
                        out TranslationStatus newStatus))
                {
                    result.Errors.Add(
                        $"{item.SourceFile}#{item.Key}: " +
                        $"trạng thái không hợp lệ '{item.NewStatus}'.");

                    continue;
                }

                entry.Status = newStatus;

                if (newStatus == TranslationStatus.Approved)
                {
                    result.Approved++;
                }
            }

            result.Updated++;
        }

        return result;
    }

    /// <summary>
    /// Tổng hợp trạng thái review.
    /// </summary>
    public ReviewSummary Summarize(TranslationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var summary = new ReviewSummary
        {
            Total = document.Entries.Count
        };

        foreach (var entry in document.Entries)
        {
            switch (entry.Status)
            {
                case TranslationStatus.Reviewed:
                    summary.MachineTranslated++;
                    break;

                case TranslationStatus.Approved:
                    summary.Approved++;
                    break;

                case TranslationStatus.Pending:
                    summary.Pending++;
                    break;
            }

            string category = entry.Category.ToString();

            summary.ByCategory.TryGetValue(category, out int count);

            summary.ByCategory[category] = count + 1;
        }

        return summary;
    }

    /// <summary>
    /// Đọc file review từ đĩa.
    /// </summary>
    public static async Task<ReviewExportFile> ReadExportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(path, cancellationToken);

        return JsonSerializer.Deserialize<ReviewExportFile>(
                   json,
                   ReviewJson.ReadOptions)
               ?? new ReviewExportFile();
    }

    /// <summary>
    /// Ghi file review ra đĩa.
    /// </summary>
    public static async Task WriteExportAsync(
        ReviewExportFile export,
        string path,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(export, ReviewJson.Options);

        await File.WriteAllTextAsync(
            path,
            json,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }
}
