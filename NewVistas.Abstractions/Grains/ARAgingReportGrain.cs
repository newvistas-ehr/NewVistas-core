// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARAgingReportGrain : Grain, IARAgingReportGrain
{
    private readonly IPersistentState<ARAgingReportState> _state;

    public ARAgingReportGrain(
        [PersistentState("arAgingReportState", "arAgingReportStore")]
        IPersistentState<ARAgingReportState> state)
    {
        _state = state;
    }

    public Task<ARAgingReportState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task GenerateAsync(
        string patientId,
        List<AgingAccountDetail> accountDetails,
        RevenueCycleMetrics metrics,
        string? generatedByUserId,
        string? generatedByUserName)
    {
        DateTime now = DateTime.UtcNow;
        string reportId = this.GetPrimaryKeyString();

        _state.State.ReportId            = reportId;
        _state.State.PatientId           = patientId;
        _state.State.ReportDate          = now;
        _state.State.AccountDetails      = accountDetails;
        _state.State.Metrics             = metrics;
        _state.State.TotalBalance        = accountDetails.Sum(a => a.CurrentBalance);
        _state.State.TotalAccounts       = accountDetails.Count;
        _state.State.GeneratedByUserId   = generatedByUserId;
        _state.State.GeneratedByUserName = generatedByUserName;
        _state.State.CreatedDate         = now;
        _state.State.LastModifiedDate    = now;

        // Compute bucket summaries
        decimal totalBalance = _state.State.TotalBalance;
        _state.State.BucketSummaries = new List<AgingBucketSummary>
        {
            MakeBucket(AgingBucket.Current, "0-30 Days", accountDetails, totalBalance),
            MakeBucket(AgingBucket.ThirtyOneToSixty, "31-60 Days", accountDetails, totalBalance),
            MakeBucket(AgingBucket.SixtyOneToNinety, "61-90 Days", accountDetails, totalBalance),
            MakeBucket(AgingBucket.NinetyOneToOneTwenty, "91-120 Days", accountDetails, totalBalance),
            MakeBucket(AgingBucket.OverOneTwenty, "121+ Days", accountDetails, totalBalance),
        };

        await _state.WriteStateAsync();
    }

    public Task<List<AgingBucketSummary>> GetBucketSummariesAsync()
        => Task.FromResult(_state.State.BucketSummaries);

    public Task<RevenueCycleMetrics?> GetMetricsAsync()
        => Task.FromResult(_state.State.Metrics);

    public Task<List<AgingAccountDetail>> GetAccountsByBucketAsync(AgingBucket bucket)
        => Task.FromResult(
            _state.State.AccountDetails.Where(a => a.Bucket == bucket).ToList());

    private static AgingBucketSummary MakeBucket(
        AgingBucket bucket,
        string label,
        List<AgingAccountDetail> details,
        decimal totalBalance)
    {
        List<AgingAccountDetail> inBucket = details.Where(a => a.Bucket == bucket).ToList();
        decimal bucketBalance = inBucket.Sum(a => a.CurrentBalance);
        return new AgingBucketSummary
        {
            Bucket         = bucket,
            Label          = label,
            AccountCount   = inBucket.Count,
            TotalBalance   = bucketBalance,
            PercentOfTotal = totalBalance > 0 ? bucketBalance / totalBalance : 0m,
        };
    }
}
