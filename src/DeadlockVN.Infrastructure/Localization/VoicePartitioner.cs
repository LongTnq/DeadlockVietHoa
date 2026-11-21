using System.Text.RegularExpressions;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;

namespace DeadlockVN.Infrastructure.Localization;

/// <summary>
/// Tách nhóm nội dung <b>Voice</b> thành các file nhỏ theo từng hero.
/// <para>
/// <b>Vì sao cần?</b> File VO gốc chứa 78 438 entry — nếu gộp vào một file
/// JSON sẽ nặng ~26 MB, rất khó để review và tra cứu. Chia theo hero biến
/// nó thành ~26 file khoảng 1 MB, mỗi file là một đơn vị công việc dịch
/// tự nhiên (một hero = một người dịch).
/// </para>
/// <para>
/// <b>Cách xác định hero của một entry:</b> key VO có dạng
/// <c>&lt;hero&gt;_&lt;...&gt;</c> (ví dụ <c>gigawatt_kill_01</c>). Danh sách
/// hero lấy từ nhóm <see cref="LocalizationCategory.Heroes"/> — các key
/// <c>hero_&lt;slug&gt;:n</c>. Khớp theo tiền tố dài nhất để tránh nhầm
/// giữa các tên hero có chung tiền tố (ví dụ <c>artist</c> và
/// <c>artillery</c>).
/// </para>
/// </summary>
public static partial class VoicePartitioner
{
    /// <summary>
    /// Tên bucket cho các entry VO không thuộc hero nào (chung cho tất cả).
    /// </summary>
    public const string CommonBucket = "_common";

    [GeneratedRegex(@"^hero_(?<slug>[a-z0-9]+)(_search|_sort)?:n$", RegexOptions.IgnoreCase)]
    private static partial Regex HeroSlugRegex();

    /// <summary>
    /// Trích xuất danh sách slug hero từ nhóm Heroes.
    /// </summary>
    public static HashSet<string> ExtractHeroSlugs(IEnumerable<TranslationEntry> entries)
    {
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.Category != LocalizationCategory.Heroes)
            {
                continue;
            }

            Match match = HeroSlugRegex().Match(entry.Key);

            if (!match.Success)
            {
                continue;
            }

            string slug = match.Groups["slug"].Value;

            // Bỏ các slug biến thể chỉ dùng để tìm kiếm/sắp xếp.
            if (slug.EndsWith("_search", StringComparison.OrdinalIgnoreCase) ||
                slug.EndsWith("_sort", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            slugs.Add(slug);
        }

        return slugs;
    }

    /// <summary>
    /// Xác định bucket (tên file con) cho một entry VO.
    /// </summary>
    public static string GetBucket(TranslationEntry entry, IReadOnlySet<string> heroSlugs)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (heroSlugs.Count == 0)
        {
            return CommonBucket;
        }

        string key = entry.Key;

        string? bestMatch = null;

        foreach (string slug in heroSlugs)
        {
            if (!key.StartsWith(slug + "_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (bestMatch is null || slug.Length > bestMatch.Length)
            {
                bestMatch = slug;
            }
        }

        return bestMatch ?? CommonBucket;
    }

    /// <summary>
    /// Gom các entry VO theo bucket hero.
    /// </summary>
    public static Dictionary<string, List<TranslationEntry>> Group(
        IEnumerable<TranslationEntry> voiceEntries,
        IReadOnlySet<string> heroSlugs)
    {
        var groups = new Dictionary<string, List<TranslationEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in voiceEntries)
        {
            string bucket = GetBucket(entry, heroSlugs);

            if (!groups.TryGetValue(bucket, out var list))
            {
                list = new List<TranslationEntry>();

                groups[bucket] = list;
            }

            list.Add(entry);
        }

        return groups;
    }
}
