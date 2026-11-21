using DeadlockVN.Core.Entities;

namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Đọc nội dung tiếng Anh gốc từ thư mục localization đã extract.
/// </summary>
public interface ILocalizationSourceReader
{
    /// <summary>
    /// Đọc tất cả entry gốc từ một thư mục chứa các file
    /// <c>*_english.txt</c>, giữ nguyên cấu trúc thư mục con.
    /// </summary>
    Task<IReadOnlyList<TranslationEntry>> ReadAsync(
        string directory,
        CancellationToken cancellationToken = default);
}
