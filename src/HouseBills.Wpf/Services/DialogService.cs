using System.Windows;

using HouseBills.Presentation.Resources;

using Microsoft.Win32;

namespace HouseBills.Wpf.Services;

internal sealed class DialogService : IDialogService
{
    private const string Caption = "HouseBills";

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public void ShowInfo(string message)
    {
        MessageBox.Show(Owner, message, Caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message)
    {
        MessageBox.Show(Owner, message, Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public string? PickBackupSaveLocation(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".db",
            Filter = BackupFilter,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickCsvSaveLocation(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            DefaultExt = ".csv",
            Filter = $"{Strings.Export_FileType} (*.csv)|*.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickBackupToOpen(string initialFolder)
    {
        var dialog = new OpenFileDialog
        {
            Filter = BackupFilter,
            InitialDirectory = System.IO.Directory.Exists(initialFolder) ? initialFolder : null,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    private static string BackupFilter => $"{Strings.Backup_FileType} (*.db)|*.db|{Strings.Backup_AllFiles} (*.*)|*.*";

    private static Window Owner => System.Windows.Application.Current.MainWindow;
}