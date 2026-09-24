using HouseBills.Application.Persistence;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;

namespace HouseBills.Infrastructure.Persistence.Repositories;

internal sealed class RecurringBillRepository(IDbContextFactory<AppDbContext> contextFactory)
    : EfRepository<RecurringBill>(contextFactory), IRecurringBillRepository
{
    public async Task<IReadOnlyList<RecurringBillDto>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from r in db.RecurringBills.AsNoTracking()
                join p in db.Payees on r.PayeeId equals p.Id
                join c in db.Categories on r.CategoryId equals c.Id
                orderby r.Name
                select new RecurringBillDto(
                    r.Id,
                    r.Name,
                    r.PayeeId,
                    p.Name,
                    r.CategoryId,
                    c.Name,
                    r.Amount,
                    r.Frequency,
                    r.StartDate,
                    r.EndDate,
                    r.Notes,
                    r.IsActive,
                    r.GeneratedThrough,
                    r.RowVersion)
                {
                    AmountVaries = r.AmountVaries,
                })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetLastActualAmountsAsync(CancellationToken cancellationToken)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);

        // An actual (non-estimated) bill with no later actual bill of the same template: a correlated NOT EXISTS that
        // SQLite can run (a "first per group" query would need APPLY) and that uses IX_Bills_RecurringBillId_DueDate.
        // That unique index also guarantees one bill per template and date, so each template appears once.
        var latest = await (
                from b in db.Bills.AsNoTracking()
                where b.RecurringBillId != null
                      && !b.IsEstimated
                      && !db.Bills.Any(later => later.RecurringBillId == b.RecurringBillId && !later.IsEstimated && later.DueDate > b.DueDate)
                select new { TemplateId = b.RecurringBillId!.Value, b.Amount })
            .ToListAsync(cancellationToken);
        return latest.ToDictionary(l => l.TemplateId, l => l.Amount);
    }

    public async Task<IReadOnlyList<RecurringBill>> ListActiveAsync(CancellationToken cancellationToken)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.RecurringBills.AsNoTracking().Where(r => r.IsActive).ToListAsync(cancellationToken);
    }

    public async Task SaveGeneratedBillsAsync(RecurringBill template, byte[] expectedRowVersion, IReadOnlyList<Bill> bills, CancellationToken cancellationToken)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        AttachModified(db, template, expectedRowVersion);
        db.Bills.AddRange(bills);

        // SaveChanges wraps the template update and the inserts in one transaction.
        await SaveChangesAsync(db, cancellationToken);
    }
}