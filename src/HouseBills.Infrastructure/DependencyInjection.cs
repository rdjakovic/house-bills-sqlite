using HouseBills.Application.Backups;
using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Application.Preferences;
using HouseBills.Application.Reports;
using HouseBills.Infrastructure.Backups;
using HouseBills.Infrastructure.Persistence;
using HouseBills.Infrastructure.Persistence.Repositories;
using HouseBills.Infrastructure.Preferences;
using HouseBills.Infrastructure.Reports;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseBills.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "HouseBills";

    /// <summary>Registers local SQLite persistence (EF Core + Dapper) for the Application interfaces.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetRequiredConnectionString(configuration);

        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<ISqlConnectionFactory>(new SqliteConnectionFactory(connectionString));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IPayeeRepository, PayeeRepository>();
        services.AddSingleton<IRecurringBillRepository, RecurringBillRepository>();
        services.AddSingleton<IBillRepository, BillRepository>();
        services.AddSingleton<IImportRepository, ImportRepository>();
        services.AddSingleton<IReportQueries, ReportQueries>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddSingleton<IDatabaseBackup, SqliteDatabaseBackup>();
        services.AddOptions<BackupOptions>()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Folder) && o.AutomaticBackupsToKeep is >= 1 and <= BackupOptions.MaxAutomaticBackupsToKeep,
                $"{BackupOptions.SectionName}:Folder must be set and {BackupOptions.SectionName}:AutomaticBackupsToKeep must be between 1 and {BackupOptions.MaxAutomaticBackupsToKeep}.")
            .ValidateOnStart();

        services.AddOptions<UserPreferencesOptions>();
        services.AddSingleton<IUserPreferencesStore, JsonUserPreferencesStore>();

        return services;
    }

    /// <summary>
    /// Reads the SQLite connection string and expands environment variables such as <c>%LOCALAPPDATA%</c> in its
    /// data source (SQLite itself doesn't).
    /// </summary>
    internal static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
        }

        var builder = new SqliteConnectionStringBuilder(configured);
        builder.DataSource = Environment.ExpandEnvironmentVariables(builder.DataSource);
        return builder.ConnectionString;
    }
}