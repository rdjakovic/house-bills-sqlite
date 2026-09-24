
using CommunityToolkit.Mvvm.ComponentModel;

using HouseBills.Application.Common;
using HouseBills.Application.Import;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Import;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.ViewModels;

/// <summary>Base for pages shown in the main window.</summary>
public abstract partial class PageViewModel(IDialogService dialogs, ILogger logger) : ObservableObject
{
    public abstract string Title { get; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    protected IDialogService Dialogs { get; } = dialogs;

    /// <summary>Called each time the page is shown; reloads data so changes made on other pages are visible.</summary>
    public abstract Task OnNavigatedToAsync();

    /// <summary>
    /// Runs I/O while showing the busy state. Unexpected failures are logged and reported with a friendly message.
    /// Returns <c>false</c> if the action failed.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> action, string failureMessage)
    {
        IsBusy = true;
        try
        {
            await action();
            return true;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Operation cancelled: {Operation}", failureMessage);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Operation failed: {Operation}", failureMessage);
            Dialogs.ShowError(string.Format(LocalizedStrings.FormattingCulture, Strings.Error_CheckDatabase, failureMessage));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Validates and submits an editor. Business errors are shown in the editor; after a successful save or a
    /// conflict the list is reloaded. Returns <c>true</c> on success.
    /// </summary>
    protected async Task<bool> SubmitAsync(EditorViewModel editor, Func<Task<Result>> submit, Func<Task> reload)
    {
        editor.ErrorMessage = null;
        if (!editor.Validate())
        {
            return false;
        }

        Result? result = null;
        var completed = await RunAsync(
            async () =>
            {
                result = await submit();
                if (result.Error?.Kind is not ErrorKind.Validation)
                {
                    await reload();
                }
            },
            Strings.Error_SaveFailed);

        if (!completed || result is null)
        {
            return false;
        }

        editor.ErrorMessage = result.Error?.Message;
        return result.IsSuccess;
    }

    /// <summary>
    /// Asks where to save, writes the CSV built by <paramref name="buildCsv"/> and reports the outcome. A failure (e.g.
    /// the file is open in Excel) is logged and shown as a friendly message.
    /// </summary>
    protected async Task ExportCsvAsync(IFileSaver files, string suggestedFileName, Func<string> buildCsv, CancellationToken cancellationToken)
    {
        if (Dialogs.PickCsvSaveLocation(suggestedFileName) is not { } path)
        {
            return;
        }

        try
        {
            await files.SaveTextAsync(path, buildCsv(), cancellationToken);
            Dialogs.ShowInfo(string.Format(LocalizedStrings.FormattingCulture, Strings.Export_Done, path));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Export failed.");
            Dialogs.ShowError(Strings.Export_Failed);
        }
    }

    /// <summary>
    /// Asks for a CSV file, reads it with <paramref name="parse"/>, imports the rows and reloads the page. Problems in
    /// the file (or rows the import rejects) are listed and nothing is imported.
    /// </summary>
    protected async Task ImportCsvAsync<T>(
        IFileReader files,
        Func<string, CsvImport<T>> parse,
        Func<IReadOnlyList<T>, Task<Result<ImportSummary>>> import,
        Func<Task> reload,
        CancellationToken cancellationToken)
    {
        if (Dialogs.PickCsvToOpen() is not { } path)
        {
            return;
        }

        string text;
        try
        {
            text = await files.ReadTextAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Import could not read the file.");
            Dialogs.ShowError(Strings.Import_ReadFailed);
            return;
        }

        var parsed = parse(text);
        if (parsed.Errors.Count > 0)
        {
            ShowImportErrors(parsed.Errors);
            return;
        }

        Result<ImportSummary>? result = null;
        var completed = await RunAsync(
            async () =>
            {
                result = await import(parsed.Rows);
                if (result.IsSuccess)
                {
                    await reload();
                }
            },
            Strings.Import_Failed);
        if (!completed || result is null)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowImportErrors(result.Error!.Message.Split(Environment.NewLine));
            return;
        }

        var summary = result.Value;
        var culture = LocalizedStrings.FormattingCulture;
        var message = string.Format(culture, Strings.Import_Done, summary.Added, summary.Skipped);
        if (summary.PayeesCreated > 0 || summary.CategoriesCreated > 0)
        {
            message += Environment.NewLine + string.Format(culture, Strings.Import_Created, summary.PayeesCreated, summary.CategoriesCreated);
        }

        Dialogs.ShowInfo(message);
    }

    /// <summary>Lists the first problems (a long list would not fit on screen).</summary>
    private void ShowImportErrors(IReadOnlyList<string> errors)
    {
        const int Shown = 15;
        var lines = new List<string> { Strings.Import_NothingImported, string.Empty };
        lines.AddRange(errors.Take(Shown));
        if (errors.Count > Shown)
        {
            lines.Add(string.Format(LocalizedStrings.FormattingCulture, Strings.Import_MoreErrors, errors.Count - Shown));
        }

        Dialogs.ShowError(string.Join(Environment.NewLine, lines));
    }

    /// <summary>Runs a list action (delete, mark paid...), shows any business error, then reloads.</summary>
    protected Task<bool> ExecuteAndReloadAsync(Func<Task<Result>> action, Func<Task> reload, string failureMessage)
    {
        return RunAsync(
            async () =>
            {
                var result = await action();
                if (!result.IsSuccess)
                {
                    Dialogs.ShowError(result.Error!.Message);
                }

                await reload();
            },
            failureMessage);
    }
}