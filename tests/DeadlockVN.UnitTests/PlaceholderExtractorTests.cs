using DeadlockVN.Core.Common;

namespace DeadlockVN.UnitTests;

public sealed class PlaceholderExtractorTests
{
    [Theory]
    [InlineData("Deals {s:damage} damage to {s:target}.", new[] { "{s:damage}", "{s:target}" })]
    [InlineData("{g:citadel_inline_attribute:'Pulling'}", new[] { "{g:citadel_inline_attribute:'Pulling'}" })]
    [InlineData("{hero} killed {kill}", new[] { "{hero}", "{kill}" })]
    [InlineData("no placeholder here", new string[0])]
    [InlineData(null, new string[0])]
    public void Extract_FindsPlaceholders(string? text, string[] expected)
    {
        var result = PlaceholderExtractor.Extract(text);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Extract_HandlesNestedBraces()
    {
        var result = PlaceholderExtractor.Extract(
            "{stat_value, plural, one{kill} other{kills}}");

        Assert.Equal(
            new[] { "{stat_value, plural, one{kill} other{kills}}" },
            result);
    }

    [Fact]
    public void Extract_UnbalancedBrace_IsIgnored()
    {
        var result = PlaceholderExtractor.Extract("broken {s:value");

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractDistinct_RemovesDuplicates()
    {
        var result = PlaceholderExtractor.ExtractDistinct(
            "{s:a} and {s:a} and {s:b}");

        Assert.Equal(new[] { "{s:a}", "{s:b}" }, result);
    }

    [Theory]
    [InlineData("{s:sign}", "s")]
    [InlineData("{g:citadel_inline_attribute:'Pulling'}", "g")]
    [InlineData("{d:buildNumber}", "d")]
    [InlineData("{i:item_level}", "i")]
    [InlineData("{f:1:damage_meter_duration}", "f")]
    [InlineData("{hero}", "(none)")]
    [InlineData("{%param1%}", "(none)")]
    [InlineData(
        "{num_damage_rows, plural, one{Top {i:num_damage_rows}} other{x}}",
        "plural")]
    public void GetKind_ReturnsPrefix(string placeholder, string expected)
    {
        Assert.Equal(expected, PlaceholderExtractor.GetKind(placeholder));
    }
}
