using System.Text;

using DeadlockVN.Core.Entities;
using DeadlockVN.Infrastructure.Exporting;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="LocalizationExporter"/> — xuất bản dịch ra file KV1 mà
/// game đọc được.
/// <para>
/// Nguyên tắc quan trọng nhất: <b>chỉ value được thay</b>. Key, cấu trúc cây,
/// comment, thụt lề và thứ tự escape phải giữ nguyên tuyệt đối.
/// </para>
/// <para>
/// Lưu ý: dùng string literal thường (không phải raw string) để
/// <c>\r\n</c> được hiểu đúng là ký tự thật — file game dùng CRLF.
/// </para>
/// </summary>
public sealed class LocalizationExporterTests : IDisposable
{
    private readonly string _tempDirectory;

    public LocalizationExporterTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-exporter-tests",
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

    private string WriteSource(string content, bool withBom = true)
    {
        string path = Path.Combine(_tempDirectory, "source.txt");

        File.WriteAllText(path, content, new UTF8Encoding(withBom));

        return path;
    }

    private static TranslationEntry Entry(string key, string translated)
    {
        return new TranslationEntry
        {
            Id = new TranslationId("file.txt", key),
            OriginalText = "original",
            TranslatedText = translated,
            Status = TranslationStatus.Approved
        };
    }

    private const string SimpleSource =
        "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n\t\t\"upgrade_close_range\" \"Close Quarters\"\r\n\t}\r\n}\r\n";

    [Fact]
    public async Task Export_WithoutTranslations_KeepsFileByteIdentical()
    {
        string sourcePath = WriteSource(SimpleSource);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        var result = await exporter.ExportAsync(
            sourcePath,
            outputPath,
            Array.Empty<TranslationEntry>(),
            "vietnamese");

        Assert.True(result.IsValid);
        Assert.Equal(1, result.TotalEntries);
        Assert.Equal(0, result.TranslatedEntries);
        Assert.Equal(1, result.UntranslatedEntries);
        Assert.True(result.LanguageHeaderRewritten);

        // Chỉ khác ở giá trị "Language" (đổi sang ngôn ngữ đích),
        // mọi thứ khác phải giống hệt.
        string output = await File.ReadAllTextAsync(outputPath);

        Assert.Equal(
            SimpleSource.Replace("\"english\"", "\"vietnamese\""),
            output);
    }

    [Fact]
    public async Task Export_PreservesCommentsIndentationAndEscapes()
    {
        const string source =
            "\"lang\"\r\n" +
            "{\r\n" +
            "\t\"Language\"\t\"english\"\r\n" +
            "\t\"Tokens\"\r\n" +
            "\t{\r\n" +
            "\t\t// comment giữ nguyên\r\n" +
            "\t\t\"key_one\"   \"value one\"\r\n" +
            "\t\t\"key_two\"\t\"has \\\"quotes\\\" and \\\\ backslash\"\r\n" +
            "\t}\r\n" +
            "}\r\n";

        string sourcePath = WriteSource(source);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        await exporter.ExportAsync(
            sourcePath,
            outputPath,
            new[] { Entry("key_one", "giá trị một") },
            "vietnamese");

        string output = await File.ReadAllTextAsync(outputPath);

        // Comment giữ nguyên.
        Assert.Contains("// comment giữ nguyên", output);

        // Thụt lề tab giữ nguyên.
        Assert.Contains("\t\t\"key_one\"   \"giá trị một\"", output);

        // Value khác dịch giữ nguyên cả escape.
        Assert.Contains("\"has \\\"quotes\\\" and \\\\ backslash\"", output);
    }

    [Fact]
    public async Task Export_ReplacesOnlyTargetedValue()
    {
        const string source =
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n" +
            "\t\t\"a\" \"one\"\r\n\t\t\"b\" \"two\"\r\n\t\t\"c\" \"three\"\r\n\t}\r\n}\r\n";

        string sourcePath = WriteSource(source);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        await exporter.ExportAsync(
            sourcePath,
            outputPath,
            new[] { Entry("b", "hai") },
            "vietnamese");

        string output = await File.ReadAllTextAsync(outputPath);

        Assert.Contains("\"a\" \"one\"", output);
        Assert.Contains("\"b\" \"hai\"", output);
        Assert.Contains("\"c\" \"three\"", output);
    }

    [Fact]
    public async Task Export_EscapesQuotesAndBackslashesInTranslation()
    {
        string sourcePath = WriteSource(SimpleSource);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        await exporter.ExportAsync(
            sourcePath,
            outputPath,
            new[] { Entry("upgrade_close_range", "nói \"xin chào\" và \\ dấu") },
            "vietnamese");

        string output = await File.ReadAllTextAsync(outputPath);

        Assert.Contains("\"nói \\\"xin chào\\\" và \\\\ dấu\"", output);
    }

