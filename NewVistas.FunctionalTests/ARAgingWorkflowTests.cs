// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Financial Reporting / AR Aging — aging analysis,
/// revenue cycle metrics, and bucket classification (VistA PRCA — RCRP*.m).
/// </summary>
[TestFixture]
public class ARAgingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IARAgingReportGrain NewReport()
        => _cluster.GrainFactory.GetGrain<IARAgingReportGrain>($"AR-AGING:{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Report Generation (Grain Level) ─────────────────────────────────────

    [Test]
    public async Task GenerateReport_StoresAccountDetails()
    {
        IARAgingReportGrain report = NewReport();
        List<AgingAccountDetail> details = new()
        {
            new() { ARAccountId = "AR-A1", PatientId = "PAT-AG-001", Category = ARAccountCategory.CopayOutpatient, Status = ARAccountStatus.Active, OriginalAmount = 50m, CurrentBalance = 50m, DateEstablished = DateTime.UtcNow.AddDays(-20), DaysOutstanding = 20, Bucket = AgingBucket.Current },
            new() { ARAccountId = "AR-A2", PatientId = "PAT-AG-001", Category = ARAccountCategory.CopayPharmacy, Status = ARAccountStatus.Active, OriginalAmount = 24m, CurrentBalance = 24m, DateEstablished = DateTime.UtcNow.AddDays(-50), DaysOutstanding = 50, Bucket = AgingBucket.ThirtyOneToSixty },
        };
        RevenueCycleMetrics metrics = new() { TotalARBalance = 74m, TotalActiveAccounts = 2, CollectionRate = 0m, ReportDate = DateTime.UtcNow };

        await report.GenerateAsync("PAT-AG-001", details, metrics, "USR-001", "Test");

        ARAgingReportState state = await report.GetAsync();
        Assert.That(state.AccountDetails, Has.Count.EqualTo(2));
        Assert.That(state.TotalBalance, Is.EqualTo(74m));
        Assert.That(state.TotalAccounts, Is.EqualTo(2));
    }

    [Test]
    public async Task GenerateReport_ComputesBucketSummaries()
    {
        IARAgingReportGrain report = NewReport();
        List<AgingAccountDetail> details = new()
        {
            new() { ARAccountId = "AR-B1", PatientId = "P1", Category = ARAccountCategory.CopayOutpatient, Status = ARAccountStatus.Active, OriginalAmount = 100m, CurrentBalance = 100m, DateEstablished = DateTime.UtcNow.AddDays(-10), DaysOutstanding = 10, Bucket = AgingBucket.Current },
            new() { ARAccountId = "AR-B2", PatientId = "P1", Category = ARAccountCategory.CopayOutpatient, Status = ARAccountStatus.Active, OriginalAmount = 200m, CurrentBalance = 200m, DateEstablished = DateTime.UtcNow.AddDays(-70), DaysOutstanding = 70, Bucket = AgingBucket.SixtyOneToNinety },
        };
        RevenueCycleMetrics metrics = new() { TotalARBalance = 300m, ReportDate = DateTime.UtcNow };

        await report.GenerateAsync("P1", details, metrics, null, null);

        List<AgingBucketSummary> buckets = await report.GetBucketSummariesAsync();
        Assert.That(buckets, Has.Count.EqualTo(5)); // all 5 buckets always present

        AgingBucketSummary currentBucket = buckets.First(b => b.Bucket == AgingBucket.Current);
        Assert.That(currentBucket.AccountCount, Is.EqualTo(1));
        Assert.That(currentBucket.TotalBalance, Is.EqualTo(100m));

        AgingBucketSummary sixtyNinety = buckets.First(b => b.Bucket == AgingBucket.SixtyOneToNinety);
        Assert.That(sixtyNinety.AccountCount, Is.EqualTo(1));
        Assert.That(sixtyNinety.TotalBalance, Is.EqualTo(200m));
    }

    [Test]
    public async Task GenerateReport_StoresMetrics()
    {
        IARAgingReportGrain report = NewReport();
        RevenueCycleMetrics metrics = new()
        {
            TotalARBalance = 500m, TotalActiveAccounts = 5, AverageDaysOutstanding = 45.5m,
            CollectionRate = 0.65m, TotalPaymentsReceived = 1000m, TotalNewCharges = 1500m,
            DelinquentAccountCount = 2, DelinquentBalance = 250m,
            InCollectionCount = 1, InCollectionBalance = 100m, ReportDate = DateTime.UtcNow,
        };

        await report.GenerateAsync("P2", new List<AgingAccountDetail>(), metrics, null, null);

        RevenueCycleMetrics? stored = await report.GetMetricsAsync();
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.CollectionRate, Is.EqualTo(0.65m));
        Assert.That(stored.TotalActiveAccounts, Is.EqualTo(5));
    }

    [Test]
    public async Task GetAccountsByBucket_FiltersCorrectly()
    {
        IARAgingReportGrain report = NewReport();
        List<AgingAccountDetail> details = new()
        {
            new() { ARAccountId = "AR-F1", PatientId = "P3", Category = ARAccountCategory.CopayOutpatient, Status = ARAccountStatus.Active, OriginalAmount = 50m, CurrentBalance = 50m, DateEstablished = DateTime.UtcNow.AddDays(-20), DaysOutstanding = 20, Bucket = AgingBucket.Current },
            new() { ARAccountId = "AR-F2", PatientId = "P3", Category = ARAccountCategory.CopayPharmacy, Status = ARAccountStatus.Active, OriginalAmount = 100m, CurrentBalance = 100m, DateEstablished = DateTime.UtcNow.AddDays(-130), DaysOutstanding = 130, Bucket = AgingBucket.OverOneTwenty },
        };

        await report.GenerateAsync("P3", details, new RevenueCycleMetrics { TotalARBalance = 150m, ReportDate = DateTime.UtcNow }, null, null);

        List<AgingAccountDetail> over120 = await report.GetAccountsByBucketAsync(AgingBucket.OverOneTwenty);
        Assert.That(over120, Has.Count.EqualTo(1));
        Assert.That(over120[0].ARAccountId, Is.EqualTo("AR-F2"));
    }

    // ─── Bucket Classification ───────────────────────────────────────────────

    [Test]
    public async Task BucketPercent_SumsToOne()
    {
        IARAgingReportGrain report = NewReport();
        List<AgingAccountDetail> details = new()
        {
            new() { ARAccountId = "A1", PatientId = "P4", Category = ARAccountCategory.CopayOutpatient, Status = ARAccountStatus.Active, OriginalAmount = 100m, CurrentBalance = 100m, DateEstablished = DateTime.UtcNow.AddDays(-10), DaysOutstanding = 10, Bucket = AgingBucket.Current },
            new() { ARAccountId = "A2", PatientId = "P4", Category = ARAccountCategory.CopayPharmacy, Status = ARAccountStatus.Active, OriginalAmount = 50m, CurrentBalance = 50m, DateEstablished = DateTime.UtcNow.AddDays(-40), DaysOutstanding = 40, Bucket = AgingBucket.ThirtyOneToSixty },
            new() { ARAccountId = "A3", PatientId = "P4", Category = ARAccountCategory.CopayInpatient, Status = ARAccountStatus.Active, OriginalAmount = 50m, CurrentBalance = 50m, DateEstablished = DateTime.UtcNow.AddDays(-100), DaysOutstanding = 100, Bucket = AgingBucket.NinetyOneToOneTwenty },
        };

        await report.GenerateAsync("P4", details, new RevenueCycleMetrics { TotalARBalance = 200m, ReportDate = DateTime.UtcNow }, null, null);
        List<AgingBucketSummary> buckets = await report.GetBucketSummariesAsync();

        decimal totalPercent = buckets.Sum(b => b.PercentOfTotal);
        Assert.That(totalPercent, Is.EqualTo(1.0m).Within(0.01m));
    }

    [Test]
    public async Task EmptyReport_AllBucketsZero()
    {
        IARAgingReportGrain report = NewReport();
        await report.GenerateAsync("P-EMPTY", new List<AgingAccountDetail>(),
            new RevenueCycleMetrics { TotalARBalance = 0m, ReportDate = DateTime.UtcNow }, null, null);

        List<AgingBucketSummary> buckets = await report.GetBucketSummariesAsync();
        Assert.That(buckets, Has.Count.EqualTo(5));
        Assert.That(buckets.All(b => b.AccountCount == 0 && b.TotalBalance == 0m), Is.True);
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_GenerateAgingReport_ClassifiesAccounts()
    {
        string patientId = $"PAT-AG-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Create two accounts (both will have DateEstablished ≈ now, so both land in Current bucket)
        await wf.CreateARAccountAsync(null, "CopayOutpatient", 50m, DateTime.UtcNow.AddDays(15));
        await wf.CreateARAccountAsync(null, "CopayPharmacy", 24m, DateTime.UtcNow.AddDays(-15));

        ARAgingReportState report = await wf.GenerateARAgingReportAsync("USR-WF", "WF User");

        Assert.That(report.TotalAccounts, Is.EqualTo(2));
        Assert.That(report.TotalBalance, Is.EqualTo(74m));
        // Both accounts are established NOW, so both fall into Current bucket
        Assert.That(report.AccountDetails.Count(a => a.Bucket == AgingBucket.Current), Is.EqualTo(2));
    }

    [Test]
    public async Task Workflow_GenerateAgingReport_ComputesCollectionRate()
    {
        string patientId = $"PAT-AG-CR-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Create account and make partial payment
        await wf.CreateARAccountAsync(null, "CopayOutpatient", 100m, DateTime.UtcNow);

        // Post a payment to reduce balance
        List<ARAccountIndexEntry> accounts = await wf.GetARAccountsAsync();
        if (accounts.Count > 0)
        {
            await wf.PostARPaymentAsync(accounts[0].ARAccountId, 60m, "CASH", "USR-WF", "WF", null, null, null);
        }

        ARAgingReportState report = await wf.GenerateARAgingReportAsync("USR-WF", "WF");

        Assert.That(report.Metrics, Is.Not.Null);
        Assert.That(report.Metrics!.CollectionRate, Is.GreaterThan(0m));
    }

    [Test]
    public async Task Workflow_GetARAgingBuckets_ReturnsFiveBuckets()
    {
        string patientId = $"PAT-AG-BK-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 50m, DateTime.UtcNow.AddDays(20));
        await wf.GenerateARAgingReportAsync(null, null);

        List<AgingBucketSummary> buckets = await wf.GetARAgingBucketsAsync();
        Assert.That(buckets, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task Workflow_GetRevenueCycleMetrics_ReturnsMetrics()
    {
        string patientId = $"PAT-AG-MET-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 75m, DateTime.UtcNow.AddDays(10));
        await wf.GenerateARAgingReportAsync("USR", "User");

        RevenueCycleMetrics? metrics = await wf.GetRevenueCycleMetricsAsync();
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.TotalARBalance, Is.EqualTo(75m));
        Assert.That(metrics.TotalActiveAccounts, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Workflow_GetAccountsByBucket_ReturnsFilteredList()
    {
        string patientId = $"PAT-AG-BKFILT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Both accounts created NOW → both in Current bucket (DateEstablished ≈ now)
        await wf.CreateARAccountAsync(null, "CopayOutpatient", 50m, DateTime.UtcNow.AddDays(15));
        await wf.CreateARAccountAsync(null, "MedicareDeductible", 150m, DateTime.UtcNow.AddDays(-120));
        await wf.GenerateARAgingReportAsync(null, null);

        List<AgingAccountDetail> current = await wf.GetAccountsByAgingBucketAsync(AgingBucket.Current);

        // Both accounts are in Current bucket since they were just created
        Assert.That(current, Has.Count.EqualTo(2));
        Assert.That(current.Sum(a => a.CurrentBalance), Is.EqualTo(200m));
    }

    [Test]
    public async Task Workflow_NoAccounts_ReportsZeroBalance()
    {
        string patientId = $"PAT-AG-ZERO-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        ARAgingReportState report = await wf.GenerateARAgingReportAsync(null, null);

        Assert.That(report.TotalBalance, Is.EqualTo(0m));
        Assert.That(report.TotalAccounts, Is.EqualTo(0));
        Assert.That(report.BucketSummaries, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task Workflow_GetARAgingReport_ReturnsCachedReport()
    {
        string patientId = $"PAT-AG-CACHE-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 50m, DateTime.UtcNow.AddDays(30));
        await wf.GenerateARAgingReportAsync(null, null);

        ARAgingReportState cached = await wf.GetARAgingReportAsync();
        Assert.That(cached.TotalAccounts, Is.EqualTo(1));
        Assert.That(cached.TotalBalance, Is.EqualTo(50m));
    }

    [Test]
    public async Task Workflow_Metrics_ReportDateIsSet()
    {
        string patientId = $"PAT-AG-DT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 25m, DateTime.UtcNow.AddDays(30));
        await wf.GenerateARAgingReportAsync(null, null);

        RevenueCycleMetrics? metrics = await wf.GetRevenueCycleMetricsAsync();
        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics!.ReportDate.Date, Is.EqualTo(DateTime.UtcNow.Date));
    }

    [Test]
    public async Task GenerateReport_EmptyBuckets_HaveZeroPercent()
    {
        IARAgingReportGrain report = NewReport();
        List<AgingAccountDetail> details = new()
        {
            new() { ARAccountId = "AR-ZP", PatientId = "PZP", Category = ARAccountCategory.CopayOutpatient, Status = ARAccountStatus.Active, OriginalAmount = 100m, CurrentBalance = 100m, DateEstablished = DateTime.UtcNow.AddDays(-10), DaysOutstanding = 10, Bucket = AgingBucket.Current },
        };

        await report.GenerateAsync("PZP", details, new RevenueCycleMetrics { TotalARBalance = 100m, ReportDate = DateTime.UtcNow }, null, null);
        List<AgingBucketSummary> buckets = await report.GetBucketSummariesAsync();

        // The Current bucket has 100% and all others have 0%
        Assert.That(buckets.First(b => b.Bucket == AgingBucket.Current).PercentOfTotal, Is.EqualTo(1.0m));
        Assert.That(buckets.First(b => b.Bucket == AgingBucket.ThirtyOneToSixty).PercentOfTotal, Is.EqualTo(0m));
        Assert.That(buckets.First(b => b.Bucket == AgingBucket.OverOneTwenty).PercentOfTotal, Is.EqualTo(0m));
    }

    [Test]
    public async Task Workflow_MultipleAccounts_AverageDaysOutstandingComputed()
    {
        string patientId = $"PAT-AG-AVG-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 50m, DateTime.UtcNow.AddDays(30));
        await wf.CreateARAccountAsync(null, "CopayPharmacy", 25m, DateTime.UtcNow.AddDays(10));
        ARAgingReportState report = await wf.GenerateARAgingReportAsync(null, null);

        Assert.That(report.Metrics, Is.Not.Null);
        Assert.That(report.Metrics!.AverageDaysOutstanding, Is.GreaterThanOrEqualTo(0m));
    }
}
