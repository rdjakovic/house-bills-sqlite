namespace HouseBills.Infrastructure.Tests;

[CollectionDefinition(Name)]
public sealed class SqliteCollection : ICollectionFixture<SqliteFixture>
{
    public const string Name = "SQLite";
}