using DeadlockVN.Application.DTOs;
using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Kiểm thử M3 trên dữ liệu thật đã extract từ game Deadlock.
/// Nếu file nguồn chưa được extract, test sẽ được bỏ qua (skip)
/// thay vì làm fail, để CI không phụ thuộc vào việc cài game.
/// </summary>
public sealed class RealTranslationWorkflowTests : IDisposable
{
    private static readonly string OriginalDirectory = FindOriginalDirectory();

    private readonly string _tempDirectory;

    public RealTranslationWorkflowTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "deadlockvn-real-m3",
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

    private static TranslationManagementService CreateService()
    {
        return new TranslationManagementService(
            new JsonTranslationStore(),
            new LocalizationSourceReader(new LocalizationClassifier()));
    }

    private static bool SourceAvailable => !string.IsNullOrEmpty(OriginalDirectory);

    [Fact]
    public async Task Export_RealFiles_ProducesExpectedEntryCount()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string output = Path.Combine(_tempDirectory, "vietnamese.json");

        var result = await CreateService().CreateAsync(OriginalDirectory, output);

        Assert.Equal(91_457, result.ExportedCount);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task Export_RealFiles_AssignsEveryCategory()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string output = Path.Combine(_tempDirectory, "vietnamese.json");

        var result = await CreateService().CreateAsync(OriginalDirectory, output);

        // Mọi entry phải thuộc một nhóm hợp lệ.
        Assert.All(
            result.ByCategory.Keys,
            name => Assert.Contains(
                Enum.GetNames<LocalizationCategory>(),
                n => string.Equals(n, name, StringComparison.Ordinal)));

        Assert.Contains(nameof(LocalizationCategory.Voice), result.ByCategory.Keys);
        Assert.Contains(nameof(LocalizationCategory.Abilities), result.ByCategory.Keys);
        Assert.Contains(nameof(LocalizationCategory.Items), result.ByCategory.Keys);
        Assert.Contains(nameof(LocalizationCategory.Heroes), result.ByCategory.Keys);
    }

    [Fact]
    public async Task Export_RealFiles_KeepsEveryEntryUnique()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string output = Path.Combine(_tempDirectory, "vietnamese.json");

        await CreateService().CreateAsync(OriginalDirectory, output);

        var document = await new JsonTranslationStore().LoadAsync(output);

        var ids = document.Entries.Select(e => e.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task ExportSplit_RealFiles_RoundTripsWithoutLoss()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string root = Path.Combine(_tempDirectory, "split");

        var service = CreateService();

        var export = await service.CreateSplitAsync(OriginalDirectory, root);

        Assert.Equal(91_457, export.ExportedCount);

        // Mỗi file VO theo hero + các file nhóm khác.
        Assert.True(export.Files.Count > 20);
        Assert.Contains(export.Files, f => f.Category == "voice");

        var loaded = await service.BuildSplitReportAsync(root);

        Assert.Equal(91_457, loaded.Total);
    }

    [Fact]
    public async Task Sync_RealFiles_RunTwice_IsIdempotent()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string output = Path.Combine(_tempDirectory, "vietnamese.json");

        var service = CreateService();

        await service.CreateAsync(OriginalDirectory, output);

        // Bộ bản dịch vừa export đã khớp 100% với bản gốc -> sync là no-op.
        var first = await service.SyncAsync(output, OriginalDirectory);

        Assert.Empty(first.Changes);
        Assert.Equal(91_457, first.TotalEntries);

        // Chạy lần nữa vẫn không có thay đổi.
        var second = await service.SyncAsync(output, OriginalDirectory);

        Assert.Empty(second.Changes);
        Assert.Equal(91_457, second.TotalEntries);
    }

    [Fact]
    public async Task Sync_RealFiles_DetectsAddedAndRemovedKeys()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string output = Path.Combine(_tempDirectory, "vietnamese.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(OriginalDirectory, output);

        // Xoá 1 entry và thêm 1 entry giả để mô phỏng game đã đổi.
        var document = await store.LoadAsync(output);
        document.Entries.RemoveAt(0);
        document.Entries.Add(new TranslationEntry
        {
            Id = new TranslationId(
                "citadel_heroes/citadel_heroes_english.txt",
                "citadel_ability_brand_new"),
            OriginalText = "Brand New Ability",
            TranslatedText = string.Empty,
            Status = TranslationStatus.Pending,
            Category = LocalizationCategory.Abilities,
            OriginalTextSnapshot = "Brand New Ability"
        });

        await store.SaveAsync(document, output);

        var result = await service.SyncAsync(output, OriginalDirectory);

        // Key "citadel_ability_brand_new" không có trong game -> bị xoá.
        Assert.Single(result.Removed);
        Assert.Equal("citadel_ability_brand_new", result.Removed[0].Id.Key);

        // Key bị xoá khỏi bộ dịch -> thêm lại.
        Assert.Single(result.Added);

        Assert.Equal(91_457, result.TotalEntries);
    }

    [Fact]
    public async Task Import_RealFiles_KeepsExistingTranslations()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string target = Path.Combine(_tempDirectory, "target.json");
        string incoming = Path.Combine(_tempDirectory, "incoming.json");

        var service = CreateService();
        var store = new JsonTranslationStore();

        await service.CreateAsync(OriginalDirectory, target);

        var document = await store.LoadAsync(target);
        document.Entries[0].TranslatedText = "Bản dịch đã có";
        await store.SaveAsync(document, target);

        // File nguồn có bản dịch khác ở entry đầu tiên.
        var incomingDoc = await store.LoadAsync(target);
        incomingDoc.Entries[0].TranslatedText = "Bản dịch khác";
        await store.SaveAsync(incomingDoc, incoming);

        var result = await service.ImportAsync(target, incoming);

        Assert.Equal(1, result.SkippedCount);

        var after = await store.LoadAsync(target);
        Assert.Equal("Bản dịch đã có", after.Entries[0].TranslatedText);
    }

    [Fact]
    public async Task Report_RealFiles_HasConsistentTotals()
    {
        Assert.True(SourceAvailable, "Chưa có dữ liệu gốc để test.");

        string output = Path.Combine(_tempDirectory, "vietnamese.json");

        var service = CreateService();

        await service.CreateAsync(OriginalDirectory, output);

        TranslationReport report = await service.BuildReportAsync(output);

        Assert.Equal(91_457, report.Total);
        Assert.Equal(0, report.Translated);
        Assert.Equal(91_457, report.Pending);

        int categoryTotal = report.Categories.Sum(c => c.Total);
        Assert.Equal(91_457, categoryTotal);
    }
}