    [Fact]
    public async Task Export_KeepsPlaceholdersUnescaped()
    {
        const string source =
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n" +
            "\t\t\"key\" \"Deals {s:damage} damage.\"\r\n\t}\r\n}\r\n";

        string sourcePath = WriteSource(source);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        await exporter.ExportAsync(
            sourcePath,
            outputPath,
            new[] { Entry("key", "Gây {s:damage} sát thương.") },
            "vietnamese");

        string output = await File.ReadAllTextAsync(outputPath);

        Assert.Contains("\"Gây {s:damage} sát thương.\"", output);
    }

    [Fact]
    public async Task Export_PreservesByteOrderMarkState()
    {
        // Có BOM
        string withBomPath = WriteSource(SimpleSource, withBom: true);
        string withBomOutput = Path.Combine(_tempDirectory, "bom.txt");

        var exporter = new LocalizationExporter();

        var bomResult = await exporter.ExportAsync(
            withBomPath,
            withBomOutput,
            Array.Empty<TranslationEntry>(),
            "vietnamese");

        Assert.True(bomResult.HadByteOrderMark);

        byte[] bomBytes = await File.ReadAllBytesAsync(withBomOutput);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bomBytes.Take(3));

        // Không BOM
        string noBomPath = WriteSource(SimpleSource, withBom: false);
        string noBomOutput = Path.Combine(_tempDirectory, "nobom.txt");

        var noBomResult = await exporter.ExportAsync(
            noBomPath,
            noBomOutput,
            Array.Empty<TranslationEntry>(),
            "vietnamese");

        Assert.False(noBomResult.HadByteOrderMark);

        byte[] noBomBytes = await File.ReadAllBytesAsync(noBomOutput);
        Assert.NotEqual(0xEF, noBomBytes[0]);
    }

    [Fact]
    public async Task Export_ReportsUnknownAndMissingKeys()
    {
        const string source =
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n" +
            "\t\t\"known\" \"value\"\r\n\t\t\"other\" \"value\"\r\n\t}\r\n}\r\n";

        string sourcePath = WriteSource(source);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        var result = await exporter.ExportAsync(
            sourcePath,
            outputPath,
            new[] { Entry("known", "đã dịch"), Entry("ghost", "không tồn tại") },
            "vietnamese");

        Assert.False(result.IsValid);
        Assert.Contains("ghost", result.UnknownKeys);
        Assert.Contains("other", result.MissingKeys);
        Assert.Equal(1, result.TranslatedEntries);
    }

    [Fact]
    public async Task Export_OnlyRewritesRootLevelLanguageKey()
    {
        const string source =
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n" +
            "\t\t\"Language\" \"not the header\"\r\n" +
            "\t\t\"citadel_settings_language\" \"Language\"\r\n\t}\r\n}\r\n";

        string sourcePath = WriteSource(source);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        await exporter.ExportAsync(
            sourcePath,
            outputPath,
            Array.Empty<TranslationEntry>(),
            "vietnamese");

        string output = await File.ReadAllTextAsync(outputPath);

        // Key Language ở cấp gốc được đổi.
        Assert.Contains("\"Language\" \"vietnamese\"", output);

        // Key Language sâu hơn KHÔNG bị đổi.
        Assert.Contains("\"Language\" \"not the header\"", output);
        Assert.Contains("\"citadel_settings_language\" \"Language\"", output);
    }

    [Fact]
    public async Task Export_NormalizesNewlineToEscapeSequence()
    {
        const string source =
            "\"lang\"\r\n{\r\n\t\"Language\" \"english\"\r\n\t\"Tokens\"\r\n\t{\r\n" +
            "\t\t\"key\" \"<b>10/24/2024</b><br>\r\n<li>Hero Added</li>\"\r\n\t}\r\n}\r\n";

        string sourcePath = WriteSource(source);
        string outputPath = Path.Combine(_tempDirectory, "output.txt");

        var exporter = new LocalizationExporter();

        await exporter.ExportAsync(
            sourcePath,
            outputPath,
            new[] { Entry("key", "<b>01/01/2025</b><br>\n<li>Hero Added</li>") },
            "vietnamese");

        string output = await File.ReadAllTextAsync(outputPath);

        // Newline trong bản dịch được ghi thành escape \n — engine Valve
        // decode ngược lại thành newline thật khi nạp file.
        Assert.Contains("<b>01/01/2025</b><br>\\n<li>Hero Added</li>", output);
    }

    [Fact]
    public async Task Export_CreatesOutputDirectoryIfMissing()
    {
        string sourcePath = WriteSource(SimpleSource);
        string outputPath = Path.Combine(
            _tempDirectory,
            "nested",
            "deeper",
            "output.txt");

        var exporter = new LocalizationExporter();

        await exporter.ExportAsync(
            sourcePath,
            outputPath,
            Array.Empty<TranslationEntry>(),
            "vietnamese");

        Assert.True(File.Exists(outputPath));
    }
}
