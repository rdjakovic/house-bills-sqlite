using HouseBills.Application.Preferences;
using HouseBills.Infrastructure.Preferences;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HouseBills.Infrastructure.Tests;

public sealed class JsonUserPreferencesStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "HouseBillsPreferencesTests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "nested", "preferences.json");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsDefault()
    {
        (await CreateStore().LoadAsync(Ct)).ShouldBe(UserPreferences.Default);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTripsAndCreatesFolder()
    {
        var store = CreateStore();

        await store.SaveAsync(new UserPreferences("sr-Latn-RS", "Dark"), Ct);

        File.Exists(FilePath).ShouldBeTrue();
        (await CreateStore().LoadAsync(Ct)).ShouldBe(new UserPreferences("sr-Latn-RS", "Dark"));
    }

    [Fact]
    public async Task LoadAsync_FileFromBeforeThemeSetting_KeepsLanguageAndDefaultsTheme()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, """{ "Language": "sr-Latn-RS" }""", Ct);

        (await CreateStore().LoadAsync(Ct)).ShouldBe(new UserPreferences("sr-Latn-RS", null));
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsDefault()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{ not json", Ct);

        (await CreateStore().LoadAsync(Ct)).ShouldBe(UserPreferences.Default);
    }

    private JsonUserPreferencesStore CreateStore() =>
        new(Options.Create(new UserPreferencesOptions { FilePath = FilePath }), NullLogger<JsonUserPreferencesStore>.Instance);
}