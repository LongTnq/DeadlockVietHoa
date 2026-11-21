using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="TextPartSplitter"/> — tách chuỗi thành phần văn bản
/// và phần cố định (token, thẻ HTML).
/// <para>
/// Đây là lớp bảo vệ thứ hai của pipeline dịch tự động. Nếu tách sai,
/// thẻ HTML sẽ bị mất hoặc đảo thứ tự — game hiển thị vỡ layout.
/// </para>
/// </summary>
public sealed class TextPartSplitterTests
{
    private static string Join(IEnumerable<TextPart> parts) =>
        string.Concat(parts.Select(part => part.Text));

    [Fact]
    public void Split_PlainText_IsOneLiteral()
    {
        var parts = TextPartSplitter.Split("Hello world");

        Assert.Single(parts);
        Assert.Equal(TextPartKind.Literal, parts[0].Kind);
        Assert.Equal("Hello world", parts[0].Text);
    }

    [Fact]
    public void Split_Empty_ReturnsEmpty()
    {
        Assert.Empty(TextPartSplitter.Split(string.Empty));
        Assert.Empty(TextPartSplitter.Split(null));
    }

    [Fact]
    public void Split_KeepsBracedTokenAsFixed()
    {
        var parts = TextPartSplitter.Split("Deal {s:Damage} damage");

        Assert.Equal(3, parts.Count);

        Assert.Equal(TextPartKind.Literal, parts[0].Kind);
        Assert.Equal("Deal ", parts[0].Text);

        Assert.Equal(TextPartKind.Fixed, parts[1].Kind);
        Assert.Equal("{s:Damage}", parts[1].Text);

        Assert.Equal(TextPartKind.Literal, parts[2].Kind);
        Assert.Equal(" damage", parts[2].Text);
    }

    [Fact]
    public void Split_KeepsHtmlTagAsFixed()
    {
        var parts = TextPartSplitter.Split(
            "<span class=\"highlight\">Melee</span> attack");

        Assert.Equal(4, parts.Count);

        Assert.Equal(TextPartKind.Fixed, parts[0].Kind);
        Assert.Equal("<span class=\"highlight\">", parts[0].Text);

        Assert.Equal(TextPartKind.Literal, parts[1].Kind);
        Assert.Equal("Melee", parts[1].Text);

        Assert.Equal(TextPartKind.Fixed, parts[2].Kind);
        Assert.Equal("</span>", parts[2].Text);

        Assert.Equal(TextPartKind.Literal, parts[3].Kind);
        Assert.Equal(" attack", parts[3].Text);
    }

    [Fact]
    public void Split_KeepsContextTagAsFixed()
    {
        // Thẻ ngữ cảnh trong VO — không dịch.
        var parts = TextPartSplitter.Split("<laughs>Hello</laughs>");

        Assert.Equal(TextPartKind.Fixed, parts[0].Kind);
        Assert.Equal("<laughs>", parts[0].Text);
        Assert.Equal(TextPartKind.Literal, parts[1].Kind);
        Assert.Equal("Hello", parts[1].Text);
        Assert.Equal(TextPartKind.Fixed, parts[2].Kind);
        Assert.Equal("</laughs>", parts[2].Text);
    }

    [Fact]
    public void Split_KeepsPrintfAndPercentTokens()
    {
        var parts = TextPartSplitter.Split("Hi %s1 from %partner%");

        Assert.Equal(TextPartKind.Fixed, parts[1].Kind);
        Assert.Equal("%s1", parts[1].Text);

        Assert.Equal(TextPartKind.Fixed, parts[3].Kind);
        Assert.Equal("%partner%", parts[3].Text);
    }

    [Fact]
    public void Split_Plural_KeepsFrameFixedButBodyTranslatable()
    {
        var parts = TextPartSplitter.Split(
            "{count, plural, one{Item} other{Items}}");

        // Khung "{count, plural, " cố định.
        Assert.Equal(TextPartKind.Fixed, parts[0].Kind);
        Assert.Equal("{count, plural, ", parts[0].Text);

        // "one{" cố định.
        Assert.Equal(TextPartKind.Fixed, parts[1].Kind);
        Assert.Equal("one{", parts[1].Text);

        // "Item" là văn bản -> dịch được.
        Assert.Equal(TextPartKind.Literal, parts[2].Kind);
        Assert.Equal("Item", parts[2].Text);
    }

