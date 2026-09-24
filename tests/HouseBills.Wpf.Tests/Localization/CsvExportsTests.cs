using System.Globalization;

using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Payees;
using HouseBills.Application.Reports;
using HouseBills.Domain;
using HouseBills.Wpf.Export;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.ViewModels.Reports;

namespace HouseBills.Wpf.Tests.Localization;

/// <summary>Changes the process-wide regional culture used for exports, so runs with the other culture tests.</summary>
[Collection(UiCultureCollection.Name)]
public sealed class CsvExportsTests : IDisposable
{
    private static readonly DateOnly Due = new(2026, 10, 15);
    private readonly CultureInfo _original = LocalizedStrings.RegionalCulture;

    public void Dispose() => LocalizedStrings.RegionalCulture = _original;

    [Fact]
    public void Bills_EnglishRegionalSettings_UsesCommaSeparatorAndDecimalPoint()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("en-US");

        var lines = Lines(CsvExports.Bills([Bill(4230.5m, estimated: true, "Power, main meter")]));

        lines[0].ShouldBe("Status,Due,Description,Payee,Category,Amount,Estimated amount,Paid on,Paid,Notes");
        lines[1].ShouldBe("Upcoming,10/15/2026,\"Power, main meter\",EPS,Utilities,4230.50,Yes,,,");
    }

    [Fact]
    public void Bills_SerbianRegionalSettings_UsesSemicolonSeparatorAndDecimalComma()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("sr-Latn-RS");

        var lines = Lines(CsvExports.Bills([Bill(4230.5m, estimated: false, "Struja")]));

        lines[1].ShouldBe("Upcoming;15.10.2026.;Struja;EPS;Utilities;4230,50;;;;");
    }

    [Fact]
    public void Report_Always_WritesMonthTableThenCategoryTable()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("en-US");
        var month = new MonthlySummaryItem("January", new MonthlySummaryRow(1, 2, 150m, 100m, 50m, 120m), 1d);

        var lines = CsvExports.Report([month], [new CategoryTotalRow("Utilities", 2, 150m, 100m)]).Split("\r\n");

        lines[0].ShouldStartWith("Month,Bills,Total,Paid,Outstanding,Previous year,Change");
        lines[1].ShouldBe("January,2,150.00,100.00,50.00,120.00,30.00");
        lines[2].ShouldBeEmpty();
        lines[3].ShouldBe("Category,Bills,Total,Paid");
        lines[4].ShouldBe("Utilities,2,150.00,100.00");
    }

    [Fact]
    public void Categories_Always_WritesNameColumn()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("en-US");

        var lines = Lines(CsvExports.Categories([new CategoryDto(1, "Internet & Phone", [1]), new CategoryDto(2, "Rent, Mortgage", [1])]));

        lines.ShouldBe(["Name", "Internet & Phone", "\"Rent, Mortgage\""]);
    }

    [Fact]
    public void Payees_SerbianRegionalSettings_WritesShownColumnsWithSemicolons()
    {
        LocalizedStrings.RegionalCulture = CultureInfo.GetCultureInfo("sr-Latn-RS");

        var lines = Lines(CsvExports.Payees([new PayeeDto(1, "EPS", "123-456; ref", null, [1])]));

        lines.ShouldBe(["Name;Account reference;Notes", "EPS;\"123-456; ref\";"]);
    }

    private static string[] Lines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    private static BillListItem Bill(decimal amount, bool estimated, string description) =>
        new(1, description, 1, "EPS", 1, "Utilities", amount, Due, null, null, null, null, [1])
        {
            Status = BillStatus.Upcoming,
            IsEstimated = estimated,
        };
}