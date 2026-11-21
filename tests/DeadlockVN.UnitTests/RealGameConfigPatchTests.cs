using System.Text;

using DeadlockVN.Core.Entities;
using DeadlockVN.Infrastructure.FileSystem;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Kiểm chứng <see cref="GameConfigPatcher"/> trên chính file cấu hình của
/// game Deadlock bản cài thật.
/// <para>
/// Test dùng bản sao trong thư mục tạm nên không bao giờ sửa file game.
/// </para>
/// </summary>
public sealed class RealGameConfigPatchTests : IDisposable
{
    private const string GameRelativePath =
        @"citadel\gameinfo.gi";

    private const string FontsRelativePath =
        @"citadel\panorama\fonts\fonts.conf";

    private readonly string _tempDirectory;

    public RealGameConfigPatchTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-realcfg-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Tìm thư mục <c>game</c> của Deadlock. Trả về <c>null</c> nếu game
    /// không được cài trên máy này (test sẽ bị bỏ qua).
    /// </summary>
    private static string? FindGameDirectory()
    {
        var candidates = new[]
        {
            @"E:\Steam\steamapps\common\Deadlock\game",
            @"C:\Program Files (x86)\Steam\steamapps\common\Deadlock\game",
            @"D:\Steam\steamapps\common\Deadlock\game"
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, GameRelativePath)))
            {
                return candidate;
            }
        }

        // Tìm theo cấu trúc thư mục cha.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "source",
                "game");

            if (File.Exists(Path.Combine(candidate, GameRelativePath)))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Sao chép file cấu hình của game ra thư mục tạm.
    /// <para>
    /// Mặc định lấy từ file <c>.bak</c> (trạng thái gốc trước khi bị vá) để
    /// test luôn chạy trên cùng một đầu vào, bất kể máy này đã cài mod hay
    /// chưa. Nếu không có <c>.bak</c> thì mới dùng file gốc đang chạy.
    /// </para>
    /// </summary>
    private string? CopyGameFile(string relativePath, bool preferBackup = true)
    {
        string? gameDirectory = FindGameDirectory();

        if (gameDirectory is null)
        {
            return null;
        }

        string source = Path.Combine(gameDirectory, relativePath);

        if (preferBackup && File.Exists(source + ".bak"))
        {
            source += ".bak";
        }

        if (!File.Exists(source))
        {
            return null;
        }

        string target = Path.Combine(
            _tempDirectory,
            Path.GetFileName(relativePath));

        File.Copy(source, target, overwrite: true);

        return target;
    }

    [SkippableFact]
    public async Task PatchSupportedLanguages_OnRealGameInfo_AddsVietnamese()
    {
        string? path = CopyGameFile(GameRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        var patcher = new GameConfigPatcher();

        ConfigPatchResult result = await patcher.PatchSupportedLanguagesAsync(
            path!,
            "vietnamese");

        Assert.True(result.Changed);

        string patched = File.ReadAllText(path!);

        Assert.Contains("\"vietnamese\" \"3\"", patched);
        Assert.Contains("\"thai\" \"3\"", patched);
        Assert.Contains("\"english\" \"3\"", patched);
    }

    [SkippableFact]
    public async Task PatchSupportedLanguages_OnRealGameInfo_KeepsCrlfAndNoBom()
    {
        string? path = CopyGameFile(GameRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        var patcher = new GameConfigPatcher();

        await patcher.PatchSupportedLanguagesAsync(path!, "vietnamese");

        byte[] bytes = File.ReadAllBytes(path!);

        // Không BOM.
        Assert.False(
            bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF);

        string text = Encoding.UTF8.GetString(bytes);

        // Không có LF trần nào ngoài CRLF.
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [SkippableFact]
    public async Task PatchSupportedLanguages_OnRealGameInfo_KeepsAllOtherLanguages()
    {
        string? path = CopyGameFile(GameRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        string original = File.ReadAllText(path!);

        var patcher = new GameConfigPatcher();

        await patcher.PatchSupportedLanguagesAsync(path!, "vietnamese");

        string patched = File.ReadAllText(path!);

        foreach (string language in new[]
                 {
                     "brazilian", "czech", "english", "french", "german",
                     "italian", "indonesian", "japanese", "koreana", "latam",
                     "polish", "russian", "schinese", "spanish", "thai",
                     "turkish", "ukrainian"
                 })
        {
            Assert.Contains($"\"{language}\" \"3\"", patched);
        }

        // Chỉ khác duy nhất là thêm dòng mới.
        Assert.Equal(
            original.Replace("\r\n", "\n").Split('\n').Length + 1,
            patched.Replace("\r\n", "\n").Split('\n').Length);
    }

    [SkippableFact]
    public async Task PatchFontFallback_OnRealFontsConf_AddsVietnameseBlock()
    {
        string? path = CopyGameFile(FontsRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        var patcher = new GameConfigPatcher();

        ConfigPatchResult result = await patcher.PatchFontFallbackAsync(
            path!,
            new FontFallbackConfig());

        Assert.True(result.Changed);

        string patched = File.ReadAllText(path!);

        Assert.Contains("<string>vi-vn</string>", patched);

        // Không phá khối tiếng Thái đã có.
        Assert.Contains("<string>th-th</string>", patched);
        Assert.Contains("<string>VALVE Oracle Thai</string>", patched);

        // Vẫn là XML hợp lệ về mặt cấu trúc thẻ.
        Assert.Contains("<fontconfig>", patched);
        Assert.Contains("</fontconfig>", patched);
    }

    [SkippableFact]
    public async Task PatchFontFallback_OnRealFontsConf_IsIdempotent()
    {
        string? path = CopyGameFile(FontsRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        var patcher = new GameConfigPatcher();

        await patcher.PatchFontFallbackAsync(path!, new FontFallbackConfig());

        byte[] afterFirst = File.ReadAllBytes(path!);

        ConfigPatchResult second = await patcher.PatchFontFallbackAsync(
            path!,
            new FontFallbackConfig());

        Assert.False(second.Changed);
        Assert.True(second.AlreadyPresent);
        Assert.Equal(afterFirst, File.ReadAllBytes(path!));
    }

    [SkippableFact]
    public async Task PatchFontFallback_OnRealFontsConf_KeepsCrlf()
    {
        string? path = CopyGameFile(FontsRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        var patcher = new GameConfigPatcher();

        await patcher.PatchFontFallbackAsync(path!, new FontFallbackConfig());

        byte[] bytes = File.ReadAllBytes(path!);

        string text = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [SkippableFact]
    public async Task PatchFontFallback_OnRealFontsConf_CoversAllFontFamilies()
    {
        string? path = CopyGameFile(FontsRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        var patcher = new GameConfigPatcher();

        var config = new FontFallbackConfig();

        await patcher.PatchFontFallbackAsync(path!, config);

        string patched = File.ReadAllText(path!);

        // Mỗi họ font phải có khối <match> riêng chứa cả họ font lẫn vi-vn.
        foreach (string family in config.BaseFontFamilies)
        {
            Assert.Contains($"<string>{family}</string>", patched);
        }

        Assert.Contains("<string>vi-vn</string>", patched);
        Assert.Contains("<string>Noto Sans JP</string>", patched);
    }

    [SkippableFact]
    public async Task PatchFontFallback_OnRealFontsConf_ReplacesOldPartialBlock()
    {
        // Mô phỏng tình huống thực tế: file game đã được vá một lần với
        // cấu hình cũ (chỉ có VALVEOracle), giờ cấu hình mới có thêm
        // VALVEPulp + Retail Text Demo.
        string? path = CopyGameFile(FontsRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        var patcher = new GameConfigPatcher();

        // Vá với cấu hình cũ (chỉ VALVEOracle).
        //
        // Phải gán List mới thay vì dùng collection initializer
        // ("{ ... }") — collection initializer chỉ THÊM vào list mặc định
        // chứ không thay thế, khiến cấu hình vẫn còn 4 họ font.
        await patcher.PatchFontFallbackAsync(
            path!,
            new FontFallbackConfig
            {
                BaseFontFamilies = new List<string> { "VALVEOracle" },
                ContainsMatchFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "VALVEPulp",
                    "Retail Text Demo",
                    "Retail Demo"
                }
            });

        string afterFirst = File.ReadAllText(path!);

        // Vá lại với cấu hình đầy đủ.
        ConfigPatchResult second = await patcher.PatchFontFallbackAsync(
            path!,
            new FontFallbackConfig());

        Assert.True(second.Changed);

        string afterSecond = File.ReadAllText(path!);

        // Khối cũ phải bị thay thế, không phải chồng lên.
        Assert.Equal(
            CountOccurrences(afterFirst, "<string>VALVEOracle</string>"),
            CountOccurrences(afterSecond, "<string>VALVEOracle</string>"));

        // Và phải có đủ các họ font mới.
        Assert.Contains("<string>VALVEPulp</string>", afterSecond);
        Assert.Contains("<string>Retail Text Demo</string>", afterSecond);
    }

    [SkippableFact]
    public async Task PatchFontFallback_OnRealFontsConf_DoesNotTouchValveThaiBlocks()
    {
        string? path = CopyGameFile(FontsRelativePath);

        Skip.If(path is null, "Không tìm thấy bản cài game Deadlock.");

        string original = File.ReadAllText(path!);

        var patcher = new GameConfigPatcher();

        await patcher.PatchFontFallbackAsync(path!, new FontFallbackConfig());

        string patched = File.ReadAllText(path!);

        // Các khối của Valve cho tiếng Thái / Nhật / Hàn / Trung phải
        // còn nguyên — ta chỉ thêm khối của mình.
        Assert.Contains("<string>VALVE Oracle Thai</string>", patched);
        Assert.Contains("<string>Noto Sans Thai</string>", patched);
        Assert.Contains("<string>Noto Sans SC</string>", patched);
        Assert.Contains("<string>BlackHanSans</string>", patched);

        // Số khối <match> chỉ tăng đúng số khối ta thêm vào.
        int originalMatches = CountOccurrences(original, "<match>");
        int patchedMatches = CountOccurrences(patched, "<match>");

        Assert.Equal(
            originalMatches + new FontFallbackConfig().BaseFontFamilies.Count,
            patchedMatches);
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
