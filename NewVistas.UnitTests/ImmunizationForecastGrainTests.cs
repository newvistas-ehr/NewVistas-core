// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Immunization Forecast grain — ACIP schedule evaluation engine.
/// Tests the IImmunizationForecastGrain directly, verifying default schedule seeding,
/// forecast generation logic for various dose/age/contraindication scenarios,
/// custom schedule management, and summary count accuracy.
/// Maps to IHS RPMS Immunization Forecasting module (BI FORECAST RPCs).
/// </summary>
[TestFixture]
public class ImmunizationForecastGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IImmunizationForecastGrain GetForecastGrain() =>
        _cluster.GrainFactory.GetGrain<IImmunizationForecastGrain>($"IMM-FORECAST:{Guid.NewGuid()}");

    [Test]
    public async Task ForecastGrain_SeedsDefaultSchedule()
    {
        // Arrange
        IImmunizationForecastGrain grain = GetForecastGrain();

        // Act
        List<VaccineSeriesDefinition> schedule = await grain.GetScheduleAsync();

        // Assert — should contain standard ACIP vaccine groups
        Assert.That(schedule, Is.Not.Empty);
        List<string> groups = schedule.Select(s => s.VaccineGroup).ToList();
        Assert.That(groups, Does.Contain("Hepatitis B"));
        Assert.That(groups, Does.Contain("DTaP/Tdap"));
        Assert.That(groups, Does.Contain("MMR"));
    }

    [Test]
    public async Task ForecastGrain_SeriesComplete_WhenAllDosesReceived()
    {
        // Arrange
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = new DateTime(2020, 1, 1);
        var history = new List<ImmunizationEntry>
        {
            new() { ImmunizationId = "IMM-1", ImmunizationName = "Hep B Dose 1", CvxCode = "08", EventDateTime = new DateTime(2020, 1, 1), VaccineGroupName = "Hepatitis B" },
            new() { ImmunizationId = "IMM-2", ImmunizationName = "Hep B Dose 2", CvxCode = "08", EventDateTime = new DateTime(2020, 2, 1), VaccineGroupName = "Hepatitis B" },
            new() { ImmunizationId = "IMM-3", ImmunizationName = "Hep B Dose 3", CvxCode = "08", EventDateTime = new DateTime(2020, 7, 1), VaccineGroupName = "Hepatitis B" },
        };

        // Act
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert
        Assert.That(result.Success, Is.True);
        ForecastRecommendation? hepB = result.Recommendations.FirstOrDefault(r => r.VaccineGroup == "Hepatitis B");
        Assert.That(hepB, Is.Not.Null);
        Assert.That(hepB!.Status, Is.EqualTo("COMPLETE"));
        Assert.That(hepB.DosesReceived, Is.EqualTo(3));
        Assert.That(hepB.DosesRequired, Is.EqualTo(3));
    }

    [Test]
    public async Task ForecastGrain_SeriesDue_WhenPartiallyComplete()
    {
        // Arrange
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = new DateTime(2020, 1, 1);
        // Use a recent dose date so the recommended interval (30 days) has passed
        // but the overdue threshold (recommended + 28 = 58 days) has not
        DateTime recentDose = DateTime.UtcNow.AddDays(-35);
        var history = new List<ImmunizationEntry>
        {
            new() { ImmunizationId = "IMM-1", ImmunizationName = "Hep B Dose 1", CvxCode = "08", EventDateTime = recentDose, VaccineGroupName = "Hepatitis B" },
        };

        // Act
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert
        Assert.That(result.Success, Is.True);
        ForecastRecommendation? hepB = result.Recommendations.FirstOrDefault(r => r.VaccineGroup == "Hepatitis B");
        Assert.That(hepB, Is.Not.Null);
        Assert.That(hepB!.Status, Is.EqualTo("DUE"));
        Assert.That(hepB.DosesReceived, Is.EqualTo(1));
        Assert.That(hepB.DosesRequired, Is.EqualTo(3));
    }

    [Test]
    public async Task ForecastGrain_SeriesOverdue_WhenSignificantlyPastDue()
    {
        // Arrange
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = new DateTime(2020, 1, 1);
        var history = new List<ImmunizationEntry>
        {
            new() { ImmunizationId = "IMM-1", ImmunizationName = "Hep B Dose 1", CvxCode = "08", EventDateTime = new DateTime(2020, 1, 1), VaccineGroupName = "Hepatitis B" },
        };

        // Act — dose 1 given over 2 months ago, dose 2 overdue
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert
        Assert.That(result.Success, Is.True);
        ForecastRecommendation? hepB = result.Recommendations.FirstOrDefault(r => r.VaccineGroup == "Hepatitis B");
        Assert.That(hepB, Is.Not.Null);
        Assert.That(hepB!.Status, Is.EqualTo("OVERDUE"));
    }

    [Test]
    public async Task ForecastGrain_Contraindicated_WhenContraindicationRecorded()
    {
        // Arrange
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = new DateTime(2020, 1, 1);
        var history = new List<ImmunizationEntry>
        {
            new() { ImmunizationId = "IMM-1", ImmunizationName = "MMR Contraindication", CvxCode = "03", EventDateTime = new DateTime(2021, 6, 1), VaccineGroupName = "MMR", IsContraindicated = true, ContraindicationReason = "Severe allergy" },
        };

        // Act
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert
        Assert.That(result.Success, Is.True);
        ForecastRecommendation? mmr = result.Recommendations.FirstOrDefault(r => r.VaccineGroup == "MMR");
        Assert.That(mmr, Is.Not.Null);
        Assert.That(mmr!.Status, Is.EqualTo("CONTRAINDICATED"));
    }

    [Test]
    public async Task ForecastGrain_NotRecommended_WhenTooYoung()
    {
        // Arrange — newborn patient, MMR min age is 12 months
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = DateTime.UtcNow.Date; // born today
        var history = new List<ImmunizationEntry>();

        // Act
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert
        Assert.That(result.Success, Is.True);
        ForecastRecommendation? mmr = result.Recommendations.FirstOrDefault(r => r.VaccineGroup == "MMR");
        Assert.That(mmr, Is.Not.Null);
        Assert.That(mmr!.Status, Is.EqualTo("NOT_RECOMMENDED"));
    }

    [Test]
    public async Task ForecastGrain_AnnualVaccine_DueWhenNoCurrentSeasonDose()
    {
        // Arrange — no influenza dose in current season
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = new DateTime(2000, 6, 15);
        var history = new List<ImmunizationEntry>
        {
            // Old dose from a prior season
            new() { ImmunizationId = "IMM-1", ImmunizationName = "Influenza 2022-2023", CvxCode = "141", EventDateTime = new DateTime(2022, 10, 1), VaccineGroupName = "Influenza" },
        };

        // Act
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert
        Assert.That(result.Success, Is.True);
        ForecastRecommendation? flu = result.Recommendations.FirstOrDefault(r => r.VaccineGroup == "Influenza");
        Assert.That(flu, Is.Not.Null);
        Assert.That(flu!.Status, Is.EqualTo("DUE"));
    }

    [Test]
    public async Task ForecastGrain_AnnualVaccine_CompleteWhenCurrentSeasonDoseExists()
    {
        // Arrange — influenza dose given within the current season (July 1 – June 30)
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = new DateTime(2000, 6, 15);
        DateTime currentSeasonStart = DateTime.UtcNow.Month >= 7
            ? new DateTime(DateTime.UtcNow.Year, 7, 1)
            : new DateTime(DateTime.UtcNow.Year - 1, 7, 1);
        var history = new List<ImmunizationEntry>
        {
            new() { ImmunizationId = "IMM-1", ImmunizationName = "Influenza Current Season", CvxCode = "141", EventDateTime = currentSeasonStart.AddMonths(3), VaccineGroupName = "Influenza" },
        };

        // Act
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert
        Assert.That(result.Success, Is.True);
        ForecastRecommendation? flu = result.Recommendations.FirstOrDefault(r => r.VaccineGroup == "Influenza");
        Assert.That(flu, Is.Not.Null);
        Assert.That(flu!.Status, Is.EqualTo("COMPLETE"));
    }

    [Test]
    public async Task ForecastGrain_CustomSchedule_CanAddAndRemoveSeries()
    {
        // Arrange
        IImmunizationForecastGrain grain = GetForecastGrain();
        var customSeries = new VaccineSeriesDefinition
        {
            VaccineGroup = "Custom Vaccine XYZ",
            CvxCodes = new List<string> { "999" },
            DosesRequired = 2,
            MinAgeMonths = 6,
            MaxAgeMonths = 0,
            MinIntervalDays = new List<int> { 28 },
            RecommendedIntervalDays = new List<int> { 56 },
            IsAnnual = false,
            SortOrder = 100,
        };

        // Act — add custom series
        await grain.AddOrUpdateSeriesDefinitionAsync(customSeries);
        List<VaccineSeriesDefinition> scheduleAfterAdd = await grain.GetScheduleAsync();

        // Assert — custom series is present
        Assert.That(scheduleAfterAdd.Any(s => s.VaccineGroup == "Custom Vaccine XYZ"), Is.True);

        // Act — remove custom series
        await grain.RemoveSeriesDefinitionAsync("Custom Vaccine XYZ");
        List<VaccineSeriesDefinition> scheduleAfterRemove = await grain.GetScheduleAsync();

        // Assert — custom series is gone
        Assert.That(scheduleAfterRemove.Any(s => s.VaccineGroup == "Custom Vaccine XYZ"), Is.False);
    }

    [Test]
    public async Task ForecastGrain_ReturnsCorrectSummaryCounts()
    {
        // Arrange — create mixed history: Hep B complete (3 doses), MMR due (0 doses, age-eligible)
        IImmunizationForecastGrain grain = GetForecastGrain();
        DateTime patientDob = new DateTime(2018, 1, 1); // old enough for most vaccines
        var history = new List<ImmunizationEntry>
        {
            // Hep B series complete
            new() { ImmunizationId = "IMM-1", ImmunizationName = "Hep B Dose 1", CvxCode = "08", EventDateTime = new DateTime(2018, 1, 1), VaccineGroupName = "Hepatitis B" },
            new() { ImmunizationId = "IMM-2", ImmunizationName = "Hep B Dose 2", CvxCode = "08", EventDateTime = new DateTime(2018, 2, 1), VaccineGroupName = "Hepatitis B" },
            new() { ImmunizationId = "IMM-3", ImmunizationName = "Hep B Dose 3", CvxCode = "08", EventDateTime = new DateTime(2018, 7, 1), VaccineGroupName = "Hepatitis B" },
        };

        // Act
        ImmunizationForecastResult result = await grain.GenerateForecastAsync(patientDob, history);

        // Assert — at least one complete, and some due/overdue for other series
        Assert.That(result.Success, Is.True);
        Assert.That(result.TotalComplete, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.TotalDue + result.TotalOverdue, Is.GreaterThanOrEqualTo(1));
        // Verify counts match recommendation list
        int countComplete = result.Recommendations.Count(r => r.Status == "COMPLETE");
        int countDue = result.Recommendations.Count(r => r.Status == "DUE");
        int countOverdue = result.Recommendations.Count(r => r.Status == "OVERDUE");
        Assert.That(result.TotalComplete, Is.EqualTo(countComplete));
        Assert.That(result.TotalDue, Is.EqualTo(countDue));
        Assert.That(result.TotalOverdue, Is.EqualTo(countOverdue));
    }
}
