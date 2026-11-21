namespace DeadlockVN.Core.Enums;

/// <summary>
/// Nhóm nội dung localization, dùng để chia nhỏ công việc dịch và báo cáo tiến độ.
/// </summary>
public enum LocalizationCategory
{
    /// <summary>Tất cả các nhóm.</summary>
    All = 0,

    /// <summary>Tên và mô tả vật phẩm (item).</summary>
    Items = 1,

    /// <summary>Tên hero.</summary>
    Heroes = 2,

    /// <summary>Mô tả và tên kỹ năng (ability).</summary>
    Abilities = 3,

    /// <summary>Giao diện người dùng, chuỗi hệ thống, thuộc tính.</summary>
    Ui = 4,

    /// <summary>Lời thoại (voice over).</summary>
    Voice = 5,

    /// <summary>Patch notes, sự kiện theo mùa, và nội dung lẫn nhóm.</summary>
    Misc = 6
}
