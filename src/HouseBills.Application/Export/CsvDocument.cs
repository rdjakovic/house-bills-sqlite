using System.Text;

namespace HouseBills.Application.Export;

/// <summary>
/// Builds CSV text (RFC 4180 quoting): a cell is quoted when it contains the separator, a quote or a line break, or
/// starts/ends with a space; quotes inside are doubled. Lines end with CRLF, as Excel expects.
/// </summary>
public sealed class CsvDocument(string separator)
{
    private readonly StringBuilder _text = new();

    /// <summary>Appends one row. <c>null</c> cells are written as empty.</summary>
    public CsvDocument AddRow(params IEnumerable<string?> cells)
    {
        _text.AppendJoin(separator, cells.Select(Escape)).Append("\r\n");
        return this;
    }

    /// <summary>Appends an empty line, e.g. between two tables in one file.</summary>
    public CsvDocument AddBlankLine()
    {
        _text.Append("\r\n");
        return this;
    }

    public override string ToString() => _text.ToString();

    private string Escape(string? cell)
    {
        if (string.IsNullOrEmpty(cell))
        {
            return string.Empty;
        }

        var needsQuotes = cell.Contains(separator, StringComparison.Ordinal)
            || cell.IndexOfAny(['"', '\r', '\n']) >= 0
            || char.IsWhiteSpace(cell[0])
            || char.IsWhiteSpace(cell[^1]);
        return needsQuotes ? $"\"{cell.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : cell;
    }
}