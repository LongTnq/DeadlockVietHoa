using System.Text;

using DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

namespace DeadlockVN.Infrastructure.Parsers.KeyValues;

/// <summary>
/// Lexer cho định dạng Valve KeyValues (KV1) — dùng bởi các file
/// localization của Deadlock (<c>citadel_*_english.txt</c>).
/// <para>
/// Chỉ dùng nội bộ trong assembly Infrastructure. Xem
/// <c>InternalsVisibleTo</c> trong <c>DeadlockVN.Infrastructure.csproj</c>.
/// </para>
/// <para>
/// Mỗi token mang theo <see cref="KeyValuesToken.Start"/> và
/// <see cref="KeyValuesToken.Length"/> — độ lệch của token trong chuỗi nguồn.
/// Nhờ vậy exporter có thể thay thế đúng đoạn value trong file gốc mà
/// không cần serialize lại toàn bộ cây (giữ nguyên comment, thụt lề,
/// thứ tự escape và mọi thứ khác của file game).
/// </para>
/// </summary>
internal sealed class KeyValuesLexer
{
    private readonly string _source;

    private int _position;

    private int _line = 1;
    private int _column = 1;

    internal KeyValuesLexer(string source)
    {
        _source = (source ?? throw new ArgumentNullException(nameof(source)))
            .TrimStart('\uFEFF');
    }

    internal KeyValuesToken ReadNextToken()
    {
        SkipTrivia();

        if (IsAtEnd())
        {
            return CreateToken(
                KeyValuesTokenType.EndOfFile,
                string.Empty,
                _line,
                _column);
        }

        int startLine = _line;
        int startColumn = _column;
        int startPosition = _position;

        char current = Peek();

        if (current == '{')
        {
            Advance();

            return new KeyValuesToken(
                KeyValuesTokenType.OpenBrace,
                "{",
                startLine,
                startColumn,
                startPosition,
                _position - startPosition);
        }

        if (current == '}')
        {
            Advance();

            return new KeyValuesToken(
                KeyValuesTokenType.CloseBrace,
                "}",
                startLine,
                startColumn,
                startPosition,
                _position - startPosition);
        }

        if (current == '"')
        {
            string value = ReadQuotedString();

            return new KeyValuesToken(
                KeyValuesTokenType.String,
                value,
                startLine,
                startColumn,
                startPosition,
                _position - startPosition);
        }

        string bareValue = ReadBareString();

        return new KeyValuesToken(
            KeyValuesTokenType.String,
            bareValue,
            startLine,
            startColumn,
            startPosition,
            _position - startPosition);
    }

    private string ReadQuotedString()
    {
        Advance(); // Skip opening quote

        var result = new StringBuilder();

        while (!IsAtEnd())
        {
            char current = Peek();

            if (current == '"')
            {
                Advance(); // Skip closing quote
                return result.ToString();
            }

            if (current == '\\')
            {
                Advance();

                if (IsAtEnd())
                {
                    throw CreateError(
                        "Unexpected end of file after escape character.");
                }

                char escaped = Peek();
                Advance();

                switch (escaped)
                {
                    case '"':
                        result.Append('"');
                        break;

                    case '\\':
                        result.Append('\\');
                        break;

                    case 'n':
                        // Engine Valve giải mã \n thành newline thật khi
                        // nạp file. Bằng chứng: 17/29 bản dịch chính thức
                        // của game vẫn giữ \n trong file heroes — nếu không
                        // decode, game sẽ hiển thị chữ "n" trong tooltip.
                        result.Append('\n');
                        break;

                    case 'r':
                        result.Append('\r');
                        break;

                    case 't':
                        result.Append('\t');
                        break;

                    default:
                        // Giữ nguyên escape lạ (ví dụ \') để không làm mất
                        // dữ liệu khi round-trip về file gốc.
                        result.Append('\\');
                        result.Append(escaped);
                        break;
                }

                continue;
            }

            // Preserve HTML, whitespace and multiline content.
            result.Append(current);
            Advance();
        }

        throw CreateError("Unterminated quoted string.");
    }

    private string ReadBareString()
    {
        var result = new StringBuilder();

        while (!IsAtEnd())
        {
            char current = Peek();

            if (char.IsWhiteSpace(current) ||
                current == '{' ||
                current == '}' ||
                current == '"')
            {
                break;
            }

            if (current == '/' && PeekNext() == '/')
            {
                break;
            }

            result.Append(current);
            Advance();
        }

        if (result.Length == 0)
        {
            throw CreateError(
                IsAtEnd()
                    ? "Unexpected end of file while reading a key or value."
                    : $"Unexpected character '{Peek()}'.");
        }

        return result.ToString();
    }

    private void SkipTrivia()
    {
        while (!IsAtEnd())
        {
            if (char.IsWhiteSpace(Peek()))
            {
                Advance();
                continue;
            }

            // Single-line comment: //
            if (Peek() == '/' && PeekNext() == '/')
            {
                SkipLineComment();
                continue;
            }

            // Multi-line comment: /* ... */
            if (Peek() == '/' && PeekNext() == '*')
            {
                SkipBlockComment();
                continue;
            }

            break;
        }
    }

    private void SkipLineComment()
    {
        while (!IsAtEnd() && Peek() != '\n' && Peek() != '\r')
        {
            Advance();
        }
    }

    private void SkipBlockComment()
    {
        int startLine = _line;
        int startColumn = _column;

        Advance(); // /
        Advance(); // *

        while (!IsAtEnd())
        {
            if (Peek() == '*' && PeekNext() == '/')
            {
                Advance();
                Advance();
                return;
            }

            Advance();
        }

        throw new FormatException(
            $"Unterminated block comment at line {startLine}, " +
            $"column {startColumn}.");
    }

    private char Peek()
    {
        return _source[_position];
    }

    private char PeekNext()
    {
        int nextPosition = _position + 1;

        return nextPosition < _source.Length
            ? _source[nextPosition]
            : '\0';
    }

    private void Advance()
    {
        if (IsAtEnd())
            return;

        char current = _source[_position++];

        if (current == '\r')
        {
            if (!IsAtEnd() && Peek() == '\n')
            {
                _position++;
            }

            _line++;
            _column = 1;
        }
        else if (current == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
    }

    private bool IsAtEnd()
    {
        return _position >= _source.Length;
    }

    private KeyValuesToken CreateToken(
        KeyValuesTokenType type,
        string value,
        int line,
        int column)
    {
        return new KeyValuesToken(
            type,
            value,
            line,
            column,
            _position,
            0);
    }

    private FormatException CreateError(string message)
    {
        return new FormatException(
            $"{message} Line {_line}, Column {_column}.");
    }
}
