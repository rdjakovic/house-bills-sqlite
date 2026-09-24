using System.Globalization;

using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Import;
using HouseBills.Application.Payees;
using HouseBills.Domain;
using HouseBills.Wpf.Export;
using HouseBills.Wpf.Import;
using HouseBills.Wpf.Localization;

namespace HouseBills.Wpf.Tests.Localization;

/// <summary>Changes the process-wide regional culture used for imports, so runs with the other culture tests.</summary>
[Collection(UiCultureCollection.Name)]
public sealed class CsvImportsTests : IDisposable
{
    private static readonly DateOnly Due = new(2026, 10, 15);
    private readonly CultureInfo _original = LocalizedStrings.RegionalCulture;

    public void Dispose() => LocalizedStrings.RegionalCulture = _original;

    [Theory]
    [InlineData("en-US")]
    [InlineData("sr-Latn-RS")]
    public void Bills_ExportedFile_ReadsBackTheSameBills(string regional)
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo(regional);
        var paid = Bill(4230.5m, "Power, main meter") with { PaidOn = Due.AddDays(-1), PaidAmount = 4200m, Notes = "line 1\r\nline 2" };
        var estimated = Bill(99.99m, "Water") with { IsEstimated = true };

        var import = CsvImports.Bills(CsvExports.Bills([paid, estimated]));

        import.Errors.ShouldBeEmpty();
        import.Rows.ShouldBe([
            new ImportedBill(2, "Power, main meter", "EPS", "Utilities", 4230.5m, Due, false, Due.AddDays(-1), 4200m, "line 1\r\nline 2"),
            new ImportedBill(3, "Water", "EPS", "Utilities", 99.99m, Due, true, null, null, null),
        ]);
    }

    [Fact]
    public void Bills_SerbianHeadersInOtherOrderWithIsoDates_AreRecognized()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("en-US");

        var import = CsvImports.Bills("Iznos,Kategorija,Primalac,Opis,Dospeće,Procenjen iznos\r\n12.50,Komunalije,EPS,Struja,2026-10-15,da\r\n,,,,,\r\n");

        import.Errors.ShouldBeEmpty();
        import.Rows.ShouldHaveSingleItem().ShouldBe(new ImportedBill(2, "Struja", "EPS", "Komunalije", 12.5m, Due, true, null, null, null));
    }

    [Fact]
    public void Bills_InvalidCells_ReportsEachWithRowNumber()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("sr-Latn-RS");

        var import = CsvImports.Bills("Due;Description;Payee;Category;Amount;Estimated amount\r\n15.10.2026.;Power;EPS;Utilities;4230.50;\r\n;Water;Infostan;Utilities;10;maybe\r\n");

        import.Errors.ShouldBe([
            "Row 2: “4230.50” in “Amount” is not an amount.",
            "Row 3: “Due” is empty.",
            "Row 3: “maybe” in “Estimated amount”: write “Yes” or leave it empty.",
        ]);
    }

    [Fact]
    public void Bills_MissingColumns_NamesThem()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("en-US");

        var import = CsvImports.Bills("Description,Amount\r\nPower,10\r\n");

        import.Rows.ShouldBeEmpty();
        import.Errors.Count.ShouldBe(3);
        import.Errors[0].ShouldStartWith("The file has no “Due” column.");
    }

    [Fact]
    public void Payees_ExportedFile_ReadsBackTheSamePayees()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("sr-Latn-RS");

        var import = CsvImports.Payees(CsvExports.Payees([new PayeeDto(1, "EPS", "123-456; ref", null, [1]), new PayeeDto(2, "Infostan", null, "voda", [1])]));

        import.Errors.ShouldBeEmpty();
        import.Rows.ShouldBe([new ImportedPayee(2, "EPS", "123-456; ref", null), new ImportedPayee(3, "Infostan", null, "voda")]);
    }

    [Fact]
    public void Categories_OneColumnFile_ReadsNames()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("sr-Latn-RS");

        var import = CsvImports.Categories(CsvExports.Categories([new CategoryDto(1, "Rent, Mortgage", [1]), new CategoryDto(2, "Pets", [1])]));

        import.Errors.ShouldBeEmpty();
        import.Rows.ShouldBe([new ImportedCategory(2, "Rent, Mortgage"), new ImportedCategory(3, "Pets")]);
    }

    [Fact]
    public void Categories_EmptyFile_IsReported()
    {
        CsvImports.Categories(string.Empty).Errors.ShouldBe(["The file is empty."]);
    }

    private static BillListItem Bill(decimal amount, string description) =>
        new(1, description, 1, "EPS", 1, "Utilities", amount, Due, null, null, null, null, [1]) { Status = BillStatus.Upcoming };
}