// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

file class GpraReportGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("gpraReportStore");
    }
}

file class GpraReportIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("gpraReportIndexStore");
    }
}

// ── GpraReportGrain Tests ───────────────────────────────────────────────────

[TestFixture]
public class GpraReportGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IGpraReportGrain GetReport(string id)
        => _cluster.GrainFactory.GetGrain<IGpraReportGrain>(id);

    [Test]
    public async Task ReportGrain_Create_PersistsAllFields()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            fiscalYear: 2026,
            reportingPeriod: GpraReportingPeriod.FullFiscalYear,
            currentPeriodStart: new DateTime(2025, 10, 1),
            currentPeriodEnd: new DateTime(2026, 9, 30),
            baselinePeriodStart: new DateTime(2022, 10, 1),
            baselinePeriodEnd: new DateTime(2023, 9, 30),
            facilityId: "IHS-SITE-001",
            facilityName: "Pine Ridge Service Unit",
            communityTaxonomy: "OGLALA",
            activeUserPopulation: 12500,
            generatedById: "USER-001",
            generatedByName: "Dr. Smith");

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.FiscalYear, Is.EqualTo(2026));
        Assert.That(state.ReportingPeriod, Is.EqualTo(GpraReportingPeriod.FullFiscalYear));
        Assert.That(state.FacilityId, Is.EqualTo("IHS-SITE-001"));
        Assert.That(state.FacilityName, Is.EqualTo("Pine Ridge Service Unit"));
        Assert.That(state.CommunityTaxonomy, Is.EqualTo("OGLALA"));
        Assert.That(state.ActiveUserPopulation, Is.EqualTo(12500));
        Assert.That(state.GeneratedById, Is.EqualTo("USER-001"));
        Assert.That(state.GeneratedByName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.CurrentPeriodStart, Is.EqualTo(new DateTime(2025, 10, 1)));
        Assert.That(state.CurrentPeriodEnd, Is.EqualTo(new DateTime(2026, 9, 30)));
        Assert.That(state.BaselinePeriodStart, Is.EqualTo(new DateTime(2022, 10, 1)));
        Assert.That(state.BaselinePeriodEnd, Is.EqualTo(new DateTime(2023, 9, 30)));
    }

    [Test]
    public async Task ReportGrain_Create_DefaultsToStatusDraft()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-002", "Rosebud Service Unit",
            null, 8000, null, null);

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Draft));
    }

    [Test]
    public async Task ReportGrain_AddIndicatorResult_AppendsToList()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            "OGLALA", 12500, "USER-001", "Dr. Smith");

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01",
            Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentNumerator = 785,
            CurrentDenominator = 1000,
            CurrentPerformanceRate = 78.5m,
            BaselineNumerator = 721,
            BaselineDenominator = 1000,
            BaselinePerformanceRate = 72.1m,
            PercentagePointChange = 6.4m,
            IsImproved = true
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators, Has.Count.EqualTo(1));
        Assert.That(state.Indicators[0].MeasureId, Is.EqualTo("GPRA-DM-01"));
        Assert.That(state.Indicators[0].CurrentPerformanceRate, Is.EqualTo(78.5m));
        Assert.That(state.Indicators[0].BaselinePerformanceRate, Is.EqualTo(72.1m));
    }

    [Test]
    public async Task ReportGrain_AddIndicatorResult_MultipleIndicators()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            "OGLALA", 12500, "USER-001", "Dr. Smith");

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01",
            Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentPerformanceRate = 78.5m,
            BaselinePerformanceRate = 72.1m,
            PercentagePointChange = 6.4m,
            IsImproved = true
        });

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-IMM-01",
            Title = "Immunizations: Childhood",
            Category = GpraClinicalCategory.Immunizations,
            CurrentPerformanceRate = 88.0m,
            BaselinePerformanceRate = 85.0m,
            PercentagePointChange = 3.0m,
            IsImproved = true
        });

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-BH-01",
            Title = "Behavioral Health: Depression Screening",
            Category = GpraClinicalCategory.BehavioralHealth,
            CurrentPerformanceRate = 65.0m,
            BaselinePerformanceRate = 70.0m,
            PercentagePointChange = -5.0m,
            IsImproved = false
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators, Has.Count.EqualTo(3));
        Assert.That(state.Indicators[0].Category, Is.EqualTo(GpraClinicalCategory.Diabetes));
        Assert.That(state.Indicators[1].Category, Is.EqualTo(GpraClinicalCategory.Immunizations));
        Assert.That(state.Indicators[2].Category, Is.EqualTo(GpraClinicalCategory.BehavioralHealth));
    }

    [Test]
    public async Task ReportGrain_AddIndicatorResult_SetsStatusEvaluating()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            null, 12500, null, null);

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01",
            Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentPerformanceRate = 78.5m,
            BaselinePerformanceRate = 72.1m,
            PercentagePointChange = 6.4m,
            IsImproved = true
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Evaluating));
    }

    [Test]
    public async Task ReportGrain_Complete_SetsStatusCompleted()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            null, 12500, null, null);

        await grain.CompleteAsync();

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Completed));
    }

    [Test]
    public async Task ReportGrain_MarkError_SetsStatusAndMessage()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            null, 12500, null, null);

        await grain.MarkErrorAsync("CQM evaluation timed out for measure GPRA-DM-01");

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Error));
        Assert.That(state.ErrorMessage, Is.EqualTo("CQM evaluation timed out for measure GPRA-DM-01"));
    }

    [Test]
    public async Task ReportGrain_AddCqmReportLink_AddsId()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            null, 12500, null, null);

        await grain.AddCqmReportLinkAsync("CQM-RPT-001");

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.CqmReportIds, Has.Count.EqualTo(1));
        Assert.That(state.CqmReportIds, Contains.Item("CQM-RPT-001"));
    }

    [Test]
    public async Task ReportGrain_AddCqmReportLink_NoDuplicates()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            null, 12500, null, null);

        await grain.AddCqmReportLinkAsync("CQM-RPT-001");
        await grain.AddCqmReportLinkAsync("CQM-RPT-001");

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.CqmReportIds, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReportGrain_BaselineComparison_TracksChange()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            "OGLALA", 12500, "USER-001", "Dr. Smith");

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-02",
            Title = "Diabetes: HbA1c Control",
            Category = GpraClinicalCategory.Diabetes,
            CurrentNumerator = 820,
            CurrentDenominator = 1000,
            CurrentPerformanceRate = 82.0m,
            BaselineNumerator = 750,
            BaselineDenominator = 1000,
            BaselinePerformanceRate = 75.0m,
            PercentagePointChange = 7.0m,
            IsImproved = true
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators[0].IsImproved, Is.True);
        Assert.That(state.Indicators[0].PercentagePointChange, Is.EqualTo(7.0m));
        Assert.That(state.Indicators[0].CurrentPerformanceRate, Is.GreaterThan(state.Indicators[0].BaselinePerformanceRate));
    }

    [Test]
    public async Task ReportGrain_TargetTracking_MetAndNotMet()
    {
        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            2026, GpraReportingPeriod.FullFiscalYear,
            new DateTime(2025, 10, 1), new DateTime(2026, 9, 30),
            new DateTime(2022, 10, 1), new DateTime(2023, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            null, 12500, null, null);

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01",
            Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentPerformanceRate = 55.0m,
            TargetRate = 50.0m,
            TargetMet = true
        });

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-CVD-01",
            Title = "CVD: Blood Pressure Control",
            Category = GpraClinicalCategory.CardiovascularDisease,
            CurrentPerformanceRate = 72.0m,
            TargetRate = 80.0m,
            TargetMet = false
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators[0].TargetMet, Is.True);
        Assert.That(state.Indicators[1].TargetMet, Is.False);
    }
}

