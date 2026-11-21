using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="PromptBuilder"/> — che/khôi phục placeholder.
/// <para>
/// Đây là lớp bảo vệ quan trọng nhất của pipeline dịch tự động: nếu
/// placeholder bị hỏng, game hiển thị sai giá trị (ví dụ sát thương hiện
/// chữ thay vì số).
/// </para>
/// </summary>
public sealed class PromptBuilderTests
{
    [Fact]
    public void MaskPlaceholders_HidesBracedTokens()
    {
        string masked = PromptBuilder.MaskPlaceholders(
            "Deal {s:Damage} damage to {g:target}",
            out var placeholders);

        Assert.Equal(2, placeholders.Count);
        Assert.Equal("{s:Damage}", placeholders[0]);
        Assert.Equal("{g:target}", placeholders[1]);
        Assert.DoesNotContain("{s:Damage}", masked);
        Assert.Contains("__T0__", masked);
    }

    [Fact]
    public void MaskPlaceholders_KeepsPluralBodyTranslatable()
    {
        // Token plural: phần "one{...} other{...}" PHẢI được dịch, nên không
        // được che — chỉ tên biến được che.
        const string original = "{count, plural, one{Item} other{Items}}";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        Assert.Single(placeholders);
        Assert.Equal("count", placeholders[0]);

        Assert.Contains("one{Item}", masked);
        Assert.Contains("other{Items}", masked);
        Assert.Contains("__T0__", masked);
        Assert.DoesNotContain("{count,", masked);
    }

    [Fact]
    public void UnmaskPlaceholders_RestoresPluralVariableOnly()
    {
        const string original = "{count, plural, one{Item} other{Items}}";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        // Mô phỏng model dịch phần trong ngoặc.
        string translated = PromptBuilder.UnmaskPlaceholders(
            "{__T0__, plural, one{món đồ} other{món đồ}}",
            placeholders);

        Assert.Equal(
            "{count, plural, one{món đồ} other{món đồ}}",
            translated);
    }

    [Fact]
    public void MaskPlaceholders_HidesNestedBracedTokens()
    {
        // Token plural i18n chứa token lồng nhau.
        const string original =
            "{count, plural, one {# item} other {# items}}";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        Assert.Single(placeholders);
        Assert.Equal("count", placeholders[0]);

        // Phần trong ngoặc phải còn lại để dịch được.
        Assert.Contains("one {# item}", masked);
    }

    [Fact]
    public void MaskPlaceholders_HidesPrintfTokens()
    {
        string masked = PromptBuilder.MaskPlaceholders(
            "Hello %s1 and %s2",
            out var placeholders);

        Assert.Equal(2, placeholders.Count);
        Assert.Equal("%s1", placeholders[0]);
        Assert.Equal("%s2", placeholders[1]);
        Assert.DoesNotContain("%s1", masked);
    }

    [Fact]
    public void MaskPlaceholders_HidesPercentNameTokens()
    {
        string masked = PromptBuilder.MaskPlaceholders(
            "Hello %partner%",
            out var placeholders);

        Assert.Single(placeholders);
        Assert.Equal("%partner%", placeholders[0]);
    }

    [Fact]
    public void MaskPlaceholders_HandlesMixedTokenTypes()
    {
        const string original = "{s:Damage} to %s1 for %partner%";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        Assert.Equal(3, placeholders.Count);
        Assert.Equal("{s:Damage}", placeholders[0]);
        Assert.Equal("%s1", placeholders[1]);
        Assert.Equal("%partner%", placeholders[2]);

        // Mỗi token che có mã riêng.
        Assert.Contains("__T0__", masked);
        Assert.Contains("__T1__", masked);
        Assert.Contains("__T2__", masked);
    }

    [Fact]
    public void UnmaskPlaceholders_RestoresOriginal()
    {
        const string original = "{s:Damage} to %s1 for %partner%";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        string restored = PromptBuilder.UnmaskPlaceholders(
            masked,
            placeholders);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void UnmaskPlaceholders_RestoresWhenTranslatedTextChanged()
    {
        const string original = "Deal {s:Damage} damage";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        // Mô phỏng model trả về có thêm/bớt chữ.
        string translated = PromptBuilder.UnmaskPlaceholders(
            "Gây __T0__ sát thương",
            placeholders);

        Assert.Equal("Gây {s:Damage} sát thương", translated);
    }

    [Fact]
    public void MaskPlaceholders_LeavesPlainTextAlone()
    {
        const string original = "Hello world, no tokens here.";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        Assert.Empty(placeholders);
        Assert.Equal(original, masked);
    }

    [Fact]
    public void MaskPlaceholders_HandlesUnbalancedBrace()
    {
        const string original = "Broken {s:Damage";

        string masked = PromptBuilder.MaskPlaceholders(
            original,
            out var placeholders);

        // Không cân bằng -> không phải token, giữ nguyên.
        Assert.Empty(placeholders);
        Assert.Equal(original, masked);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsHardRules()
    {
        string prompt = PromptBuilder.BuildSystemPrompt("heroes");

        Assert.Contains("Việt Nam", prompt);
        Assert.Contains("tiếng Việt", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("heroes", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_DoesNotAskModelToPreserveMarkup()
    {
        // Model KHÔNG BAO GIỜ nhìn thấy token hay thẻ HTML — nên prompt
        // không được yêu cầu giữ chúng. Yêu cầu đó chỉ làm model 3B
        // rối và tăng thời gian sinh.
        string prompt = PromptBuilder.BuildSystemPrompt(null);

        Assert.DoesNotContain("placeholder", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<span>", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUserPrompt_NumbersEveryString()
    {
        string prompt = PromptBuilder.BuildUserPrompt(
            new[] { "First", "Second" });

        Assert.Contains("[0] First", prompt);
        Assert.Contains("[1] Second", prompt);
        Assert.Contains("JSON", prompt);
    }
}
