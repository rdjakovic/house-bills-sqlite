using HouseBills.Application.Backups;
using HouseBills.Application.Common;
using HouseBills.Application.Persistence;
using HouseBills.Domain;
using HouseBills.Infrastructure.Backups;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

namespace HouseBills.Infrastructure.Tests;

public sealed class SqliteDatabaseBackupTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "HouseBillsTests", Guid.NewGuid().ToString("N"));
    private readonly IClock _clock = Substitute.For<IClock>();
    private ServiceProvider _services = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string BackupFolder => Path.Combine(_root, "Backups");

    private IDatabaseBackup Backup => _services.GetRequiredService<IDatabaseBackup>();

    private IPayeeRepository Payees => _services.GetRequiredService<IPayeeRepository>();

    public async ValueTask InitializeAsync()
    {
        _clock.Today.Returns(Today);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = $"Data Source={Path.Combine(_root, "Data", "HouseBills.db")}",
            })
            .Build();

        var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration);
        services.Configure<BackupOptions>(o =>
        {
            o.Folder = BackupFolder;
            o.AutomaticBackupsToKeep = 3;
        });
        services.Replace(ServiceDescriptor.Singleton(_clock));
        _services = services.BuildServiceProvider();

        await _services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreAsync_BackupMadeEarlier_BringsBackThatDataAndKeepsSafetyCopy()
    {
        await Payees.AddAsync(new Payee("Before backup", null, null), Ct);
        var backupFile = Path.Combine(_root, "Manual", "HouseBills-2026-09-24.db");
        await Backup.BackupToAsync(backupFile, Ct);
        await Payees.AddAsync(new Payee("After backup", null, null), Ct);

        var result = await Backup.RestoreAsync(backupFile, Ct);

        result.IsSuccess.ShouldBeTrue();
        (await Payees.ListAsync(Ct)).Select(p => p.Name).ShouldBe(["Before backup"]);
        var safetyCopy = Directory.GetFiles(BackupFolder, "HouseBills-before-restore-*.db").ShouldHaveSingleItem();
        (await PayeeNamesInAsync(safetyCopy)).ShouldBe(["After backup", "Before backup"], ignoreOrder: true);
    }

    [Fact]
    public async Task RestoreAsync_PlainCopyOfClosedDatabaseFile_Restores()
    {
        // What the README suggests for moving data: copy HouseBills.db while the app is closed.
        await Payees.AddAsync(new Payee("Copied by hand", null, null), Ct);
        SqliteConnection.ClearAllPools();
        var copy = Path.Combine(_root, "copy.db");
        File.Copy(Path.Combine(_root, "Data", "HouseBills.db"), copy);
        await Payees.AddAsync(new Payee("Later", null, null), Ct);

        var result = await Backup.RestoreAsync(copy, Ct);

        result.IsSuccess.ShouldBeTrue();
        (await Payees.ListAsync(Ct)).Select(p => p.Name).ShouldBe(["Copied by hand"]);
    }

    [Fact]
    public async Task RestoreAsync_NotADatabase_FailsAndKeepsData()
    {
        await Payees.AddAsync(new Payee("Kept", null, null), Ct);
        var file = Path.Combine(_root, "notes.db");
        await File.WriteAllTextAsync(file, "not a database, just text that is long enough to not be empty", Ct);

        var result = await Backup.RestoreAsync(file, Ct);

        result.Error.ShouldNotBeNull().Kind.ShouldBe(ErrorKind.Validation);
        (await Payees.ListAsync(Ct)).Select(p => p.Name).ShouldBe(["Kept"]);
    }

    [Fact]
    public async Task RestoreAsync_OtherSqliteDatabase_FailsAsNotABackup()
    {
        var file = Path.Combine(_root, "other.db");
        await using (var other = new SqliteConnection($"Data Source={file};Pooling=False"))
        {
            await other.OpenAsync(Ct);
            await using var command = other.CreateCommand();
            command.CommandText = "CREATE TABLE Things (Id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync(Ct);
        }

        var result = await Backup.RestoreAsync(file, Ct);

        result.Error.ShouldNotBeNull().Message.ShouldBe(HouseBills.Application.Resources.Messages.Backup_NotABackup);
    }

    [Fact]
    public async Task RestoreAsync_BackupFromNewerVersion_FailsAndKeepsData()
    {
        await Payees.AddAsync(new Payee("Kept", null, null), Ct);
        var backupFile = Path.Combine(_root, "newer.db");
        await Backup.BackupToAsync(backupFile, Ct);
        await using (var newer = new SqliteConnection($"Data Source={backupFile};Pooling=False"))
        {
            await newer.OpenAsync(Ct);
            await using var command = newer.CreateCommand();
            command.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_FromTheFuture', '10.0.12');";
            await command.ExecuteNonQueryAsync(Ct);
        }

        await Payees.AddAsync(new Payee("Added later", null, null), Ct);

        var result = await Backup.RestoreAsync(backupFile, Ct);

        result.Error.ShouldNotBeNull().Message.ShouldBe(HouseBills.Application.Resources.Messages.Backup_FromNewerVersion);
        (await Payees.ListAsync(Ct)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateAutomaticBackupAsync_CalledTwiceOnSameDay_CreatesOneBackup()
    {
        (await Backup.CreateAutomaticBackupAsync(Ct)).ShouldBeTrue();
        (await Backup.CreateAutomaticBackupAsync(Ct)).ShouldBeFalse();

        Directory.GetFiles(BackupFolder).Select(Path.GetFileName).ShouldBe(["HouseBills-auto-2026-09-24.db"]);
    }

    [Fact]
    public async Task CreateAutomaticBackupAsync_MoreThanKept_DeletesOldestAutomaticOnly()
    {
        Directory.CreateDirectory(BackupFolder);
        foreach (var name in new[] { "HouseBills-auto-2026-09-20.db", "HouseBills-auto-2026-09-21.db", "HouseBills-auto-2026-09-23.db", "HouseBills-before-restore-20260101-120000.db", "my-backup.db" })
        {
            await File.WriteAllTextAsync(Path.Combine(BackupFolder, name), "x", Ct);
        }

        await Backup.CreateAutomaticBackupAsync(Ct);

        Directory.GetFiles(BackupFolder).Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe([
            "HouseBills-auto-2026-09-21.db",
            "HouseBills-auto-2026-09-23.db",
            "HouseBills-auto-2026-09-24.db",
            "HouseBills-before-restore-20260101-120000.db",
            "my-backup.db",
        ]);
    }

    [Fact]
    public async Task BackupToAsync_ExistingFile_IsReplacedByAValidBackup()
    {
        await Payees.AddAsync(new Payee("In backup", null, null), Ct);
        var backupFile = Path.Combine(_root, "existing.db");
        await File.WriteAllTextAsync(backupFile, "old content", Ct);

        await Backup.BackupToAsync(backupFile, Ct);

        (await PayeeNamesInAsync(backupFile)).ShouldBe(["In backup"]);
        File.Exists(backupFile + ".tmp").ShouldBeFalse();
    }

    private static async Task<List<string>> PayeeNamesInAsync(string databaseFile)
    {
        await using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Payees;";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}