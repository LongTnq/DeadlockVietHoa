using DeadlockVN.Core.Entities;
using DeadlockVN.Infrastructure.FileSystem;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="GameConfigPatcher"/> — vá <c>gameinfo.gi</c> và
/// <c>fonts.conf</c>.
/// </summary>
public sealed class GameConfigPatcherTests : IDisposable
{
    private readonly string _tempDirectory;

    public GameConfigPatcherTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-patcher-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static string GameInfoContent() =>
        "\"EngineVersion\"\t\"4\"\r\n" +
        "\r\n" +
        "\tSupportedLanguages\r\n" +
        "\t{\r\n" +
        "\t\t\"brazilian\" \"3\"\r\n" +
        "\t\t\"czech\" \"3\"\r\n" +
        "\t\t\"english\" \"3\"\r\n" +
        "\t\t\"thai\" \"3\"\r\n" +
        "\t}\r\n" +
        "\r\n" +
        "\tFileSystem\r\n" +
        "\t{\r\n" +
        "\t}\r\n";

    private string WriteGameInfo(string content)
    {
        string path = Path.Combine(_tempDirectory, "gameinfo.gi");

        File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));

        return path;
    }

    [Fact]
    public async Task PatchSupportedLanguages_AddsLanguageToBlock()
    {
        string path = WriteGameInfo(GameInfoContent());

        var patcher = new GameConfigPatcher();

        ConfigPatchResult result = await patcher.PatchSupportedLanguagesAsync(
            path,
            "vietnamese");

        Assert.True(result.Changed);
        Assert.False(result.AlreadyPresent);

        string patched = File.ReadAllText(path);

        Assert.Contains("\"vietnamese\" \"3\"", patched);
        Assert.Contains("\"thai\" \"3\"", patched);
    }

    [Fact]
    public async Task PatchSupportedLanguages_KeepsTabIndentationAndCrlf()
    {
        string path = WriteGameInfo(GameInfoContent());

        var patcher = new GameConfigPatcher();

        await patcher.PatchSupportedLanguagesAsync(path, "vietnamese");

        byte[] bytes = File.ReadAllBytes(path);
        string text = System.Text.Encoding.UTF8.GetString(bytes);

        // Giữ nguyên CRLF, không sinh LF trần.
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
        Assert.Contains("\t\t\"vietnamese\" \"3\"\r\n", text);
    }

    [Fact]
    public async Task PatchSupportedLanguages_IsIdempotent()
    {
        string path = WriteGameInfo(GameInfoContent());

        var patcher = new GameConfigPatcher();

        await patcher.PatchSupportedLanguagesAsync(path, "vietnamese");

        byte[] afterFirst = File.ReadAllBytes(path);

        ConfigPatchResult second = await patcher.PatchSupportedLanguagesAsync(
            path,
            "vietnamese");

        Assert.False(second.Changed);
        Assert.True(second.AlreadyPresent);
        Assert.Equal(afterFirst, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task PatchSupportedLanguages_CreatesBackup()
    {
        string path = WriteGameInfo(GameInfoContent());

        var patcher = new GameConfigPatcher();

        ConfigPatchResult result = await patcher.PatchSupportedLanguagesAsync(
            path,
            "vietnamese");

        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        // Bản sao lưu phải chứa nội dung gốc.
        Assert.Equal(
            GameInfoContent(),
            File.ReadAllText(result.BackupPath!));
    }

    [Fact]
    public async Task PatchSupportedLanguages_PreservesTabSeparator()
    {
        string path = WriteGameInfo(
            "\tSupportedLanguages\r\n" +
            "\t{\r\n" +
            "\t\t\"english\"\t\"3\"\r\n" +
            "\t}\r\n");

        var patcher = new GameConfigPatcher();

        await patcher.PatchSupportedLanguagesAsync(path, "vietnamese");

        string patched = File.ReadAllText(path);

        Assert.Contains("\t\t\"vietnamese\"\t\"3\"", patched);
    }

    [Fact]
    public async Task PatchSupportedLanguages_PreservesNoBomFormat()    {
        string path = WriteGameInfo(GameInfoContent());

        var patcher = new GameConfigPatcher();

        await patcher.PatchSupportedLanguagesAsync(path, "vietnamese");

        byte[] bytes = File.ReadAllBytes(path);

        Assert.False(
            bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF);
    }

    [Fact]
    public async Task PatchSupportedLanguages_ThrowsWhenFileMissing()
    {
        var patcher = new GameConfigPatcher();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            patcher.PatchSupportedLanguagesAsync(
                Path.Combine(_tempDirectory, "khong-ton-tai.gi"),
                "vietnamese"));
    }

    [Fact]
    public async Task PatchFontFallback_AddsMatchBlock()
    {
        string path = Path.Combine(_tempDirectory, "fonts.conf");

        File.WriteAllText(
            path,
            "<match>\r\n\t<test name=\"lang\">\r\n\t\t<string>th-th</string>\r\n" +
            "\t</test>\r\n</match>\r\n",
            new System.Text.UTF8Encoding(false));

        var patcher = new GameConfigPatcher();

        ConfigPatchResult result = await patcher.PatchFontFallbackAsync(
            path,
            new FontFallbackConfig());

        Assert.True(result.Changed);

        string patched = File.ReadAllText(path);

        Assert.Contains("<string>vi-vn</string>", patched);
        Assert.Contains("<string>VALVEOracle</string>", patched);
        Assert.Contains("<string>Noto Sans JP</string>", patched);
        Assert.Contains("<string>th-th</string>", patched);
    }

    [Fact]
    public async Task PatchFontFallback_IsIdempotent()
    {
        string path = Path.Combine(_tempDirectory, "fonts.conf");

        File.WriteAllText(path, "<match></match>\r\n", new System.Text.UTF8Encoding(false));

        var patcher = new GameConfigPatcher();

        await patcher.PatchFontFallbackAsync(path, new FontFallbackConfig());

        byte[] afterFirst = File.ReadAllBytes(path);

        ConfigPatchResult second = await patcher.PatchFontFallbackAsync(
            path,
            new FontFallbackConfig());

        Assert.False(second.Changed);
        Assert.True(second.AlreadyPresent);
        Assert.Equal(afterFirst, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task PatchFontFallback_DoesNotBreakExistingXml()
    {
        string path = Path.Combine(_tempDirectory, "fonts.conf");

        File.WriteAllText(
            path,
            "<?xml version=\"1.0\"?>\r\n<fontconfig>\r\n</fontconfig>\r\n",
            new System.Text.UTF8Encoding(false));

        var patcher = new GameConfigPatcher();

        await patcher.PatchFontFallbackAsync(path, new FontFallbackConfig());

        string patched = File.ReadAllText(path);

        Assert.Contains("<?xml version=\"1.0\"?>", patched);
        Assert.Contains("<fontconfig>", patched);
        Assert.Contains("</fontconfig>", patched);
        Assert.Contains("<string>vi-vn</string>", patched);
    }
}
