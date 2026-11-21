using System.Text.RegularExpressions;

namespace DeadlockVN.Infrastructure.Validation;

/// <summary>
/// Kiểm tra cấu trúc thẻ HTML/Panorama trong một chuỗi localization.
/// <para>
/// <b>Vì sao cần whitelist thay vì heuristic:</b> bản gốc của Deadlock chứa
/// rất nhiều "thẻ ngữ cảnh" trong file VO — <c>&lt;laughs&gt;</c>,
/// <c>&lt;battle cry&gt;</c>, <c>&lt;None&gt;</c>, <c>&lt;in&gt;</c>... Đây là
/// chú thích ngữ cảnh trong lời thoại, không phải HTML. Nếu coi bất kỳ
/// thẻ nào có dấu <c>&lt;&gt;</c> là HTML thì sẽ có hàng trăm báo động giả.
/// </para>
/// <para>
/// Vì vậy chỉ những thẻ nằm trong whitelist (thẻ HTML/Panorama thật) mới
/// được tính vào cấu trúc. Mọi thẻ khác bị bỏ qua hoàn toàn.
/// </para>
/// </summary>
public static class HtmlTagChecker
{
    /// <summary>
    /// Whitelist thẻ HTML/Panorama thật của Deadlock.
    /// <para>
    /// Dữ liệu thật đo được trong 11 file: span (1 476), br (1 029),
    /// li (166), i (155), b (76), panel (47), a (1).
    /// </para>
    /// </summary>
    private static readonly HashSet<string> KnownTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "span", "br", "b", "i", "li", "ul", "ol", "a", "p", "div",
        "em", "strong", "u", "s", "sub", "sup", "h1", "h2", "h3",
        "h4", "h5", "h6", "img", "hr", "pre", "code", "font",
        "table", "tr", "td", "th", "blockquote", "center", "small",
        "panel"
    };

    /// <summary>
    /// Thẻ rỗng (void) — không có thẻ đóng.
    /// </summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "img", "hr"
    };

    private static readonly Regex TagRegex = new(
        @"<(/?)([A-Za-z][A-Za-z0-9_]*)(?<attrs>[^<>]*?)(/?)>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Trả về danh sách thẻ HTML/Panorama thật tìm thấy trong chuỗi.
    /// Thẻ không nằm trong whitelist bị bỏ qua.
    /// </summary>
    public static IReadOnlyList<HtmlTag> ExtractTags(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<HtmlTag>();
        }

        var result = new List<HtmlTag>();

        foreach (Match match in TagRegex.Matches(text))
        {
            string name = match.Groups[2].Value;

            if (!KnownTags.Contains(name))
            {
                continue;
            }

            bool isClosing = match.Groups[1].Value == "/";
            bool isSelfClosing = match.Groups[4].Value == "/";

            if (isSelfClosing)
            {
                continue;
            }

            result.Add(new HtmlTag(
                Name: name.ToLowerInvariant(),
                IsClosing: isClosing,
                IsVoid: VoidElements.Contains(name),
                Raw: match.Value));
        }

        return result;
    }

    /// <summary>
    /// Tính chữ ký cấu trúc thẻ, dùng để so sánh bản gốc với bản dịch.
    /// <para>
    /// Chữ ký chỉ gồm tên thẻ (mở = <c>span</c>, đóng = <c>/span</c>), bỏ qua
    /// thuộc tính — vì bản dịch tiếng Việt có thể đổi nội dung thuộc tính
    /// (ví dụ <c>class="highlight"</c>) mà vẫn render đúng.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ComputeStructureSignature(string? text)
    {
        var tags = ExtractTags(text);

        if (tags.Count == 0)
        {
            return Array.Empty<string>();
        }

        var signature = new List<string>();

        foreach (var tag in tags)
        {
            signature.Add(tag.IsClosing ? $"/{tag.Name}" : tag.Name);
        }

        return signature;
    }
}

/// <summary>
/// Một thẻ HTML/Panorama được trích xuất.
/// </summary>
public readonly record struct HtmlTag(
    string Name,
    bool IsClosing,
    bool IsVoid,
    string Raw);
