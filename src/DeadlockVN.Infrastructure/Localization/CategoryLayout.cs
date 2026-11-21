using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;

namespace DeadlockVN.Infrastructure.Localization;

/// <summary>
/// Quy tắc ánh xạ giữa nhóm nội dung và tên thư mục trong workspace.
/// <para>
/// Mục đích: một file JSON cho toàn bộ 91 457 entry nặng ~31 MB — quá lớn để
/// quản lý bằng git và khó cho nhiều người cùng dịch. Tách theo nhóm vừa
/// đúng với cách chia việc, vừa giữ mỗi file ở kích thước hợp lý.
/// </para>
/// </summary>
public static class CategoryLayout
{
    /// <summary>
    /// Thư mục con của <c>workspace/translations</c> cho từng nhóm.
    /// </summary>
    public static string GetFolder(LocalizationCategory category)
    {
        return category switch
        {
            LocalizationCategory.Items => "items",
            LocalizationCategory.Heroes => "heroes",
            LocalizationCategory.Abilities => "abilities",
            LocalizationCategory.Ui => "ui",
            LocalizationCategory.Voice => "voice",
            LocalizationCategory.Misc => "misc",
            _ => "misc"
        };
    }

    /// <summary>
    /// Tên file JSON cho một nhóm.
    /// </summary>
    public static string GetFileName(LocalizationCategory category)
    {
        return $"{GetFolder(category)}.json";
    }

    /// <summary>
    /// Tất cả các nhóm (không bao gồm <see cref="LocalizationCategory.All"/>).
    /// </summary>
    public static IReadOnlyList<LocalizationCategory> All { get; } = new[]
    {
        LocalizationCategory.Items,
        LocalizationCategory.Heroes,
        LocalizationCategory.Abilities,
        LocalizationCategory.Ui,
        LocalizationCategory.Voice,
        LocalizationCategory.Misc
    };

    /// <summary>
    /// Đường dẫn file đầy đủ cho một nhóm, dựa trên thư mục gốc.
    /// </summary>
    public static string GetPath(string rootDirectory, LocalizationCategory category)
    {
        return Path.Combine(
            rootDirectory,
            GetFolder(category),
            GetFileName(category));
    }
}
