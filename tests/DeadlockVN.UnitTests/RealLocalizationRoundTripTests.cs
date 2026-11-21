using System.Text;

using DeadlockVN.Core.Entities;
using DeadlockVN.Infrastructure.Exporting;
using DeadlockVN.Infrastructure.Parsers.KeyValues;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test round-trip trên dữ liệu thật: xuất lại file gốc (không có bản dịch
/// nào, giữ nguyên tên ngôn ngữ) phải cho ra file <b>giống hệt từng byte</b>
/// với file gốc.
/// <para>
/// Đây là bảo đảm mạnh nhất rằng exporter không làm hỏng file game: comment,
/// thụt lề, thứ tự escape, BOM, xuống dòng thật trong value — tất cả phải
/// được giữ nguyên tuyệt đối.
/// </para>
/// <para>
/// Nếu file nguồn chưa được extract, test sẽ bỏ qua (return) thay vì fail,
/// để CI không phụ thuộc vào việc cài game.
/// </para>
/// </summary>
public sealed class RealLocalizationRoundTripTests : IDisposable
{
    private static readonly string OriginalDirectory = FindOriginalDirectory();

    private readonly string _tempDirectory;

    public RealLocalizationRoundTripTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-roundtrip-tests",
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

    private static string FindOriginalDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "source",
                "original");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }

    private static string ResolveFile(params string[] segments)
    {
        return Path.Combine(
            new[] { OriginalDirectory }.Concat(segments).ToArray());
    }

    public static IEnumerable<object[]> RealFiles
    {
        get
        {
            yield return new object[] { "citadel_attributes", "citadel_attributes_english.txt" };
            yield return new object[] { "citadel_gc", "citadel_gc_english.txt" };
            yield return new object[] { "citadel_gc_hero_names", "citadel_gc_hero_names_english.txt" };
            yield return new object[] { "citadel_gc_mod_names", "citadel_gc_mod_names_english.txt" };
            yield return new object[] { "citadel_generated_vo", "citadel_generated_vo_english.txt" };
            yield return new object[] { "citadel_heroes", "citadel_heroes_english.txt" };
            yield return new object[] { "citadel_main", "citadel_main_english.txt" };
            yield return new object[] { "citadel_mods", "citadel_mods_english.txt" };
            yield return new object[] { "citadel_patch_notes", "citadel_patch_notes_english.txt" };
            yield return new object[] { "citadel_seasonal", "citadel_seasonal_english.txt" };
            yield return new object[] { "citadel_vdata", "accolades/accolades_english.txt" };
        }
    }

    [Theory]
    [MemberData(nameof(RealFiles))]
    public async Task Export_RealFile_WithoutTranslations_IsByteIdentical(
        string folder,
        string fileName)
    {
        string sourcePath = ResolveFile(folder, fileName);

        if (!File.Exists(sourcePath))
        {
            // Không có file thật -> bỏ qua thay vì fail.
            return;
        }

        byte[] original = await File.ReadAllBytesAsync(sourcePath);

        string outputPath = Path.Combine(_tempDirectory, fileName);

        var exporter = new LocalizationExporter();

        // Giữ nguyên tên ngôn ngữ gốc để output phải giống hệt từng byte.
        // Một số file gốc viết "English", số khác viết "english".
        string originalLanguage = ReadRootLanguage(original);

        var result = await exporter.ExportAsync(
            sourcePath,
            outputPath,
            Array.Empty<TranslationEntry>(),
            originalLanguage);

        byte[] exported = await File.ReadAllBytesAsync(outputPath);

        Assert.Equal(original.Length, exported.Length);
        Assert.Equal(original, exported);

        Assert.True(result.IsValid);
        Assert.Empty(result.UnknownKeys);
        Assert.Equal(0, result.TranslatedEntries);

        // BOM phải được giữ nguyên.
        bool hadBom = original.Length >= 3 && original[0] == 0xEF;

        Assert.Equal(hadBom, result.HadByteOrderMark);
    }

    [Theory]
    [MemberData(nameof(RealFiles))]
    public async Task Export_RealFile_WithTranslation_OnlyChangesTargetedValue(
        string folder,
        string fileName)
    {
        string sourcePath = ResolveFile(folder, fileName);

        if (!File.Exists(sourcePath))
        {
            return;
        }

        byte[] original = await File.ReadAllBytesAsync(sourcePath);

        string originalText = DecodeUtf8(original);

        var originalDocument = new KeyValuesParser().Parse(originalText);

        var originalEntries = originalDocument.EnumerateEntries()
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        // Chọn key đầu tiên khác "Language" để dịch thử.
        var target = originalEntries.Keys
            .FirstOrDefault(key => !string.Equals(key, "Language", StringComparison.Ordinal));

        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        const string Vietnamese = "Bản dịch tiếng Việt";

        string outputPath = Path.Combine(_tempDirectory, "translated_" + fileName);

        var exporter = new LocalizationExporter();

        var result = await exporter.ExportAsync(
            sourcePath,
            outputPath,
            new[]
            {
                new TranslationEntry
                {
                    Id = new TranslationId(fileName, target),
                    OriginalText = originalEntries[target],
                    TranslatedText = Vietnamese,
                    Status = TranslationStatus.Approved
                }
            },
            "vietnamese");

        Assert.Equal(1, result.TranslatedEntries);
        Assert.True(result.IsValid);

        // Parse lại file đã xuất và so sánh từng value.
        string exportedText = DecodeUtf8(await File.ReadAllBytesAsync(outputPath));

        var exportedDocument = new KeyValuesParser().Parse(exportedText);

        var exportedEntries = exportedDocument.EnumerateEntries()
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        // Số lượng key phải giống nhau.
        Assert.Equal(originalEntries.Count, exportedEntries.Count);

        foreach (var pair in originalEntries)
        {
            Assert.True(
                exportedEntries.TryGetValue(pair.Key, out string? exportedValue),
                $"Thiếu key sau khi xuất: {pair.Key}");

            if (string.Equals(pair.Key, target, StringComparison.Ordinal))
            {
                // Key được dịch -> giá trị mới.
                Assert.Equal(Vietnamese, exportedValue);
            }
            else if (string.Equals(pair.Key, "Language", StringComparison.Ordinal))
            {
                // Header ngôn ngữ -> đổi sang ngôn ngữ đích.
                Assert.Equal("vietnamese", exportedValue);
            }
            else
            {
                // Mọi key khác giữ nguyên giá trị gốc.
                Assert.Equal(pair.Value, exportedValue);
            }
        }

        // File nào có header "Language" thì phải đổi sang ngôn ngữ đích.
        if (originalEntries.ContainsKey("Language"))
        {
            Assert.Equal("vietnamese", exportedEntries["Language"]);
        }
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        bool hasBom =
            bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;

        return new UTF8Encoding(false).GetString(
            hasBom ? bytes[3..] : bytes);
    }

    /// <summary>
    /// Đọc giá trị của key <c>"Language"</c> ở cấp gốc của file gốc.
    /// </summary>
    private static string ReadRootLanguage(byte[] bytes)
    {
        var document = new KeyValuesParser().Parse(DecodeUtf8(bytes));

        foreach (var node in document.Nodes)
        {
            if (!node.IsObject)
            {
                continue;
            }

            foreach (var child in node.Children)
            {
                if (child.IsValue &&
                    string.Equals(child.Key, "Language", StringComparison.Ordinal))
                {
                    return child.Value!;
                }
            }
        }

        return "english";
    }
}
