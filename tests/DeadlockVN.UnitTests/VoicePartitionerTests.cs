using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Kiểm thử tách nhóm Voice theo hero.
/// </summary>
public class VoicePartitionerTests
{
    private static TranslationEntry Entry(
        string key,
        LocalizationCategory category = LocalizationCategory.Voice)
    {
        return new TranslationEntry
        {
            Id = new TranslationId("citadel_generated_vo/citadel_generated_vo_english.txt", key),
            Category = category
        };
    }

    private static TranslationEntry HeroEntry(string slug)
    {
        return Entry($"hero_{slug}:n", LocalizationCategory.Heroes);
    }

    [Fact]
    public void ExtractHeroSlugs_ReadsSlugsFromHeroesCategory()
    {
        var entries = new[]
        {
            HeroEntry("gigawatt"),
            HeroEntry("haze"),
            Entry("gigawatt_kill_01")
        };

        var slugs = VoicePartitioner.ExtractHeroSlugs(entries);

        Assert.Equal(2, slugs.Count);
        Assert.Contains("gigawatt", slugs);
        Assert.Contains("haze", slugs);
    }

    [Fact]
    public void ExtractHeroSlugs_IgnoresSearchAndSortVariants()
    {
        var entries = new[]
        {
            HeroEntry("gigawatt"),
            Entry("hero_gigawatt_search:n", LocalizationCategory.Heroes),
            Entry("hero_gigawatt_sort:n", LocalizationCategory.Heroes)
        };

        var slugs = VoicePartitioner.ExtractHeroSlugs(entries);

        Assert.Single(slugs);
        Assert.Contains("gigawatt", slugs);
    }

    [Fact]
    public void GetBucket_MatchesHeroPrefix()
    {
        var slugs = new HashSet<string> { "gigawatt", "haze" };

        string bucket = VoicePartitioner.GetBucket(Entry("gigawatt_kill_01"), slugs);

        Assert.Equal("gigawatt", bucket);
    }

    [Fact]
    public void GetBucket_ReturnsCommon_WhenNoHeroMatches()
    {
        var slugs = new HashSet<string> { "gigawatt" };

        string bucket = VoicePartitioner.GetBucket(Entry("Language"), slugs);

        Assert.Equal(VoicePartitioner.CommonBucket, bucket);
    }

    [Fact]
    public void GetBucket_UsesLongestPrefixMatch()
    {
        // "artist" và "artillery" có tiền tố chung -> phải chọn tiền tố dài hơn.
        var slugs = new HashSet<string> { "art", "artist" };

        string bucket = VoicePartitioner.GetBucket(Entry("artist_kill_01"), slugs);

        Assert.Equal("artist", bucket);
    }

    [Fact]
    public void GetBucket_DoesNotMatchPartialWord()
    {
        // "haze" không được match với "hazel" -> phải có dấu gạch dưới phía sau.
        var slugs = new HashSet<string> { "haze" };

        string bucket = VoicePartitioner.GetBucket(Entry("hazelnut_01"), slugs);

        Assert.Equal(VoicePartitioner.CommonBucket, bucket);
    }

    [Fact]
    public void GetBucket_ReturnsCommon_WhenNoSlugsKnown()
    {
        string bucket = VoicePartitioner.GetBucket(
            Entry("gigawatt_kill_01"),
            new HashSet<string>());

        Assert.Equal(VoicePartitioner.CommonBucket, bucket);
    }

    [Fact]
    public void Group_SplitsEntriesIntoBuckets()
    {
        var slugs = new HashSet<string> { "gigawatt", "haze" };

        var entries = new[]
        {
            Entry("gigawatt_kill_01"),
            Entry("gigawatt_kill_02"),
            Entry("haze_kill_01"),
            Entry("Language")
        };

        var groups = VoicePartitioner.Group(entries, slugs);

        Assert.Single(groups["haze"]);
        Assert.Single(groups[VoicePartitioner.CommonBucket]);
    }

    [Fact]
    public void Group_PreservesAllEntries()
    {
        var slugs = new HashSet<string> { "gigawatt" };

        var entries = Enumerable
            .Range(0, 100)
            .Select(i => Entry($"gigawatt_line_{i}"))
            .ToArray();

        var groups = VoicePartitioner.Group(entries, slugs);

        int total = groups.Sum(g => g.Value.Count);

        Assert.Equal(100, total);
    }
}
