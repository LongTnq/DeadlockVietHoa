namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Cung cấp bản dịch tự động cho một lô chuỗi tiếng Anh.
/// <para>
/// <b>Vì sao cần interface:</b> sprint này dùng Ollama (chạy local, miễn
/// phí) nhưng chất lượng tiếng Việt của model 7B chưa chắc đạt. Kiến trúc
/// này cho phép thêm provider khác (Claude API, OpenAI, Google Gemini…)
/// mà không phải sửa lại logic chia lô, guard hay lưu trữ — chỉ cài thêm
/// một implementation và chọn qua tham số <c>--provider</c>.
/// </para>
/// <para>
/// <b>Hợp đồng quan trọng:</b> provider <b>không</b> được sửa token hay thẻ
/// HTML trong chuỗi trả về. Việc đó là của
/// <see cref="ITranslationGuard"/> — nếu không, không thể phát hiện được
/// bản dịch hỏng.
/// </para>
/// </summary>
public interface ITranslationProvider
{
    /// <summary>
    /// Tên hiển thị của provider (dùng cho log và thông báo lỗi).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Dịch một lô chuỗi.
    /// <para>
    /// Thứ tự kết quả phải khớp với thứ tự đầu vào. Nếu một chuỗi không
    /// dịch được, trả về chuỗi rỗng tại vị trí tương ứng — caller sẽ
    /// loại bỏ và ghi log.
    /// </para>
    /// </summary>
    /// <param name="texts">Các chuỗi tiếng Anh cần dịch.</param>
    /// <param name="context">Ngữ cảnh cho lô (ví dụ tên nhóm nội dung), dùng để tinh chỉnh prompt.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    /// <returns>Danh sách bản dịch, cùng thứ tự với đầu vào.</returns>
    Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts,
        string? context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra provider có sẵn sàng nhận yêu cầu hay không
    /// (ví dụ: Ollama có đang chạy không, model đã được tải chưa).
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
