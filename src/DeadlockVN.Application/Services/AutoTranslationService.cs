using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Application.Services;

/// <summary>
/// Dịch tự động hàng loạt các entry chưa được dịch.
/// <para>
/// <b>Quy trình vòng lặp cho mỗi lô:</b>
/// </para>
/// <list type="number">
///   <item><description>Gom các entry <c>Pending</c> thuộc nhóm được chọn.</description></item>
///   <item><description>
///     Gửi lô sang provider. Mọi placeholder được che trước khi gửi và
///     khôi phục sau khi nhận.
///   </description></item>
///   <item><description>
///     Chạy <see cref="ITranslationGuard"/> trên từng bản dịch. Bản dịch
///     <b>hỏng</b> bị loại và thử lại (tối đa <c>MaxRetries</c> lần).
///   </description></item>
///   <item><description>
///     Bản dịch qua guard được đánh dấu <c>Reviewed</c> — tức là "đã dịch
///     bằng máy, cần người kiểm tra". Không bao giờ tự động đánh dấu
///     <c>Approved</c>.
///   </description></item>
/// </list>
/// <para>
/// <b>Quan trọng:</b> bản dịch hỏng <b>không</b> được ghi vào cơ sở dữ
/// liệu — nó vẫn ở trạng thái <c>Pending</c> để dịch lại sau. Ghi bản
/// dịch hỏng vào DB sẽ khiến game hiển thị sai (mất số liệu, vỡ layout).
/// </para>
/// </summary>
public sealed class AutoTranslationService
{
    private readonly ITranslationProvider _provider;
    private readonly ITranslationGuard _guard;
    private readonly TranslationProviderConfig _config;

    public AutoTranslationService(
        ITranslationProvider provider,
        ITranslationGuard guard,
        TranslationProviderConfig config)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Kết quả của một lần chạy dịch tự động.
    /// </summary>
    public sealed class AutoTranslateResult
    {
        /// <summary>Số entry đã được dịch thành công.</summary>
        public int Translated { get; set; }

        /// <summary>Số entry bị loại vì bản dịch hỏng (thiếu placeholder, vỡ thẻ).</summary>
        public int Failed { get; set; }

        /// <summary>Số entry bị bỏ qua (đã dịch, hoặc không thuộc nhóm đã chọn).</summary>
        public int Skipped { get; set; }

        /// <summary>Số lô đã gửi.</summary>
        public int Batches { get; set; }

