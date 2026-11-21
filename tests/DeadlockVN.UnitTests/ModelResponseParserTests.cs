using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="ModelResponseParser"/> — phân tích phản hồi JSON
/// của model.
/// <para>
/// Model nhỏ trả về JSON theo nhiều kiểu khác nhau. Nếu parser không
/// hiểu đúng kiểu, bản dịch sẽ bị gán nhầm cho chuỗi khác — sai hoàn
/// toàn, và game sẽ hiển thị lung tung.
/// </para>
/// </summary>
public sealed class ModelResponseParserTests
{
    [Fact]
    public void Parse_PlainStringArray()
    {
        var response = ModelResponseParser.Parse("[\"a\", \"b\"]");

        Assert.Equal(2, response.Translations.Count);
        Assert.Equal("a", response.Translations[0]);
        Assert.Equal("b", response.Translations[1]);
    }

    [Fact]
    public void Parse_ObjectArrayWithOriginalAndTranslated()
    {
        // Kiểu hay gặp nhất với qwen2.5:3b.
        const string json =
            "[{\"original\": \"Hello\", \"translated\": \"Xin chào\"}," +
            " {\"original\": \"World\", \"translated\": \"Thế giới\"}]";

        var response = ModelResponseParser.Parse(json);

        Assert.Equal(2, response.Translations.Count);
        Assert.Equal("Xin chào", response.Translations[0]);

        Assert.Equal(2, response.ByOriginal.Count);
        Assert.Equal("Xin chào", response.ByOriginal["Hello"]);
        Assert.Equal("Thế giới", response.ByOriginal["World"]);
    }

    [Fact]
    public void Parse_IndexedObject()
    {
        var response = ModelResponseParser.Parse("{\"1\": \"b\", \"0\": \"a\"}");

        Assert.Equal(2, response.Translations.Count);
        Assert.Equal("a", response.Translations[0]);
        Assert.Equal("b", response.Translations[1]);
    }

    [Fact]
    public void Parse_IndexedObject_ExposesByIndex()
    {
        // Định dạng provider yêu cầu trong prompt: JSON đánh số.
        // Nhờ ByIndex ta biết chính xác chỉ số nào bị thiếu.
        var response = ModelResponseParser.Parse(
            "{\"0\": \"a\", \"2\": \"c\"}");

        Assert.Equal(2, response.ByIndex.Count);
        Assert.Equal("a", response.ByIndex[0]);
        Assert.Equal("c", response.ByIndex[2]);
        Assert.False(response.ByIndex.ContainsKey(1));
    }

    [Fact]
    public void Parse_IndexedObjectWithNestedObjectValue_JoinsParts()
    {
        // Model hay tự tạo JSON lồng khi chuỗi gốc có nhiều thẻ HTML
        // chia thành các phần (ví dụ <span>Human:</span> ... <span>Werewolf:</span>).
        var response = ModelResponseParser.Parse(
            "{\"0\": {\"Human\": \"Theo doi hero\", \"Werewolf\": \"Theo doi mau thap\"}}");

        Assert.Single(response.Translations);
        Assert.Contains("Theo doi hero", response.Translations[0]);
        Assert.Contains("Theo doi mau thap", response.Translations[0]);
        Assert.Single(response.ByIndex);
        Assert.Equal(
            response.Translations[0],
            response.ByIndex[0]);
    }

    [Fact]
    public void Parse_IndexedObjectWithNestedObjectValue_MultipleKeys()
    {
        var response = ModelResponseParser.Parse(
            "{\"0\": {\"a\": \"x\"}, \"1\": {\"b\": \"y\"}}");

        Assert.Equal(2, response.Translations.Count);
        Assert.Equal("x", response.Translations[0]);
        Assert.Equal("y", response.Translations[1]);
    }

    [Fact]
    public void Parse_NestedArray()
    {        var response = ModelResponseParser.Parse(
            "{\"translations\": [\"a\", \"b\"]}");

        Assert.Equal(2, response.Translations.Count);
    }

    [Fact]
    public void Parse_StripsMarkdownFence()
    {
        var response = ModelResponseParser.Parse(
            "```json\n[\"a\", \"b\"]\n```");

        Assert.Equal(2, response.Translations.Count);
        Assert.Equal("a", response.Translations[0]);
    }

    [Fact]
    public void Parse_HandlesEmpty()
    {
        Assert.Empty(ModelResponseParser.Parse(null).Translations);
        Assert.Empty(ModelResponseParser.Parse(string.Empty).Translations);
        Assert.Empty(ModelResponseParser.Parse("   ").Translations);
    }

    [Fact]
    public void Parse_HandlesInvalidJson()
    {
        Assert.Empty(ModelResponseParser.Parse("not json at all").Translations);
        Assert.Empty(ModelResponseParser.Parse("{broken").Translations);
    }

    [Fact]
    public void Parse_AcceptsAlternativeFieldNames()
    {
        const string json =
            "[{\"source\": \"Hello\", \"translation\": \"Xin chào\"}]";

        var response = ModelResponseParser.Parse(json);

        Assert.Single(response.Translations);
        Assert.Equal("Xin chào", response.Translations[0]);
        Assert.Equal("Xin chào", response.ByOriginal["Hello"]);
    }

    [Fact]
    public void Parse_IgnoresArrayElementsWithoutTranslationField()
    {
        const string json =
            "[{\"foo\": 1}, {\"original\": \"Hi\", \"translated\": \"Chào\"}]";

        var response = ModelResponseParser.Parse(json);

        Assert.Single(response.Translations);
        Assert.Equal("Chào", response.Translations[0]);
    }
}
