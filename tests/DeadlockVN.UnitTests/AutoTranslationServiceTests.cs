using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="AutoTranslationService"/> — logic chia lô, retry và
/// trạng thái.
/// <para>
/// Dùng provider giả để kiểm tra logic mà không cần Ollama.
/// </para>
/// </summary>
public sealed class AutoTranslationServiceTests
{
    /// <summary>
    /// Provider giả: dịch mọi chuỗi thành "VI: " + chuỗi gốc.
    /// </summary>
    private sealed class FakeProvider : ITranslationProvider
    {
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<string>> _translate;

        public FakeProvider(
            Func<IReadOnlyList<string>, IReadOnlyList<string>>? translate = null)
        {
            _translate = translate ?? (texts =>
                texts.Select(t => "VI: " + t).ToList());
        }

        public string Name => "fake";

        public int BatchCount { get; private set; }

        public int TotalRequested { get; private set; }

        public Task<bool> IsAvailableAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<string>> TranslateBatchAsync(
            IReadOnlyList<string> texts,
            string? context,
            CancellationToken cancellationToken = default)
        {
            BatchCount++;
            TotalRequested += texts.Count;

            return Task.FromResult(_translate(texts));
        }
    }

    private static TranslationDocument CreateDocument(int count)
    {
        var document = new TranslationDocument();

        for (int i = 0; i < count; i++)
        {
            document.Entries.Add(new TranslationEntry
            {
                Id = new TranslationId(
                    "citadel_heroes/english.txt",
                    $"key_{i:D4}"),
                OriginalText = $"Text {i}",
                TranslatedText = string.Empty,
                Status = TranslationStatus.Pending,
                Category = LocalizationCategory.Heroes,
                OriginalTextSnapshot = $"Text {i}"
            });
        }

        return document;
    }

    private static TranslationProviderConfig CreateConfig(int batchSize = 30) =>
        new()
        {
            BatchSize = batchSize,
            MaxRetries = 3
        };

