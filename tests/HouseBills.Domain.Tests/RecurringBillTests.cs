using HouseBills.Domain;

namespace HouseBills.Domain.Tests;

public sealed class RecurringBillTests
{
    [Fact]
    public void GenerateBills_FirstRun_CreatesBillsFromStartDateAndCopiesTemplate()
    {
        var template = CreateTemplate(new DateOnly(2026, 1, 15));

        var bills = template.GenerateBills(new DateOnly(2026, 3, 20));

        bills.Select(b => b.DueDate).ShouldBe([new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15), new DateOnly(2026, 3, 15)]);
        bills.ShouldAllBe(b => b.Description == "Electricity" && b.Amount == 80m && b.PayeeId == 2 && b.CategoryId == 3 && b.Notes == "Meter 42");
        template.GeneratedThrough.ShouldBe(new DateOnly(2026, 3, 20));
    }

    [Fact]
    public void GenerateBills_SecondRun_ResumesAfterGeneratedThrough()
    {
        var template = CreateTemplate(new DateOnly(2026, 1, 15));
        template.GenerateBills(new DateOnly(2026, 2, 15));

        var bills = template.GenerateBills(new DateOnly(2026, 4, 15));

        bills.Select(b => b.DueDate).ShouldBe([new DateOnly(2026, 3, 15), new DateOnly(2026, 4, 15)]);
    }

    [Fact]
    public void GenerateBills_AlreadyGeneratedThroughDate_ReturnsNothing()
    {
        var template = CreateTemplate(new DateOnly(2026, 1, 15));
        template.GenerateBills(new DateOnly(2026, 4, 15));

        template.GenerateBills(new DateOnly(2026, 4, 1)).ShouldBeEmpty();
        template.GeneratedThrough.ShouldBe(new DateOnly(2026, 4, 15));
    }

    [Fact]
    public void GenerateBills_Inactive_ReturnsNothing()
    {
        var template = CreateTemplate(new DateOnly(2026, 1, 15));
        template.SetActive(false, new DateOnly(2026, 1, 1));

        template.GenerateBills(new DateOnly(2026, 6, 1)).ShouldBeEmpty();
    }

    [Fact]
    public void SetActive_ResumeAfterPause_DoesNotBackFillPausedPeriod()
    {
        var template = CreateTemplate(new DateOnly(2026, 1, 15));
        template.GenerateBills(new DateOnly(2026, 1, 31));
        template.SetActive(false, new DateOnly(2026, 2, 1));

        template.SetActive(true, new DateOnly(2026, 5, 1));
        var bills = template.GenerateBills(new DateOnly(2026, 6, 30));

        bills.Select(b => b.DueDate).ShouldBe([new DateOnly(2026, 5, 15), new DateOnly(2026, 6, 15)]);
    }

    private static RecurringBill CreateTemplate(DateOnly start) =>
        new("Electricity", payeeId: 2, categoryId: 3, amount: 80m, new RecurrenceSchedule(BillFrequency.Monthly, start, null), "Meter 42");

    [Fact]
    public void GenerateBills_AmountVaries_EstimatesFromLastActualAmount()
    {
        var template = new RecurringBill("Electricity", 2, 3, 80m, new RecurrenceSchedule(BillFrequency.Monthly, new DateOnly(2026, 1, 15), null), null, amountVaries: true);

        var bills = template.GenerateBills(new DateOnly(2026, 2, 20), lastActualAmount: 97.35m);

        bills.Count.ShouldBe(2);
        bills.ShouldAllBe(b => b.Amount == 97.35m && b.IsEstimated);
    }

    [Fact]
    public void GenerateBills_AmountVariesWithoutHistory_EstimatesFromTemplateAmount()
    {
        var template = new RecurringBill("Electricity", 2, 3, 80m, new RecurrenceSchedule(BillFrequency.Monthly, new DateOnly(2026, 1, 15), null), null, amountVaries: true);

        template.GenerateBills(new DateOnly(2026, 1, 20)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            b => b.Amount.ShouldBe(80m),
            b => b.IsEstimated.ShouldBeTrue());
    }

    [Fact]
    public void GenerateBills_FixedAmount_IgnoresLastActualAmount()
    {
        var template = CreateTemplate(new DateOnly(2026, 1, 15));

        template.GenerateBills(new DateOnly(2026, 1, 20), lastActualAmount: 97.35m).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            b => b.Amount.ShouldBe(80m),
            b => b.IsEstimated.ShouldBeFalse());
    }
}