namespace HouseBills.Application.Preferences;

/// <summary>Per-user application preferences.</summary>
/// <param name="Language">Chosen UI language as a culture name (e.g. "en", "sr-Latn-RS"), or <c>null</c> for the default.</param>
/// <param name="Theme">Chosen color theme ("System", "Light" or "Dark"), or <c>null</c> for the default (same as Windows).</param>
/// <param name="LastReminderOn">The day the last bill reminder was shown, so there is at most one per day.</param>
public sealed record UserPreferences(string? Language, string? Theme = null, DateOnly? LastReminderOn = null)
{
    public static UserPreferences Default { get; } = new((string?)null);
}