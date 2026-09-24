using HouseBills.Application.Common;
using HouseBills.Application.Import;
using HouseBills.Application.Payees;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HouseBills.Wpf.Tests;

public sealed class PayeesViewModelTests
{
    private const string Path = @"C:\Users\me\Documents\payees.csv";

    private readonly IPayeeService _payees = Substitute.For<IPayeeService>();
    private readonly IImportService _imports = Substitute.For<IImportService>();
    private readonly IFileReader _reader = Substitute.For<IFileReader>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly PayeesViewModel _viewModel;

    public PayeesViewModelTests()
    {
        _payees.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        _dialogs.PickCsvToOpen().Returns(Path);
        _viewModel = new PayeesViewModel(_payees, _imports, Substitute.For<IFileSaver>(), _reader, _dialogs, NullLogger<PayeesViewModel>.Instance);
    }

    [Fact]
    public async Task Import_ValidFile_ImportsRowsReloadsAndShowsSummary()
    {
        _reader.ReadTextAsync(Path, Arg.Any<CancellationToken>()).Returns("Name\r\nEPS\r\nInfostan\r\n");
        _imports.ImportPayeesAsync(Arg.Any<IReadOnlyList<ImportedPayee>>(), Arg.Any<CancellationToken>()).Returns(new ImportSummary(1, 1));

        await _viewModel.ImportCommand.ExecuteAsync(null);

        await _imports.Received(1).ImportPayeesAsync(
            Arg.Is<IReadOnlyList<ImportedPayee>>(rows => rows.Select(r => r.Name).SequenceEqual(new[] { "EPS", "Infostan" })),
            Arg.Any<CancellationToken>());
        await _payees.Received(1).ListAsync(Arg.Any<CancellationToken>());
        _dialogs.Received(1).ShowInfo("Added: 1. Skipped (already in the list): 1.");
    }

    [Fact]
    public async Task Import_FileWithoutNameColumn_ListsProblemAndImportsNothing()
    {
        _reader.ReadTextAsync(Path, Arg.Any<CancellationToken>()).Returns("Company\r\nEPS\r\n");

        await _viewModel.ImportCommand.ExecuteAsync(null);

        await _imports.DidNotReceive().ImportPayeesAsync(Arg.Any<IReadOnlyList<ImportedPayee>>(), Arg.Any<CancellationToken>());
        _dialogs.Received(1).ShowError(Arg.Is<string>(m => m.StartsWith("Nothing was imported.", StringComparison.Ordinal) && m.Contains("“Name”", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Import_RowsRejectedByService_ListsFirstProblemsOnly()
    {
        _reader.ReadTextAsync(Path, Arg.Any<CancellationToken>()).Returns("Name\r\nEPS\r\n");
        var errors = Enumerable.Range(2, 20).Select(r => $"Row {r}: Name is required.");
        _imports.ImportPayeesAsync(Arg.Any<IReadOnlyList<ImportedPayee>>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation(string.Join(Environment.NewLine, errors)));

        await _viewModel.ImportCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowError(Arg.Is<string>(m => m.Contains("Row 16:", StringComparison.Ordinal) && !m.Contains("Row 17:", StringComparison.Ordinal) && m.EndsWith("…and 5 more.", StringComparison.Ordinal)));
        await _payees.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_FileLocked_ShowsFriendlyMessage()
    {
        _reader.ReadTextAsync(Path, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("in use"));

        await _viewModel.ImportCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowError("The file could not be read. If it is open in Excel, close it and try again.");
    }

    [Fact]
    public async Task Import_Cancelled_DoesNothing()
    {
        _dialogs.PickCsvToOpen().Returns((string?)null);

        await _viewModel.ImportCommand.ExecuteAsync(null);

        await _reader.DidNotReceive().ReadTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}