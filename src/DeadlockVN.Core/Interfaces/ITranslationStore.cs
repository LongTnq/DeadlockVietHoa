using DeadlockVN.Core.Entities;

namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Quản lý vòng đời của bộ bản dịch: nạp, lưu, đồng bộ với bản gốc.
/// </summary>
public interface ITranslationStore
{
    /// <summary>
    /// Nạp bộ bản dịch từ một file JSON.
    /// </summary>
    Task<TranslationDocument> LoadAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu bộ bản dịch ra file JSON.
    /// </summary>
    Task SaveAsync(
        TranslationDocument document,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đồng bộ bộ bản dịch với bản gốc mới từ game.
    /// <para>
    /// Giữ nguyên bản dịch cho key không đổi, thêm entry cho key mới,
    /// đánh dấu entry bị thay đổi nội dung gốc.
    /// </para>
    /// </summary>
    TranslationSyncResult Sync(
        TranslationDocument document,
        IEnumerable<TranslationEntry> currentOriginals);

    /// <summary>
    /// Lưu bộ bản dịch ra nhiều file, tách theo nhóm nội dung.
    /// </summary>
    Task<IReadOnlyList<string>> SaveSplitAsync(
        TranslationDocument document,
        string rootDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nạp bộ bản dịch từ nhiều file trong một thư mục (dạng tách theo nhóm).
    /// </summary>
    Task<TranslationDocument> LoadSplitAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default);
}
