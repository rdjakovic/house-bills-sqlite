using HouseBills.Application.Preferences;
using HouseBills.Wpf.Theming;

using NSubstitute;

namespace HouseBills.Wpf.Tests.Theming;

public sealed class ThemeServiceTests
{
    private readonly IUserPreferencesStore _preferences = Substitute.For<IUserPreferencesStore>();
    private readonly ThemeService _service;

    public ThemeServiceTests()
    {
        _preferences.LoadAsync(Arg.Any<CancellationToken>()).Returns(UserPreferences.Default);
        _service = new ThemeService(_preferences);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Dark", AppTheme.Dark)]
    [InlineData("light", AppTheme.Light)]
    [InlineData("System", AppTheme.System)]
    [InlineData(null, AppTheme.System)]
    [InlineData("Purple", AppTheme.System)]
    [InlineData("7", AppTheme.System)]
    public async Task InitializeAsync_SavedTheme_AppliesItOrFallsBackToSystem(string? saved, AppTheme expected)
    {
        _preferences.LoadAsync(Arg.Any<CancellationToken>()).Returns(new UserPreferences(null, saved));

        await _service.InitializeAsync(Ct);

        _service.Current.ShouldBe(expected);
    }

    [Fact]
    public async Task SetThemeAsync_NewTheme_AppliesAndSavesKeepingOtherPreferences()
    {
        _preferences.LoadAsync(Arg.Any<CancellationToken>()).Returns(new UserPreferences("sr-Latn-RS"));

        await _service.SetThemeAsync(AppTheme.Dark, Ct);

        _service.Current.ShouldBe(AppTheme.Dark);
        await _preferences.Received(1).SaveAsync(new UserPreferences("sr-Latn-RS", "Dark"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetThemeAsync_SameTheme_DoesNotSave()
    {
        await _service.SetThemeAsync(AppTheme.System, Ct);

        await _preferences.DidNotReceiveWithAnyArgs().SaveAsync(default!, Ct);
    }
}