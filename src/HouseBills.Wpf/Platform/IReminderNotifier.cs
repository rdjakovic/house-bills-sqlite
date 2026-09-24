namespace HouseBills.Wpf.Platform;

/// <summary>Shows a notification outside the app window.</summary>
public interface IReminderNotifier
{
    /// <summary>Shows a notification; clicking it opens HouseBills.</summary>
    void Show(string title, IReadOnlyList<string> lines);
}