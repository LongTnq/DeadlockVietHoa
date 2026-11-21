using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Infrastructure.Exporting;
using DeadlockVN.Infrastructure.FileSystem;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="ModPackageService"/> — đóng gói bộ cài đặt cho mod.
/// </summary>
public sealed class ModPackageServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public ModPackageServiceTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-pkg-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string CreateSourceDirectory()
    {
        string source = Path.Combine(_tempDirectory, "original");

        Directory.CreateDirectory(Path.Combine(source, "citadel_main"));
        Directory.CreateDirectory(Path.Combine(source, "citadel_heroes"));

        File.WriteAllText(
            Path.Combine(source, "citadel_main", "citadel_main_english.txt"),
            "\"lang\"\r\n{\r\n\t\"Language\"\t\"English\"\r\n\t\"Tokens\"\r\n\t{\r\n" +
            "\t\t\"#Game_Hello\"\t\"Hello\"\r\n\t\t\"#Game_Bye\"\t\"Goodbye\"\r\n" +
            "\t}\r\n}\r\n",
            new System.Text.UTF8Encoding(false));

        File.WriteAllText(
            Path.Combine(source, "citadel_heroes", "citadel_heroes_english.txt"),
            "\"lang\"\r\n{\r\n\t\"Language\"\t\"English\"\r\n\t\"Tokens\"\r\n\t{\r\n" +
            "\t\t\"#Hero_Abrams\"\t\"Abrams\"\r\n\t}\r\n}\r\n",
            new System.Text.UTF8Encoding(false));

        return source;
    }

    private string CreateTranslationsDirectory()
    {
        string translations = Path.Combine(_tempDirectory, "translations");

        Directory.CreateDirectory(translations);

        File.WriteAllText(
            Path.Combine(translations, "citadel_main.json"),
            "{\n" +
            "  \"metadata\": {\n" +
            "    \"sourceLanguage\": \"English\",\n" +
            "    \"targetLanguage\": \"Vietnamese\",\n" +
            "    \"version\": \"0.1.0\"\n" +
            "  },\n" +
            "  \"entries\": [\n" +
            "    {\n" +
            "      \"key\": \"#Game_Hello\",\n" +
            "      \"sourceFile\": \"citadel_main/citadel_main_english.txt\",\n" +
            "      \"originalText\": \"Hello\",\n" +
            "      \"translatedText\": \"Xin chào\",\n" +
            "      \"status\": \"Approved\"\n" +
            "    }\n" +
            "  ]\n" +
            "}\n");

        return translations;
    }

    private static ModPackageService CreateService()
    {
        return new ModPackageService(
            new JsonTranslationStore(),
            new LocalizationExporter(),
            new GameConfigPatcher());
    }

    [Fact]
    public async Task BuildAsync_CreatesVietnameseFilesWithGameLayout()
    {
        string source = CreateSourceDirectory();
        string translations = CreateTranslationsDirectory();
        string output = Path.Combine(_tempDirectory, "staging");

        var config = new ModPackageConfig
        {
            SourceDirectory = source,
            TranslationsDirectory = translations,
            OutputDirectory = output,
            GameDirectory = string.Empty,
            TargetLanguage = "vietnamese"
        };

        ModPackageResult result = await CreateService().BuildAsync(config);

        Assert.Equal(2, result.LocalizationFileCount);

        string mainOutput = Path.Combine(
            output,
            "resource",
            "localization",
            "citadel_main",
            "citadel_main_vietnamese.txt");

        Assert.True(File.Exists(mainOutput));

        string heroesOutput = Path.Combine(
            output,
            "resource",
            "localization",
            "citadel_heroes",
            "citadel_heroes_vietnamese.txt");

        Assert.True(File.Exists(heroesOutput));

        // Đường dẫn tương đối trong gói phải khớp với đường dẫn VPK.
        Assert.Contains(
            "resource/localization/citadel_main/citadel_main_vietnamese.txt",
            result.LocalizationFiles);
    }

    [Fact]
    public async Task BuildAsync_AppliesTranslations()
    {
        string source = CreateSourceDirectory();
        string translations = CreateTranslationsDirectory();
        string output = Path.Combine(_tempDirectory, "staging");

        var config = new ModPackageConfig
        {
            SourceDirectory = source,
            TranslationsDirectory = translations,
            OutputDirectory = output,
            GameDirectory = string.Empty
        };

        ModPackageResult result = await CreateService().BuildAsync(config);

        Assert.Equal(1, result.TranslatedEntries);

        // 2 entry trong citadel_main + 1 entry trong citadel_heroes.
        Assert.Equal(3, result.TotalEntries);

        string content = File.ReadAllText(Path.Combine(
            output,
            "resource",
            "localization",
            "citadel_main",
            "citadel_main_vietnamese.txt"));

        Assert.Contains("Xin chào", content);
        Assert.Contains("Goodbye", content);

        // Header Language phải đổi sang vietnamese.
        Assert.Contains("\"Language\"\t\"vietnamese\"", content);
    }

    [Fact]
    public async Task BuildAsync_ReportsUnknownKeys()
    {
        string source = CreateSourceDirectory();

        string translations = Path.Combine(_tempDirectory, "translations");

        Directory.CreateDirectory(translations);

        File.WriteAllText(
            Path.Combine(translations, "bad.json"),
            "{\n" +
            "  \"metadata\": {\n" +
            "    \"sourceLanguage\": \"English\",\n" +
            "    \"targetLanguage\": \"Vietnamese\",\n" +
            "    \"version\": \"0.1.0\"\n" +
            "  },\n" +
            "  \"entries\": [\n" +
            "    {\n" +
            "      \"key\": \"#Khong_Ton_Tai\",\n" +
            "      \"sourceFile\": \"citadel_main/citadel_main_english.txt\",\n" +
            "      \"originalText\": \"???\",\n" +
            "      \"translatedText\": \"Không có\",\n" +
            "      \"status\": \"Approved\"\n" +
            "    }\n" +
            "  ]\n" +
            "}\n");

        string output = Path.Combine(_tempDirectory, "staging");

        var config = new ModPackageConfig
        {
            SourceDirectory = source,
            TranslationsDirectory = translations,
            OutputDirectory = output,
            GameDirectory = string.Empty
        };

        ModPackageResult result = await CreateService().BuildAsync(config);

        Assert.Single(result.UnknownKeys);
        Assert.Contains("#Khong_Ton_Tai", result.UnknownKeys[0]);
    }

    [Fact]
    public async Task BuildAsync_PatchesGameInfoAndFonts()
    {
        string source = CreateSourceDirectory();
        string translations = CreateTranslationsDirectory();
        string output = Path.Combine(_tempDirectory, "staging");

        // Dựng bản sao "game" giả lập.
        string game = Path.Combine(_tempDirectory, "game");

        Directory.CreateDirectory(Path.Combine(game, "citadel"));
        Directory.CreateDirectory(Path.Combine(game, "citadel", "panorama", "fonts"));

        string gameInfoPath = Path.Combine(game, "citadel", "gameinfo.gi");

        File.WriteAllText(
            gameInfoPath,
            "\tSupportedLanguages\r\n\t{\r\n\t\t\"english\" \"3\"\r\n\t}\r\n",
            new System.Text.UTF8Encoding(false));

        string fontsPath = Path.Combine(game, "citadel", "panorama", "fonts", "fonts.conf");

        File.WriteAllText(
            fontsPath,
            "<fontconfig>\r\n</fontconfig>\r\n",
            new System.Text.UTF8Encoding(false));

        var config = new ModPackageConfig
        {
            SourceDirectory = source,
            TranslationsDirectory = translations,
            OutputDirectory = output,
            GameDirectory = game,
            TargetLanguage = "vietnamese"
        };

        ModPackageResult result = await CreateService().BuildAsync(config);

        Assert.NotNull(result.GameInfoPatch);
        Assert.True(result.GameInfoPatch.Changed);
        Assert.Contains("\"vietnamese\" \"3\"", File.ReadAllText(gameInfoPath));

        Assert.NotNull(result.FontsPatch);
        Assert.True(result.FontsPatch.Changed);
        Assert.Contains("<string>vi-vn</string>", File.ReadAllText(fontsPath));

        // fonts.conf đã vá cũng được copy vào staging.
        Assert.Equal("panorama/fonts/fonts.conf", result.FontsConfRelativePath);
        Assert.True(File.Exists(Path.Combine(output, "panorama", "fonts", "fonts.conf")));
    }

    [Fact]
    public async Task BuildAsync_SkipsPatchesWhenGameDirectoryMissing()
    {
        string source = CreateSourceDirectory();
        string translations = CreateTranslationsDirectory();
        string output = Path.Combine(_tempDirectory, "staging");

        var config = new ModPackageConfig
        {
            SourceDirectory = source,
            TranslationsDirectory = translations,
            OutputDirectory = output,
            GameDirectory = Path.Combine(_tempDirectory, "khong-co"),
            PatchGameInfo = true,
            PatchFonts = true
        };

        ModPackageResult result = await CreateService().BuildAsync(config);

        Assert.Null(result.GameInfoPatch);
        Assert.Null(result.FontsPatch);
        Assert.Equal(2, result.LocalizationFileCount);
    }
}
