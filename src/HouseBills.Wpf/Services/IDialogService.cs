namespace HouseBills.Wpf.Services;

/// <summary>User-facing message boxes, abstracted so ViewModels stay testable.</summary>
public interface IDialogService
{
    /// <summary>Asks a yes/no question; returns <c>true</c> for yes.</summary>
    bool Confirm(string title, string message);

    void ShowInfo(string message);

    void ShowError(string message);

    /// <summary>Asks where to save a backup; returns the chosen path, or <c>null</c> if cancelled.</summary>
    string? PickBackupSaveLocation(string suggestedFileName);

    /// <summary>Asks for a backup file to open, starting in <paramref name="initialFolder"/>; returns the path, or <c>null</c> if cancelled.</summary>
    string? PickBackupToOpen(string initialFolder);
}