// ── GpraReportIndexGrain Tests ──────────────────────────────────────────────

[TestFixture]
public class GpraReportIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IGpraReportIndexGrain GetIndex()
        => _cluster.GrainFactory.GetGrain<IGpraReportIndexGrain>("GPRA-REPORT-IDX");

    [Test]
    public async Task IndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        IGpraReportIndexGrain index = GetIndex();

        string reportId1 = $"GPRA-REPORT:{Guid.NewGuid()}";
        string reportId2 = $"GPRA-REPORT:{Guid.NewGuid()}";

        await index.AddEntryAsync(new GpraReportIndexEntry
        {
            ReportId = reportId1,
            FiscalYear = 2026,
            ReportingPeriod = GpraReportingPeriod.FullFiscalYear,
            Status = GpraReportStatus.Draft,
            FacilityName = "Pine Ridge Service Unit",
            ActiveUserPopulation = 12500,
            IndicatorCount = 0,
            CreatedDate = DateTime.UtcNow.AddMinutes(-5)
        });

        await index.AddEntryAsync(new GpraReportIndexEntry
        {
            ReportId = reportId2,
            FiscalYear = 2026,
            ReportingPeriod = GpraReportingPeriod.Quarter1,
            Status = GpraReportStatus.Draft,
            FacilityName = "Rosebud Service Unit",
            ActiveUserPopulation = 8000,
            IndicatorCount = 0,
            CreatedDate = DateTime.UtcNow
        });

        List<GpraReportIndexEntry> entries = await index.GetAllAsync();

        Assert.That(entries, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(entries[0].ReportId, Is.EqualTo(reportId2));
        Assert.That(entries[1].ReportId, Is.EqualTo(reportId1));
    }

    [Test]
    public async Task IndexGrain_GetByFiscalYear_FiltersCorrectly()
    {
        IGpraReportIndexGrain index = GetIndex();

        string reportId2025 = $"GPRA-REPORT:{Guid.NewGuid()}";
        string reportId2026 = $"GPRA-REPORT:{Guid.NewGuid()}";

        await index.AddEntryAsync(new GpraReportIndexEntry
        {
            ReportId = reportId2025,
            FiscalYear = 2025,
            ReportingPeriod = GpraReportingPeriod.FullFiscalYear,
            Status = GpraReportStatus.Completed,
            FacilityName = "Pine Ridge Service Unit",
            ActiveUserPopulation = 12000,
            IndicatorCount = 10,
            CreatedDate = DateTime.UtcNow.AddDays(-30)
        });

        await index.AddEntryAsync(new GpraReportIndexEntry
        {
            ReportId = reportId2026,
            FiscalYear = 2026,
            ReportingPeriod = GpraReportingPeriod.FullFiscalYear,
            Status = GpraReportStatus.Draft,
            FacilityName = "Pine Ridge Service Unit",
            ActiveUserPopulation = 12500,
            IndicatorCount = 0,
            CreatedDate = DateTime.UtcNow
        });

        List<GpraReportIndexEntry> fy2026 = await index.GetByFiscalYearAsync(2026);

        Assert.That(fy2026.Any(e => e.ReportId == reportId2026), Is.True);
        Assert.That(fy2026.Any(e => e.ReportId == reportId2025), Is.False);
    }

    [Test]
    public async Task IndexGrain_UpdateStatus_ChangesStatus()
    {
        IGpraReportIndexGrain index = GetIndex();

        string reportId = $"GPRA-REPORT:{Guid.NewGuid()}";

        await index.AddEntryAsync(new GpraReportIndexEntry
        {
            ReportId = reportId,
            FiscalYear = 2026,
            ReportingPeriod = GpraReportingPeriod.FullFiscalYear,
            Status = GpraReportStatus.Draft,
            FacilityName = "Pine Ridge Service Unit",
            ActiveUserPopulation = 12500,
            IndicatorCount = 5,
            CreatedDate = DateTime.UtcNow
        });

        await index.UpdateStatusAsync(reportId, GpraReportStatus.Completed);

        List<GpraReportIndexEntry> entries = await index.GetAllAsync();
        GpraReportIndexEntry? updated = entries.FirstOrDefault(e => e.ReportId == reportId);

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Status, Is.EqualTo(GpraReportStatus.Completed));
    }
}

