namespace HouseBills.Wpf.ViewModels.Reports;

/// <summary>A choice in the report's recurring bill filter.</summary>
/// <param name="RecurringBillId">The recurring bill to report on, or <c>null</c> for all bills.</param>
/// <param name="Name">Shown in the list.</param>
public sealed record RecurringBillOption(int? RecurringBillId, string Name)
{
    /// <summary>The name, which is also what screen readers announce for the list item.</summary>
    public override string ToString() => Name;
}