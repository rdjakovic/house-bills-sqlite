using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HouseBills.Application.Categories;
using HouseBills.Application.Import;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Export;
using HouseBills.Wpf.Import;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels.Categories;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.ViewModels;

public sealed partial class CategoriesViewModel(
    ICategoryService categories,
    IImportService imports,
    IFileSaver files,
    IFileReader fileReader,
    IDialogService dialogs,
    ILogger<CategoriesViewModel> logger)
    : PageViewModel(dialogs, logger)
{
    public override string Title => Strings.Page_Categories;

    public ObservableCollection<CategoryDto> Items { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))]
    public partial CategoryDto? SelectedItem { get; set; }

    [ObservableProperty]
    public partial CategoryEditorViewModel? Editor { get; set; }

    public override Task OnNavigatedToAsync()
    {
        return RunAsync(() => LoadAsync(CancellationToken.None), Strings.Categories_LoadFailed);
    }

    [RelayCommand]
    private void New()
    {
        Editor = new CategoryEditorViewModel(null);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        Editor = new CategoryEditorViewModel(SelectedItem);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (Editor is not { } editor)
        {
            return;
        }

        if (await SubmitAsync(editor, async () => await categories.SaveAsync(editor.ToRequest(), cancellationToken), () => LoadAsync(cancellationToken)))
        {
            Editor = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Editor = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedItem is not { } item || !Dialogs.Confirm(Strings.Categories_DeleteTitle, string.Format(LocalizedStrings.FormattingCulture, Strings.Common_DeleteConfirm, item.Name)))
        {
            return;
        }

        Editor = null;
        await ExecuteAndReloadAsync(() => categories.DeleteAsync(item.Id, item.RowVersion, cancellationToken), () => LoadAsync(cancellationToken), Strings.Categories_DeleteFailed);
    }

    /// <summary>Exports the list to a CSV file.</summary>
    [RelayCommand]
    private Task ExportAsync(CancellationToken cancellationToken) =>
        ExportCsvAsync(files, $"HouseBills-{Title}.csv", () => CsvExports.Categories(Items), cancellationToken);

    /// <summary>Adds the rows of a CSV file (e.g. an edited export); names already in the list are skipped.</summary>
    [RelayCommand]
    private Task ImportAsync(CancellationToken cancellationToken) =>
        ImportCsvAsync(fileReader, CsvImports.Categories, rows => imports.ImportCategoriesAsync(rows, cancellationToken), () => LoadAsync(cancellationToken), cancellationToken);

    private bool HasSelection() => SelectedItem is not null;

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var items = await categories.ListAsync(cancellationToken);
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }
}