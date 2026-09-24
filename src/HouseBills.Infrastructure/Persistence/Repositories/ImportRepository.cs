using HouseBills.Application.Persistence;
using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;

namespace HouseBills.Infrastructure.Persistence.Repositories;

internal sealed class ImportRepository(IDbContextFactory<AppDbContext> contextFactory) : IImportRepository
{
    public async Task AddAsync(IReadOnlyList<Payee> payees, IReadOnlyList<Category> categories, Func<IReadOnlyList<Bill>> createBills, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Payees.AddRange(payees);
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync(cancellationToken);

        db.Bills.AddRange(createBills());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}