namespace HouseBills.Wpf.Platform;

/// <summary>Whether HouseBills runs its reminder check when the user signs in to Windows.</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    /// <summary>Runs the reminder check at sign-in (for the current Windows user only).</summary>
    void Enable();

    void Disable();

    /// <summary>If enabled, points the entry at the current program file (e.g. after reinstalling elsewhere).</summary>
    void RefreshIfEnabled();
}