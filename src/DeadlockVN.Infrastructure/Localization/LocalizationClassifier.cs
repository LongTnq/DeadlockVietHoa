using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;

namespace DeadlockVN.Infrastructure.Localization;

/// <summary>
/// Phân loại entry localization vào nhóm nội dung, dựa trên đường dẫn file
/// nguồn và tiền tố của localization key.
/// <para>
/// Quy tắc xác định từ dữ liệu thật của Deadlock:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>citadel_generated_vo</c> → <b>Voice</b> (78 438 entry, 86% tổng số).
///   </description></item>
///   <item><description>
///     <c>citadel_gc_mod_names</c> → <b>Items</b> (tên + mô tả vật phẩm).
///   </description></item>
///   <item><description>
///     <c>citadel_gc_hero_names</c> → <b>Heroes</b>.
///   </description></item>
///   <item><description>
///     <c>citadel_heroes</c> → <b>Abilities</b> (mô tả kỹ năng, trạng thái).
///   </description></item>
///   <item><description>
///     <c>citadel_attributes</c>, <c>citadel_gc</c>, <c>citadel_main</c>,
///     <c>citadel_vdata</c> → <b>Ui</b>.
///   </description></item>
///   <item><description>
///     <c>citadel_patch_notes</c>, <c>citadel_seasonal</c> → <b>Misc</b>.
///   </description></item>
/// </list>
/// </summary>
public sealed class LocalizationClassifier : ILocalizationClassifier
{
    public LocalizationCategory Classify(TranslationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string file = entry.SourceFile;

        if (file.Contains("generated_vo", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Voice;
        }

        if (file.Contains("mod_names", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Items;
        }

        if (file.Contains("hero_names", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Heroes;
        }

        if (file.Contains("heroes", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Abilities;
        }

        if (file.Contains("patch_notes", StringComparison.OrdinalIgnoreCase) ||
            file.Contains("seasonal", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Misc;
        }

        if (file.Contains("attributes", StringComparison.OrdinalIgnoreCase) ||
            file.Contains("citadel_gc", StringComparison.OrdinalIgnoreCase) ||
            file.Contains("citadel_main", StringComparison.OrdinalIgnoreCase) ||
            file.Contains("vdata", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Ui;
        }

        // Dựa trên tiền tố key làm dự phòng.
        string key = entry.Key;

        if (key.StartsWith("citadel_ability_", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("ability_", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Abilities;
        }

        if (key.StartsWith("upgrade_", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Items;
        }

        if (key.Contains("hero", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationCategory.Heroes;
        }

        return LocalizationCategory.Ui;
    }
}
