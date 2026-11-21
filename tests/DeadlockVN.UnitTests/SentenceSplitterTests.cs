using DeadlockVN.Infrastructure.Translation;
using Xunit;

namespace DeadlockVN.UnitTests;

public sealed class SentenceSplitterTests
{
    [Fact]
    public void Split_ShortText_ReturnsSinglePiece()
    {
        var pieces = SentenceSplitter.Split("Hello world.");

        Assert.Single(pieces);
        Assert.Equal("Hello world.", pieces[0]);
    }

    [Fact]
    public void Split_MultipleSentences_PreservesAllCharacters()
    {
        const string text = "First one. Second one! Third one?";

        var pieces = SentenceSplitter.Split(text);

        Assert.Equal(text, string.Concat(pieces));
    }

    [Fact]
    public void Split_DecimalPoint_IsNotTreatedAsBoundary()
    {
        const string text = "Deals 2.86 damage per second. That is a lot.";

        var pieces = SentenceSplitter.Split(text);

        Assert.Equal(text, string.Concat(pieces));
        Assert.Contains("2.86", string.Concat(pieces));
    }

    [Fact]
    public void Split_VeryLongText_ProducesPiecesUnderLimit()
    {
        string text = string.Join(" ", Enumerable.Repeat(
            "The quick brown fox jumps over the lazy dog.", 100));

        var pieces = SentenceSplitter.Split(text, maxChars: 200);

        Assert.NotEmpty(pieces);

        foreach (string piece in pieces)
        {
            Assert.True(
                piece.Length <= 200,
                $"Mảnh quá dài ({piece.Length}): {piece[..Math.Min(60, piece.Length)]}");
        }

        Assert.Equal(text, string.Concat(pieces));
    }

    [Fact]
    public void Split_LongTextWithoutSpaces_StillSplits()
    {
        string text = new string('a', 1000);

        var pieces = SentenceSplitter.Split(text, maxChars: 300);

        Assert.NotEmpty(pieces);

        foreach (string piece in pieces)
        {
            Assert.True(piece.Length <= 300);
        }

        Assert.Equal(text, string.Concat(pieces));
    }

    [Fact]
    public void Split_EmptyText_ReturnsEmptyString()
    {
        var pieces = SentenceSplitter.Split(string.Empty);

        Assert.Single(pieces);
        Assert.Equal(string.Empty, pieces[0]);
    }

    [Fact]
    public void Split_HtmlTags_AreNotBrokenApart()
    {
        const string text = "<b>Bold</b> and <i>italic</i> text. More.";

        var pieces = SentenceSplitter.Split(text);

        Assert.Equal(text, string.Concat(pieces));
    }
}
