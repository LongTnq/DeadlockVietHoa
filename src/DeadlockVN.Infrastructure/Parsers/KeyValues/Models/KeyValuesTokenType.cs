namespace DeadlockVN.Infrastructure.Parsers.KeyValues.Models;

/// <summary>
/// Loại token của Valve KeyValues (KV1).
/// <para>
/// Chỉ dùng nội bộ trong assembly Infrastructure.
/// </para>
/// </summary>
internal enum KeyValuesTokenType
{
    String,
    OpenBrace,
    CloseBrace,
    EndOfFile
}
