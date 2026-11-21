using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="UntranslatedDetector"/> — phát hiện bản dịch mà
/// model bỏ qua (trả về nguyên văn tiếng Anh).
/// </summary>
public sealed class UntranslatedDetectorTests
{
    [Fact]
    public void IsUntranslated_DetectsIdenticalLongText()
    {
        Assert.True(UntranslatedDetector.IsUntranslated(
            "Your bullets build up to a Silence",
            "Your bullets build up to a Silence"));
    }

    [Fact]
    public void IsUntranslated_IgnoresShortText()
    {
        // Tên riêng giữ nguyên là hợp lệ.
        Assert.False(UntranslatedDetector.IsUntranslated(
            "Spirit Bolt",
            "Spirit Bolt"));

        Assert.False(UntranslatedDetector.IsUntranslated(
            "Deadlock",
            "Deadlock"));
    }

    [Fact]
    public void IsUntranslated_IgnoresProperNames()
    {
        // Tên hero / tên vật phẩm giữ nguyên là đúng.
        Assert.False(UntranslatedDetector.IsUntranslated(
            "Hogarth the Fabulous",
            "Hogarth the Fabulous"));
    }

    [Fact]
    public void IsUntranslated_IgnoresTranslatedText()
    {
        Assert.False(UntranslatedDetector.IsUntranslated(
            "Your bullets build up to a Silence",
            "Đạn của bạn tích tụ thành một Thụt kín"));
    }

    [Fact]
    public void IsUntranslated_IgnoresEmpty()
    {
        Assert.False(UntranslatedDetector.IsUntranslated("", ""));
        Assert.False(UntranslatedDetector.IsUntranslated("Hello", ""));
    }

    [Fact]
    public void IsUntranslated_IgnoresWhitespaceDifferences()
    {
        // Chỉ khác khoảng trắng -> vẫn coi là chưa dịch.
        Assert.True(UntranslatedDetector.IsUntranslated(
            "Deal damage to target",
            "Deal damage to target  "));
    }

    [Fact]
    public void IsUntranslated_RequiresFunctionWord()
    {
        // 3 từ nhưng chỉ có một từ viết thường -> tên riêng, không báo.
        Assert.False(UntranslatedDetector.IsUntranslated(
            "Spirit Bolt fired",
            "Spirit Bolt fired"));

        // Câu tiếng Anh thật -> báo.
        Assert.True(UntranslatedDetector.IsUntranslated(
            "The Spirit Bolt fires at your target",
            "The Spirit Bolt fires at your target"));
    }
}
