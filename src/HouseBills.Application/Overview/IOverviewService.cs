namespace HouseBills.Application.Overview;

/// <summary>Builds the start page summary.</summary>
public interface IOverviewService
{
    /// <summary>Returns the summary relative to today, with up to <paramref name="nextBillsCount"/> upcoming bills.</summary>
    Task<OverviewSummary> GetSummaryAsync(int nextBillsCount, CancellationToken cancellationToken);
}