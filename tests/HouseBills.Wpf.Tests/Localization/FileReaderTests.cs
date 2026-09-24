using System.Globalization;
using System.Text;

using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;

namespace HouseBills.Wpf.Tests.Localization;

/// <summary>Changes the process-wide regional culture (its code page), so runs with the other culture tests.</summary>
[Collection(UiCultureCollection.Name)]
public sealed class FileReaderTests : IDisposable
{
    private const string Text = "Naziv\r\nČistoća ž\r\n";
    private readonly CultureInfo _original = LocalizedStrings.RegionalCulture;

    public void Dispose() => LocalizedStrings.RegionalCulture = _original;

    [Fact]
    public void Decode_Utf8WithByteOrderMark_DropsTheMark()
    {
        FileReader.Decode([.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(Text)]).ShouldBe(Text);
    }

    [Fact]
    public void Decode_Utf8WithoutByteOrderMark_ReadsUtf8()
    {
        FileReader.Decode(Encoding.UTF8.GetBytes(Text)).ShouldBe(Text);
    }

    [Fact]
    public void Decode_ExcelAnsiCsvWithSerbianRegionalSettings_ReadsWindows1250()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("sr-Latn-RS");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        FileReader.Decode(Encoding.GetEncoding(1250).GetBytes(Text)).ShouldBe(Text);
    }
}