using System.Text;

namespace HouseBills.Application.Export;

/// <summary>Reads CSV text written by <see cref="CsvDocument"/>, Excel or another spreadsheet (RFC 4180 quoting).</summary>
public static class CsvReader
{
    private static readonly string[] CommonSeparators = [";", ",", "\t"];

    /// <summary>
    /// The separator the header line uses: <paramref name="preferred"/> (the regional one) if it appears there, else
    /// the most frequent of ";", "," and tab. A one-column file has none, so it gets <paramref name="preferred"/>.
    /// </summary>
    public static string DetectSeparator(string text, string preferred)
    {
        var header = FirstLineOutsideQuotes(text);
        if (header.Contains(preferred, StringComparison.Ordinal))
        {
            return preferred;
        }

        var (separator, count) = CommonSeparators
            .Select(s => (Separator: s, Count: header.Split(s).Length - 1))
            .MaxBy(c => c.Count);
        return count > 0 ? separator : preferred;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into rows of cells. Quoted cells may contain the separator, doubled quotes and
    /// line breaks. Line breaks may be CRLF or LF; a final line break does not start another row.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string text, string separator)
    {
        ArgumentException.ThrowIfNullOrEmpty(separator);
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i += 2;
                    continue;
                }

                if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(c);
                }

                i++;
                continue;
            }

            if (c == '"')
            {
                quoted = true;
                i++;
            }
            else if (string.CompareOrdinal(text, i, separator, 0, separator.Length) == 0)
            {
                row.Add(cell.ToString());
                cell.Clear();
                i += separator.Length;
            }
            else if (c is '\r' or '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = [];
                i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            }
            else
            {
                cell.Append(c);
                i++;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static string FirstLineOutsideQuotes(string text)
    {
        var quoted = false;
        var line = new StringBuilder();
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && c is '\r' or '\n')
            {
                break;
            }
            else if (!quoted)
            {
                line.Append(c);
            }
        }

        return line.ToString();
    }
}