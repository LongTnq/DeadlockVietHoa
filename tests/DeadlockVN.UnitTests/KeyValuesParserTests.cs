using DeadlockVN.Infrastructure.Parsers.KeyValues;
using DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

namespace DeadlockVN.UnitTests;

public sealed class KeyValuesParserTests
{
    private static KeyValuesDocument Parse(string source)
    {
        return new KeyValuesParser().Parse(source);
    }

    [Fact]
    public void Parse_EmptySource_ReturnsEmptyDocument()
    {
        var document = Parse(string.Empty);

        Assert.Empty(document.Nodes);
    }

    [Fact]
    public void Parse_SimpleKeyValue_ReturnsValue()
    {
        var document = Parse("""
            "lang"
            {
                "Language" "english"
                "Tokens"
                {
                    "upgrade_close_range" "Close Quarters"
                }
            }
            """);

        Assert.Single(document.Nodes);
        Assert.Equal("lang", document.Nodes[0].Key);
        Assert.True(document.Nodes[0].IsObject);

        var entries = document.EnumerateEntries().ToList();

        Assert.Equal(2, entries.Count);
        Assert.Equal("lang/Language", entries[0].Path);
        Assert.Equal("english", entries[0].Value);
        Assert.Equal("lang/Tokens/upgrade_close_range", entries[1].Path);
        Assert.Equal("Close Quarters", entries[1].Value);
    }

    [Fact]
    public void Parse_SingleLineComment_IsIgnored()
    {
        var document = Parse("""
            // This is a comment
            "lang"
            {
                // Another comment
                "Language" "english" // trailing comment
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Equal("english", entries[0].Value);
    }

    [Fact]
    public void Parse_BlockComment_IsIgnored()
    {
        var document = Parse("""
            /* multi
               line
               comment */
            "lang"
            {
                "Language" "english"
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Equal("english", entries[0].Value);
    }

    [Fact]
    public void Parse_EscapedQuote_IsDecoded()
    {
        var document = Parse("""
            "lang"
            {
                "hero" "<span class=\"highlight\">+{s:Damage}%</span>"
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Contains("\"highlight\"", entries[0].Value);
    }

    [Fact]
    public void Parse_UnknownEscapeSequence_IsPreserved()
    {
        var document = Parse("""
            "lang"
            {
                "key" "line1\'line2"
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Equal("line1\\'line2", entries[0].Value);
    }

    [Fact]
    public void Parse_BackslashN_IsDecodedToNewline()
    {
        // Engine Valve giải mã \n thành newline thật khi nạp file.
        var document = Parse("""
            "lang"
            {
                "key" "line1\nline2"
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Equal("line1\nline2", entries[0].Value);
    }

    [Fact]
    public void Parse_ValueWithRealNewline_IsPreserved()
    {
        var document = Parse("""
            "lang"
            {
                "key" "<b>10/24/2024</b><br>
                    <li>Hero Added</li>"
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Contains("<br>", entries[0].Value);
        Assert.Contains("Hero Added", entries[0].Value);
    }

    [Fact]
    public void Parse_Utf8Bom_IsSkipped()
    {
        var document = Parse("\uFEFF\"lang\"\n{\n\"Language\" \"english\"\n}\n");

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Equal("english", entries[0].Value);
    }

    [Fact]
    public void Parse_TwoKeyValuePairsOnSameLine_AreBothParsed()
    {
        // Trong citadel_heroes_english.txt có các dòng dạng:
        //   "key_a" "value_a"    "key_b" "value_b"
        var document = Parse("""
            "lang"
            {
                "EvasionWhileSliding_postvalue_label" "Evasion While Sliding"		"EvasionWhileSliding_postfix"			"%"
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Equal(2, entries.Count);
        Assert.Equal("lang/EvasionWhileSliding_postvalue_label", entries[0].Path);
        Assert.Equal("lang/EvasionWhileSliding_postfix", entries[1].Path);
        Assert.Equal("%", entries[1].Value);
    }

    [Fact]
    public void Parse_KeyWithValueOnNextLine_IsParsed()
    {
        // Regex theo dòng sẽ bỏ sót trường hợp này, parser thì không.
        var document = Parse("""
            "lang"
            {
                "ability_unicorn_luminousstrike_t2_desc"		"{s:AbilityCooldown}s Cooldown"						
            "ability_unicorn_luminousstrike_t3_desc"		"+{s:MagicIncreasePerStack}% Spirit Amp"
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Equal(2, entries.Count);
        Assert.Equal(
            "lang/ability_unicorn_luminousstrike_t3_desc",
            entries[1].Path);
    }

    [Fact]
    public void Parse_VietnameseText_IsPreserved()
    {
        var document = Parse("""
            "lang"
            {
                "key" "Cận Chiến - Tăng sát thương ở cự ly gần."
            }
            """);

        var entries = document.EnumerateEntries().ToList();

        Assert.Single(entries);
        Assert.Equal("Cận Chiến - Tăng sát thương ở cự ly gần.", entries[0].Value);
    }

    [Fact]
    public void Parse_MissingClosingBrace_ThrowsFormatException()
    {
        var ex = Assert.Throws<FormatException>(
            () => Parse("\"lang\"\n{\n\"Language\" \"english\"\n"));

        Assert.Contains("closing brace", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_UnterminatedString_ThrowsFormatException()
    {
        var ex = Assert.Throws<FormatException>(
            () => Parse("\"lang\"\n{\n\"key\" \"unterminated\n}\n"));

        Assert.Contains("Unterminated quoted string", ex.Message);
    }

    [Fact]
    public void Parse_UnexpectedClosingBrace_ThrowsFormatException()
    {
        var ex = Assert.Throws<FormatException>(
            () => Parse("\"lang\"\n{\n\"key\" \"value\"\n}\n}\n"));

        Assert.Contains("Unexpected closing brace", ex.Message);
    }
}