    [Fact]
    public async Task TranslateAsync_TranslatesAllPendingEntries()
    {
        var document = CreateDocument(10);
        var provider = new FakeProvider();

        var service = new AutoTranslationService(
            provider,
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(document);

        Assert.Equal(10, result.Translated);
        Assert.Equal(0, result.Failed);

        Assert.All(
            document.Entries,
            entry =>
            {
                Assert.Equal(TranslationStatus.Reviewed, entry.Status);
                Assert.StartsWith("VI: ", entry.TranslatedText);
            });
    }

    [Fact]
    public async Task TranslateAsync_SkipsAlreadyTranslatedEntries()
    {
        var document = CreateDocument(5);

        document.Entries[0].Status = TranslationStatus.Approved;
        document.Entries[0].TranslatedText = "Đã dịch";

        document.Entries[1].Status = TranslationStatus.Reviewed;
        document.Entries[1].TranslatedText = "Đã dịch";

        var provider = new FakeProvider();

        var service = new AutoTranslationService(
            provider,
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(document);

        Assert.Equal(3, result.Translated);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(3, provider.TotalRequested);
    }

    [Fact]
    public async Task TranslateAsync_MarksMachineTranslationAsReviewedNotApproved()
    {
        var document = CreateDocument(3);

        var service = new AutoTranslationService(
            new FakeProvider(),
            new TranslationGuard(),
            CreateConfig());

        await service.TranslateAsync(document);

        // Bản dịch máy TUYỆT ĐỐI không được tự động Approved.
        Assert.All(
            document.Entries,
            entry => Assert.NotEqual(TranslationStatus.Approved, entry.Status));
    }

    [Fact]
    public async Task TranslateAsync_SplitsIntoBatches()
    {
        var document = CreateDocument(25);
        var provider = new FakeProvider();

        var service = new AutoTranslationService(
            provider,
            new TranslationGuard(),
            CreateConfig(batchSize: 10));

        var result = await service.TranslateAsync(document);

        Assert.Equal(3, provider.BatchCount);
        Assert.Equal(3, result.Batches);
        Assert.Equal(25, result.Translated);
    }

    [Fact]
    public async Task TranslateAsync_RetriesBrokenTranslationsAndKeepsThemPending()
    {
        var document = CreateDocument(2);

        // Entry có placeholder -> bản dịch làm mất placeholder sẽ bị guard
        // chặn thật sự.
        document.Entries[0].OriginalText = "Deal {s:Damage} damage";
        document.Entries[0].OriginalTextSnapshot = "Deal {s:Damage} damage";

        document.Entries[1].OriginalText = "Heal {s:Health} over time";
        document.Entries[1].OriginalTextSnapshot = "Heal {s:Health} over time";

        // Provider luôn trả về bản dịch hỏng (mất placeholder).
        var provider = new FakeProvider(texts =>
            texts.Select(t => "Không có placeholder").ToList());

        var service = new AutoTranslationService(
            provider,
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(document);

        Assert.Equal(0, result.Translated);
        Assert.Equal(2, result.Failed);

        // Bản dịch hỏng KHÔNG được ghi -> entry vẫn Pending.
        Assert.All(
            document.Entries,
            entry =>
            {
                Assert.Equal(TranslationStatus.Pending, entry.Status);
                Assert.True(string.IsNullOrEmpty(entry.TranslatedText));
            });

        // 2 entry x 3 lần thử = 6 lời gọi provider.
        Assert.Equal(6, provider.TotalRequested);
    }

    [Fact]
    public async Task TranslateAsync_AcceptsTranslationOnSecondAttempt()
    {
        var document = CreateDocument(1);

        document.Entries[0].OriginalText = "Deal {s:Damage} damage";
        document.Entries[0].OriginalTextSnapshot = "Deal {s:Damage} damage";

        int calls = 0;

        var provider = new FakeProvider(texts =>
        {
            calls++;

            // Lần đầu mất placeholder (hỏng), lần sau giữ nguyên.
            return calls == 1
                ? new List<string> { "Gây sát thương" }
                : new List<string> { "Gây {s:Damage} sát thương" };
        });

        var service = new AutoTranslationService(
            provider,
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(document);

        Assert.Equal(1, result.Translated);
        Assert.Equal(0, result.Failed);

        var entry = document.Entries[0];

        Assert.Equal(TranslationStatus.Reviewed, entry.Status);
        Assert.Equal("Gây {s:Damage} sát thương", entry.TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_FiltersByCategory()
    {
        var document = CreateDocument(10);

        for (int i = 0; i < 5; i++)
        {
            document.Entries[i].Category = LocalizationCategory.Items;
        }

        var service = new AutoTranslationService(
            new FakeProvider(),
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(
            document,
            new[] { LocalizationCategory.Heroes });

        Assert.Equal(5, result.Translated);
        Assert.Equal(5, result.Skipped);
    }

    [Fact]
    public async Task TranslateAsync_SkipsVoiceByDefault()
    {
        var document = CreateDocument(4);

        document.Entries[0].Category = LocalizationCategory.Voice;

        var service = new AutoTranslationService(
            new FakeProvider(),
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(document);

        // 4 entry, 1 là voice -> chỉ 3 được dịch.
        Assert.Equal(3, result.Translated);
    }

    [Fact]
    public async Task TranslateAsync_IncludesVoiceWhenExplicitlyRequested()
    {
        var document = CreateDocument(2);

        document.Entries[0].Category = LocalizationCategory.Voice;

        var service = new AutoTranslationService(
            new FakeProvider(),
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(
            document,
            new[] { LocalizationCategory.Voice });

        Assert.Equal(1, result.Translated);
    }

    [Fact]
    public async Task TranslateAsync_SkipsEmptyOriginalText()
    {
        var document = CreateDocument(3);

        document.Entries[1].OriginalText = string.Empty;

        var service = new AutoTranslationService(
            new FakeProvider(),
            new TranslationGuard(),
            CreateConfig());

        var result = await service.TranslateAsync(document);

        Assert.Equal(2, result.Translated);
    }

    [Fact]
    public async Task TranslateAsync_ReportsProgress()
    {
        var document = CreateDocument(10);

        var progress = new List<int>();

        var service = new AutoTranslationService(
            new FakeProvider(),
            new TranslationGuard(),
            CreateConfig(batchSize: 5));

        await service.TranslateAsync(
            document,
            null,
            (done, total, _) => progress.Add(done));

        Assert.Equal(new[] { 5, 10 }, progress);
    }
}
