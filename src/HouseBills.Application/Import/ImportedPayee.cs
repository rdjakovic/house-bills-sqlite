namespace HouseBills.Application.Import;

/// <summary>A payee read from an import file.</summary>
/// <param name="Row">Row number in the file (the header is row 1), for error messages.</param>
public sealed record ImportedPayee(int Row, string Name, string? AccountReference, string? Notes);