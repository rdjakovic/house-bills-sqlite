using HouseBills.Application.Common;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HouseBills.Infrastructure.Persistence;

/// <summary>
/// Creates and upgrades the SQLite database file at startup. The file lives in the Windows user's own profile, so
/// there is no shared schema to protect (AGENTS.md §6).
/// </summary>
internal sealed class SqliteDatabaseInitializer(IDbContextFactory<AppDbContext> contextFactory, ILogger<SqliteDatabaseInitializer> logger)
    : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // SQLite creates the file on first use, but not the folder it goes in.
        var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        var folder = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        logger.LogInformation("Applying {MigrationCount} migration(s) to the database: {Migrations}", pending.Count, pending);
        await db.Database.MigrateAsync(cancellationToken);
    }
}