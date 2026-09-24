using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HouseBills.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. Reads the connection string from the startup project's appsettings.json
/// (the tools run with the startup project as working directory) or from environment variables.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(DependencyInjection.GetRequiredConnectionString(configuration))
            .Options;
        return new AppDbContext(options);
    }
}