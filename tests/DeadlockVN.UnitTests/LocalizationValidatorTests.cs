using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Infrastructure.Validation;

namespace DeadlockVN.UnitTests;

public sealed class LocalizationValidatorTests
{
    private static readonly LocalizationValidator Validator = new();

    private static TranslationEntry Entry(
        string original,
        string translated,
        string key = "test_key")
    {
        return new TranslationEntry
        {
            Id = new TranslationId("test/test_english.txt", key),
            OriginalText = original,
            TranslatedText = translated
        };
    }

    private static bool HasRule(
        IReadOnlyList<ValidationIssue> issues,
        ValidationRule rule)
    {
        return issues.Any(i => i.Rule == rule);
    }

    // ---------- Placeholder ----------

    [Fact]
    public void Validate_MatchingPlaceholders_Passes()
    {
        var issues = Validator.ValidateEntry(Entry(
            "Deals {s:damage} damage to {s:target}.",
            "Gây {s:damage} sát thương lên {s:target}."));

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_MissingPlaceholder_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "Deals {s:damage} damage to {s:target}.",
            "Gây {s:damage} sát thương."));

        Assert.True(HasRule(issues, ValidationRule.MissingPlaceholder));
        Assert.Contains(issues, i => i.Message.Contains("{s:target}"));
    }

    [Fact]
    public void Validate_ExtraPlaceholder_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "Deals {s:damage} damage.",
            "Gây {s:damage} sát thương lên {s:target}."));

        Assert.True(HasRule(issues, ValidationRule.ExtraPlaceholder));
    }

    [Fact]
    public void Validate_PlaceholderCountMismatch_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "{s:a} and {s:a}",
            "{s:a}"));

        Assert.True(HasRule(issues, ValidationRule.PlaceholderCountMismatch));
    }

    [Fact]
    public void Validate_PluralTokenWithNestedToken_IsMatched()
    {
        var issues = Validator.ValidateEntry(Entry(
            "{num_damage_rows, plural, one{Top {i:num_damage_rows}} other{x}}",
            "{num_damage_rows, plural, one{Top {i:num_damage_rows}} other{x}}"));

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_PrintfTokenChanged_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "%s1 hits %s2 for %s3 damage%s4.",
            "%s1 đánh %s2 gây %s2 sát thương%s4."));

        Assert.True(HasRule(issues, ValidationRule.PrintfTokenMismatch));
    }

    [Fact]
    public void Validate_PercentNameTokenChanged_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "Gift sent to %partner%",
            "Quà tặng gửi tới %ban%"));

        Assert.True(HasRule(issues, ValidationRule.ExtraPlaceholder));
    }

    // ---------- Rỗng / trùng ----------

    [Fact]
    public void Validate_EmptyTranslation_IsError()
    {
        var issues = Validator.ValidateEntry(Entry("Close Quarters", string.Empty));

        Assert.True(HasRule(issues, ValidationRule.MissingTranslation));
    }

    [Fact]
    public void Validate_EmptyOriginalAndTranslation_NoError()
    {
        var issues = Validator.ValidateEntry(Entry(string.Empty, string.Empty));

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_SameAsOriginal_IsWarning()
    {
        var issues = Validator.ValidateEntry(Entry("Close Quarters", "Close Quarters"));

        Assert.True(HasRule(issues, ValidationRule.Untranslated));
        Assert.All(issues, i => Assert.Equal(ValidationSeverity.Warning, i.Severity));
    }

    // ---------- HTML ----------

    [Fact]
    public void Validate_MatchingHtmlStructure_Passes()
    {
        var issues = Validator.ValidateEntry(Entry(
            "<span class=\"highlight\">+{s:Damage}%</span> Speed",
            "<span class=\"highlight\">+{s:Damage}%</span> Tốc độ"));

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_DroppedHtmlTag_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "<span class=\"highlight\">+{s:Damage}%</span> Speed",
            "+{s:Damage}% Tốc độ"));

        Assert.True(HasRule(issues, ValidationRule.UnbalancedHtmlTag));
    }

    [Fact]
    public void Validate_ContextAngleBracket_IsNotTreatedAsHtml()
    {
        // VO của Deadlock có các thẻ ngữ cảnh như <laughs>, <battle cry>, <None>.
        // Chúng không phải HTML và không được báo lỗi, kể cả khi bản dịch
        // dùng thẻ ngữ cảnh khác.
        var issues = Validator.ValidateEntry(Entry(
            "<battle cry>",
            "<hét chiến tranh>"));

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_PanelTagCountMustMatch()
    {
        // <Panel> là thẻ Panorama thật (47 lần trong bản gốc).
        // Số lượng thẻ mở/đóng phải khớp với bản gốc.
        var issues = Validator.ValidateEntry(Entry(
            "<Panel class=\"AbilityPropertyIcon prop_tech_damage\"> Damage",
            "<Panel class=\"AbilityPropertyIcon prop_tech_damage\"> Sát thương"));

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Validate_DroppedPanelTag_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "<Panel class=\"AbilityPropertyIcon prop_tech_damage\"> Damage",
            "Sát thương"));

        Assert.True(HasRule(issues, ValidationRule.UnbalancedHtmlTag));
    }

    // ---------- Ký tự ----------

    [Fact]
    public void Validate_VietnameseDiacritics_AreSupported()
    {
        var issues = Validator.ValidateEntry(Entry(
            "Close Quarters",
            "Cận Chiến — Tăng sát thương ở cự ly gần, giảm 30% thời gian hồi chiêu (ẩn ứ ở ữ ỹ ị ọ ủ ế ề ể ệ ớ ờ ở ợ ắ ằ ẵ ặ)"));

        Assert.DoesNotContain(issues, i => i.Rule == ValidationRule.UnsupportedCharacter);
    }

    [Fact]
    public void Validate_JapaneseCharacters_AreSupported()
    {
        // Lore Yamato và keybind Nhật trong bản gốc phải được giữ nguyên.
        var issues = Validator.ValidateEntry(Entry(
            "Hero Lore",
            "「不屈の精神」<br>全角 変換 変"));

        Assert.DoesNotContain(issues, i => i.Rule == ValidationRule.UnsupportedCharacter);
    }

    [Fact]
    public void Validate_ControlCharacter_IsError()
    {
        var issues = Validator.ValidateEntry(Entry(
            "Test",
            "Test\u0007"));

        Assert.True(HasRule(issues, ValidationRule.ControlCharacter));
    }

    // ---------- Độ dài / khoảng trắng ----------

    [Fact]
    public void Validate_LeadingTrailingWhitespace_IsWarning()
    {
        var issues = Validator.ValidateEntry(Entry("Test", "  Kiểm tra  "));

        Assert.True(HasRule(issues, ValidationRule.LeadingOrTrailingWhitespace));
    }

    [Fact]
    public void Validate_ExcessiveLength_IsWarning()
    {
        var issues = Validator.ValidateEntry(
            Entry("Short", new string('a', 500)));

        Assert.True(HasRule(issues, ValidationRule.ExcessiveLength));
    }

    // ---------- Cấp tập ----------

    [Fact]
    public void Validate_DuplicateKeys_IsError()
    {
        var entries = new[]
        {
            Entry("A", "A", "same_key"),
            Entry("B", "B", "same_key")
        };

        var result = Validator.Validate(entries);

        Assert.True(result.HasErrors);
        Assert.True(HasRule(result.Issues, ValidationRule.DuplicateKey));
        Assert.Equal(2, result.CheckedCount);
    }

    [Fact]
    public void Validate_ValidTranslationSet_CanBuild()
    {
        var entries = new[]
        {
            Entry("Close Quarters", "Cận Chiến", "upgrade_close_range"),
            Entry("Increases close range damage.", "Tăng sát thương ở cự ly gần.", "upgrade_close_range_desc")
        };

        var result = Validator.Validate(entries);

        Assert.True(result.CanBuild);
        Assert.False(result.HasErrors);
        Assert.Equal(2, result.TranslatedCount);
    }

    [Fact]
    public void Validate_EmptySet_ReturnsEmptyResult()
    {
        var result = Validator.Validate(Array.Empty<TranslationEntry>());

        Assert.Empty(result.Issues);
        Assert.True(result.CanBuild);
        Assert.Equal(0, result.CheckedCount);
    }
}