        /// <summary>Chi tiết các lỗi.</summary>
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Dịch tự động các entry chưa dịch.
    /// </summary>
    /// <param name="document">Bộ bản dịch (được cập nhật tại chỗ).</param>
    /// <param name="categories">
    /// Các nhóm nội dung cần dịch. <c>null</c> = tất cả trừ <c>Voice</c>.
    /// </param>
    /// <param name="onProgress">
    /// Callback báo tiến độ: (đã dịch, tổng số, thông điệp).
    /// </param>
    /// <param name="onBatchCompleted">
    /// Callback được gọi sau <b>mỗi lô</b> — dùng để lưu tiến độ, để việc
    /// dừng bằng <c>Ctrl+C</c> không mất công sức đã làm.
    /// </param>
    /// <param name="cancellationToken">Token hủy.</param>
    public async Task<AutoTranslateResult> TranslateAsync(
        TranslationDocument document,
        IReadOnlyCollection<LocalizationCategory>? categories = null,
        Action<int, int, string>? onProgress = null,
        Func<Task>? onBatchCompleted = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var result = new AutoTranslateResult();

        var pending = SelectPending(document, categories);

        result.Skipped = document.Entries.Count - pending.Count;

        if (pending.Count == 0)
        {
            return result;
        }

        int total = pending.Count;
        int done = 0;

        // Nhóm theo file nguồn để context có ý nghĩa.
        foreach (var group in pending.GroupBy(entry => entry.SourceFile))
        {
            var groupEntries = group.ToList();

            for (int offset = 0; offset < groupEntries.Count; offset += _config.BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = groupEntries
                    .Skip(offset)
                    .Take(_config.BatchSize)
                    .ToList();

                result.Batches++;

                string context = DescribeContext(group.Key);

                await TranslateBatchAsync(
                    batch,
                    context,
                    result,
                    cancellationToken);

                done += batch.Count;

                onProgress?.Invoke(
                    done,
                    total,
                    $"{group.Key} — lô {result.Batches} " +
                    $"({done}/{total} đã xử lý)");

                // Lưu sau mỗi lô để không mất công sức khi bị hủy.
                if (onBatchCompleted is not null)
                {
                    await onBatchCompleted();
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Dịch một lô, có retry khi bản dịch không qua guard.
    /// </summary>
    private async Task TranslateBatchAsync(
        IReadOnlyList<TranslationEntry> batch,
        string context,
        AutoTranslateResult result,
        CancellationToken cancellationToken)
    {
        var pendingInBatch = batch
            .Where(entry => entry.Status == TranslationStatus.Pending)
            .ToList();

        if (pendingInBatch.Count == 0)
        {
            return;
        }

        var texts = pendingInBatch
            .Select(entry => entry.OriginalText)
            .ToList();

        for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> translated;

            try
            {
                translated = await _provider.TranslateBatchAsync(
                    texts,
                    context,
                    cancellationToken);
            }
            catch (Exception ex) when (
                ex is HttpRequestException or TaskCanceledException
                    or InvalidOperationException)
            {
                result.Errors.Add(
                    $"Lỗi provider (lần {attempt}/{_config.MaxRetries}): {ex.Message}");

                if (attempt == _config.MaxRetries)
                {
                    break;
                }

                // Chờ tăng dần trước khi thử lại.
                await Task.Delay(
                    TimeSpan.FromSeconds(2 * attempt),
                    cancellationToken);

                continue;
            }

            // Những entry đã thành công trong lần trước không cần làm lại.
            var retryTexts = new List<string>();
            var retryEntries = new List<TranslationEntry>();

            for (int i = 0; i < pendingInBatch.Count; i++)
            {
                var entry = pendingInBatch[i];

                if (entry.Status != TranslationStatus.Pending)
                {
                    continue;
                }

                if (i >= translated.Count || string.IsNullOrWhiteSpace(translated[i]))
                {
                    retryEntries.Add(entry);
                    retryTexts.Add(entry.OriginalText);

                    continue;
                }

                var check = _guard.Check(entry.OriginalText, translated[i]);

                if (check.IsValid)
                {
                    entry.TranslatedText = translated[i];
                    entry.OriginalTextSnapshot = entry.OriginalText;

                    // Dịch bằng máy -> "Reviewed" (cần người kiểm tra),
                    // KHÔNG phải "Approved".
                    entry.Status = TranslationStatus.Reviewed;

                    result.Translated++;
                }
                else
                {
                    retryEntries.Add(entry);
                    retryTexts.Add(entry.OriginalText);

                    result.Errors.Add(
                        $"{entry.SourceFile}#{entry.Key}: " +
                        string.Join(" ", check.Errors));
                }
            }

            if (retryEntries.Count == 0)
            {
                return;
            }

            if (attempt == _config.MaxRetries)
            {
                result.Failed += retryEntries.Count;

                foreach (var entry in retryEntries)
                {
                    result.Errors.Add(
                        $"{entry.SourceFile}#{entry.Key}: " +
                        "thất bại sau " +
                        $"{_config.MaxRetries} lần thử.");
                }

                return;
            }

            // Thử lại với các entry còn lỗi.
            texts = retryTexts;
            pendingInBatch = retryEntries;
        }
    }

    /// <summary>
    /// Chọn các entry cần dịch.
    /// </summary>
    private static List<TranslationEntry> SelectPending(
        TranslationDocument document,
        IReadOnlyCollection<LocalizationCategory>? categories)
    {
        var selected = new List<TranslationEntry>();

        foreach (var entry in document.Entries)
        {
            if (entry.Status != TranslationStatus.Pending)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.OriginalText))
            {
                continue;
            }

            if (categories is not null && categories.Count > 0)
            {
                bool included = categories.Contains(entry.Category);

                // "All" bao gồm mọi nhóm.
                if (categories.Contains(LocalizationCategory.All))
                {
                    included = true;
                }

                if (!included)
                {
                    continue;
                }
            }
            else
            {
                // Mặc định: bỏ qua VO — lời thoại cần người dịch có cảm giác
                // về giọng điệu nhân vật, máy dịch hỏng gần như 100%.
                if (entry.Category == LocalizationCategory.Voice)
                {
                    continue;
                }
            }

            selected.Add(entry);
        }

        return selected;
    }

    /// <summary>
    /// Tạo mô tả ngữ cảnh cho prompt, dựa trên tên file nguồn.
    /// </summary>
    private static string DescribeContext(string sourceFile)
    {
        string name = Path.GetFileNameWithoutExtension(sourceFile);

        return name switch
        {
            "citadel_heroes" =>
                "Tên hero, tên kỹ năng và mô tả kỹ năng của các hero " +
                "Deadlock. Giữ tên riêng nhân vật.",
            "citadel_main" =>
                "Giao diện người dùng, chuỗi hệ thống, thông báo và hướng dẫn " +
                "trong game. Dùng văn phong ngắn gọn, lệnh lệc khi có thể.",
            "citadel_attributes" =>
                "Tên và mô tả thuộc tính nhân vật (máu, tốc độ, sát thương…).",
            "citadel_vdata" =>
                "Dữ liệu vật phẩm và thành tựu (accolades).",
            "citadel_gc" =>
                "Chuỗi giao diện chung (GameCommunity).",
            "citadel_patch_notes" =>
                "Ghi chú cập nhật của game.",
            _ => $"Nội dung game Deadlock, nhóm: {name}."
        };
    }
}
