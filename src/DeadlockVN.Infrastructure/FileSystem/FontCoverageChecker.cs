namespace DeadlockVN.Infrastructure.FileSystem;

/// <summary>
/// Đọc bảng <c>cmap</c> của font TrueType/OpenType để kiểm tra font có phủ
/// đủ ký tự hay không.
/// <para>
/// Chỉ đọc, không sửa font. Hỗ trợ các định dạng cmap phổ biến: 0, 4, 6
/// và 12 (là định dạng dùng cho Unicode đầy đủ).
/// </para>
/// <para>
/// <b>Lưu ý:</b> toàn bộ cấu trúc OpenType dùng <b>big-endian</b>
/// (network byte order), khác với C# mặc định.
/// </para>
/// </summary>
public static class FontCoverageChecker
{
    /// <summary>29 chữ cái tiếng Việt viết thường.</summary>
    private static readonly int[] VietnameseLowerLetters =
    {
        0x0061, 0x0103, 0x00E2, 0x0062, 0x0063, 0x0064, 0x0111, 0x0065,
        0x00EA, 0x0067, 0x0068, 0x0069, 0x006B, 0x006C, 0x006D, 0x006E,
        0x006F, 0x00F4, 0x01A1, 0x0070, 0x0071, 0x0072, 0x0073, 0x0074,
        0x0075, 0x01B0, 0x0076, 0x0078, 0x0079
    };

    /// <summary>29 chữ cái tiếng Việt viết hoa.</summary>
    private static readonly int[] VietnameseUpperLetters =
    {
        0x0041, 0x0102, 0x00C2, 0x0042, 0x0043, 0x0044, 0x0110, 0x0045,
        0x00CA, 0x0047, 0x0048, 0x0049, 0x004B, 0x004C, 0x004D, 0x004E,
        0x004F, 0x00D4, 0x01A0, 0x0050, 0x0051, 0x0052, 0x0053, 0x0054,
        0x0055, 0x01AF, 0x0056, 0x0058, 0x0059
    };

    /// <summary>59 tổ hợp nguyên âm có dấu, viết thường.</summary>
    private static readonly int[] VietnameseCombinationsLower =
    {
        0x00E0, 0x00E1, 0x1EA3, 0x00E3, 0x1EA1, 0x1EB1, 0x1EAF, 0x1EB3,
        0x1EB5, 0x1EB7, 0x1EA7, 0x1EA5, 0x1EA9, 0x1EAB, 0x1EAD, 0x00E8,
        0x00E9, 0x1EBB, 0x1EBD, 0x1EB9, 0x1EC1, 0x1EBF, 0x1EC3, 0x1EC5,
        0x1EC7, 0x00EC, 0x00ED, 0x1EC9, 0x0129, 0x1ECB, 0x00F2, 0x00F3,
        0x1ECF, 0x00F5, 0x1ECD, 0x1ED3, 0x1ED1, 0x1ED5, 0x1ED7, 0x1ED9,
        0x1EDD, 0x1EDB, 0x1EDF, 0x1EE1, 0x1EE3, 0x00F9, 0x00FA, 0x1EE7,
        0x0169, 0x1EE5, 0x1EEB, 0x1EE9, 0x1EED, 0x1EEF, 0x1EF1, 0x1EF3,
        0x00FD, 0x1EF7, 0x1EF9
    };

    /// <summary>59 tổ hợp nguyên âm có dấu, viết hoa.</summary>
    private static readonly int[] VietnameseCombinationsUpper =
    {
        0x00C0, 0x00C1, 0x1EA2, 0x00C3, 0x1EA0, 0x1EB0, 0x1EAE, 0x1EB2,
        0x1EB4, 0x1EB6, 0x1EA6, 0x1EA4, 0x1EA8, 0x1EAA, 0x1EAC, 0x00C8,
        0x00C9, 0x1EBA, 0x1EBC, 0x1EB8, 0x1EC0, 0x1EBE, 0x1EC2, 0x1EC4,
        0x1EC6, 0x00CC, 0x00CD, 0x1EC8, 0x0128, 0x1ECA, 0x00D2, 0x00D3,
        0x1ECE, 0x00D5, 0x1ECC, 0x1ED2, 0x1ED0, 0x1ED4, 0x1ED6, 0x1ED8,
        0x1EDC, 0x1EDA, 0x1EDE, 0x1EE0, 0x1EE2, 0x00D9, 0x00DA, 0x1EE6,
        0x0168, 0x1EE4, 0x1EEA, 0x1EE8, 0x1EEC, 0x1EEE, 0x1EF0, 0x1EF2,
        0x00DD, 0x1EF6, 0x1EF8
    };

