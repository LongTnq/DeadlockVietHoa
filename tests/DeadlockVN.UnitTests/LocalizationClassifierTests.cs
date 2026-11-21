using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Localization;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Kiểm thử phân loại nhóm nội dung localization.
/// </summary>
public class LocalizationClassifierTests
{
    private readonly LocalizationClassifier _classifier = new();

    private static TranslationEntry Entry(string sourceFile, string key)
    {
        return new TranslationEntry
        {
            Id = new TranslationId(sourceFile, key),
            OriginalText = "text"
        };
    }

    [Theory]
    [InlineData("citadel_generated_vo/citadel_generated_vo_english.txt", "gigawatt_kill_01", LocalizationCategory.Voice)]
    [InlineData("citadel_gc_mod_names/citadel_gc_mod_names_english.txt", "upgrade_close_range", LocalizationCategory.Items)]
    [InlineData("citadel_gc_hero_names/citadel_gc_hero_names_english.txt", "hero_gigawatt:n", LocalizationCategory.Heroes)]
    [InlineData("citadel_heroes/citadel_heroes_english.txt", "citadel_ability_gigawatt_bomb", LocalizationCategory.Abilities)]
    [InlineData("citadel_attributes/citadel_attributes_english.txt", "MODIFIER_STATE_ROOTED", LocalizationCategory.Ui)]
    [InlineData("citadel_main/citadel_main_english.txt", "HUD_Health", LocalizationCategory.Ui)]
    [InlineData("citadel_vdata/accolades/accolades_english.txt", "accolade_1", LocalizationCategory.Ui)]
    [InlineData("citadel_patch_notes/citadel_patch_notes_english.txt", "patch_1", LocalizationCategory.Misc)]
    [InlineData("citadel_seasonal/citadel_seasonal_english.txt", "season_1", LocalizationCategory.Misc)]
    public void Classify_MapsFileToExpectedCategory(
        string sourceFile,
        string key,
        LocalizationCategory expected)
    {
        var entry = Entry(sourceFile, key);

        LocalizationCategory actual = _classifier.Classify(entry);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Classify_FallsBackToKeyPrefix_WhenFileUnknown()
    {
        var ability = Entry("unknown/unknown_english.txt", "citadel_ability_test");
        var upgrade = Entry("unknown/unknown_english.txt", "upgrade_test");

        Assert.Equal(LocalizationCategory.Abilities, _classifier.Classify(ability));
        Assert.Equal(LocalizationCategory.Items, _classifier.Classify(upgrade));
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        var entry = Entry("CITADEL_GENERATED_VO/CITADEL_GENERATED_VO_ENGLISH.TXT", "x");

        Assert.Equal(LocalizationCategory.Voice, _classifier.Classify(entry));
    }
}
