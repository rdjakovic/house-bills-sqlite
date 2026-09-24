using HouseBills.Application.Common;

namespace HouseBills.Application.Import;

/// <summary>
/// Adds rows read from a file. Every row is checked first: if any is invalid, nothing is written and the error lists
/// each problem with its row number. Otherwise all rows are written in one transaction. Rows already in the database
/// are skipped, so importing the same file twice adds nothing the second time.
/// </summary>
public interface IImportService
{
    /// <summary>Adds payees; a payee whose name already exists (ignoring case) is skipped.</summary>
    Task<Result<ImportSummary>> ImportPayeesAsync(IReadOnlyList<ImportedPayee> rows, CancellationToken cancellationToken);

    /// <summary>Adds categories; a category whose name already exists (ignoring case) is skipped.</summary>
    Task<Result<ImportSummary>> ImportCategoriesAsync(IReadOnlyList<ImportedCategory> rows, CancellationToken cancellationToken);

    /// <summary>
    /// Adds bills, creating payees and categories that don't exist yet. A bill with the same due date, description,
    /// payee and amount as an existing one (ignoring case) is skipped. Paid bills are added as paid.
    /// </summary>
    Task<Result<ImportSummary>> ImportBillsAsync(IReadOnlyList<ImportedBill> rows, CancellationToken cancellationToken);
}