using HouseBills.Application.Bills;
using HouseBills.Application.Persistence;
using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;

namespace HouseBills.Infrastructure.Persistence.Repositories;

internal sealed class BillRepository(IDbContextFactory<AppDbContext> contextFactory)
    : EfRepository<Bill>(contextFactory), IBillRepository
{
    private const string LikeEscape = "\\";

    public async Task<IReadOnlyList<BillListItem>> ListAsync(BillFilter filter, DateOnly today, CancellationToken cancellationToken)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var bills = db.Bills.AsNoTracking();

        if (filter.DueFrom is { } from)
        {
            bills = bills.Where(b => b.DueDate >= from);
        }

        if (filter.DueTo is { } to)
        {
            bills = bills.Where(b => b.DueDate <= to);
        }

        if (filter.CategoryId is { } categoryId)
        {
            bills = bills.Where(b => b.CategoryId == categoryId);
        }

        if (filter.PayeeId is { } payeeId)
        {
            bills = bills.Where(b => b.PayeeId == payeeId);
        }

        bills = filter.Status switch
        {
            BillStatusFilter.Unpaid => bills.Where(b => b.PaidOn == null),
            BillStatusFilter.Overdue => bills.Where(b => b.PaidOn == null && b.DueDate < today),
            BillStatusFilter.Paid => bills.Where(b => b.PaidOn != null),
            _ => bills,
        };

        var rows =
            from b in bills
            join p in db.Payees on b.PayeeId equals p.Id
            join c in db.Categories on b.CategoryId equals c.Id
            select new { Bill = b, PayeeName = p.Name, CategoryName = c.Name };

        // SQLite's LIKE ignores case (for A-Z). A leading wildcard can't use an index, which is fine for a household's
        // number of bills; the other filters above narrow the rows first.
        if (ToContainsPattern(filter.Search) is { } pattern)
        {
            rows = rows.Where(r =>
                EF.Functions.Like(r.Bill.Description, pattern, LikeEscape)
                || EF.Functions.Like(r.PayeeName, pattern, LikeEscape)
                || EF.Functions.Like(r.CategoryName, pattern, LikeEscape)
                || (r.Bill.Notes != null && EF.Functions.Like(r.Bill.Notes, pattern, LikeEscape)));
        }

        return await rows
            .OrderBy(r => r.Bill.DueDate)
            .ThenBy(r => r.Bill.Description)
            .Select(r => new BillListItem(
                r.Bill.Id,
                r.Bill.Description,
                r.Bill.PayeeId,
                r.PayeeName,
                r.Bill.CategoryId,
                r.CategoryName,
                r.Bill.Amount,
                r.Bill.DueDate,
                r.Bill.PaidOn,
                r.Bill.PaidAmount,
                r.Bill.Notes,
                r.Bill.RecurringBillId,
                r.Bill.RowVersion)
            {
                IsEstimated = r.Bill.IsEstimated,
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>"%text%" with LIKE's wildcards in the text escaped, or <c>null</c> for blank text.</summary>
    private static string? ToContainsPattern(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Trim()
            .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
            .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscape + "_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}