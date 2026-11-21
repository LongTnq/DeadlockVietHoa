using System.Text;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Infrastructure.FileSystem;

/// <summary>
/// Vá các file cấu hình của game để bật ngôn ngữ mới.
/// <para>
/// <b>Nguyên tắc:</b> chỉ <b>chèn</b> dòng mới, không sửa hay xoá dòng cũ.
/// Mọi thay đổi đều tạo bản sao lưu <c>.bak</c> trước.
/// </para>
/// <para>
/// Cấu trúc file <c>gameinfo.gi</c> và <c>fonts.conf</c> của game đều dùng
/// <b>CRLF</b> và <b>không BOM</b> — vá cũng phải giữ nguyên đặc tính này.
/// </para>
/// </summary>
public sealed class GameConfigPatcher : IGameConfigPatcher
{
    /// <summary>
    /// Chuỗi nhận diện khối <c>SupportedLanguages</c> trong <c>gameinfo.gi</c>.
    /// </summary>
    private const string SupportedLanguagesMarker = "SupportedLanguages";

    /// <summary>
    /// Marker đánh dấu khối fallback do mod này tạo ra.
    /// <para>
    /// Dùng để <b>thay thế</b> khối cũ khi cấu hình font thay đổi (ví dụ
    /// bổ sung họ font mới), thay vì chèn chồng khối mới lên khối cũ.
    /// Khối của Valve (tiếng Thái, tiếng Nhật…) không có marker này nên
    /// không bao giờ bị đụng tới.
    /// </para>
    /// </summary>
    private const string VietnameseFallbackMarker =
        "Deadlock Vietnamese Localization: font fallback";

    public async Task<ConfigPatchResult> PatchSupportedLanguagesAsync(
        string gameInfoPath,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameInfoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        if (!File.Exists(gameInfoPath))
        {
            throw new FileNotFoundException(
                $"Không tìm thấy file cấu hình: {gameInfoPath}",
                gameInfoPath);
        }

        byte[] original = await File.ReadAllBytesAsync(
            gameInfoPath,
            cancellationToken);

        string text = DecodeUtf8(original);

        if (ContainsLanguage(text, languageCode))
        {
            return new ConfigPatchResult
            {
                FilePath = gameInfoPath,
                Changed = false,
                AlreadyPresent = true
            };
        }

        string patched = InsertLanguage(text, languageCode);

        if (patched == text)
        {
            return new ConfigPatchResult
            {
                FilePath = gameInfoPath,
                Changed = false,
                AlreadyPresent = false
            };
        }

        string? backupPath = await CreateBackupAsync(
            gameInfoPath,
            original,
            cancellationToken);

        await WritePreservingFormatAsync(
            gameInfoPath,
            original,
            patched,
            cancellationToken);

        return new ConfigPatchResult
        {
            FilePath = gameInfoPath,
            Changed = true,
            AlreadyPresent = false,
            InsertedContent = $"\"{languageCode}\" \"3\"",
            BackupPath = backupPath
        };
    }

    public async Task<ConfigPatchResult> PatchFontFallbackAsync(
        string fontsConfPath,
        FontFallbackConfig fontConfig,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontsConfPath);
        ArgumentNullException.ThrowIfNull(fontConfig);

        if (!File.Exists(fontsConfPath))
        {
            throw new FileNotFoundException(
                $"Không tìm thấy file cấu hình: {fontsConfPath}",
                fontsConfPath);
        }

        byte[] original = await File.ReadAllBytesAsync(
            fontsConfPath,
            cancellationToken);

        string text = DecodeUtf8(original);

        // Đã có khối fallback đầy đủ cho ngôn ngữ này chưa?
        //
        // Phải kiểm tra TỪNG họ font, không chỉ kiểm tra có
        // "<string>vi-vn</string>" — vì file game có thể đã được vá một
        // phần (chỉ VALVEOracle) từ lần chạy trước. Khi đó ta vẫn cần
        // chèn thêm khối cho các họ font còn thiếu.
        if (HasAllFallbackBlocks(text, fontConfig))
        {
            return new ConfigPatchResult
            {
                FilePath = fontsConfPath,
                Changed = false,
                AlreadyPresent = true
            };
        }

        string block = BuildFallbackBlock(fontConfig);

        string patched = InsertFallbackBlock(text, block);

        if (patched == text)
        {
            return new ConfigPatchResult
            {
                FilePath = fontsConfPath,
                Changed = false,
                AlreadyPresent = false
            };
        }

        string? backupPath = await CreateBackupAsync(
            fontsConfPath,
            original,
            cancellationToken);