    [Fact]
    public void Split_Plural_CommaSeparatedBranches()
    {
        // Một số bản dịch dùng dấu phẩy phân cách nhánh.
        var parts = TextPartSplitter.Split(
            "{count, plural, one{Item}, other{Items}}");

        Assert.Contains(
            parts,
            part => part.Kind == TextPartKind.Fixed &&
                    part.Text == "{count, plural, ");

        Assert.Contains(
            parts,
            part => part.Kind == TextPartKind.Literal &&
                    part.Text == "Item");

        Assert.Contains(
            parts,
            part => part.Kind == TextPartKind.Literal &&
                    part.Text == "Items");
    }

    [Fact]
    public void Split_Plural_NestedTokenInsideBranch()
    {
        // Dữ liệu thật của game: token {s:...} nằm bên trong nhánh plural.
        var parts = TextPartSplitter.Split(
            "{num_conflicts, plural, one{You have {s:binding_primary} saved} other{You have saved}}");

        // Token lồng phải được giữ cố định.
        Assert.Contains(
            parts,
            part => part.Kind == TextPartKind.Fixed &&
                    part.Text == "{s:binding_primary}");

        // Phần chữ vẫn dịch được.
        Assert.Contains(
            parts,
            part => part.Kind == TextPartKind.Literal &&
                    part.Text.Contains("You have"));

        Assert.Contains(
            parts,
            part => part.Kind == TextPartKind.Literal &&
                    part.Text.Contains("saved"));
    }

    [Fact]
    public void Split_BoonToken_IsFixed()
    {
        // "{#}" là token số boon của Valve — phải giữ nguyên.
        var parts = TextPartSplitter.Split("Stat Rewards max at Boon {#}");

        Assert.Contains(
            parts,
            part => part.Kind == TextPartKind.Fixed &&
                    part.Text == "{#}");
    }

    [Fact]
    public void Split_UnbalancedBrace_KeepsAsLiteral()
    {
        var parts = TextPartSplitter.Split("Broken {s:Damage");

        Assert.Single(parts);
        Assert.Equal(TextPartKind.Literal, parts[0].Kind);
        Assert.Equal("Broken {s:Damage", parts[0].Text);
    }

    [Fact]
    public void Split_RoundTrip_ReconstructsOriginal()
    {
        // Bất kỳ chuỗi nào cũng phải ghép lại được nguyên vẹn.
        string[] samples =
        {
            "Hello world",
            "Deal {s:Damage} damage",
            "<span class=\"highlight\">Melee</span> attack",
            "{count, plural, one{Item} other{Items}} ready",
            "<b>Warning</b><br><span class=\"diminish\">Heal</span>",
            "{s:ammo} back and %s1 from %partner%",
            "<laughs>Heh</laughs>",
            "Stat Rewards max at Boon {#}",
            "{num_conflicts, plural, one{You have {s:binding} saved} other{You saved}}",
            "",
            "Mixed {s:A} and <i>b</i> text %s1"
        };

        foreach (string sample in samples)
        {
            Assert.Equal(sample, Join(TextPartSplitter.Split(sample)));
        }
    }

    [Fact]
    public void Split_NoLiteralContainsTagOrToken()
    {
        // Bất kỳ phần văn bản nào gửi cho model cũng không được chứa
        // thẻ hoặc token — nếu không thì model có thể phá hỏng.
        string[] samples =
        {
            "Deal {s:Damage} damage",
            "<span class=\"highlight\">Melee</span> attack",
            "{count, plural, one{Item} other{Items}} ready",
            "Mixed {s:A} and <i>b</i> text %s1"
        };

        foreach (string sample in samples)
        {
            foreach (TextPart part in TextPartSplitter.Split(sample))
            {
                if (part.Kind != TextPartKind.Literal)
                {
                    continue;
                }

                Assert.DoesNotContain("{", part.Text);
                Assert.DoesNotContain("}", part.Text);
                Assert.DoesNotContain("<", part.Text);
                Assert.DoesNotContain(">", part.Text);
                Assert.DoesNotContain("%s1", part.Text);
            }
        }
    }
}
