using System.Globalization;

using HouseBills.Wpf.Converters;
using HouseBills.Wpf.Localization;

namespace HouseBills.Wpf.Tests.Localization;

[Collection(UiCultureCollection.Name)]
public sealed class MoneyConverterTests : IDisposable
{
    private readonly CultureInfo _original = LocalizedStrings.FormattingCulture;

    public void Dispose() => LocalizedStrings.FormattingCulture = _original;

    [Theory]
    [InlineData("sr", "1.234,56 RSD")]
    [InlineData("en-US", "$1,234.56")]
    public void Convert_Amount_UsesChosenLanguageFormats(string formats, string expected)
    {
        LocalizedStrings.FormattingCulture = formats == "sr" ? FormattingCultures.Serbian : CultureInfo.GetCultureInfo(formats);

        // The binding's own culture (WPF passes the element Language) is deliberately ignored.
        new MoneyConverter().Convert(1234.56m, typeof(string), null, CultureInfo.InvariantCulture).ShouldBe(expected);
    }

    [Fact]
    public void Convert_Null_ReturnsNull()
    {
        new MoneyConverter().Convert(null, typeof(string), null, CultureInfo.InvariantCulture).ShouldBeNull();
    }

    [Theory]
    [InlineData(true, "≈ 1.234,56 RSD")]
    [InlineData(false, "1.234,56 RSD")]
    public void EstimatedMoney_Amount_PrefixesEstimates(bool isEstimated, string expected)
    {
        LocalizedStrings.FormattingCulture = FormattingCultures.Serbian;

        new EstimatedMoneyConverter().Convert([1234.56m, isEstimated], typeof(string), null, CultureInfo.InvariantCulture).ShouldBe(expected);
    }
}