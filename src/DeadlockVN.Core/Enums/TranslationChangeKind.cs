namespace DeadlockVN.Core.Enums;

/// <summary>
/// Loại thay đổi giữa bản dịch hiện tại và bản gốc mới từ game.
/// </summary>
public enum TranslationChangeKind
{
    /// <summary>Không có gì thay đổi.</summary>
    Unchanged = 0,

    /// <summary>Key mới, game vừa thêm vào.</summary>
    Added = 1,

    /// <summary>Nội dung gốc đã thay đổi -> bản dịch có thể đã lỗi thời.</summary>
    Modified = 2,

    /// <summary>Key không còn tồn tại trong game (đã bị xoá).</summary>
    Removed = 3
}
