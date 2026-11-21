using DeadlockVN.Core.Common;

namespace DeadlockVN.UnitTests;

public sealed class TextTokenExtractorTests
{
    [Fact]
    public void Extract_FindsBracedTokens()
    {
        var tokens = TextTokenExtractor.Extract("Deals {s:damage} to {s:target}.");

        Assert.Equal(2, tokens.Count);
        Assert.Equal(TextTokenKind.Braced, tokens[0].Kind);
        Assert.Equal("{s:damage}", tokens[0].Value);
    }

    [Fact]
    public void Extract_FindsTokenWithoutColon()
    {
        var tokens = TextTokenExtractor.Extract("{hero} killed {kill}");

        Assert.Equal(2, tokens.Count);
        Assert.Equal("{hero}", tokens[0].Value);
    }

    [Fact]
    public void Extract_HandlesNestedBraces()
    {
        const string text = "{num_damage_rows, plural, one{Top {i:num_damage_rows}} other{x}}";

        var tokens = TextTokenExtractor.Extract(text);

        Assert.Single(tokens);
        Assert.Equal(text, tokens[0].Value);
    }

    [Fact]
    public void Extract_FindsPrintfTokens()
    {
        var tokens = TextTokenExtractor.Extract("%s1 hits %s2 for %s3 damage%s4.");

        Assert.Equal(4, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(TextTokenKind.Printf, t.Kind));
        Assert.Equal("%s1", tokens[0].Value);
    }

    [Fact]
    public void Extract_FindsPercentNameTokens()
    {
        var tokens = TextTokenExtractor.Extract("Gift sent to %partner%");

        Assert.Single(tokens);
        Assert.Equal(TextTokenKind.PercentName, tokens[0].Kind);
        Assert.Equal("%partner%", tokens[0].Value);
    }

    [Fact]
    public void Extract_DoesNotDoubleCountTokensInsideBraces()
    {
        // {g:citadel_inline_attribute:'SlowResistance'} chứa % không được
        // tính thành token printf.
        var tokens = TextTokenExtractor.Extract(
            "{g:citadel_inline_attribute:'50% SlowResistance'}");

        Assert.Single(tokens);
        Assert.Equal(TextTokenKind.Braced, tokens[0].Kind);
    }

    [Fact]
    public void Extract_KeepsHashPlaceholderToken()
    {
        // "{#}" là token thật trong bản gốc: "Stat Rewards max at Boon {#}".
        var tokens = TextTokenExtractor.Extract("Value: {#} end");

        Assert.Single(tokens);
        Assert.Equal("{#}", tokens[0].Value);
    }

    [Fact]
    public void Extract_UnbalancedBrace_IsIgnored()
    {
        var tokens = TextTokenExtractor.Extract("broken {s:value");

        Assert.Empty(tokens);
    }

    [Fact]
    public void Extract_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(TextTokenExtractor.Extract(null));
        Assert.Empty(TextTokenExtractor.Extract(string.Empty));
    }
}
