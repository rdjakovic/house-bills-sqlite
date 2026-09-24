using System.Security.Cryptography;

using HouseBills.Domain;

using Microsoft.EntityFrameworkCore;

namespace HouseBills.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const int RowVersionLength = 8;

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Payee> Payees => Set<Payee>();

    public DbSet<RecurringBill> RecurringBills => Set<RecurringBill>();

    public DbSet<Bill> Bills => Set<Bill>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// SQLite has no <c>rowversion</c>, so the app issues a new concurrency token on every insert and update. EF Core
    /// still checks the original token in the UPDATE/DELETE, so a stale edit fails with a concurrency conflict.
    /// </summary>
    private void StampRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(e => e.RowVersion).CurrentValue = RandomNumberGenerator.GetBytes(RowVersionLength);
            }
        }
    }
}