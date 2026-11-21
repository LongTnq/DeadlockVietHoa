using DeadlockVN.Core.Entities;

namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Vá các file cấu hình của game để bật ngôn ngữ mới (ví dụ tiếng Việt).
/// <para>
/// Deadlock đọc danh sách ngôn ngữ từ <c>gameinfo.gi</c> và cấu hình font
/// fallback từ <c>panorama/fonts/fonts.conf</c>. Không vá hai file này thì
/// game sẽ không hiển thị tiếng Việt dù file localization đã đúng.
/// </para>
/// </summary>
public interface IGameConfigPatcher
{
    /// <summary>
    /// Thêm ngôn ngữ vào <c>SupportedLanguages</c> của <c>gameinfo.gi</c>.
    /// </summary>
    /// <param name="gameInfoPath">Đường dẫn tới file <c>gameinfo.gi</c>.</param>
    /// <param name="languageCode">Mã ngôn ngữ, ví dụ <c>vietnamese</c>.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    /// <returns>Kết quả vá.</returns>
    Task<ConfigPatchResult> PatchSupportedLanguagesAsync(
        string gameInfoPath,
        string languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm khối font fallback cho ngôn ngữ vào <c>fonts.conf</c>.
    /// </summary>
    /// <param name="fontsConfPath">Đường dẫn tới file <c>fonts.conf</c>.</param>
    /// <param name="fontConfig">Cấu hình fallback.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    /// <returns>Kết quả vá.</returns>
    Task<ConfigPatchResult> PatchFontFallbackAsync(
        string fontsConfPath,
        FontFallbackConfig fontConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra một font có phủ đủ ký tự của ngôn ngữ hay không.
    /// <para>
    /// Font chính của game (<c>VALVE Oracle</c>) đã phủ 100% tiếng Việt,
    /// nên thường <b>không</b> cần font fallback. Phương thức này giúp
    /// tránh thêm khối fallback thừa.
    /// </para>
    /// </summary>
    /// <param name="fontFilePath">Đường dẫn tới file font (<c>.ttf</c>/<c>.otf</c>).</param>
    /// <param name="characters">Các ký tự cần kiểm tra.</param>
    /// <returns>
    /// <c>true</c> nếu font phủ đủ mọi ký tự; <c>false</c> nếu thiếu.
    /// </returns>
    bool FontCoversCharacters(string fontFilePath, IEnumerable<char> characters);
}
