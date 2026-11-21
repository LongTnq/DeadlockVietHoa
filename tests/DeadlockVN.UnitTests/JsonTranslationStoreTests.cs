using System.Text.Json;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Kiểm thử lưu/load bộ bản dịch JSON và đồng bộ với bản gốc.
/// </summary>
public class JsonTranslationStoreTests : IDisposable
{
    private readonly string _tempDirectory;

    public JsonTranslationStoreTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string TempPath(string name) => Path.Combine(_tempDirectory, name);

    private static TranslationEntry Entry(
        string sourceFile,
        string key,
        string original = "Original",
        string translated = "",
        TranslationStatus status = TranslationStatus.Pending,
        LocalizationCategory category = LocalizationCategory.Ui)
    {
        return new TranslationEntry
        {
            Id = new TranslationId(sourceFile, key),
            OriginalText = original,
            TranslatedText = translated,
            Status = status,
            Category = category,
            OriginalTextSnapshot = original
        };
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsAllFields()
    {
        var store = new JsonTranslationStore();
        string path = TempPath("translations.json");

        var document = new TranslationDocument
        {
            SourceLanguage = "English",
            TargetLanguage = "Vietnamese",
            Version = "1.2.3"
        };

        document.Entries.Add(Entry(
            "citadel_main/citadel_main_english.txt",
            "HUD_Health",
            original: "Health",
            translated: "Máu",
            status: TranslationStatus.Approved,
            category: LocalizationCategory.Ui));

        await store.SaveAsync(document, path);

        var loaded = await store.LoadAsync(path);

        Assert.Equal("English", loaded.SourceLanguage);
        Assert.Equal("Vietnamese", loaded.TargetLanguage);
        Assert.Equal("1.2.3", loaded.Version);
        Assert.Single(loaded.Entries);

        var entry = loaded.Entries[0];
        Assert.Equal("HUD_Health", entry.Key);
        Assert.Equal("citadel_main/citadel_main_english.txt", entry.SourceFile);
        Assert.Equal("Health", entry.OriginalText);
        Assert.Equal("Máu", entry.TranslatedText);
        Assert.Equal(TranslationStatus.Approved, entry.Status);
        Assert.Equal(LocalizationCategory.Ui, entry.Category);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesVietnameseDiacritics()
    {
        var store = new JsonTranslationStore();
        string path = TempPath("vietnamese.json");

        var document = new TranslationDocument();
        document.Entries.Add(Entry(
            "f/f.txt",
            "k",
            original: "Close Quarters",
            translated: "Cận Chiến Thắng Trận"));

        await store.SaveAsync(document, path);

        var loaded = await store.LoadAsync(path);

        Assert.Equal("Cận Chiến Thắng Trận", loaded.Entries[0].TranslatedText);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesMultilineText()
    {
        var store = new JsonTranslationStore();
        string path = TempPath("multiline.json");

        var document = new TranslationDocument();
        document.Entries.Add(Entry(
            "f/f.txt",
            "k",
            original: "line1\nline2",
            translated: "dòng1\ndòng2"));

        await store.SaveAsync(document, path);

        var loaded = await store.LoadAsync(path);

        Assert.Equal("dòng1\ndòng2", loaded.Entries[0].TranslatedText);
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenFileMissing()
    {
        var store = new JsonTranslationStore();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => store.LoadAsync(TempPath("khong-ton-tai.json")));
    }

    [Fact]
    public async Task SaveSplitAsync_WritesOneFilePerCategory()
    {
        var store = new JsonTranslationStore();
        string root = TempPath("split");

        var document = new TranslationDocument();
        document.Entries.Add(Entry("f/a.txt", "k1", category: LocalizationCategory.Items));
        document.Entries.Add(Entry("f/b.txt", "k2", category: LocalizationCategory.Heroes));
        document.Entries.Add(Entry("f/c.txt", "k3", category: LocalizationCategory.Ui));

        var files = await store.SaveSplitAsync(document, root);

        Assert.Equal(3, files.Count);
        Assert.True(File.Exists(Path.Combine(root, "items", "items.json")));
        Assert.True(File.Exists(Path.Combine(root, "heroes", "heroes.json")));
        Assert.True(File.Exists(Path.Combine(root, "ui", "ui.json")));
    }

    [Fact]
    public async Task SaveSplitAsync_SplitsVoiceByHero()
    {
        var store = new JsonTranslationStore();
        string root = TempPath("split-voice");

        var document = new TranslationDocument();

        // Cần entry Heroes để suy ra danh sách slug hero.
        document.Entries.Add(Entry(
            "citadel_gc_hero_names/x.txt",
            "hero_gigawatt:n",
            category: LocalizationCategory.Heroes));

        document.Entries.Add(Entry(
            "citadel_generated_vo/x.txt",
            "gigawatt_kill_01",
            category: LocalizationCategory.Voice));

        document.Entries.Add(Entry(
            "citadel_generated_vo/x.txt",
            "haze_kill_01",
            category: LocalizationCategory.Voice));

        await store.SaveSplitAsync(document, root);

        Assert.True(File.Exists(Path.Combine(root, "voice", "gigawatt.json")));
        Assert.True(File.Exists(Path.Combine(root, "voice", "_common.json")));

        // "haze" chưa biết nên rơi vào _common.
        var common = await store.LoadAsync(Path.Combine(root, "voice", "_common.json"));
        Assert.Contains(common.Entries, e => e.Key == "haze_kill_01");
    }

    [Fact]
    public async Task LoadSplitAsync_ReadsAllFilesRecursively()
    {
        var store = new JsonTranslationStore();
        string root = TempPath("load-split");

        var document = new TranslationDocument();
        document.Entries.Add(Entry("f/a.txt", "k1", category: LocalizationCategory.Items));
        document.Entries.Add(Entry("f/b.txt", "k2", category: LocalizationCategory.Ui));

        await store.SaveSplitAsync(document, root);

        var loaded = await store.LoadSplitAsync(root);

        Assert.Equal(2, loaded.Entries.Count);
        Assert.Contains(loaded.Entries, e => e.Key == "k1");
        Assert.Contains(loaded.Entries, e => e.Key == "k2");
    }

    [Fact]
    public async Task LoadSplitAsync_ReturnsEmpty_WhenDirectoryMissing()
    {
        var store = new JsonTranslationStore();

        var loaded = await store.LoadSplitAsync(TempPath("khong-ton-tai"));

        Assert.Empty(loaded.Entries);
    }

    [Fact]
    public void Sync_AddsNewKeys()
    {
        var store = new JsonTranslationStore();

        var document = new TranslationDocument();
        document.Entries.Add(Entry("f.txt", "existing"));

        var originals = new[]
        {
            Entry("f.txt", "existing"),
            Entry("f.txt", "brand_new")
        };

        var result = store.Sync(document, originals);

        Assert.Equal(2, result.TotalEntries);
        Assert.Single(result.Added);
        Assert.Equal("brand_new", result.Added[0].Id.Key);
        Assert.Empty(result.Modified);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public void Sync_KeepsExistingTranslation_WhenOriginalUnchanged()
    {
        var store = new JsonTranslationStore();

        var document = new TranslationDocument();
        document.Entries.Add(Entry(
            "f.txt",
            "k",
            original: "Hello",
            translated: "Xin chào",
            status: TranslationStatus.Approved));

        var result = store.Sync(document, new[] { Entry("f.txt", "k", original: "Hello") });

        Assert.Empty(result.Changes);

        var entry = document.Entries.Single();
        Assert.Equal("Xin chào", entry.TranslatedText);
        Assert.Equal(TranslationStatus.Approved, entry.Status);
    }

    [Fact]
    public void Sync_DowngradesApprovedStatus_WhenOriginalChanged()
    {
        var store = new JsonTranslationStore();

        var document = new TranslationDocument();
        document.Entries.Add(Entry(
            "f.txt",
            "k",
            original: "Old text",
            translated: "Bản dịch cũ",
            status: TranslationStatus.Approved));

        var result = store.Sync(document, new[] { Entry("f.txt", "k", original: "New text") });

        Assert.Single(result.Modified);
        Assert.Equal("Old text", result.Modified[0].OldOriginalText);
        Assert.Equal("New text", result.Modified[0].NewOriginalText);

        var entry = document.Entries.Single();
        Assert.Equal("New text", entry.OriginalText);
        Assert.Equal("Bản dịch cũ", entry.TranslatedText);
        Assert.Equal(TranslationStatus.Reviewed, entry.Status);
    }

    [Fact]
    public void Sync_KeepsStatus_WhenOriginalChangedButNotApproved()
    {
        var store = new JsonTranslationStore();

        var document = new TranslationDocument();
        document.Entries.Add(Entry(
            "f.txt",
            "k",
            original: "Old",
            translated: "Cũ",
            status: TranslationStatus.Translating));

        store.Sync(document, new[] { Entry("f.txt", "k", original: "New") });

        Assert.Equal(TranslationStatus.Translating, document.Entries.Single().Status);
    }

    [Fact]
    public void Sync_ReportsRemovedKeys()
    {
        var store = new JsonTranslationStore();

        var document = new TranslationDocument();
        document.Entries.Add(Entry("f.txt", "gone"));

        var result = store.Sync(document, Array.Empty<TranslationEntry>());

        Assert.Single(result.Removed);
        Assert.Equal("gone", result.Removed[0].Id.Key);
        Assert.Empty(document.Entries);
    }

    [Fact]
    public void Sync_DistinguishesSameKeyInDifferentFiles()
    {
        var store = new JsonTranslationStore();

        var document = new TranslationDocument();
        document.Entries.Add(Entry("file_a.txt", "Language", translated: "A"));

        var originals = new[] { Entry("file_b.txt", "Language") };

        var result = store.Sync(document, originals);

        // Key "Language" ở file khác là entry khác -> key mới ở file_b.txt,
        // entry cũ ở file_a.txt không còn tồn tại -> bị xoá.
        Assert.Single(result.Added);
        Assert.Equal("file_b.txt", result.Added[0].Id.SourceFile);

        Assert.Single(result.Removed);
        Assert.Equal("file_a.txt", result.Removed[0].Id.SourceFile);
    }

    [Fact]
    public async Task SavedFile_UsesCamelCaseProperties()
    {
        var store = new JsonTranslationStore();
        string path = TempPath("camel.json");

        var document = new TranslationDocument();
        document.Entries.Add(Entry("f.txt", "k"));

        await store.SaveAsync(document, path);

        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(path));

        var root = json.RootElement;

        Assert.True(root.TryGetProperty("metadata", out _));
        Assert.True(root.TryGetProperty("entries", out _));

        var first = root.GetProperty("entries")[0];
        Assert.True(first.TryGetProperty("key", out _));
        Assert.True(first.TryGetProperty("sourceFile", out _));
        Assert.True(first.TryGetProperty("originalText", out _));
        Assert.True(first.TryGetProperty("translatedText", out _));
    }

    [Fact]
    public async Task SavedFile_HasNoUtf8Bom()
    {
        var store = new JsonTranslationStore();
        string path = TempPath("nobom.json");

        var document = new TranslationDocument();
        document.Entries.Add(Entry("f.txt", "k"));

        await store.SaveAsync(document, path);

        byte[] bytes = await File.ReadAllBytesAsync(path);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }
}
