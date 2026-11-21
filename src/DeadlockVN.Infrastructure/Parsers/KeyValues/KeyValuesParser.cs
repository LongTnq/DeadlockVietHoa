using DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

namespace DeadlockVN.Infrastructure.Parsers.KeyValues;

/// <summary>
/// Parser cho định dạng Valve KeyValues (KV1) — định dạng dùng bởi các
/// file localization của Deadlock (<c>citadel_*_english.txt</c>).
/// <para>
/// Ví dụ:
/// </para>
/// <code>
/// "lang"
/// {
///     "Language" "english"
///     "Tokens"
///     {
///         "upgrade_close_range" "Close Quarters"
///     }
/// }
/// </code>
/// <para>
/// Parser chỉ đọc; không ghi lại file. Việc xuất lại file để đóng gói VPK
/// sẽ dùng cùng AST này ở milestone sau.
/// </para>
/// </summary>
public sealed class KeyValuesParser
{
    private KeyValuesLexer _lexer = null!;

    private KeyValuesToken _currentToken = null!;

    /// <summary>
    /// Parse nội dung một file KeyValues.
    /// </summary>
    /// <param name="source">Nội dung file (hỗ trợ BOM UTF-8 ở đầu).</param>
    /// <returns>Cây node đã parse.</returns>
    /// <exception cref="FormatException">
    /// Nội dung không đúng cú pháp KeyValues.
    /// </exception>
    public KeyValuesDocument Parse(string source)
    {
        _lexer = new KeyValuesLexer(source);

        _currentToken = _lexer.ReadNextToken();

        var nodes = ParseNodes(
            expectClosingBrace: false);

        return new KeyValuesDocument(nodes);
    }

    private List<KeyValuesNode> ParseNodes(
        bool expectClosingBrace)
    {
        var nodes = new List<KeyValuesNode>();

        while (_currentToken.Type != KeyValuesTokenType.EndOfFile)
        {
            if (_currentToken.Type == KeyValuesTokenType.CloseBrace)
            {
                if (!expectClosingBrace)
                {
                    throw CreateError(
                        "Unexpected closing brace.");
                }

                Advance();
                return nodes;
            }

            if (_currentToken.Type != KeyValuesTokenType.String)
            {
                throw CreateError(
                    "Expected a key, but found another token.");
            }

            nodes.Add(ParseNode());
        }

        if (expectClosingBrace)
        {
            throw CreateError(
                "Expected closing brace '}', but reached end of file.");
        }

        return nodes;
    }

    private KeyValuesNode ParseNode()
    {
        string key = _currentToken.Value;

        Advance();

        if (_currentToken.Type == KeyValuesTokenType.OpenBrace)
        {
            Advance();

            var children = ParseNodes(
                expectClosingBrace: true);

            return new KeyValuesNode(
                key,
                value: null,
                children);
        }

        if (_currentToken.Type != KeyValuesTokenType.String)
        {
            throw CreateError(
                $"Expected a value or opening brace after key '{key}'.");
        }

        string value = _currentToken.Value;

        var valueSpan = new KeyValuesSpan(
            _currentToken.Start,
            _currentToken.Length);

        Advance();

        return new KeyValuesNode(
            key,
            value,
            children: null,
            valueSpan);
    }

    private void Advance()
    {
        _currentToken = _lexer.ReadNextToken();
    }

    private FormatException CreateError(string message)
    {
        return new FormatException(
            $"{message} " +
            $"Line {_currentToken.Line}, " +
            $"Column {_currentToken.Column}.");
    }
}
