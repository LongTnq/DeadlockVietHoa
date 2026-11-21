using DeadlockVN.Application.DTOs;
using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="ReviewService"/> — quy trình xuất/nhập bản dịch để
/// người dịch duyệt.
/// </summary>
public sealed class ReviewServiceTests
{
    private static TranslationDocument CreateDocument()
    {
        var document = new TranslationDocument();

        document.Entries.Add(new TranslationEntry
        {
            Id = new TranslationId("citadel_heroes/english.txt", "hero_bebop"),
            OriginalText = "Bebop",
            TranslatedText = "Bebop",
            Status = TranslationStatus.Reviewed,
            Category = LocalizationCategory.Heroes,
            OriginalTextSnapshot = "Bebop"
        });

        document.Entries.Add(new TranslationEntry
        {
            Id = new TranslationId("citadel_main/english.txt", "ui_ok"),
            OriginalText = "OK",
            TranslatedText = "OK",
            Status = TranslationStatus.Approved,
            Category = LocalizationCategory.Ui,
            OriginalTextSnapshot = "OK"
        });

        document.Entries.Add(new TranslationEntry
        {
            Id = new TranslationId("citadel_main/english.txt", "ui_pending"),
            OriginalText = "Pending item",
            TranslatedText = string.Empty,
            Status = TranslationStatus.Pending,
            Category = LocalizationCategory.Ui,
            OriginalTextSnapshot = "Pending item"
        });

        return document;
    }

    [Fact]
    public void BuildExport_OnlyIncludesMachineTranslatedByDefault()
    {
        var service = new ReviewService();

        var export = service.BuildExport(CreateDocument());

        Assert.Single(export.Items);
        Assert.Equal("hero_bebop", export.Items[0].Key);
    }

    [Fact]
    public void BuildExport_IncludesAllWhenAllFlagSet()
    {
        var service = new ReviewService();

        var export = service.BuildExport(
            CreateDocument(),
            null,
            onlyMachineTranslated: false);

        // Approved + Reviewed — Pending bị loại vì chưa có bản dịch.
        Assert.Equal(2, export.Items.Count);
    }

    [Fact]
    public void BuildExport_FiltersByCategory()
    {
        var service = new ReviewService();

        var export = service.BuildExport(
            CreateDocument(),
            new[] { LocalizationCategory.Ui },
            onlyMachineTranslated: false);

        Assert.Single(export.Items);
        Assert.Equal("ui_ok", export.Items[0].Key);
    }

    [Fact]
    public void BuildExport_RespectsLimit()
    {
        var service = new ReviewService();

        var export = service.BuildExport(
            CreateDocument(),
            null,
            onlyMachineTranslated: false,
            limit: 1);

        Assert.Single(export.Items);
    }

    [Fact]
    public void ApplyImport_UpdatesTranslationAndStatus()
    {
        var document = CreateDocument();
        var service = new ReviewService();

        var imported = new ReviewExportFile();
        imported.Items.Add(new ReviewItem
        {
            Key = "hero_bebop",
            SourceFile = "citadel_heroes/english.txt",
            NewTranslation = "Bebop (chỉnh sửa)",
            NewStatus = "Approved"
        });

        var result = service.ApplyImport(document, imported);

        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Approved);

        var entry = document.Entries.First(e => e.Key == "hero_bebop");

        Assert.Equal("Bebop (chỉnh sửa)", entry.TranslatedText);
        Assert.Equal(TranslationStatus.Approved, entry.Status);
    }

    [Fact]
    public void ApplyImport_SkipsEntriesWithoutChanges()
    {
        var document = CreateDocument();
        var service = new ReviewService();

        var imported = new ReviewExportFile();
        imported.Items.Add(new ReviewItem
        {
            Key = "hero_bebop",
            SourceFile = "citadel_heroes/english.txt"
        });

        var result = service.ApplyImport(document, imported);

        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped);

        var entry = document.Entries.First(e => e.Key == "hero_bebop");

        Assert.Equal("Bebop", entry.TranslatedText);
        Assert.Equal(TranslationStatus.Reviewed, entry.Status);
    }

    [Fact]
    public void ApplyImport_ReportsUnknownKeys()
    {
        var document = CreateDocument();
        var service = new ReviewService();

        var imported = new ReviewExportFile();
        imported.Items.Add(new ReviewItem
        {
            Key = "khong_ton_tai",
            SourceFile = "citadel_heroes/english.txt",
            NewTranslation = "X"
        });

        var result = service.ApplyImport(document, imported);

        Assert.Equal(1, result.NotFound);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void ApplyImport_RejectsInvalidStatus()
    {
        var document = CreateDocument();
        var service = new ReviewService();

        var imported = new ReviewExportFile();
        imported.Items.Add(new ReviewItem
        {
            Key = "hero_bebop",
            SourceFile = "citadel_heroes/english.txt",
            NewStatus = "KhongTonTai"
        });

        var result = service.ApplyImport(document, imported);

        Assert.Single(result.Errors);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public void Summarize_CountsByStatus()
    {
        var service = new ReviewService();

        var summary = service.Summarize(CreateDocument());

        Assert.Equal(3, summary.Total);
        Assert.Equal(1, summary.MachineTranslated);
        Assert.Equal(1, summary.Approved);
        Assert.Equal(1, summary.Pending);
        Assert.Equal(1, summary.ByCategory["Heroes"]);
        Assert.Equal(2, summary.ByCategory["Ui"]);
    }

    [Fact]
    public async Task ExportRoundTrip_PreservesData()
    {
        var document = CreateDocument();
        var service = new ReviewService();

        var export = service.BuildExport(document);

        string path = Path.Combine(
            Path.GetTempPath(),
            "dvn-review-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            await ReviewService.WriteExportAsync(export, path);

            var reloaded = await ReviewService.ReadExportAsync(path);

            Assert.Single(reloaded.Items);
            Assert.Equal("hero_bebop", reloaded.Items[0].Key);
            Assert.Equal("Bebop", reloaded.Items[0].Original);
            Assert.Equal("Bebop", reloaded.Items[0].Translation);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
