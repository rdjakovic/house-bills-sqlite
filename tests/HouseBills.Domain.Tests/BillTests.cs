using HouseBills.Domain;

namespace HouseBills.Domain.Tests;

public sealed class BillTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    [Theory]
    [InlineData(-1, BillStatus.Overdue)]
    [InlineData(0, BillStatus.DueSoon)]
    [InlineData(Bill.DueSoonDays, BillStatus.DueSoon)]
    [InlineData(Bill.DueSoonDays + 1, BillStatus.Upcoming)]
    public void DetermineStatus_Unpaid_DependsOnDueDate(int daysFromToday, BillStatus expected)
    {
        Bill.DetermineStatus(Today.AddDays(daysFromToday), null, Today).ShouldBe(expected);
    }

    [Fact]
    public void DetermineStatus_PaidAndPastDue_IsPaid()
    {
        Bill.DetermineStatus(Today.AddDays(-30), Today.AddDays(-31), Today).ShouldBe(BillStatus.Paid);
    }

    [Fact]
    public void MarkPaid_ValidPayment_RecordsDateAndAmount()
    {
        var bill = CreateBill();

        bill.MarkPaid(Today, 99.50m);

        bill.IsPaid.ShouldBeTrue();
        bill.PaidOn.ShouldBe(Today);
        bill.PaidAmount.ShouldBe(99.50m);
    }

    [Fact]
    public void MarkUnpaid_PaidBill_ClearsPayment()
    {
        var bill = CreateBill();
        bill.MarkPaid(Today, 10m);

        bill.MarkUnpaid();

        bill.IsPaid.ShouldBeFalse();
        bill.PaidAmount.ShouldBeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.005")]
    public void Constructor_InvalidAmount_Throws(string amount)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Bill("Water", 1, 1, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Today, null));
    }

    [Fact]
    public void Constructor_BlankDescription_Throws()
    {
        Should.Throw<ArgumentException>(() => new Bill("  ", 1, 1, 10m, Today, null));
    }

    [Fact]
    public void Constructor_TextWithWhitespace_IsTrimmedAndEmptyNotesBecomeNull()
    {
        var bill = new Bill("  Water  ", 1, 1, 10m, Today, "   ");

        bill.Description.ShouldBe("Water");
        bill.Notes.ShouldBeNull();
    }

    private static Bill CreateBill() => new("Electricity", 1, 1, 100m, Today, null);

    [Fact]
    public void MarkPaid_EstimatedBill_PaidAmountBecomesTheActualAmount()
    {
        var bill = new Bill("Electricity", 1, 1, 80m, Today, null, isEstimated: true);

        bill.MarkPaid(Today, 93.10m);

        bill.Amount.ShouldBe(93.10m);
        bill.IsEstimated.ShouldBeFalse();
        bill.PaidAmount.ShouldBe(93.10m);
    }

    [Fact]
    public void MarkPaid_ActualBill_KeepsItsAmount()
    {
        var bill = new Bill("Rent", 1, 1, 500m, Today, null);

        bill.MarkPaid(Today, 450m);

        bill.Amount.ShouldBe(500m);
        bill.PaidAmount.ShouldBe(450m);
    }

    [Fact]
    public void Update_EstimateConfirmed_ClearsEstimate()
    {
        var bill = new Bill("Electricity", 1, 1, 80m, Today, null, isEstimated: true);

        bill.Update("Electricity", 1, 1, 91.40m, Today, null, isEstimated: false);

        bill.Amount.ShouldBe(91.40m);
        bill.IsEstimated.ShouldBeFalse();
    }
}