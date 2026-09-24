namespace HouseBills.Wpf.Import;

/// <summary>Items read from an import file, or the problems that prevent importing it (with row numbers).</summary>
public sealed record CsvImport<T>(IReadOnlyList<T> Rows, IReadOnlyList<string> Errors)
{
    public static CsvImport<T> Failed(params IReadOnlyList<string> errors) => new([], errors);
}