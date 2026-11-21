namespace DeadlockVN.Core.Entities;

public sealed class LocalizationFileInfo
{
    public string RelativePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}