// ── Enum Tests ──────────────────────────────────────────────────────────────

[TestFixture]
public class GpraEnumTests
{
    [Test]
    public void GpraReportingPeriod_EnumValues()
    {
        Assert.That((int)GpraReportingPeriod.FullFiscalYear, Is.EqualTo(0));
        Assert.That((int)GpraReportingPeriod.Quarter1, Is.EqualTo(1));
        Assert.That((int)GpraReportingPeriod.Quarter2, Is.EqualTo(2));
        Assert.That((int)GpraReportingPeriod.Quarter3, Is.EqualTo(3));
        Assert.That((int)GpraReportingPeriod.Quarter4, Is.EqualTo(4));
    }

    [Test]
    public void GpraClinicalCategory_EnumValues()
    {
        Assert.That((int)GpraClinicalCategory.Diabetes, Is.EqualTo(0));
        Assert.That((int)GpraClinicalCategory.CardiovascularDisease, Is.EqualTo(1));
        Assert.That((int)GpraClinicalCategory.WomensHealth, Is.EqualTo(2));
        Assert.That((int)GpraClinicalCategory.Immunizations, Is.EqualTo(3));
        Assert.That((int)GpraClinicalCategory.BehavioralHealth, Is.EqualTo(4));
        Assert.That((int)GpraClinicalCategory.PreventiveCare, Is.EqualTo(5));
        Assert.That((int)GpraClinicalCategory.Asthma, Is.EqualTo(6));
        Assert.That((int)GpraClinicalCategory.ChildHealth, Is.EqualTo(7));
        Assert.That((int)GpraClinicalCategory.OralHealth, Is.EqualTo(8));
        Assert.That((int)GpraClinicalCategory.ObstetricsGynecology, Is.EqualTo(9));
    }
}
