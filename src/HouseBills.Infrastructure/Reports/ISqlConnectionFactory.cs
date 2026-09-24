using Microsoft.Data.Sqlite;

namespace HouseBills.Infrastructure.Reports;

/// <summary>Creates (unopened) connections for Dapper queries.</summary>
internal interface ISqlConnectionFactory
{
    SqliteConnection Create();
}