namespace DeadlockVN.Core.Entities;

/// <summary>
/// Cấu hình font fallback cho một ngôn ngữ.
/// <para>
/// <b>Sự thật đo được trên bản cài game thật</b> (đo bằng <c>fontTools</c>
/// trên bảng <c>cmap</c> của từng file font, bộ 176 mã tiếng Việt):
/// </para>
/// <list type="table">
///   <item><term><c>VALVE Oracle</c> (6 weight)</term>
///     <description>90/176 — thiếu <b>86</b> mã (mọi nguyên âm móc
///     <c>ơ ư ờ ở ị ọ ộ ụ ữ</c> và các tổ hợp <c>U+1EA0–U+1EF9</c>).</description></item>
///   <item><term><c>VALVE Pulp</c></term>
///     <description>90/176 — thiếu <b>86</b> mã, y hệt VALVE Oracle.</description></item>
///   <item><term><c>Retail Text Demo</c>, <c>Retail Demo</c></term>
///     <description>45/176 — thiếu <b>131</b> mã.</description></item>
///   <item><term><c>Jost</c> (mọi weight)</term>
///     <description>86/176 — thiếu <b>90</b> mã.</description></item>
///   <item><term><c>Noto Sans JP/KR/SC/TC</c>, <c>Hahmlet</c>,
///   <c>Radiance</c>, <c>Reaver</c></term>
///     <description>176/176 — phủ đầy đủ, dùng làm font thay thế.</description></item>
/// </list>
/// <para>
/// Vì vậy khối fallback phải áp cho <b>nhiều</b> họ font, không chỉ
/// <c>VALVE Oracle</c> — nếu không, tiêu đề in bằng <c>VALVE Pulp</c> hoặc
/// <c>Retail Text Demo</c> sẽ hiện ký tự vuông □.
/// </para>
/// </summary>
public sealed class FontFallbackConfig
{
    /// <summary>
    /// Mã ngôn ngữ theo chuẩn locale, ví dụ <c>vi-vn</c>.
    /// </summary>
    public string LanguageCode { get; set; } = "vi-vn";

    /// <summary>
    /// Danh sách họ font của game cần bổ sung font thay thế.
    /// <para>
    /// Mỗi phần tử là tên họ font theo đúng cách game khai báo trong
    /// <c>fonts.conf</c> (ví dụ <c>VALVEOracle</c>, <c>VALVEPulp</c>,
    /// <c>Retail Text Demo</c>).
    /// </para>
    /// </summary>
    public List<string> BaseFontFamilies { get; set; } = new()
    {
        "VALVEOracle",
        "VALVEPulp",
        "Retail Text Demo",
        "Retail Demo"
    };

    /// <summary>
    /// Tên font thay thế có đủ glyph. Game <b>không</b> có font
    /// "Noto Sans" tổng quát — chỉ có <c>notosansjp/kr/sc/tc/thai</c>.
    /// <c>Noto Sans JP</c> phủ 100% ký tự Latin mở rộng nên là lựa chọn
    /// an toàn nhất.
    /// </summary>
    public string FallbackFontFamily { get; set; } = "Noto Sans JP";

    /// <summary>
    /// Kiểu binding của fontconfig: <c>strong</c>, <c>weak</c> hoặc
    /// <c>same</c>.
    /// </summary>
    public string Binding { get; set; } = "strong";

    /// <summary>
    /// Các họ font dùng <c>compare="contains"</c> khi khớp.
    /// <para>
    /// Cần cho <c>VALVEPulp</c> và <c>Retail Text Demo</c>: trong
    /// <c>fonts.conf</c> của game, fontconfig khai báo các weight như
    /// <c>VALVEPulp-Bold</c>, <c>RetailTextDemo-Regular</c>… nên khớp
    /// chính xác tên họ sẽ bỏ sót. Game cũng dùng
    /// <c>compare="contains"</c> cho chính các họ font này (xem khối
    /// <c>ja-jp</c>/<c>ko-kr</c>/<c>zh-cn</c> trong file gốc).
    /// </para>
    /// </summary>
    public HashSet<string> ContainsMatchFamilies { get; set; } = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "VALVEPulp",
        "Retail Text Demo",
        "Retail Demo"
    };
}
