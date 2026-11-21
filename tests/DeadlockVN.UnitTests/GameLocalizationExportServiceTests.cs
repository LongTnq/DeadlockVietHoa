using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Exporting;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="GameLocalizationExportService"/> — xuất bộ bản dịch ra
/// thư mục staging theo cấu trúc file của game.
/// </summary>
public sealed class GameLocalizationExportServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public GameLocalizationExportServiceTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-gameservice-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string CreateSourceFile(string relativePath, string content)
    {
        string path = Path.Combine(
            _tempDirectory,
            "source",
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));

        return path;
    }

    private string CreateTranslationFile(params TranslationEntry[] entries)
    {
        string path = Path.Combine(_tempDirectory, "translations.json");

        var document = new TranslationDocument
        {
            SourceLanguage = "English",
            TargetLanguage = "Vietnamese"
        };

        document.Entries.AddRange(entries);

        var store = new JsonTranslationStore();

        store.SaveAsync(document, path).GetAwaiter().GetResult();

        return path;
    }

    [Fact]
    public async Task Export_CreatesVietnameseFileNextToSourceLayout()
    {
        CreateSourceFile(
            "citadel_main/citadel_main_english.txt",
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n\t\t\"OK\" \"OK\"\r\n\t}\r\n}\r\n");

        string translationsPath = CreateTranslationFile(new TranslationEntry
        {
            Id = new TranslationId(
                "citadel_main/citadel_main_english.txt",
                "OK"),
            OriginalText = "OK",
            TranslatedText = "Đồng ý",
            Status = TranslationStatus.Approved
        });

        string outputDirectory = Path.Combine(_tempDirectory, "staging");

        var service = new GameLocalizationExportService(
            new JsonTranslationStore(),
            new LocalizationExporter());

        var result = await service.ExportAsync(
            Path.Combine(_tempDirectory, "source"),
            translationsPath,
            outputDirectory);

        Assert.Equal(1, result.FileCount);
        Assert.Equal(1, result.TranslatedEntries);
        Assert.Empty(result.UnknownKeys);

        string expected = Path.Combine(
            outputDirectory,
            "citadel_main",
            "citadel_main_vietnamese.txt");

        Assert.True(File.Exists(expected));

        string content = await File.ReadAllTextAsync(expected);

        Assert.Contains("\"Đồng ý\"", content);
        Assert.Contains("\"Language\" \"vietnamese\"", content);
    }

    [Fact]
    public async Task Export_ReportsKeyThatDoesNotExistInSource()
    {
        CreateSourceFile(
            "citadel_main/citadel_main_english.txt",
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n\t\t\"OK\" \"OK\"\r\n\t}\r\n}\r\n");

        string translationsPath = CreateTranslationFile(new TranslationEntry
        {
            Id = new TranslationId(
                "citadel_main/citadel_main_english.txt",
                "ghost_key"),
            OriginalText = "ghost",
            TranslatedText = "ma",
            Status = TranslationStatus.Approved
        });

        string outputDirectory = Path.Combine(_tempDirectory, "staging");

        var service = new GameLocalizationExportService(
            new JsonTranslationStore(),
            new LocalizationExporter());

        var result = await service.ExportAsync(
            Path.Combine(_tempDirectory, "source"),
            translationsPath,
            outputDirectory);

        Assert.Contains(
            result.UnknownKeys,
            key => key.Contains("ghost_key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_KeepsUntranslatedEntriesInEnglish()
    {
        CreateSourceFile(
            "citadel_main/citadel_main_english.txt",
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n\t\t\"OK\" \"OK\"\r\n\t\t\"Cancel\" \"Cancel\"\r\n\t}\r\n}\r\n");

        string translationsPath = CreateTranslationFile(new TranslationEntry
        {
            Id = new TranslationId(
                "citadel_main/citadel_main_english.txt",
                "OK"),
            OriginalText = "OK",
            TranslatedText = "Đồng ý",
            Status = TranslationStatus.Approved
        });

        string outputDirectory = Path.Combine(_tempDirectory, "staging");

        var service = new GameLocalizationExportService(
            new JsonTranslationStore(),
            new LocalizationExporter());

        var result = await service.ExportAsync(
            Path.Combine(_tempDirectory, "source"),
            translationsPath,
            outputDirectory);

        Assert.Equal(1, result.TranslatedEntries);
        Assert.Equal(1, result.UntranslatedEntries);

        string content = await File.ReadAllTextAsync(
            Path.Combine(
                outputDirectory,
                "citadel_main",
                "citadel_main_vietnamese.txt"));

        Assert.Contains("\"Cancel\" \"Cancel\"", content);
    }
}
