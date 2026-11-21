using System.Text;
using System.Text.Json;

namespace DeadlockVN.Infrastructure.Translation;

/// <summary>
/// Kết quả phân tích phản hồi của model.
/// </summary>
public sealed class ModelResponse
{
    /// <summary>
    /// Danh sách bản dịch, theo thứ tự lô gửi đi.
    /// </summary>
    public IReadOnlyList<string> Translations { get; init; } =
        Array.Empty<string>();

    /// <summary>
    /// Bản dịch được ánh xạ theo văn bản gốc (nếu model trả về kèm
    /// trường "original").
    /// <para>
    /// Ánh xạ này đáng tin cậy hơn chỉ số: khi model trả về nhiều hơn
    /// hoặc ít hơn số chuỗi, ta vẫn ghép đúng bản dịch với chuỗi gốc.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string> ByOriginal { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Bản dịch được ánh xạ theo chỉ số (khi model trả về JSON dạng
    /// <c>{"0": "...", "1": "..."}</c>).
    /// <para>
    /// <b>Vì sao quan trọng:</b> khi model bỏ sót một phần tử giữa lô,
    /// mảng phẳng khiến ta không biết phần tử nào bị thiếu — mọi bản dịch
    /// từ đó trở đi bị lệch. Với ánh xạ theo chỉ số, ta biết chính xác
    /// chỉ số nào thiếu và chỉ cần dịch lại phần đó.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<int, string> ByIndex { get; init; } =
        new Dictionary<int, string>();
}

/// <summary>
/// Phân tích phản hồi JSON của model dịch.
/// <para>
/// <b>Vì sao cần lớp riêng:</b> model ngôn ngữ nhỏ trả về JSON theo
/// rất nhiều kiểu khác nhau. Đo thực tế với qwen2.5:3b đã gặp:
/// </para>
/// <list type="bullet">
///   <item><description>Mảng chuỗi: <c>["a", "b"]</c></description></item>
///   <item><description>
///     Mảng object: <c>[{"original": "...", "translated": "..."}]</c> —
///     kiểu hay gặp nhất với prompt yêu cầu "JSON".
///   </description></item>
///   <item><description>
///     Object đánh số: <c>{"0": "a", "1": "b"}</c>.
///   </description></item>
///   <item><description>
///     Object có mảng lồng: <c>{"translations": [...]}</c>.
///   </description></item>
///   <item><description>
///     Bọc trong markdown fence: <c>```json [...] ```</c>.
///   </description></item>
/// </list>
/// </summary>
public static class ModelResponseParser
{
    /// <summary>
    /// Tên các trường có thể chứa bản dịch trong object.
    /// </summary>
    private static readonly string[] TranslationKeys =
    {
        "translated", "translation", "vietnamese", "vi", "text", "value",
        "result"
    };

    /// <summary>
    /// Tên các trường có thể chứa văn bản gốc trong object.
    /// </summary>
    private static readonly string[] OriginalKeys =
    {
        "original", "source", "english", "en", "input"
    };

