using DeadlockVN.Core.Entities;

namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Kiểm tra bản dịch do máy sinh ra có hợp lệ hay không.
/// <para>
/// <b>Vì sao cần:</b> model ngôn ngữ rất dễ làm hỏng cấu trúc của chuỗi
/// localization — đặc biệt là:
///
/// <list type="bullet">
///   <item><description>
///     Bỏ hoặc thêm placeholder (<c>{s:damage}</c>, <c>%s1</c>,
///     <c>{g:key:'value'}</c>). Mất placeholder = game hiển thị sai giá trị.
///   </description></item>
///   <item><description>
///     Bỏ hoặc thêm thẻ HTML/Panorama (<c>&lt;b&gt;</c>, <c>&lt;span&gt;</c>,
///     <c>&lt;br&gt;</c>). Mất thẻ = mất định dạng, hỏng layout.
///   </description></item>
///   <item><description>
///     Thêm bớt chữ, hoặc bỏ trống.
///   </description></item>
/// </list>
///
/// Vì vậy mọi bản dịch tự động <b>phải</b> đi qua guard trước khi được ghi
/// vào cơ sở dữ liệu. Bản dịch hỏng không bao giờ được lưu — tốt hơn là
/// để trống và dịch lại.
/// </para>
/// </summary>
public interface ITranslationGuard
{
    /// <summary>
    /// Kiểm tra một bản dịch so với bản gốc.
    /// </summary>
    /// <param name="original">Chuỗi tiếng Anh gốc.</param>
    /// <param name="translated">Chuỗi tiếng Việt do máy sinh ra.</param>
    /// <returns>
    /// Kết quả kiểm tra. <see cref="TranslationCheck.IsValid"/> là
    /// <c>false</c> nếu có bất kỳ lỗi nghiêm trọng nào.
    /// </returns>
    TranslationCheck Check(string original, string translated);
}
