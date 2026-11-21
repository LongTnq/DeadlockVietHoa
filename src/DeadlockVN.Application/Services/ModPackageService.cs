using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.FileSystem;

namespace DeadlockVN.Application.Services;

/// <summary>
/// Đóng gói bộ bản dịch thành bộ cài đặt cho mod.
/// <para>
/// Quy trình:
/// </para>
/// <list type="number">
/// <item><description>
/// Xuất 11 file <c>*_vietnamese.txt</c> vào thư mục output, giữ nguyên
/// cấu trúc thư mục của game.
/// </description></item>
/// <item><description>
/// Vá <c>gameinfo.gi</c> để đăng ký ngôn ngữ mới (nếu được yêu cầu).
/// </description></item>
/// <item><description>
/// Vá <c>fonts.conf</c> để thêm font fallback (nếu được yêu cầu).
/// </description></item>
/// </list>
/// </summary>
public sealed class ModPackageService
{
    private readonly ITranslationStore _store;
    private readonly ILocalizationExporter _exporter;
    private readonly IGameConfigPatcher _configPatcher;

    public ModPackageService(
        ITranslationStore store,
        ILocalizationExporter exporter,
        IGameConfigPatcher configPatcher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _configPatcher = configPatcher ?? throw new ArgumentNullException(nameof(configPatcher));
    }