    /// <summary>
    /// Bộ ký tự tiếng Việt cần font phải hỗ trợ, ở dạng mã Unicode.
    /// <para>
    /// 29 chữ cái thường + 29 chữ hoa + 59 tổ hợp nguyên âm có dấu
    /// (thường) + 59 tổ hợp (hoa) = <b>176 mã</b>.
    /// </para>
    /// <para>
    /// Bộ này gồm cả các nguyên âm móc <c>ơ</c>/<c>ư</c> và các tổ hợp
    /// <c>ờ ở ị ọ ộ ụ ữ</c> — những ký tự mà font chính của game
    /// (<c>VALVE Oracle</c>) <b>không</b> có.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> VietnameseCodePoints { get; } =
        VietnameseLowerLetters
            .Concat(VietnameseUpperLetters)
            .Concat(VietnameseCombinationsLower)
            .Concat(VietnameseCombinationsUpper)
            .Distinct()
            .OrderBy(code => code)
            .ToList();

    /// <summary>
    /// Bộ 29 chữ cái tiếng Việt viết thường + hoa.
    /// </summary>
    public static string VietnameseAlphabet { get; } =
        new string(
            VietnameseLowerLetters
                .Concat(VietnameseUpperLetters)
                .Select(code => (char)code)
                .ToArray());

    /// <summary>
    /// Kiểm tra font có phủ đủ các ký tự được yêu cầu hay không.
    /// </summary>
    /// <param name="fontFilePath">Đường dẫn file font.</param>
    /// <param name="characters">Các ký tự cần kiểm tra.</param>
    /// <returns>
    /// <c>true</c> nếu font phủ đủ mọi ký tự; <c>false</c> nếu thiếu bất kỳ
    /// ký tự nào, hoặc không đọc được font.
    /// </returns>
    public static bool Covers(string fontFilePath, IEnumerable<char> characters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFilePath);
        ArgumentNullException.ThrowIfNull(characters);

        HashSet<int> covered = GetCoveredCodePoints(fontFilePath);

        if (covered.Count == 0)
        {
            return false;
        }

