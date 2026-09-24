using Microsoft.Data.Sqlite;

namespace HouseBills.Infrastructure.Reports;

internal sealed class SqliteConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public SqliteConnection Create() => new(connectionString);
}