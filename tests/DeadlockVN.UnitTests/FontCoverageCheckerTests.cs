using DeadlockVN.Infrastructure.FileSystem;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="FontCoverageChecker"/> — kiểm tra phủ glyph của font.
/// </summary>
public sealed class FontCoverageCheckerTests
{
    private const string GameFontsDirectory =
        @"E:\Steam\steamapps\common\Deadlock\game\citadel\panorama\fonts";

    private static string? GameFont(string name)
    {
        string path = Path.Combine(GameFontsDirectory, name);

        return File.Exists(path) ? path : null;
    }

    [SkippableFact]
    public void ValveOracle_MissesHornVowels()
    {
        string? font = GameFont("valveoracle-medium.ttf");

        Skip.If(font is null, "Không tìm thấy font game.");

        // Font chính của game KHÔNG phủ đủ tiếng Việt — thiếu nguyên âm
        // "móc" ơ/ư và các tổ hợp ờ ở ị ọ ộ ụ ữ.
        // Đây là lý do font fallback là BẮT BUỘC.
        Assert.False(FontCoverageChecker.CoversVietnamese(font!));

        var missing = FontCoverageChecker.GetMissingCodePoints(
            font!,
            FontCoverageChecker.VietnameseCodePoints);

        // ơ (U+01A1) và ư (U+01B0) phải nằm trong danh sách thiếu.
        Assert.Contains(0x01A1, missing);
        Assert.Contains(0x01B0, missing);
    }

    [SkippableFact]
    public void NotoSansJP_CoversVietnamese()
    {
        string? font = GameFont("notosansjp-regular.ttf");

        Skip.If(font is null, "Không tìm thấy font game.");

        // Noto Sans JP phủ đủ toàn bộ bộ chữ tiếng Việt — đây là lựa chọn
        // fallback an toàn nhất trong số font đi kèm game.
        Assert.True(FontCoverageChecker.CoversVietnamese(font!));
    }

    [SkippableFact]
    public void NotoSansSC_CoversVietnamese()
    {
        string? font = GameFont("notosanssc-regular.ttf");

        Skip.If(font is null, "Không tìm thấy font game.");

        Assert.True(FontCoverageChecker.CoversVietnamese(font!));
    }

    [SkippableFact]
    public void NotoSansThai_DoesNotCoverVietnamese()
    {
        string? font = GameFont("notosansthai-regular.ttf");

        Skip.If(font is null, "Không tìm thấy font game.");

        // Font Thái chỉ dùng cho tiếng Thái.
        Assert.False(FontCoverageChecker.CoversVietnamese(font!));
    }

    [Fact]
    public void Covers_MissingFile_ReturnsFalse()
    {
        Assert.False(
            FontCoverageChecker.Covers(
                Path.Combine(Path.GetTempPath(), "khong-ton-tai.ttf"),
                FontCoverageChecker.VietnameseAlphabet));
    }

    [Fact]
    public void VietnameseCodePoints_HasFullCoverage()
    {
        var codePoints = FontCoverageChecker.VietnameseCodePoints;

        // 29 chữ thường + 29 chữ hoa + 59 + 59 tổ hợp có dấu.
        Assert.Equal(176, codePoints.Count);

        // Không trùng lặp.
        Assert.Equal(codePoints.Count, codePoints.Distinct().Count());

        // Phải chứa đủ nguyên âm móc ơ/ư và tổ hợp của chúng.
        foreach (int code in new[]
                 {
                     0x01A1, 0x01B0, 0x1EA1, 0x1EB9, 0x1ECD, 0x1EDB, 0x1EDF,
                     0x1EE3, 0x1EEF, 0x1EF3
                 })
        {
            Assert.Contains(code, codePoints);
        }
    }

    [Fact]
    public void VietnameseAlphabet_ContainsAllLetters()
    {
        string alphabet = FontCoverageChecker.VietnameseAlphabet;

        // 58 ký tự (29 thường + 29 hoa).
        Assert.Equal(58, alphabet.Length);

        foreach (char character in "ăâđêôơưĂÂĐÊÔƠƯ")
        {
            Assert.Contains(character, alphabet);
        }
    }
}