        foreach (char character in characters)
        {
            if (!covered.Contains(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Kiểm tra font có phủ đủ bộ ký tự tiếng Việt không.
    /// </summary>
    public static bool CoversVietnamese(string fontFilePath)
    {
        return CoversCodePoints(fontFilePath, VietnameseCodePoints);
    }

    /// <summary>
    /// Kiểm tra font có phủ đủ các mã Unicode được yêu cầu hay không.
    /// </summary>
    public static bool CoversCodePoints(string fontFilePath, IEnumerable<int> codePoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFilePath);
        ArgumentNullException.ThrowIfNull(codePoints);

        HashSet<int> covered = GetCoveredCodePoints(fontFilePath);

        if (covered.Count == 0)
        {
            return false;
        }

        foreach (int codePoint in codePoints)
        {
            if (!covered.Contains(codePoint))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Lấy danh sách mã Unicode mà font thiếu.
    /// </summary>
    public static IReadOnlyList<int> GetMissingCodePoints(
        string fontFilePath,
        IEnumerable<int> codePoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFilePath);
        ArgumentNullException.ThrowIfNull(codePoints);

        HashSet<int> covered = GetCoveredCodePoints(fontFilePath);

        return codePoints.Where(code => !covered.Contains(code)).ToList();
    }

    /// <summary>
    /// Lấy tập mã Unicode mà font hỗ trợ.
    /// </summary>
    public static HashSet<int> GetCoveredCodePoints(string fontFilePath)
    {
        var covered = new HashSet<int>();

        if (!File.Exists(fontFilePath))
        {
            return covered;
        }

        byte[] data;

        try
        {
            data = File.ReadAllBytes(fontFilePath);
        }
        catch (IOException)
        {
            return covered;
        }
        catch (UnauthorizedAccessException)
        {
            return covered;
        }

        if (data.Length < 12)
        {
            return covered;
        }

        // Kiểm tra chữ ký sfnt: 0x00010000 hoặc 'true' hoặc 'ttcf'/'OTTO'.
        uint version = ReadUInt32BigEndian(data, 0);

        bool isTrueType = version == 0x00010000 ||
                          (data[0] == 'O' && data[1] == 'T' && data[2] == 'T' && data[3] == 'O');

        if (!isTrueType)
        {
            return covered;
        }

        int tableCount = ReadUInt16BigEndian(data, 4);

        for (int i = 0; i < tableCount; i++)
        {
            int recordOffset = 12 + (i * 16);

            if (recordOffset + 16 > data.Length)
            {
                break;
            }

            string tag = System.Text.Encoding.ASCII.GetString(data, recordOffset, 4);

            if (tag != "cmap")
            {
                continue;
            }

            int cmapOffset = (int)ReadUInt32BigEndian(data, recordOffset + 8);
            int cmapLength = (int)ReadUInt32BigEndian(data, recordOffset + 12);

            if (cmapOffset <= 0 ||
                cmapOffset + 4 > data.Length ||
                cmapLength <= 0)
            {
                continue;
            }

            ReadCmap(data, cmapOffset, covered);
        }

        return covered;
    }

    private static void ReadCmap(byte[] data, int cmapOffset, HashSet<int> covered)
    {
        int subtableCount = ReadUInt16BigEndian(data, cmapOffset + 2);

        for (int i = 0; i < subtableCount; i++)
        {
            int recordOffset = cmapOffset + 4 + (i * 8);

            if (recordOffset + 8 > data.Length)
            {
                break;
            }

            int platformId = ReadUInt16BigEndian(data, recordOffset);
            int encodingId = ReadUInt16BigEndian(data, recordOffset + 2);
            int subtableOffset = (int)ReadUInt32BigEndian(data, recordOffset + 4);

            int absoluteOffset = cmapOffset + subtableOffset;

            if (absoluteOffset + 2 > data.Length)
            {
                continue;
            }

            int format = ReadUInt16BigEndian(data, absoluteOffset);

            switch (format)
            {
                case 0:
                    ReadFormat0(data, absoluteOffset, covered);
                    break;

                case 4:
                    ReadFormat4(data, absoluteOffset, covered);
                    break;

                case 6:
                    ReadFormat6(data, absoluteOffset, covered);
                    break;

                case 12:
                    ReadFormat12(data, absoluteOffset, covered);
                    break;
            }
        }
    }

    private static void ReadFormat0(byte[] data, int offset, HashSet<int> covered)
    {
        for (int i = 0; i < 256; i++)
        {
            int glyphIndex = data[offset + 6 + i];

            if (glyphIndex != 0)
            {
                covered.Add(i);
            }
        }
    }

    private static void ReadFormat4(byte[] data, int offset, HashSet<int> covered)
    {
        int segCountX2 = ReadUInt16BigEndian(data, offset + 6);

        if (segCountX2 == 0)
        {
            return;
        }

        int segCount = segCountX2 / 2;

        int endCodesOffset = offset + 14;
        int startCodesOffset = endCodesOffset + segCountX2 + 2;
        int idDeltasOffset = startCodesOffset + segCountX2;
        int idRangeOffsetsOffset = idDeltasOffset + segCountX2;

        for (int seg = 0; seg < segCount; seg++)
        {
            int endCode = ReadUInt16BigEndian(data, endCodesOffset + (seg * 2));
            int startCode = ReadUInt16BigEndian(data, startCodesOffset + (seg * 2));
            int idDelta = ReadUInt16BigEndian(data, idDeltasOffset + (seg * 2));
            int idRangeOffset = ReadUInt16BigEndian(data, idRangeOffsetsOffset + (seg * 2));

            if (startCode > endCode)
            {
                continue;
            }

            for (int code = startCode; code <= endCode && code <= 0xFFFF; code++)
            {
                int glyphIndex;

                if (idRangeOffset == 0)
                {
                    glyphIndex = (code + idDelta) & 0xFFFF;
                }
                else
                {
                    int glyphOffset =
                        idRangeOffsetsOffset + (seg * 2) + idRangeOffset +
                        ((code - startCode) * 2);

                    if (glyphOffset + 1 >= data.Length)
                    {
                        continue;
                    }

                    glyphIndex = ReadUInt16BigEndian(data, glyphOffset);

                    if (glyphIndex != 0)
                    {
                        glyphIndex = (glyphIndex + idDelta) & 0xFFFF;
                    }
                }

                if (glyphIndex != 0)
                {
                    covered.Add(code);
                }
            }
        }
    }

    private static void ReadFormat6(byte[] data, int offset, HashSet<int> covered)
    {
        int firstCode = ReadUInt16BigEndian(data, offset + 6);
        int entryCount = ReadUInt16BigEndian(data, offset + 8);

        for (int i = 0; i < entryCount; i++)
        {
            int position = offset + 10 + (i * 2);

            if (position + 1 >= data.Length)
            {
                break;
            }

            if (ReadUInt16BigEndian(data, position) != 0)
            {
                covered.Add(firstCode + i);
            }
        }
    }

    private static void ReadFormat12(byte[] data, int offset, HashSet<int> covered)
    {
        uint groupCount = ReadUInt32BigEndian(data, offset + 12);

        for (uint i = 0; i < groupCount; i++)
        {
            int position = offset + 16 + (int)(i * 12);

            if (position + 11 >= data.Length)
            {
                break;
            }

            int startChar = (int)ReadUInt32BigEndian(data, position);
            int endChar = (int)ReadUInt32BigEndian(data, position + 4);

            if (endChar < startChar || endChar - startChar > 0x10FFFF)
            {
                continue;
            }

            for (int code = startChar; code <= endChar; code++)
            {
                covered.Add(code);
            }
        }
    }

    private static ushort ReadUInt16BigEndian(byte[] data, int offset)    {
        if (offset < 0 || offset + 1 >= data.Length)
        {
            return 0;
        }

        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint ReadUInt32BigEndian(byte[] data, int offset)
    {
        if (offset < 0 || offset + 3 >= data.Length)
        {
            return 0;
        }

        return (uint)((data[offset] << 24) |
                     (data[offset + 1] << 16) |
                     (data[offset + 2] << 8) |
                     data[offset + 3]);
    }
}
