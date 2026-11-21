namespace DeadlockVN.Core.Entities;

/// <summary>
/// Cấu hình để tạo bộ cài đặt cho mod (dùng để đóng gói VPK hoặc chép thẳng).
/// </summary>
public sealed class ModPackageConfig
{
    /// <summary>
    /// Thư mục gốc chứa file tiếng Anh đã trích xuất.
    /// </summary>
    public string SourceDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Thư mục gốc của game, ví dụ <c>E:\Steam\steamapps\common\Deadlock\game</c>.
    /// </summary>
    public string GameDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Thư mục chứa bản dịch (JSON đã tách theo nhóm hoặc file gộp).
    /// </summary>
    public string TranslationsDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Thư mục output — chứa cấu trúc sẵn sàng để đóng gói VPK.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Mã ngôn ngữ đích, ví dụ <c>vietnamese</c>.
    /// </summary>
    public string TargetLanguage { get; set; } = "vietnamese";

    /// <summary>
    /// <c>true</c> nếu bản dịch được lưu tách theo nhóm.
    /// </summary>
    public bool Split { get; set; } = true;

    /// <summary>
    /// <c>true</c> nếu cần vá <c>gameinfo.gi</c> để đăng ký ngôn ngữ.
    /// </summary>
    public bool PatchGameInfo { get; set; } = true;

    /// <summary>
    /// <c>true</c> nếu cần vá <c>fonts.conf</c> để thêm font fallback.
    /// </summary>
    public bool PatchFonts { get; set; } = true;

    /// <summary>
    /// Tiền tố thư mục trong gói mod. File localization của game nằm ở
    /// <c>resource/localization/&lt;nhóm&gt;/</c> nên giá trị mặc định phải
    /// khớp với đường dẫn VPK cuối cùng.
    /// </summary>
    public string ResourceRoot { get; set; } = "resource/localization";
}
