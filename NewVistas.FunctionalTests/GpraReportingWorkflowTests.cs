// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for GPRA Reporting — RPMS CIMGAGP / BQIGPRA.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class GpraReportingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private IGpraReportGrain GetReport(string id)
        => _cluster.GrainFactory.GetGrain<IGpraReportGrain>(id);

    private IGpraReportIndexGrain GetIndex()
        => _cluster.GrainFactory.GetGrain<IGpraReportIndexGrain>("GPRA-REPORT-IDX");

    private ISiteParametersGrain GetSiteParams()
        => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp()
    {
        await GetSiteParams().EnableFeatureAsync("GPRA_REPORTING");
    }

    private async Task<string> CreateDefaultReportAsync(
        int fiscalYear = 2026,
        GpraReportingPeriod period = GpraReportingPeriod.FullFiscalYear,
        bool addToIndex = true)
    {
        string reportId = $"GPRA-REPORT-{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            fiscalYear, period,
            new DateTime(fiscalYear - 1, 10, 1), new DateTime(fiscalYear, 9, 30),
            new DateTime(fiscalYear - 4, 10, 1), new DateTime(fiscalYear - 3, 9, 30),
            "IHS-SITE-001", "Pine Ridge Service Unit",
            "OGLALA", 12500, "USER-001", "Dr. Smith");

        if (addToIndex)
        {
            await GetIndex().AddEntryAsync(new GpraReportIndexEntry
            {
                ReportId = reportId,
                FiscalYear = fiscalYear,
                ReportingPeriod = period,
                Status = GpraReportStatus.Draft,
                FacilityName = "Pine Ridge Service Unit",
                ActiveUserPopulation = 12500,
                IndicatorCount = 0,
                CreatedDate = DateTime.UtcNow
            });
        }

        return reportId;
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateReport_FullFiscalYear_PersistsAndAppearsInIndex()
    {
        string reportId = await CreateDefaultReportAsync();

        GpraReportState state = await GetReport(reportId).GetAsync();
        Assert.That(state.FiscalYear, Is.EqualTo(2026));
        Assert.That(state.ReportingPeriod, Is.EqualTo(GpraReportingPeriod.FullFiscalYear));
        Assert.That(state.FacilityId, Is.EqualTo("IHS-SITE-001"));
        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Draft));

        List<GpraReportIndexEntry> entries = await GetIndex().GetAllAsync();
        Assert.That(entries.Any(e => e.ReportId == reportId), Is.True);
    }

    [Test]
    public async Task CreateReport_QuarterlyPeriod_SetsCorrectDates()
    {
        string reportId = $"GPRA-REPORT-{Guid.NewGuid()}";
        IGpraReportGrain grain = GetReport(reportId);

        await grain.CreateAsync(
            fiscalYear: 2026,
            reportingPeriod: GpraReportingPeriod.Quarter2,
            currentPeriodStart: new DateTime(2025, 10, 1),
            currentPeriodEnd: new DateTime(2025, 12, 31),
            baselinePeriodStart: new DateTime(2022, 10, 1),
            baselinePeriodEnd: new DateTime(2022, 12, 31),
            facilityId: "IHS-SITE-002",
            facilityName: "Rosebud Service Unit",
            communityTaxonomy: "SICANGU",
            activeUserPopulation: 8000,
            generatedById: "USER-002",
            generatedByName: "Dr. Jones");

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.ReportingPeriod, Is.EqualTo(GpraReportingPeriod.Quarter2));
        Assert.That(state.CurrentPeriodStart, Is.EqualTo(new DateTime(2025, 10, 1)));
        Assert.That(state.CurrentPeriodEnd, Is.EqualTo(new DateTime(2025, 12, 31)));
        Assert.That(state.FacilityName, Is.EqualTo("Rosebud Service Unit"));
    }

    [Test]
    public async Task AddIndicatorResults_DiabetesBundle()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        GpraIndicatorResult[] diabetesIndicators = new[]
        {
            new GpraIndicatorResult
            {
                MeasureId = "GPRA-DM-01", Title = "Diabetes: HbA1c Testing",
                Category = GpraClinicalCategory.Diabetes,
                CurrentNumerator = 785, CurrentDenominator = 1000, CurrentPerformanceRate = 78.5m,
                BaselineNumerator = 721, BaselineDenominator = 1000, BaselinePerformanceRate = 72.1m,
                PercentagePointChange = 6.4m, IsImproved = true
            },
            new GpraIndicatorResult
            {
                MeasureId = "GPRA-DM-02", Title = "Diabetes: HbA1c Control (<9%)",
                Category = GpraClinicalCategory.Diabetes,
                CurrentNumerator = 650, CurrentDenominator = 1000, CurrentPerformanceRate = 65.0m,
                BaselineNumerator = 600, BaselineDenominator = 1000, BaselinePerformanceRate = 60.0m,
                PercentagePointChange = 5.0m, IsImproved = true
            },
            new GpraIndicatorResult
            {
                MeasureId = "GPRA-DM-03", Title = "Diabetes: LDL Testing",
                Category = GpraClinicalCategory.Diabetes,
                CurrentNumerator = 720, CurrentDenominator = 1000, CurrentPerformanceRate = 72.0m,
                BaselineNumerator = 680, BaselineDenominator = 1000, BaselinePerformanceRate = 68.0m,
                PercentagePointChange = 4.0m, IsImproved = true
            },
            new GpraIndicatorResult
            {
                MeasureId = "GPRA-DM-04", Title = "Diabetes: Foot Exam",
                Category = GpraClinicalCategory.Diabetes,
                CurrentNumerator = 550, CurrentDenominator = 1000, CurrentPerformanceRate = 55.0m,
                BaselineNumerator = 520, BaselineDenominator = 1000, BaselinePerformanceRate = 52.0m,
                PercentagePointChange = 3.0m, IsImproved = true
            },
            new GpraIndicatorResult
            {
                MeasureId = "GPRA-DM-05", Title = "Diabetes: Eye Exam",
                Category = GpraClinicalCategory.Diabetes,
                CurrentNumerator = 480, CurrentDenominator = 1000, CurrentPerformanceRate = 48.0m,
                BaselineNumerator = 450, BaselineDenominator = 1000, BaselinePerformanceRate = 45.0m,
                PercentagePointChange = 3.0m, IsImproved = true
            }
        };

        foreach (GpraIndicatorResult indicator in diabetesIndicators)
            await grain.AddIndicatorResultAsync(indicator);

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators, Has.Count.EqualTo(5));
        Assert.That(state.Indicators.All(i => i.Category == GpraClinicalCategory.Diabetes), Is.True);
        Assert.That(state.Indicators[0].MeasureId, Is.EqualTo("GPRA-DM-01"));
        Assert.That(state.Indicators[4].MeasureId, Is.EqualTo("GPRA-DM-05"));
    }

    [Test]
    public async Task AddIndicatorResults_VerifyPerformanceRates()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-IMM-01", Title = "Immunizations: Childhood",
            Category = GpraClinicalCategory.Immunizations,
            CurrentNumerator = 440, CurrentDenominator = 500, CurrentPerformanceRate = 88.0m,
            BaselineNumerator = 400, BaselineDenominator = 500, BaselinePerformanceRate = 80.0m,
            PercentagePointChange = 8.0m, IsImproved = true
        });

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-PC-01", Title = "Preventive Care: Cancer Screening",
            Category = GpraClinicalCategory.PreventiveCare,
            CurrentNumerator = 360, CurrentDenominator = 600, CurrentPerformanceRate = 60.0m,
            BaselineNumerator = 330, BaselineDenominator = 600, BaselinePerformanceRate = 55.0m,
            PercentagePointChange = 5.0m, IsImproved = true
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators[0].CurrentPerformanceRate, Is.EqualTo(88.0m));
        Assert.That(state.Indicators[0].CurrentNumerator, Is.EqualTo(440));
        Assert.That(state.Indicators[0].CurrentDenominator, Is.EqualTo(500));
        Assert.That(state.Indicators[1].CurrentPerformanceRate, Is.EqualTo(60.0m));
        Assert.That(state.Indicators[1].CurrentNumerator, Is.EqualTo(360));
        Assert.That(state.Indicators[1].CurrentDenominator, Is.EqualTo(600));
    }

    [Test]
    public async Task CompleteReport_SyncsIndexStatus()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01", Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentPerformanceRate = 78.5m, BaselinePerformanceRate = 72.1m,
            PercentagePointChange = 6.4m, IsImproved = true
        });

        await grain.CompleteAsync();
        await GetIndex().UpdateStatusAsync(reportId, GpraReportStatus.Completed);

        GpraReportState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Completed));

        List<GpraReportIndexEntry> entries = await GetIndex().GetAllAsync();
        GpraReportIndexEntry? indexEntry = entries.FirstOrDefault(e => e.ReportId == reportId);
        Assert.That(indexEntry, Is.Not.Null);
        Assert.That(indexEntry!.Status, Is.EqualTo(GpraReportStatus.Completed));
    }

    [Test]
    public async Task ErrorReport_SyncsIndexStatus()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        await grain.MarkErrorAsync("CQM evaluation failed");
        await GetIndex().UpdateStatusAsync(reportId, GpraReportStatus.Error);

        GpraReportState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Error));
        Assert.That(state.ErrorMessage, Is.EqualTo("CQM evaluation failed"));

        List<GpraReportIndexEntry> entries = await GetIndex().GetAllAsync();
        GpraReportIndexEntry? indexEntry = entries.FirstOrDefault(e => e.ReportId == reportId);
        Assert.That(indexEntry, Is.Not.Null);
        Assert.That(indexEntry!.Status, Is.EqualTo(GpraReportStatus.Error));
    }

    [Test]
    public async Task BaselineComparison_ImprovementTracking()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-02", Title = "Diabetes: HbA1c Control",
            Category = GpraClinicalCategory.Diabetes,
            CurrentNumerator = 820, CurrentDenominator = 1000, CurrentPerformanceRate = 82.0m,
            BaselineNumerator = 750, BaselineDenominator = 1000, BaselinePerformanceRate = 75.0m,
            PercentagePointChange = 7.0m, IsImproved = true
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators[0].IsImproved, Is.True);
        Assert.That(state.Indicators[0].PercentagePointChange, Is.EqualTo(7.0m));
        Assert.That(state.Indicators[0].CurrentPerformanceRate, Is.GreaterThan(state.Indicators[0].BaselinePerformanceRate));
    }

    [Test]
    public async Task BaselineComparison_RegressionTracking()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-BH-01", Title = "Behavioral Health: Depression Screening",
            Category = GpraClinicalCategory.BehavioralHealth,
            CurrentNumerator = 680, CurrentDenominator = 1000, CurrentPerformanceRate = 68.0m,
            BaselineNumerator = 730, BaselineDenominator = 1000, BaselinePerformanceRate = 73.0m,
            PercentagePointChange = -5.0m, IsImproved = false
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators[0].IsImproved, Is.False);
        Assert.That(state.Indicators[0].PercentagePointChange, Is.EqualTo(-5.0m));
        Assert.That(state.Indicators[0].CurrentPerformanceRate, Is.LessThan(state.Indicators[0].BaselinePerformanceRate));
    }

    [Test]
    public async Task TargetTracking_MetVsNotMet()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01", Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentPerformanceRate = 55.0m,
            TargetRate = 50.0m, TargetMet = true
        });

        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-CVD-01", Title = "CVD: Blood Pressure Control",
            Category = GpraClinicalCategory.CardiovascularDisease,
            CurrentPerformanceRate = 72.0m,
            TargetRate = 80.0m, TargetMet = false
        });

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Indicators[0].TargetMet, Is.True);
        Assert.That(state.Indicators[0].CurrentPerformanceRate, Is.GreaterThanOrEqualTo(state.Indicators[0].TargetRate!.Value));
        Assert.That(state.Indicators[1].TargetMet, Is.False);
        Assert.That(state.Indicators[1].CurrentPerformanceRate, Is.LessThan(state.Indicators[1].TargetRate!.Value));
    }

    [Test]
    public async Task CqmReportLink_DrillDown()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        await grain.AddCqmReportLinkAsync("CQM-RPT-001");
        await grain.AddCqmReportLinkAsync("CQM-RPT-002");
        await grain.AddCqmReportLinkAsync("CQM-RPT-003");

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.CqmReportIds, Has.Count.EqualTo(3));
        Assert.That(state.CqmReportIds, Contains.Item("CQM-RPT-001"));
        Assert.That(state.CqmReportIds, Contains.Item("CQM-RPT-002"));
        Assert.That(state.CqmReportIds, Contains.Item("CQM-RPT-003"));
    }

    [Test]
    public async Task MultipleReports_IndependentByFiscalYear()
    {
        string reportId2025 = await CreateDefaultReportAsync(fiscalYear: 2025);
        string reportId2026 = await CreateDefaultReportAsync(fiscalYear: 2026);

        List<GpraReportIndexEntry> fy2025 = await GetIndex().GetByFiscalYearAsync(2025);
        List<GpraReportIndexEntry> fy2026 = await GetIndex().GetByFiscalYearAsync(2026);

        Assert.That(fy2025.Any(e => e.ReportId == reportId2025), Is.True);
        Assert.That(fy2025.Any(e => e.ReportId == reportId2026), Is.False);
        Assert.That(fy2026.Any(e => e.ReportId == reportId2026), Is.True);
        Assert.That(fy2026.Any(e => e.ReportId == reportId2025), Is.False);
    }

    [Test]
    public async Task FullWorkflow_CreateEvaluateComplete()
    {
        string reportId = await CreateDefaultReportAsync();
        IGpraReportGrain grain = GetReport(reportId);

        // Add 6 indicators across 3 categories
        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01", Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentPerformanceRate = 78.5m, BaselinePerformanceRate = 72.1m,
            PercentagePointChange = 6.4m, IsImproved = true
        });
        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-02", Title = "Diabetes: HbA1c Control",
            Category = GpraClinicalCategory.Diabetes,
            CurrentPerformanceRate = 65.0m, BaselinePerformanceRate = 60.0m,
            PercentagePointChange = 5.0m, IsImproved = true
        });
        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-IMM-01", Title = "Immunizations: Childhood",
            Category = GpraClinicalCategory.Immunizations,
            CurrentPerformanceRate = 88.0m, BaselinePerformanceRate = 85.0m,
            PercentagePointChange = 3.0m, IsImproved = true
        });
        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-IMM-02", Title = "Immunizations: Influenza",
            Category = GpraClinicalCategory.Immunizations,
            CurrentPerformanceRate = 42.0m, BaselinePerformanceRate = 45.0m,
            PercentagePointChange = -3.0m, IsImproved = false
        });
        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-BH-01", Title = "Behavioral Health: Depression Screening",
            Category = GpraClinicalCategory.BehavioralHealth,
            CurrentPerformanceRate = 70.0m, BaselinePerformanceRate = 65.0m,
            PercentagePointChange = 5.0m, IsImproved = true
        });
        await grain.AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-BH-02", Title = "Behavioral Health: SBIRT",
            Category = GpraClinicalCategory.BehavioralHealth,
            CurrentPerformanceRate = 55.0m, BaselinePerformanceRate = 50.0m,
            PercentagePointChange = 5.0m, IsImproved = true
        });

        // Link CQM reports
        await grain.AddCqmReportLinkAsync("CQM-RPT-DM-001");
        await grain.AddCqmReportLinkAsync("CQM-RPT-IMM-001");

        // Complete
        await grain.CompleteAsync();
        await GetIndex().UpdateStatusAsync(reportId, GpraReportStatus.Completed);

        GpraReportState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Completed));
        Assert.That(state.Indicators, Has.Count.EqualTo(6));
        Assert.That(state.CqmReportIds, Has.Count.EqualTo(2));
        Assert.That(state.FiscalYear, Is.EqualTo(2026));
        Assert.That(state.FacilityId, Is.EqualTo("IHS-SITE-001"));
        Assert.That(state.CommunityTaxonomy, Is.EqualTo("OGLALA"));

        // Verify categories represented
        Assert.That(state.Indicators.Count(i => i.Category == GpraClinicalCategory.Diabetes), Is.EqualTo(2));
        Assert.That(state.Indicators.Count(i => i.Category == GpraClinicalCategory.Immunizations), Is.EqualTo(2));
        Assert.That(state.Indicators.Count(i => i.Category == GpraClinicalCategory.BehavioralHealth), Is.EqualTo(2));

        List<GpraReportIndexEntry> entries = await GetIndex().GetAllAsync();
        GpraReportIndexEntry? indexEntry = entries.FirstOrDefault(e => e.ReportId == reportId);
        Assert.That(indexEntry!.Status, Is.EqualTo(GpraReportStatus.Completed));
    }

    [Test]
    public async Task GetReportDetail_ReturnsFullState()
    {
        string reportId = $"GPRA-REPORT-{Guid.NewGuid()}";
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

        Assert.That(state.ReportId, Is.EqualTo(reportId));
        Assert.That(state.FiscalYear, Is.EqualTo(2026));
        Assert.That(state.ReportingPeriod, Is.EqualTo(GpraReportingPeriod.FullFiscalYear));
        Assert.That(state.CurrentPeriodStart, Is.EqualTo(new DateTime(2025, 10, 1)));
        Assert.That(state.CurrentPeriodEnd, Is.EqualTo(new DateTime(2026, 9, 30)));
        Assert.That(state.BaselinePeriodStart, Is.EqualTo(new DateTime(2022, 10, 1)));
        Assert.That(state.BaselinePeriodEnd, Is.EqualTo(new DateTime(2023, 9, 30)));
        Assert.That(state.FacilityId, Is.EqualTo("IHS-SITE-001"));
        Assert.That(state.FacilityName, Is.EqualTo("Pine Ridge Service Unit"));
        Assert.That(state.CommunityTaxonomy, Is.EqualTo("OGLALA"));
        Assert.That(state.ActiveUserPopulation, Is.EqualTo(12500));
        Assert.That(state.GeneratedById, Is.EqualTo("USER-001"));
        Assert.That(state.GeneratedByName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Draft));
        Assert.That(state.Indicators, Is.Not.Null);
        Assert.That(state.CqmReportIds, Is.Not.Null);
        Assert.That(state.CreatedDate, Is.GreaterThan(DateTime.MinValue));
        Assert.That(state.LastModifiedDate, Is.GreaterThan(DateTime.MinValue));
    }

    [Test]
    public async Task GetByFiscalYear_ReturnsOnlyMatchingYear()
    {
        string reportId2025 = await CreateDefaultReportAsync(fiscalYear: 2025);
        string reportId2026 = await CreateDefaultReportAsync(fiscalYear: 2026);

        List<GpraReportIndexEntry> fy2026 = await GetIndex().GetByFiscalYearAsync(2026);

        Assert.That(fy2026.Any(e => e.ReportId == reportId2026), Is.True);
        Assert.That(fy2026.All(e => e.FiscalYear == 2026), Is.True);
    }
}
