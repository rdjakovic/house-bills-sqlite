using HouseBills.Application;
using HouseBills.Application.Common;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

namespace HouseBills.Infrastructure.Tests;

/// <summary>
/// Creates a SQLite database file in a temporary folder with the app's own initializer (real migrations), and wires
/// Application + Infrastructure exactly as the app does (except for a fixed clock). Shared by all tests in
/// <see cref="SqliteCollection"/>; tests use unique names/dates so they don't interfere with each other.
/// </summary>
public sealed class SqliteFixture : IAsyncLifetime
{
    public static readonly DateOnly Today = new(2026, 9, 24);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "HouseBillsTests", Guid.NewGuid().ToString("N"));
    private ServiceProvider? _services;

    public IServiceProvider Services => _services ?? throw new InvalidOperationException("The fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = $"Data Source={Path.Combine(_folder, "HouseBills.db")}",
            })
            .Build();

        var clock = Substitute.For<IClock>();
        clock.Today.Returns(Today);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.Replace(ServiceDescriptor.Singleton(clock));
        _services = services.BuildServiceProvider();

        await Get<IDatabaseInitializer>().InitializeAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        // Pooled connections keep the file open.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();
}