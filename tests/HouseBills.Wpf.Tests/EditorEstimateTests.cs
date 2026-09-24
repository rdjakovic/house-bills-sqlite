using HouseBills.Application.Bills;
using HouseBills.Application.RecurringBills;
using HouseBills.Domain;
using HouseBills.Wpf.ViewModels.Bills;
using HouseBills.Wpf.ViewModels.RecurringBills;

namespace HouseBills.Wpf.Tests;

public sealed class EditorEstimateTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    [Fact]
    public void BillEditor_EstimatedBill_StartsCheckedAndSendsWhatTheUserChose()
    {
        var bill = new BillListItem(1, "Electricity", 1, "EPS", 1, "Utilities", 80m, Today, null, null, null, 3, [1]) { IsEstimated = true };
        var editor = new BillEditorViewModel(bill, Today, [], []);

        editor.IsEstimated.ShouldBeTrue();
        editor.IsEstimated = false;
        editor.Amount = 93.10m;

        editor.ToRequest().ShouldSatisfyAllConditions(r => r.IsEstimated.ShouldBeFalse(), r => r.Amount.ShouldBe(93.10m));
    }

    [Fact]
    public void RecurringEditor_AmountVaries_IsLoadedAndSent()
    {
        var template = new RecurringBillDto(1, "Electricity", 1, "EPS", 1, "Utilities", 80m, BillFrequency.Monthly, Today, null, null, true, null, [1])
        {
            AmountVaries = true,
        };
        var editor = new RecurringBillEditorViewModel(template, Today, [], []);

        editor.AmountVaries.ShouldBeTrue();
        editor.ToRequest().AmountVaries.ShouldBeTrue();
    }
}