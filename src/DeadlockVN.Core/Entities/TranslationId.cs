namespace DeadlockVN.Core.Entities;

/// <summary>
/// Định danh duy nhất cho một entry bản dịch.
/// <para>
/// <b>Vì sao cần cả SourceFile?</b> Key <c>Language</c> xuất hiện ở 10 file
/// khác nhau trong game. Nếu chỉ định danh bằng key thì sẽ lẫn các entry.
/// Vì vậy định danh là cặp <c>(SourceFile, Key)</c>.
/// </para>
/// </summary>
public readonly record struct TranslationId
{
    public TranslationId(string sourceFile, string key)
    {
        SourceFile = (sourceFile ?? string.Empty).Replace('\\', '/');
        Key = key ?? string.Empty;
    }

    /// <summary>
    /// Đường dẫn file tương đối so với thư mục localization, dùng dấu <c>/</c>
    /// (không phụ thuộc hệ điều hành).
    /// </summary>
    public string SourceFile { get; }

    /// <summary>Localization key.</summary>
    public string Key { get; }

    public override string ToString() => $"{SourceFile}#{Key}";
}
