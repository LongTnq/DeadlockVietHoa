using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;

namespace DeadlockVN.Core.Interfaces;

/// <summary>
/// Phân loại nội dung localization theo nhóm, để chia nhỏ công việc dịch.
/// </summary>
public interface ILocalizationClassifier
{
    /// <summary>
    /// Xác định nhóm nội dung của một entry.
    /// </summary>
    LocalizationCategory Classify(TranslationEntry entry);
}
