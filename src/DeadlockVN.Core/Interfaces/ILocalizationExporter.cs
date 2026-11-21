using DeadlockVN.Core.Entities;

namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Xuất bộ bản dịch ra file KeyValues mà game đọc được.
/// <para>
/// <b>Nguyên tắc bất di bất dịch:</b> chỉ thay đổi <b>value</b>, giữ nguyên
/// key, cấu trúc cây, comment, thụt lề và thứ tự escape của file gốc.
/// Việc này được thực hiện bằng cách thay thế <i>tại chỗ</i> trên chuỗi gốc
/// dựa trên offset của từng value — không serialize lại toàn bộ cây.
/// </para>
/// </summary>
public interface ILocalizationExporter
{
    /// <summary>
    /// Xuất một file KeyValues đã dịch.
    /// </summary>
    /// <param name="sourcePath">Đường dẫn file gốc (tiếng Anh) trên đĩa.</param>
    /// <param name="outputPath">Đường dẫn file đích cần ghi.</param>
    /// <param name="entries">
    /// Các entry bản dịch thuộc file nguồn tương ứng. Key được so khớp
    /// theo <see cref="TranslationEntry.Key"/>.
    /// </param>
    /// <param name="targetLanguage">
    /// Tên ngôn ngữ đích ghi vào header <c>"Language"</c> (ví dụ <c>vietnamese</c>).
    /// </param>
    /// <param name="cancellationToken">Token hủy.</param>
    /// <returns>Kết quả xuất.</returns>
    Task<LocalizationExportResult> ExportAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyCollection<TranslationEntry> entries,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}
