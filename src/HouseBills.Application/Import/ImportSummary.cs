namespace HouseBills.Application.Import;

/// <summary>What an import did.</summary>
/// <param name="Added">Rows added.</param>
/// <param name="Skipped">Rows already in the database (or repeated in the file), left out.</param>
/// <param name="PayeesCreated">Payees created because bills named them (bill import only).</param>
/// <param name="CategoriesCreated">Categories created because bills named them (bill import only).</param>
public sealed record ImportSummary(int Added, int Skipped, int PayeesCreated = 0, int CategoriesCreated = 0);