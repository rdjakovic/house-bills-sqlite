using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Domain;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseBills.Infrastructure.Tests;

public sealed class SqliteDatabaseInitializerTests : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HouseBillsTests", Guid.NewGuid().ToString("N"));
    private readonly List<ServiceProvider> _providers = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InitializeAsync_FolderDoesNotExist_CreatesAndMigratesDatabase()
    {
        var databaseFile = Path.Combine(_root, "not", "yet", "created", "HouseBills.db");
        var services = BuildServices($"Data Source={databaseFile}");

        await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(Ct);

        File.Exists(databaseFile).ShouldBeTrue();
        (await services.GetRequiredService<ICategoryRepository>().ListAsync(Ct)).Count.ShouldBe(7);
    }

    [Fact]
    public async Task InitializeAsync_ExistingDatabase_KeepsData()
    {
        var connectionString = $"Data Source={Path.Combine(_root, "HouseBills.db")}";
        var services = BuildServices(connectionString);
        await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(Ct);
        await services.GetRequiredService<IPayeeRepository>().AddAsync(new Payee("Kept payee", null, null), Ct);

        // A fresh container, as on the next app start.
        var restarted = BuildServices(connectionString);
        await restarted.GetRequiredService<IDatabaseInitializer>().InitializeAsync(Ct);

        var payees = await restarted.GetRequiredService<IPayeeRepository>().ListAsync(Ct);
        payees.ShouldHaveSingleItem().Name.ShouldBe("Kept payee");
    }

    [Fact]
    public void AddInfrastructure_EnvironmentVariableInDataSource_ExpandsIt()
    {
        var connectionString = DependencyInjection.GetRequiredConnectionString(Configuration(@"Data Source=%TEMP%\HouseBills\HouseBills.db"));

        new SqliteConnectionStringBuilder(connectionString).DataSource
            .ShouldBe(Path.Combine(Environment.GetEnvironmentVariable("TEMP")!, "HouseBills", "HouseBills.db"));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            await provider.DisposeAsync();
        }

        // Pooled connections keep the files open.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static IConfiguration Configuration(string connectionString)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = connectionString,
            })
            .Build();
    }

    private ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection().AddLogging().AddInfrastructure(Configuration(connectionString)).BuildServiceProvider();
        _providers.Add(services);
        return services;
    }
}