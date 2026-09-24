using System.Globalization;

using HouseBills.Application.Export;
using HouseBills.Application.Import;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Localization;

namespace HouseBills.Wpf.Import;

/// <summary>
/// Reads the CSV files <see cref="Export.CsvExports"/> writes, also after editing in Excel. Columns are found by their
/// header in either app language, in any order; other columns (e.g. Status) are ignored. Numbers and dates follow the
/// Windows regional settings, as in the export; dates may also be written as yyyy-MM-dd.
/// </summary>
public static class CsvImports
{
    private static readonly CultureInfo[] HeaderLanguages =
        [CultureInfo.GetCultureInfo(LocalizationService.English.CultureName), CultureInfo.GetCultureInfo(LocalizationService.Serbian.CultureName)];

    public static CsvImport<ImportedCategory> Categories(string text) =>
        Read(text, [nameof(Strings.Field_Name)], [], row => new ImportedCategory(row.Number, row.Text(nameof(Strings.Field_Name)) ?? string.Empty));

    public static CsvImport<ImportedPayee> Payees(string text) =>
        Read(
            text,
            [nameof(Strings.Field_Name)],
            [nameof(Strings.Field_AccountReference), nameof(Strings.Field_Notes)],
            row => new ImportedPayee(
                row.Number,
                row.Text(nameof(Strings.Field_Name)) ?? string.Empty,
                row.Text(nameof(Strings.Field_AccountReference)),
                row.Text(nameof(Strings.Field_Notes))));

    public static CsvImport<ImportedBill> Bills(string text) =>
        Read(
            text,
            [nameof(Strings.Column_Due), nameof(Strings.Field_Description), nameof(Strings.Field_Payee), nameof(Strings.Field_Category), nameof(Strings.Field_Amount)],
            [nameof(Strings.Field_IsEstimated), nameof(Strings.Field_PaidOn), nameof(Strings.Column_Paid), nameof(Strings.Field_Notes)],
            row => new ImportedBill(
                row.Number,
                row.Text(nameof(Strings.Field_Description)) ?? string.Empty,
                row.Text(nameof(Strings.Field_Payee)) ?? string.Empty,
                row.Text(nameof(Strings.Field_Category)) ?? string.Empty,
                row.RequiredAmount(nameof(Strings.Field_Amount)),
                row.RequiredDate(nameof(Strings.Column_Due)),
                row.Yes(nameof(Strings.Field_IsEstimated)),
                row.Date(nameof(Strings.Field_PaidOn)),
                row.Amount(nameof(Strings.Column_Paid)),
                row.Text(nameof(Strings.Field_Notes))));

    /// <param name="text">The file content.</param>
    /// <param name="required">Resource keys of the columns the file must have.</param>
    /// <param name="optional">Resource keys of the columns read when present.</param>
    /// <param name="map">Builds an item from one row; conversion problems are collected by the row.</param>
    private static CsvImport<T> Read<T>(string text, string[] required, string[] optional, Func<CsvImportRow, T> map)
    {
        var culture = LocalizedStrings.RegionalCulture;
        text = text.TrimStart('\uFEFF');
        var records = CsvReader.Parse(text, CsvReader.DetectSeparator(text, culture.TextInfo.ListSeparator));
        if (records.Count == 0)
        {
            return CsvImport<T>.Failed(Strings.Import_Empty);
        }

        var columns = new Dictionary<string, int>();
        foreach (var key in required.Concat(optional))
        {
            var names = HeaderLanguages.Select(l => Strings.ResourceManager.GetString(key, l)).OfType<string>().ToList();
            var index = records[0].ToList().FindIndex(h => names.Contains(h.Trim(), StringComparer.OrdinalIgnoreCase));
            if (index >= 0)
            {
                columns[key] = index;
            }
        }

        var missing = required.Where(k => !columns.ContainsKey(k)).ToList();
        if (missing.Count > 0)
        {
            return CsvImport<T>.Failed([.. missing.Select(k => Format(Strings.Import_MissingColumn, LocalizedStrings.Instance[k]))]);
        }

        var rows = new List<T>();
        var errors = new List<string>();
        for (var i = 1; i < records.Count; i++)
        {
            // Excel keeps rows that only had their content deleted, as lines of separators.
            if (records[i].All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var row = new CsvImportRow(records[i], i + 1, columns, culture);
            var item = map(row);
            errors.AddRange(row.Errors.Select(e => Format(Strings.Import_RowError, row.Number, e)));
            rows.Add(item);
        }

        return new CsvImport<T>(rows, errors);
    }

    private static string Format(string format, params object[] args) => string.Format(LocalizedStrings.FormattingCulture, format, args);
}