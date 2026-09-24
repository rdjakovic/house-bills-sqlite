using System.Globalization;

using HouseBills.Application.Bills;
using HouseBills.Application.Categories;
using HouseBills.Application.Export;
using HouseBills.Application.Payees;
using HouseBills.Application.Reports;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Converters;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.ViewModels.Reports;

namespace HouseBills.Wpf.Export;

/// <summary>
/// CSV files for Excel. Headers and status names are in the app language; numbers, dates and the column separator follow
/// the Windows regional settings (<see cref="LocalizedStrings.RegionalCulture"/>), because that is how Excel reads a CSV
/// file — regardless of the language chosen in HouseBills.
/// </summary>
public static class CsvExports
{
    public static string Bills(IEnumerable<BillListItem> bills)
    {
        var culture = LocalizedStrings.RegionalCulture;
        var csv = new CsvDocument(culture.TextInfo.ListSeparator).AddRow(
            Strings.Field_Status,
            Strings.Column_Due,
            Strings.Field_Description,
            Strings.Field_Payee,
            Strings.Field_Category,
            Strings.Field_Amount,
            Strings.Field_IsEstimated,
            Strings.Field_PaidOn,
            Strings.Column_Paid,
            Strings.Field_Notes);
        foreach (var bill in bills)
        {
            csv.AddRow(
                LocalizedStrings.Instance[EnumToLocalizedTextConverter.KeyFor(bill.Status)],
                Date(bill.DueDate, culture),
                bill.Description,
                bill.PayeeName,
                bill.CategoryName,
                Amount(bill.Amount, culture),
                bill.IsEstimated ? Strings.Common_Yes : null,
                bill.PaidOn is { } paidOn ? Date(paidOn, culture) : null,
                bill.PaidAmount is { } paid ? Amount(paid, culture) : null,
                bill.Notes);
        }

        return csv.ToString();
    }

    /// <summary>Two tables in one file: by month, then (after a blank line) by category.</summary>
    public static string Report(IEnumerable<MonthlySummaryItem> months, IEnumerable<CategoryTotalRow> categories)
    {
        var culture = LocalizedStrings.RegionalCulture;
        var csv = new CsvDocument(culture.TextInfo.ListSeparator).AddRow(
            Strings.Column_Month,
            Strings.Column_BillCount,
            Strings.Column_Total,
            Strings.Column_Paid,
            Strings.Column_Outstanding,
            Strings.Column_PreviousYear,
            Strings.Column_Change);
        foreach (var month in months)
        {
            csv.AddRow(
                month.MonthName,
                month.Row.BillCount.ToString(culture),
                Amount(month.Row.TotalAmount, culture),
                Amount(month.Row.PaidAmount, culture),
                Amount(month.Row.OutstandingAmount, culture),
                Amount(month.Row.PreviousYearTotalAmount, culture),
                Amount(month.ChangeFromPreviousYear, culture));
        }

        csv.AddBlankLine().AddRow(Strings.Field_Category, Strings.Column_BillCount, Strings.Column_Total, Strings.Column_Paid);
        foreach (var category in categories)
        {
            csv.AddRow(category.CategoryName, category.BillCount.ToString(culture), Amount(category.TotalAmount, culture), Amount(category.PaidAmount, culture));
        }

        return csv.ToString();
    }

    public static string Categories(IEnumerable<CategoryDto> categories)
    {
        var csv = new CsvDocument(LocalizedStrings.RegionalCulture.TextInfo.ListSeparator).AddRow(Strings.Field_Name);
        foreach (var category in categories)
        {
            csv.AddRow(category.Name);
        }

        return csv.ToString();
    }

    public static string Payees(IEnumerable<PayeeDto> payees)
    {
        var csv = new CsvDocument(LocalizedStrings.RegionalCulture.TextInfo.ListSeparator)
            .AddRow(Strings.Field_Name, Strings.Field_AccountReference, Strings.Field_Notes);
        foreach (var payee in payees)
        {
            csv.AddRow(payee.Name, payee.AccountReference, payee.Notes);
        }

        return csv.ToString();
    }

    /// <summary>A plain number (no currency, no thousands separator), so Excel reads it as a number.</summary>
    private static string Amount(decimal amount, CultureInfo culture) => amount.ToString("0.00", culture);

    private static string Date(DateOnly date, CultureInfo culture) => date.ToString("d", culture);
}