        await WritePreservingFormatAsync(
            fontsConfPath,
            original,
            patched,
            cancellationToken);

        return new ConfigPatchResult
        {
            FilePath = fontsConfPath,
            Changed = true,
            AlreadyPresent = false,
            InsertedContent = block,
            BackupPath = backupPath
        };
    }

    public bool FontCoversCharacters(
        string fontFilePath,
        IEnumerable<char> characters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFilePath);
        ArgumentNullException.ThrowIfNull(characters);

        return FontCoverageChecker.Covers(fontFilePath, characters);
    }

    /// <summary>
    /// Kiểm tra ngôn ngữ đã có trong khối <c>SupportedLanguages</c> chưa.
    /// </summary>
    private static bool ContainsLanguage(string text, string languageCode)
    {
        int start = text.IndexOf(
            SupportedLanguagesMarker,
            StringComparison.Ordinal);

        if (start < 0)
        {
            return false;
        }

        int openBrace = text.IndexOf('{', start);
        int closeBrace = text.IndexOf('}', openBrace);

        if (openBrace < 0 || closeBrace < 0)
        {
            return false;
        }

        string block = text[openBrace..closeBrace];

        return block.Contains(
            $"\"{languageCode}\"",
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Chèn ngôn ngữ vào cuối khối <c>SupportedLanguages</c>.
    /// <para>
    /// Giữ đúng thụt lề và ký tự xuống dòng của file gốc (phát hiện từ dòng
    /// ngay trước đó).
    /// </para>
    /// </summary>
    private static string InsertLanguage(string text, string languageCode)
    {
        int start = text.IndexOf(
            SupportedLanguagesMarker,
            StringComparison.Ordinal);

        if (start < 0)
        {
            return text;
        }

        int openBrace = text.IndexOf('{', start);
        int closeBrace = text.IndexOf('}', openBrace);

        if (openBrace < 0 || closeBrace < 0)
        {
            return text;
        }

        // Lấy dòng entry cuối cùng trong khối để sao chép thụt lề và
        // ký tự phân tách (dòng `}` đóng khối có thụt lề khác).
        string block = text[openBrace..closeBrace];

        string? entryLine = FindLastEntryLine(block);

        string indent = entryLine is null
            ? "\t\t"
            : entryLine[..(entryLine.Length - entryLine.TrimStart('\t').Length)];

        string newline = DetectNewLine(text);
        string separator = DetectSeparator(block);
        string entry = $"{indent}\"{languageCode}\"{separator}\"3\"";

        // Chèn ngay trước dấu '}' đóng khối.
        int insertAt = closeBrace;

        // Bỏ qua khoảng trắng/dòng trống ngay trước '}'.
        int cursor = insertAt;

        while (cursor > openBrace &&
               (text[cursor - 1] == '\r' || text[cursor - 1] == '\n' ||
                text[cursor - 1] == '\t' || text[cursor - 1] == ' '))
        {
            cursor--;
        }

        // text[cursor] bắt đầu bằng ký tự xuống dòng kết thúc dòng entry
        // cuối cùng — giữ nguyên phần đó để không sinh dòng trống thừa.
        string insertion = $"{newline}{entry}{text[cursor..insertAt]}";

        return text[..cursor] + insertion + text[insertAt..];
    }

    /// <summary>
    /// Tìm dòng cuối cùng trong khối chứa một giá trị có dấu nháy.
    /// </summary>
    private static string? FindLastEntryLine(string block)
    {
        string[] lines = block.Split('\n');

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i].TrimEnd('\r');

            if (line.Contains('"', StringComparison.Ordinal))
            {
                return line;
            }
        }

        return null;
    }

    /// <summary>
    /// Tạo các khối font fallback theo đúng mẫu của Valve cho tiếng Thái.
    /// <para>
    /// Mỗi họ font cần thay thế sẽ sinh một khối <c>&lt;match&gt;</c> riêng.
    /// Khối dùng thụt lề tab và kết thúc bằng ký tự xuống dòng, để chèn
    /// được vào giữa file mà không tạo dòng trống thừa.
    /// </para>
    /// <para>
    /// Họ font nào cần dùng <c>compare="contains"</c> được quyết định bởi
    /// <see cref="FontFallbackConfig.ContainsMatchFamilies"/> — vì trong
    /// <c>fonts.conf</c> của game, các weight được khai báo dạng
    /// <c>VALVEPulp-Bold</c>, <c>RetailTextDemo-Regular</c>… nên khớp
    /// chính xác tên họ sẽ bỏ sót.
    /// </para>
    /// </summary>
    private static string BuildFallbackBlock(FontFallbackConfig config)
    {
        var builder = new StringBuilder();

        builder.AppendLine("\t<!-- Deadlock Vietnamese Localization: font fallback -->");

        foreach (string family in config.BaseFontFamilies)
        {
            bool useContains =
                config.ContainsMatchFamilies.Contains(family);

            builder.AppendLine("\t<match>");
            builder.AppendLine("\t\t<test name=\"family\"" +
                               (useContains ? " compare=\"contains\"" : string.Empty) +
                               ">");
            builder.AppendLine($"\t\t\t<string>{family}</string>");
            builder.AppendLine("\t\t</test>");
            builder.AppendLine("\t\t<test name=\"lang\">");
            builder.AppendLine($"\t\t\t<string>{config.LanguageCode}</string>");
            builder.AppendLine("\t\t</test>");
            builder.AppendLine(
                $"\t\t<edit name=\"family\" mode=\"append_last\" binding=\"{config.Binding}\">");
            builder.AppendLine($"\t\t\t<string>{config.FallbackFontFamily}</string>");
            builder.AppendLine("\t\t</edit>");
            builder.AppendLine("\t</match>");
        }

        // Bỏ ký tự xuống dòng cuối cùng — sẽ thêm lại khi chèn.
        return builder.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Chèn khối fallback vào cuối file, trước phần <c>--&gt;</c> cuối cùng
    /// nếu có, hoặc cuối file.
    /// <para>
    /// Nếu file đã có khối do mod này tạo (có marker), khối cũ sẽ bị
    /// <b>gỡ bỏ</b> trước khi chèn khối mới — tránh chèn chồng nhiều khối
    /// trùng lặp khi chạy lại lệnh với cấu hình font mới.
    /// </para>
    /// </summary>
    private static string InsertFallbackBlock(string text, string block)
    {
        string newline = DetectNewLine(text);

        // Gỡ khối cũ (nếu có) trước.
        text = RemovePreviousFallbackBlocks(text);

        // Chuẩn hoá block sang ký tự xuống dòng của file.
        string normalized = block
            .Replace("\r\n", "\n")
            .Replace("\n", newline);

        int insertAt = text.Length;

        // Chèn trước comment cuối cùng (nếu có) để giữ cấu trúc file.
        int lastComment = text.LastIndexOf("<!--", StringComparison.Ordinal);
        int lastCommentEnd = text.LastIndexOf("-->", StringComparison.Ordinal);

        if (lastComment >= 0 && lastCommentEnd > lastComment)
        {
            insertAt = lastComment;
        }
        else
        {
            // Bỏ khoảng trắng thừa ở cuối file.
            while (insertAt > 0 &&
                   (text[insertAt - 1] == '\r' ||
                    text[insertAt - 1] == '\n'))
            {
                insertAt--;
            }
        }

        // Lùi về đầu dòng chứa vị trí chèn để khối mới bắt đầu ở dòng riêng.
        int lineStart = text.LastIndexOf('\n', insertAt - 1);

        lineStart = lineStart >= 0 ? lineStart + 1 : 0;

        return text[..lineStart] +
               normalized +
               newline +
               text[lineStart..];
    }

    private static string DetectNewLine(string text)
    {
        return text.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
    }

    /// <summary>
    /// Kiểm tra khối fallback của mod đã đầy đủ cho mọi họ font chưa.
    /// <para>
    /// Một họ font coi như đã có nếu trong file có một khối <c>&lt;match&gt;</c>
    /// chứa cả tên họ font lẫn mã ngôn ngữ.
    /// </para>
    /// </summary>
    private static bool HasAllFallbackBlocks(string text, FontFallbackConfig config)
    {
        foreach (string family in config.BaseFontFamilies)
        {
            if (!HasFallbackBlock(text, family, config.LanguageCode))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Kiểm tra có khối <c>&lt;match&gt;</c> chứa cả họ font lẫn mã ngôn ngữ
    /// hay không.
    /// </summary>
    private static bool HasFallbackBlock(string text, string family, string languageCode)
    {
        // Dòng chứa <string>Family</string> và dòng chứa
        // <string>lang</string> phải nằm trong CÙNG một khối <match>.
        int searchFrom = 0;

        while (true)
        {
            int matchStart = text.IndexOf("<match>", searchFrom, StringComparison.Ordinal);

            if (matchStart < 0)
            {
                return false;
            }

            int matchEnd = text.IndexOf("</match>", matchStart, StringComparison.Ordinal);

            if (matchEnd < 0)
            {
                return false;
            }

            string block = text[matchStart..matchEnd];

            bool hasFamily = block.Contains(
                $"<string>{family}</string>",
                StringComparison.Ordinal);

            bool hasLanguage = block.Contains(
                $"<string>{languageCode}</string>",
                StringComparison.Ordinal);

            if (hasFamily && hasLanguage)
            {
                return true;
            }

            searchFrom = matchEnd + 1;
        }
    }

    /// <summary>
    /// Gỡ khối fallback do mod này tạo ra (được đánh dấu bằng
    /// <see cref="VietnameseFallbackMarker"/>).
    /// <para>
    /// Chỉ gỡ phần do mod tạo — các khối <c>&lt;match&gt;</c> của Valve cho
    /// tiếng Thái / Nhật / Hàn / Trung vẫn được giữ nguyên.
    /// </para>
    /// </summary>
    private static string RemovePreviousFallbackBlocks(string text)
    {
        while (true)
        {
            int markerIndex = text.IndexOf(
                VietnameseFallbackMarker,
                StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                return text;
            }

            // Lùi về đầu dòng chứa marker để xoá trọn dòng comment.
            int blockStart = text.LastIndexOf('\n', markerIndex);

            blockStart = blockStart >= 0 ? blockStart + 1 : 0;

            // Tìm khối <match>...</match> kèm theo sau marker.
            int matchStart = text.IndexOf(
                "<match>",
                markerIndex,
                StringComparison.Ordinal);

            if (matchStart < 0)
            {
                // Marker mồ côi (không còn khối nào) -> xoá tới cuối file.
                return text[..blockStart].TrimEnd('\r', '\n');
            }

            int matchEnd = text.IndexOf(
                "</match>",
                matchStart,
                StringComparison.Ordinal);

            if (matchEnd < 0)
            {
                return text[..blockStart].TrimEnd('\r', '\n');
            }

            // Bao gồm cả dấu ">" của </match> và ký tự xuống dòng sau nó.
            int afterMatch = matchEnd + "</match>".Length;

            if (afterMatch < text.Length && text[afterMatch] == '\r')
            {
                afterMatch++;
            }

            if (afterMatch < text.Length && text[afterMatch] == '\n')
            {
                afterMatch++;
            }

            text = text[..blockStart] + text[afterMatch..];
        }
    }

    /// <summary>
    /// Phát hiện ký tự phân tách giữa key và value trong khối
    /// <c>SupportedLanguages</c>: file game dùng khoảng trắng, nhưng một số
    /// bản mod dùng tab.
    /// </summary>
    private static string DetectSeparator(string block)
    {
        int index = block.IndexOf('"', StringComparison.Ordinal);

        if (index < 0)
        {
            return " ";
        }

        int closeQuote = block.IndexOf('"', index + 1);

        if (closeQuote < 0)
        {
            return " ";
        }

        int cursor = closeQuote + 1;

        while (cursor < block.Length && block[cursor] != '\r' && block[cursor] != '\n')
        {
            cursor++;
        }

        string between = block[(closeQuote + 1)..cursor];

        return between.Contains('\t', StringComparison.Ordinal)
            ? "\t"
            : " ";
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        bool hasBom =
            bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;

        return new UTF8Encoding(false).GetString(
            hasBom ? bytes[3..] : bytes);
    }

    /// <summary>
    /// Ghi lại file giữ nguyên BOM và ký tự xuống dòng gốc.
    /// </summary>
    private static async Task WritePreservingFormatAsync(
        string path,
        byte[] original,
        string text,
        CancellationToken cancellationToken)
    {
        bool hasBom =
            original.Length >= 3 &&
            original[0] == 0xEF &&
            original[1] == 0xBB &&
            original[2] == 0xBF;

        byte[] body = new UTF8Encoding(false).GetBytes(text);

        byte[] output;

        if (hasBom)
        {
            output = new byte[body.Length + 3];

            output[0] = 0xEF;
            output[1] = 0xBB;
            output[2] = 0xBF;

            Buffer.BlockCopy(body, 0, output, 3, body.Length);
        }
        else
        {
            output = body;
        }

        await File.WriteAllBytesAsync(path, output, cancellationToken);
    }

    /// <summary>
    /// Tạo bản sao lưu <c>.bak</c> nếu chưa có.
    /// </summary>
    private static async Task<string?> CreateBackupAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        string backupPath = path + ".bak";

        if (!File.Exists(backupPath))
        {
            await File.WriteAllBytesAsync(
                backupPath,
                content,
                cancellationToken);
        }

        return backupPath;
    }
}