    /// <summary>
    /// Phân tích nội dung phản hồi của model.
    /// </summary>
    /// <param name="content">Nội dung trong <c>message.content</c>.</param>
    public static ModelResponse Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ModelResponse();
        }

        string text = StripFence(content);

        // Cách 1: mảng các object {original, translated}.
        if (TryParseObjectArray(text, out var objectArray))
        {
            return objectArray;
        }

        // Cách 2: mảng chuỗi thuần.
        if (TryParseStringArray(text, out var direct))
        {
            return new ModelResponse { Translations = direct };
        }

        // Cách 3: object đánh số có giá trị lồng object
        // {"0": {"Human": "...", "Werewolf": "..."}}.
        //
        // Model hay làm thế khi chuỗi gốc bị chia thành nhiều phần nhờ
        // thẻ HTML — ví dụ "<span>Human:</span> ... <span>Werewolf:</span>
        // ...". Model tự nghĩ ra cấu trúc JSON con thay vì trả về một
        // chuỗi dịch thuần. Nếu không xử lý, cả chuỗi đó bị coi là hỏng.
        //
        // Thử TRƯỚC cách 3b: nếu không phải dạng lồng thì cách này tự
        // trả về false và rơi xuống cách 3b.
        if (TryParseNestedIndexedObject(text, out var nestedIndexed))
        {
            return new ModelResponse
            {
                Translations = nestedIndexed.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value).ToList(),
                ByIndex = nestedIndexed
            };
        }

        // Cách 3b: object đánh số {"0": "a", "1": "b"}.
        if (TryParseIndexedObject(text, out var indexed))
        {
            return new ModelResponse
            {
                Translations = indexed.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value).ToList(),
                ByIndex = indexed
            };
        }

        // Cách 4: object có mảng lồng bên trong.
        if (TryParseNestedArray(text, out var nested))
        {
            return nested;
        }

        return new ModelResponse();
    }

    /// <summary>
    /// Đọc object đánh số có giá trị là object con, ví dụ
    /// <c>{"0": {"Human": "...", "Werewolf": "..."}}</c>.
    /// <para>
    /// Các giá trị con được nối lại bằng dấu phẩy (hoặc xuống dòng) để
    /// tạo thành một chuỗi dịch hoàn chỉnh.
    /// </para>
    /// </summary>
    private static bool TryParseNestedIndexedObject(
        string json,
        out Dictionary<int, string> values)
    {
        values = new Dictionary<int, string>();

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out int index))
                {
                    return false;
                }

                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    // Không phải dạng lồng -> không dùng cách này.
                    return false;
                }

                var parts = new List<string>();

                foreach (var child in property.Value.EnumerateObject())
                {
                    string text = child.Value.ValueKind == JsonValueKind.String
                        ? child.Value.GetString() ?? string.Empty
                        : child.Value.ToString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(text.Trim());
                    }
                }

                if (parts.Count == 0)
                {
                    return false;
                }

                values[index] = string.Join(", ", parts);
            }

            if (values.Count == 0)
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            values = new Dictionary<int, string>();

            return false;
        }
    }

    /// <summary>
    /// Bỏ markdown fence nếu có.
    /// </summary>
    private static string StripFence(string content)
    {
        string text = content.Trim();

        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        int firstNewline = text.IndexOf('\n');

        if (firstNewline > 0)
        {
            text = text[(firstNewline + 1)..];
        }

        int fence = text.IndexOf("```", StringComparison.Ordinal);

        if (fence > 0)
        {
            text = text[..fence];
        }

        return text.Trim();
    }

    /// <summary>
    /// Đọc mảng các object có trường "original"/"translated".
    /// </summary>
    private static bool TryParseObjectArray(
        string json,
        out ModelResponse response)
    {
        response = new ModelResponse();

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var translations = new List<string>();
            var byOriginal = new Dictionary<string, string>(
                StringComparer.Ordinal);

            bool anyParsed = false;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string? translated = ReadFirstString(element, TranslationKeys);

                if (translated is null)
                {
                    continue;
                }

                anyParsed = true;

                translations.Add(translated);

                string? original = ReadFirstString(element, OriginalKeys);

                if (original is not null)
                {
                    byOriginal[original] = translated;
                }
            }

            if (!anyParsed)
            {
                return false;
            }

            response = new ModelResponse
            {
                Translations = translations,
                ByOriginal = byOriginal
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Đọc mảng chuỗi thuần.
    /// </summary>
    private static bool TryParseStringArray(
        string json,
        out List<string> values)
    {
        values = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    values.Add(element.GetString() ?? string.Empty);
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    // Phần tử object lẫn trong mảng -> xử lý ở cách 1.
                    return false;
                }
                else
                {
                    values.Add(element.ToString());
                }
            }

            return true;
        }
        catch (JsonException)
        {
            values = new List<string>();

            return false;
        }
    }

    /// <summary>
    /// Đọc object có key số <c>{"0": "a", "1": "b"}</c> thành mảng, sắp
    /// xếp theo giá trị số của key.
    /// </summary>
    private static bool TryParseIndexedObject(
        string json,
        out Dictionary<int, string> values)
    {
        values = new Dictionary<int, string>();

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out int index))
                {
                    // Không phải object đánh số -> không dùng cách này.
                    return false;
                }

                string value = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();

                values[index] = value;
            }

            if (values.Count == 0)
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            values = new Dictionary<int, string>();

            return false;
        }
    }

    /// <summary>
    /// Đọc object có mảng bên trong, ví dụ <c>{"translations": [...]}</c>.
    /// </summary>
    private static bool TryParseNestedArray(
        string json,
        out ModelResponse response)
    {
        response = new ModelResponse();

        int arrayStart = json.IndexOf('[');
        int arrayEnd = json.LastIndexOf(']');

        if (arrayStart < 0 || arrayEnd <= arrayStart)
        {
            return false;
        }

        string slice = json[arrayStart..(arrayEnd + 1)];

        if (TryParseObjectArray(slice, out var objectArray))
        {
            response = objectArray;

            return true;
        }

        if (TryParseStringArray(slice, out var values))
        {
            response = new ModelResponse { Translations = values };

            return true;
        }

        return false;
    }

    /// <summary>
    /// Đọc giá trị chuỗi đầu tiên tìm thấy trong các khoá được chỉ định.
    /// </summary>
    private static string? ReadFirstString(
        JsonElement element,
        IReadOnlyList<string> keys)
    {
        foreach (string key in keys)
        {
            if (!element.TryGetProperty(key, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
