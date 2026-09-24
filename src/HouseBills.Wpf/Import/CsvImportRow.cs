using System.Globalization;

using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Localization;

namespace HouseBills.Wpf.Import;

/// <summary>
/// One data row of an import file. Reads cells by column (resource key of the header) and collects conversion problems
/// in <see cref="Errors"/> instead of throwing, so every problem in the file can be reported at once.
/// </summary>
internal sealed class CsvImportRow(IReadOnlyList<string> cells, int number, IReadOnlyDictionary<string, int> columns, CultureInfo culture)
{
    // Only a decimal separator and sign: with thousands separators allowed, "4230.50" would read as 423050 under
    // Serbian settings instead of being reported.
    private const NumberStyles AmountStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    private static readonly string[] YesWords =
        [.. new[] { LocalizationService.English, LocalizationService.Serbian }
            .Select(l => Strings.ResourceManager.GetString(nameof(Strings.Common_Yes), CultureInfo.GetCultureInfo(l.CultureName)))
            .OfType<string>()];

    /// <summary>Row number as Excel shows it (the header is row 1).</summary>
    public int Number { get; } = number;

    public List<string> Errors { get; } = [];

    /// <summary>The trimmed cell, or <c>null</c> if the column is missing or the cell is empty.</summary>
    public string? Text(string column)
    {
        if (!columns.TryGetValue(column, out var index) || index >= cells.Count)
        {
            return null;
        }

        var text = cells[index].Trim();
        return text.Length == 0 ? null : text;
    }

    public decimal? Amount(string column)
    {
        if (Text(column) is not { } text)
        {
            return null;
        }

        if (decimal.TryParse(text, AmountStyle, culture, out var amount))
        {
            return amount;
        }

        Errors.Add(Format(Strings.Import_InvalidAmount, Header(column), text));
        return null;
    }

    /// <summary>Like <see cref="Amount"/>, but an empty cell is an error; returns 0 then (the row is not imported).</summary>
    public decimal RequiredAmount(string column) => Required(column, Amount) ?? 0m;

    public DateOnly? Date(string column)
    {
        if (Text(column) is not { } text)
        {
            return null;
        }

        if (DateOnly.TryParse(text, culture, DateTimeStyles.None, out var date)
            || DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        Errors.Add(Format(Strings.Import_InvalidDate, Header(column), text));
        return null;
    }

    /// <summary>Like <see cref="Date"/>, but an empty cell is an error; returns a placeholder then (the row is not imported).</summary>
    public DateOnly RequiredDate(string column) => Required(column, Date) ?? DateOnly.MinValue;

    /// <summary><c>true</c> for "Yes" in either language, <c>false</c> for an empty cell; anything else is an error.</summary>
    public bool Yes(string column)
    {
        if (Text(column) is not { } text)
        {
            return false;
        }

        if (YesWords.Contains(text, StringComparer.CurrentCultureIgnoreCase))
        {
            return true;
        }

        Errors.Add(Format(Strings.Import_InvalidYes, Header(column), text, Strings.Common_Yes));
        return false;
    }

    private T? Required<T>(string column, Func<string, T?> read)
        where T : struct
    {
        if (Text(column) is null)
        {
            Errors.Add(Format(Strings.Import_Required, Header(column)));
            return null;
        }

        return read(column);
    }

    private static string Header(string column) => LocalizedStrings.Instance[column];

    private static string Format(string format, params object[] args) => string.Format(LocalizedStrings.FormattingCulture, format, args);
}