    public async Task<ModPackageResult> BuildAsync(
        ModPackageConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        ArgumentException.ThrowIfNullOrWhiteSpace(config.SourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.TranslationsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.OutputDirectory);

        var document = config.Split
            ? await _store.LoadSplitAsync(config.TranslationsDirectory, cancellationToken)
            : await _store.LoadAsync(config.TranslationsDirectory, cancellationToken);

        var entriesByFile = document.Entries
            .GroupBy(entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<TranslationEntry>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var result = new ModPackageResult
        {
            OutputDirectory = config.OutputDirectory
        };

        var sourceFiles = Directory
            .GetFiles(config.SourceDirectory, "*_english.txt", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path
                .GetRelativePath(config.SourceDirectory, sourceFile)
                .Replace('\\', '/');

            // Đường dẫn trong gói mod phải khớp với đường dẫn VPK:
            //   resource/localization/<nhóm>/<tên>_vietnamese.txt
            string outputRelativePath =
                $"{config.ResourceRoot.Trim('/')}/{relativePath}"
                    .Replace(
                        "_english.txt",
                        $"_{config.TargetLanguage}.txt",
                        StringComparison.OrdinalIgnoreCase);

            string outputPath = Path.Combine(
                config.OutputDirectory,
                outputRelativePath.Replace('/', Path.DirectorySeparatorChar));

            entriesByFile.TryGetValue(relativePath, out var entries);

            var fileResult = await _exporter.ExportAsync(
                sourceFile,
                outputPath,
                entries ?? Array.Empty<TranslationEntry>(),
                config.TargetLanguage,
                cancellationToken);

            result.LocalizationFileCount++;
            result.TranslatedEntries += fileResult.TranslatedEntries;
            result.TotalEntries += fileResult.TotalEntries;

            result.LocalizationFiles.Add(outputRelativePath);

            result.UnknownKeys.AddRange(
                fileResult.UnknownKeys.Select(key => $"{relativePath}#{key}"));
        }

        // Vá gameinfo.gi.
        if (config.PatchGameInfo)
        {
            string gameInfoPath = Path.Combine(
                config.GameDirectory,
                "citadel",
                "gameinfo.gi");

            if (File.Exists(gameInfoPath))
            {
                result.GameInfoPatch = await _configPatcher
                    .PatchSupportedLanguagesAsync(
                        gameInfoPath,
                        config.TargetLanguage,
                        cancellationToken);
            }
        }

        // Vá fonts.conf.
        if (config.PatchFonts)
        {
            string fontsConfPath = Path.Combine(
                config.GameDirectory,
                "citadel",
                "panorama",
                "fonts",
                "fonts.conf");

            if (File.Exists(fontsConfPath))
            {
                var fontConfig = new FontFallbackConfig
                {
                    LanguageCode = LocaleCodeFor(config.TargetLanguage)
                };

                // Chỉ vá khi có họ font THỰC SỰ thiếu glyph.
                //
                // Không nên đo bằng một file đại diện: các họ font khác nhau
                // có mức phủ rất khác nhau (đo thật trên bản cài game):
                //   VALVE Oracle / VALVE Pulp  -> 90/176 (thiếu 86 mã)
                //   Retail Text Demo / Retail Demo -> 45/176 (thiếu 131 mã)
                //   Jost -> 86/176 (thiếu 90 mã)
                //   Noto Sans JP/KR/SC/TC, Hahmlet, Radiance, Reaver -> 176/176
                //
                // Nếu chỉ kiểm tra "VALVE Oracle" thì sẽ bỏ sót
                // VALVE Pulp và Retail Text Demo -> ký tự vuông □ trong
                // tiêu đề và heading.
                string fontsDirectory = Path.Combine(
                    config.GameDirectory,
                    "citadel",
                    "panorama",
                    "fonts");

                var deficientFamilies = FindDeficientFontFamilies(
                    fontsDirectory,
                    fontConfig.BaseFontFamilies);

                if (deficientFamilies.Count > 0)
                {
                    // Chỉ vá những họ font thực sự thiếu glyph.
                    fontConfig.BaseFontFamilies = deficientFamilies;

                    result.FontsPatch = await _configPatcher
                        .PatchFontFallbackAsync(
                            fontsConfPath,
                            fontConfig,
                            cancellationToken);

                    // Copy fonts.conf đã vá vào staging để có thể đóng gói
                    // cùng VPK nếu người dùng muốn.
                    string fontsOutputPath = Path.Combine(
                        config.OutputDirectory,
                        "panorama",
                        "fonts",
                        "fonts.conf");

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(fontsOutputPath)!);

                    byte[] fontsBytes = await File.ReadAllBytesAsync(
                        fontsConfPath,
                        cancellationToken);

                    await File.WriteAllBytesAsync(
                        fontsOutputPath,
                        fontsBytes,
                        cancellationToken);

                    result.FontsConfRelativePath = "panorama/fonts/fonts.conf";
                    result.DeficientFontFamilies = deficientFamilies;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Tìm các họ font trong danh sách mà file font tương ứng <b>không</b> phủ
    /// đủ bộ ký tự tiếng Việt.
    /// <para>
    /// Ánh xạ tên họ font (theo cách game khai báo trong <c>fonts.conf</c>)
    /// sang tên file font trong thư mục <c>panorama/fonts</c>.
    /// </para>
    /// </summary>
    private static List<string> FindDeficientFontFamilies(
        string fontsDirectory,
        IReadOnlyList<string> families)
    {
        var deficient = new List<string>();

        foreach (string family in families)
        {
            // Chọn file font đại diện: file đầu tiên khớp tên họ.
            string? fontFile = FindFontFile(fontsDirectory, family);

            if (fontFile is null)
            {
                // Không tìm thấy file font cho họ này -> không chắc chắn
                // an toàn -> coi như thiếu để vá (fallback vẫn vô hại).
                deficient.Add(family);

                continue;
            }

            if (!FontCoverageChecker.CoversVietnamese(fontFile))
            {
                deficient.Add(family);
            }
        }

        return deficient;
    }

    /// <summary>
    /// Tìm file font đại diện cho một họ font.
    /// <para>
    /// Tên file bỏ dấu gạch và viết thường, ví dụ họ <c>Retail Text Demo</c>
    /// khớp <c>retailtextdemo-regular.otf</c>.
    /// </para>
    /// </summary>
    private static string? FindFontFile(string fontsDirectory, string family)
    {
        if (!Directory.Exists(fontsDirectory))
        {
            return null;
        }

        string normalized = Normalize(family);

        return Directory
            .EnumerateFiles(fontsDirectory)
            .Where(path =>
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();

                return extension is ".ttf" or ".otf";
            })
            .Where(path =>
                Normalize(Path.GetFileNameWithoutExtension(path))
                    .StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// Chuẩn hoá tên: bỏ khoảng trắng, gạch nối và gạch dưới.
    /// </summary>
    private static string Normalize(string value)
    {
        return new string(
            value
                .Where(char.IsLetterOrDigit)
                .ToArray())
            .ToLowerInvariant();
    }

    /// <summary>
    /// Ánh xạ mã ngôn ngữ của game sang mã locale chuẩn dùng trong
    /// <c>fonts.conf</c>.
    /// </summary>
    private static string LocaleCodeFor(string languageCode)
    {
        return languageCode.ToLowerInvariant() switch
        {
            "vietnamese" => "vi-vn",
            "thai" => "th-th",
            "indonesian" => "id-id",
            "koreana" => "ko-kr",
            "schinese" => "zh-cn",
            "tchinese" => "zh-tw",
            "brazilian" => "pt-br",
            "latam" => "es-419",
            "ukrainian" => "uk-ua",
            "polish" => "pl-pl",
            "czech" => "cs-cz",
            "turkish" => "tr-tr",
            "romanian" => "ro-ro",
            "hungarian" => "hu-hu",
            "danish" => "da-dk",
            "dutch" => "nl-nl",
            "finnish" => "fi-fi",
            "norwegian" => "no-no",
            "swedish" => "sv-se",
            "greek" => "el-gr",
            "bulgarian" => "bg-bg",
            "portuguese" => "pt-pt",
            "japanese" => "ja-jp",
            "russian" => "ru-ru",
            "german" => "de-de",
            "french" => "fr-fr",
            "italian" => "it-it",
            "spanish" => "es-es",
            "english" => "en-us",
            _ => "vi-vn"
        };
    }
}
