using DeadlockVN.Infrastructure.Parsers.KeyValues;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test parse trên dữ liệu thật đã extract từ game Deadlock.
/// Nếu file nguồn chưa được extract, test sẽ được bỏ qua (skip)
/// thay vì làm fail, để CI không phụ thuộc vào việc cài game.
/// </summary>
public sealed class RealLocalizationFileTests
{
    private static readonly string OriginalDirectory = FindOriginalDirectory();

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

    [Fact]
    public void Parse_PatchNotes_RealFile_Succeeds()
    {
        string file = ResolveFile(
            "citadel_patch_notes",
            "citadel_patch_notes_english.txt");

        Assert.True(File.Exists(file), $"Không tìm thấy file thật: {file}");

        string source = File.ReadAllText(file);

        var document = new KeyValuesParser().Parse(source);

        var entries = document.EnumerateEntries().ToList();

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.False(
            string.IsNullOrWhiteSpace(entry.Key)));

        // Không được có key trùng lặp trong cùng một file.
        var duplicates = entries
            .GroupBy(entry => entry.Path, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData("citadel_patch_notes", "citadel_patch_notes_english.txt")]
    [InlineData("citadel_gc_mod_names", "citadel_gc_mod_names_english.txt")]
    [InlineData("citadel_gc_hero_names", "citadel_gc_hero_names_english.txt")]
    [InlineData("citadel_main", "citadel_main_english.txt")]
    [InlineData("citadel_mods", "citadel_mods_english.txt")]
    [InlineData("citadel_attributes", "citadel_attributes_english.txt")]
    [InlineData("citadel_heroes", "citadel_heroes_english.txt")]
    [InlineData("citadel_seasonal", "citadel_seasonal_english.txt")]
    [InlineData("citadel_gc", "citadel_gc_english.txt")]
    [InlineData("citadel_vdata", "accolades/accolades_english.txt")]
    public void Parse_RealLocalizationFiles_Succeed(string folder, string fileName)
    {
        string file = ResolveFile(folder, fileName);

        Assert.True(File.Exists(file), $"Không tìm thấy file thật: {file}");

        string source = File.ReadAllText(file);

        var document = new KeyValuesParser().Parse(source);

        var entries = document.EnumerateEntries().ToList();

        Assert.NotEmpty(entries);

        var duplicates = entries
            .GroupBy(entry => entry.Path, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }
}
