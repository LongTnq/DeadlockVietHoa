using DeadlockVN.Application.DTOs;
using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Kiểm thử lớp Application: export, import, sync, report.
/// </summary>
public class TranslationManagementServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public TranslationManagementServiceTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-m3-tests",
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

    private TranslationManagementService CreateService()
    {
        return new TranslationManagementService(
            new JsonTranslationStore(),
            new LocalizationSourceReader(new LocalizationClassifier()));
    }

    /// <summary>
    /// Tạo thư mục nguồn giả lập với file KV1 hợp lệ.
    /// </summary>
    private string CreateSourceDirectory()
    {
        string source = TempPath("source");
        string heroesDir = Path.Combine(source, "citadel_heroes");
        string voDir = Path.Combine(source, "citadel_generated_vo");

        Directory.CreateDirectory(heroesDir);
        Directory.CreateDirectory(voDir);

        File.WriteAllText(
            Path.Combine(heroesDir, "citadel_heroes_english.txt"),
            """
            "citadel_heroes"
            {
                "citadel_ability_test" "Test Ability"
                "citadel_upgrade_test" "Test Upgrade"
            }
            """);

        File.WriteAllText(
            Path.Combine(voDir, "citadel_generated_vo_english.txt"),
            """
            "citadel_generated_vo"
            {
                "Language" "english"
                "testhero_kill_01" "Kill line one"
                "testhero_kill_02" "Kill line two"
            }
            """);

        return source;
    }

    [Fact]
    public async Task CreateAsync_BuildsDocumentFromSource()
    {
        string source = CreateSourceDirectory();
        string output = TempPath("out.json");

        var service = CreateService();

        ExportResult result = await service.CreateAsync(source, output);

        Assert.Equal(5, result.ExportedCount);
        Assert.Equal(0, result.TranslatedCount);
        Assert.True(File.Exists(output));

        var document = await new JsonTranslationStore().LoadAsync(output);

        Assert.All(document.Entries, e => Assert.Equal(TranslationStatus.Pending, e.Status));
        Assert.All(
            document.Entries,
            e => Assert.Equal(e.OriginalText, e.OriginalTextSnapshot));
    }

    [Fact]
    public async Task CreateAsync_UsesRelativeSourceFilePaths()
    {
        string source = CreateSourceDirectory();
        string output = TempPath("out.json");

        await CreateService().CreateAsync(source, output);

        var document = await new JsonTranslationStore().LoadAsync(output);

        Assert.Contains(
            document.Entries,
            e => e.SourceFile == "citadel_heroes/citadel_heroes_english.txt");

        Assert.Contains(
            document.Entries,
            e => e.SourceFile == "citadel_generated_vo/citadel_generated_vo_english.txt");

        // Đường dẫn phải dùng dấu '/' độc lập hệ điều hành.
        Assert.All(document.Entries, e => Assert.DoesNotContain('\\', e.SourceFile));
    }

    [Fact]
    public async Task CreateSplitAsync_WritesMultipleFiles()
    {
        string source = CreateSourceDirectory();
        string root = TempPath("split");

        var result = await CreateService().CreateSplitAsync(source, root);

        Assert.Equal(5, result.ExportedCount);
        Assert.True(result.Files.Count >= 2);

        // Nhóm Voice tách theo hero -> hero "testhero" không có trong danh sách
        // hero nên rơi vào _common.
        Assert.True(File.Exists(Path.Combine(root, "voice", "_common.json")));
        Assert.True(File.Exists(Path.Combine(root, "abilities", "abilities.json")));
    }

    [Fact]
    public async Task ImportAsync_SkipsExistingTranslations_ByDefault()
    {
        string source = CreateSourceDirectory();
        string target = TempPath("target.json");

        var service = CreateService();

        await service.CreateAsync(source, target);

        // Giả lập người dịch đã dịch một entry.
        var store = new JsonTranslationStore();
        var document = await store.LoadAsync(target);

        var entry = document.Entries.First();
        entry.TranslatedText = "Bản dịch của tôi";
        entry.Status = TranslationStatus.Approved;

        await store.SaveAsync(document, target);

        // File nguồn chứa bản dịch khác.
        string incoming = TempPath("incoming.json");
        var incomingDoc = await store.LoadAsync(target);
        incomingDoc.Entries[0].TranslatedText = "Bản dịch khác";
        incomingDoc.Entries[1].TranslatedText = "Mới mới";
        await store.SaveAsync(incomingDoc, incoming);

        ImportResult result = await service.ImportAsync(target, incoming);

        // 1 entry đã có bản dịch -> bỏ qua; 4 entry còn trống -> được điền.
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(4, result.UpdatedCount);

        var after = await store.LoadAsync(target);
        Assert.Equal("Bản dịch của tôi", after.Entries[0].TranslatedText);
        Assert.Equal("Mới mới", after.Entries[1].TranslatedText);
    }

    [Fact]
    public async Task ImportAsync_Overwrites_WhenRequested()
    {
        string source = CreateSourceDirectory();
        string target = TempPath("target.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(source, target);

        var document = await store.LoadAsync(target);
        document.Entries[0].TranslatedText = "Cũ";
        await store.SaveAsync(document, target);

        string incoming = TempPath("incoming.json");
        var incomingDoc = await store.LoadAsync(target);
        incomingDoc.Entries[0].TranslatedText = "Mới";
        await store.SaveAsync(incomingDoc, incoming);

        ImportResult result = await service.ImportAsync(target, incoming, overwrite: true);

        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(5, result.UpdatedCount);

        var after = await store.LoadAsync(target);
        Assert.Equal("Mới", after.Entries[0].TranslatedText);
    }

    [Fact]
    public async Task ImportAsync_AddsUnknownKeys()
    {
        string source = CreateSourceDirectory();
        string target = TempPath("target.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(source, target);

        var document = await store.LoadAsync(target);
        document.Entries.Add(new TranslationEntry
        {
            Id = new TranslationId("other/file_english.txt", "brand_new_key"),
            OriginalText = "Brand new",
            TranslatedText = "Mới hoàn toàn"
        });

        string incoming = TempPath("incoming.json");
        await store.SaveAsync(document, incoming);

        ImportResult result = await service.ImportAsync(target, incoming);

        Assert.Equal(1, result.AddedCount);

        // 5 entry có sẵn trong target (bản dịch còn trống) được điền vào.
        Assert.Equal(5, result.UpdatedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public async Task SyncAsync_AddsNewKeysAndKeepsTranslations()
    {
        string source = CreateSourceDirectory();
        string target = TempPath("target.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(source, target);

        var document = await store.LoadAsync(target);
        document.Entries[0].TranslatedText = "Dịch rồi";
        await store.SaveAsync(document, target);

        // Thêm key mới vào file nguồn.
        File.AppendAllText(
            Path.Combine(source, "citadel_heroes", "citadel_heroes_english.txt"),
            "\n                \"citadel_ability_new\" \"Brand New Ability\"");

        var result = await service.SyncAsync(target, source);

        Assert.Single(result.Added);
        Assert.Equal("citadel_ability_new", result.Added[0].Id.Key);

        var after = await store.LoadAsync(target);
        Assert.Equal(6, after.Entries.Count);
        Assert.Equal("Dịch rồi", after.Entries[0].TranslatedText);
    }

    [Fact]
    public async Task SyncAsync_MarksChangedOriginals()
    {
        string source = CreateSourceDirectory();
        string target = TempPath("target.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(source, target);

        var document = await store.LoadAsync(target);

        var abilityEntry = document.Entries.First(e => e.Key == "citadel_ability_test");
        abilityEntry.TranslatedText = "Dịch cũ";
        abilityEntry.Status = TranslationStatus.Approved;

        await store.SaveAsync(document, target);

        // Đổi nội dung gốc.
        string heroFile = Path.Combine(source, "citadel_heroes", "citadel_heroes_english.txt");
        File.WriteAllText(
            heroFile,
            File.ReadAllText(heroFile).Replace("Test Ability", "Changed Ability"));

        var result = await service.SyncAsync(target, source);

        Assert.Single(result.Modified);
        Assert.Equal("citadel_ability_test", result.Modified[0].Id.Key);

        var after = await store.LoadAsync(target);
        var syncedEntry = after.Entries.First(e => e.Key == "citadel_ability_test");

        Assert.Equal(TranslationStatus.Reviewed, syncedEntry.Status);
        Assert.Equal("Changed Ability", syncedEntry.OriginalText);
        Assert.True(syncedEntry.IsStale);
    }

    [Fact]
    public async Task BuildReportAsync_CountsByCategory()
    {
        string source = CreateSourceDirectory();
        string target = TempPath("target.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(source, target);

        var document = await store.LoadAsync(target);
        document.Entries[0].TranslatedText = "Xin chào";
        document.Entries[0].Status = TranslationStatus.Approved;
        document.Entries[1].TranslatedText = "Tạm biên dịch";
        document.Entries[1].Status = TranslationStatus.Translating;
        await store.SaveAsync(document, target);

        TranslationReport report = await service.BuildReportAsync(target);

        Assert.Equal(5, report.Total);
        Assert.Equal(2, report.Translated);
        Assert.Equal(1, report.Approved);
        Assert.Equal(1, report.Translating);
        Assert.Equal(40.0, Math.Round(report.TranslatedPercent, 1));
        Assert.Equal(20.0, Math.Round(report.ApprovedPercent, 1));
        Assert.Contains(report.Categories, c => c.Name == nameof(LocalizationCategory.Voice));
    }

    [Fact]
    public async Task BuildReportAsync_CountsStaleEntries()
    {
        string source = CreateSourceDirectory();
        string target = TempPath("target.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(source, target);

        var document = await store.LoadAsync(target);
        document.Entries[0].TranslatedText = "Xin chào";
        document.Entries[0].OriginalTextSnapshot = "Nội dung cũ hơn";
        await store.SaveAsync(document, target);

        TranslationReport report = await service.BuildReportAsync(target);

        Assert.Equal(1, report.Stale);
    }

    [Fact]
    public async Task BuildSplitReportAsync_ReadsAllCategoryFiles()
    {
        string source = CreateSourceDirectory();
        string root = TempPath("split");

        var service = CreateService();
        await service.CreateSplitAsync(source, root);

        TranslationReport report = await service.BuildSplitReportAsync(root);

        Assert.Equal(5, report.Total);
    }
}
