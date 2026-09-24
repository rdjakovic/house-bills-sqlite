using System.Globalization;

using Dapper;

using HouseBills.Application.Reports;
using HouseBills.Infrastructure.Persistence.Configurations;

namespace HouseBills.Infrastructure.Reports;

internal sealed class ReportQueries(ISqlConnectionFactory connectionFactory) : IReportQueries
{
    // Dates are stored as 'yyyy-MM-dd' text, so the range predicates compare as text, stay SARGable and use
    // IX_Bills_DueDate; strftime() is only applied to the filtered rows. Amounts are INTEGER minor units, so the sums
    // are exact.
    private const string MonthlySummarySql = """
        WITH RECURSIVE Months(Month) AS (
            SELECT 1 UNION ALL SELECT Month + 1 FROM Months WHERE Month < 12
        ),
        Totals AS (
            SELECT CAST(strftime('%Y', b.DueDate) AS INTEGER) AS Year,
                   CAST(strftime('%m', b.DueDate) AS INTEGER) AS Month,
                   COUNT(*) AS BillCount,
                   SUM(b.Amount) AS TotalAmount,
                   SUM(CASE WHEN b.PaidOn IS NOT NULL THEN b.PaidAmount ELSE 0 END) AS PaidAmount,
                   SUM(CASE WHEN b.PaidOn IS NULL THEN b.Amount ELSE 0 END) AS OutstandingAmount
            FROM Bills AS b
            WHERE b.DueDate >= @PreviousYearStart AND b.DueDate < @NextYearStart
            GROUP BY 1, 2
        )
        SELECT m.Month,
               COALESCE(cur.BillCount, 0) AS BillCount,
               COALESCE(cur.TotalAmount, 0) AS TotalAmount,
               COALESCE(cur.PaidAmount, 0) AS PaidAmount,
               COALESCE(cur.OutstandingAmount, 0) AS OutstandingAmount,
               COALESCE(prev.TotalAmount, 0) AS PreviousYearTotalAmount
        FROM Months AS m
        LEFT JOIN Totals AS cur ON cur.Month = m.Month AND cur.Year = @Year
        LEFT JOIN Totals AS prev ON prev.Month = m.Month AND prev.Year = @Year - 1
        ORDER BY m.Month;
        """;

    private const string CategoryTotalsSql = """
        SELECT c.Name AS CategoryName,
               COUNT(*) AS BillCount,
               SUM(b.Amount) AS TotalAmount,
               SUM(CASE WHEN b.PaidOn IS NOT NULL THEN b.PaidAmount ELSE 0 END) AS PaidAmount
        FROM Bills AS b
        INNER JOIN Categories AS c ON c.Id = b.CategoryId
        WHERE b.DueDate >= @From AND b.DueDate <= @To
        GROUP BY c.Id, c.Name
        ORDER BY TotalAmount DESC, c.Name;
        """;

    public async Task<IReadOnlyList<MonthlySummaryRow>> GetMonthlySummaryAsync(int year, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, DateOnly.MinValue.Year + 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, DateOnly.MaxValue.Year - 1);

        var parameters = new
        {
            Year = year,
            PreviousYearStart = ToStoredDate(new DateOnly(year - 1, 1, 1)),
            NextYearStart = ToStoredDate(new DateOnly(year + 1, 1, 1)),
        };

        await using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<MonthlyTotals>(
            new CommandDefinition(MonthlySummarySql, parameters, cancellationToken: cancellationToken));
        return rows
            .Select(r => new MonthlySummaryRow(
                (int)r.Month,
                (int)r.BillCount,
                FromMinorUnits(r.TotalAmount),
                FromMinorUnits(r.PaidAmount),
                FromMinorUnits(r.OutstandingAmount),
                FromMinorUnits(r.PreviousYearTotalAmount)))
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryTotalRow>> GetCategoryTotalsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var parameters = new { From = ToStoredDate(from), To = ToStoredDate(to) };

        await using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<CategoryTotals>(
            new CommandDefinition(CategoryTotalsSql, parameters, cancellationToken: cancellationToken));
        return rows
            .Select(r => new CategoryTotalRow(r.CategoryName, (int)r.BillCount, FromMinorUnits(r.TotalAmount), FromMinorUnits(r.PaidAmount)))
            .ToList();
    }

    /// <summary>The format EF Core's SQLite provider stores <see cref="DateOnly"/> values in.</summary>
    private static string ToStoredDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static decimal FromMinorUnits(long minorUnits) => minorUnits / MoneyConversion.MinorUnitsPerUnit;

    // SQLite returns every integer as Int64; these rows match that before converting to the Application DTOs.
    private sealed record MonthlyTotals(long Month, long BillCount, long TotalAmount, long PaidAmount, long OutstandingAmount, long PreviousYearTotalAmount);

    private sealed record CategoryTotals(string CategoryName, long BillCount, long TotalAmount, long PaidAmount);
}