using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.UnitTests;

/// <summary>
/// Test cho <see cref="TranslationGuard"/> — chống bản dịch hỏng.
/// </summary>
public sealed class TranslationGuardTests
{
    private readonly TranslationGuard _guard = new();

    [Fact]
    public void Check_AcceptsGoodTranslation()
    {
        var check = _guard.Check(
            "Deal {s:Damage} damage",
            "Gây {s:Damage} sát thương");

        Assert.True(check.IsValid);
        Assert.Empty(check.Errors);
    }

    [Fact]
    public void Check_RejectsMissingPlaceholder()
    {
        var check = _guard.Check(
            "Deal {s:Damage} damage",
            "Gây sát thương");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("{s:Damage}"));
    }

    [Fact]
    public void Check_RejectsExtraPlaceholder()
    {
        var check = _guard.Check(
            "Hello",
            "Xin chào {s:Damage}");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("Thừa"));
    }

    [Fact]
    public void Check_RejectsChangedPlaceholderName()
    {
        // Model hay đổi chữ hoa/thường trong placeholder.
        var check = _guard.Check(
            "Deal {s:Damage} damage",
            "Gây {s:damage} sát thương");

        Assert.False(check.IsValid);
    }

    [Fact]
    public void Check_RejectsDuplicatedPlaceholder()
    {
        var check = _guard.Check(
            "{s:A} and {s:B}",
            "{s:A} và {s:A}");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("xuất hiện"));
    }

    [Fact]
    public void Check_RejectsEmptyTranslation()
    {
        var check = _guard.Check("Hello", "   ");

        Assert.False(check.IsValid);
    }

    [Fact]
    public void Check_RejectsBrokenHtmlStructure()
    {
        var check = _guard.Check(
            "<b>Warning</b><br>Deal damage",
            "<b>Warning<br>Deal damage");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("thẻ HTML"));
    }

    [Fact]
    public void Check_AcceptsSameHtmlStructure()
    {
        var check = _guard.Check(
            "<b>Warning</b><br><span class=\"x\">Damage</span>",
            "<b>Cảnh báo</b><br><span class=\"x\">Sát thương</span>");

        Assert.True(check.IsValid);
    }

    [Fact]
    public void Check_IgnoresContextTagsInVoiceLines()
    {
        // Thẻ ngữ cảnh trong VO không phải HTML — không được coi là lỗi.
        var check = _guard.Check(
            "<laughs> Get over here!",
            "<laughs> Đến đây đi!");

        Assert.True(check.IsValid);
    }

    [Fact]
    public void Check_WarnsWhenTranslationEqualsOriginal()
    {
        var check = _guard.Check("Deadlock", "Deadlock");

        // Không chặn lưu, chỉ cảnh báo.
        Assert.True(check.IsValid);
        Assert.Contains(check.Warnings, w => w.Contains("trùng"));
    }

    [Fact]
    public void Check_RejectsUntranslatedLongText()
    {
        // Model bỏ sót đoạn này -> game hiển thị tiếng Anh lẫn trong
        // bản dịch. Phải báo lỗi để service thử lại.
        var check = _guard.Check(
            "Your bullets build up to a Silence",
            "Your bullets build up to a Silence");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("chưa được dịch"));
    }

    [Fact]
    public void Check_AcceptsUntranslatedShortText()
    {
        // Tên riêng giữ nguyên là đúng — không được chặn.
        var check = _guard.Check("Spirit Bolt", "Spirit Bolt");

        Assert.True(check.IsValid);
    }

    [Fact]
    public void Check_RejectsChineseCharacters()
    {
        // Model đôi khi sinh tiếng Trung — phải chặn.
        var check = _guard.Check(
            "Cloak of Opportunity",
            "L㎜atoria của cơ hội");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("CJK"));
    }

    [Fact]
    public void Check_AcceptsJapaneseInOriginal()
    {
        // Lore Nhật (Yamato) và keybind Nhật là hợp lệ.
        var check = _guard.Check(
            "全/半/角",
            "全/半/角");

        Assert.True(check.IsValid);
    }

    [Fact]
    public void Check_AcceptsEmptyOriginalWithEmptyTranslation()
    {
        var check = _guard.Check("", "");

        Assert.True(check.IsValid);
    }

    [Fact]
    public void Check_RejectsContentWhenOriginalIsEmpty()
    {
        var check = _guard.Check("", "Có nội dung");

        Assert.False(check.IsValid);
    }

    [Fact]
    public void Check_HandlesPluralTokens()
    {
        var check = _guard.Check(
            "{count, plural, one{# item} other{# items}}",
            "{count, plural, one{# món đồ} other{# món đồ}}");

        Assert.True(check.IsValid);
    }

    // ---------------- luật mới: chất lượng bản dịch ----------------

    [Fact]
    public void Check_RejectsFrenchTranslation()
    {
        // Model 7B đôi khi dịch sang tiếng Pháp.
        var check = _guard.Check("Heroic Aura", "Aura Héroïque");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("Pháp"));
    }

    [Fact]
    public void Check_AcceptsVietnameseAccents()
    {
        // Tiếng Việt dùng chung nhiều ký tự có dấu với tiếng Pháp —
        // không được báo nhầm.
        var check = _guard.Check(
            "Cận Chiến",
            "Hỗ trợ đồng đội khi bị tấn công");

        Assert.True(check.IsValid);
    }

    [Fact]
    public void Check_AcceptsHyphenInProperName()
    {
        // Dấu gạch nối trong tên là chữ, không phải dấu âm.
        var check = _guard.Check(
            "High-Velocity Magazine",
            "Hộp đạn tốc độ cao");

        Assert.True(check.IsValid);
    }

    [Fact]
    public void Check_RejectsMissingMathSymbol()
    {
        // Model hay làm rơi dấu "+" khi rút gọn.
        var check = _guard.Check(
            "+{s:BonusFireRatePerStack}% Fire Rate",
            "{s:BonusFireRatePerStack}% Tốc độ bắn");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("'+'"));
    }

    [Fact]
    public void Check_RejectsMissingNumber()
    {
        var check = _guard.Check(
            "Deals 75% damage over 2.5 seconds",
            "Gây sát thương trong vài giây");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("thiếu số"));
    }

    [Fact]
    public void Check_RejectsMostlyEnglishTranslation()
    {
        // Model rút gọn câu, còn lại toàn tiếng Anh.
        var check = _guard.Check(
            "Create a circular display of ink that blocks vision",
            "Create a circle of ink blocking vision");

        Assert.False(check.IsValid);
        Assert.Contains(check.Errors, e => e.Contains("tiếng Anh"));
    }

    [Fact]
    public void Check_AcceptsProperNameKeptInEnglish()
    {
        // Tên riêng giữ nguyên là hợp lệ — không báo "toàn tiếng Anh".
        var check = _guard.Check(
            "Heroic Aura",
            "Heroic Aura");

        Assert.True(check.IsValid);
    }
}
