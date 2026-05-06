// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for <see cref="IGpraSubmissionGrain"/>: read a completed
/// GPRA report, format it via the registered formatter, write the file to
/// disk, and walk the lifecycle (Packaged → Submitted → Accepted/Rejected).
/// </summary>
[TestFixture]
public class GpraSubmissionTests
{
    private TestCluster _cluster = null!;
    private string _outputDir = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
        _outputDir = Path.Combine(Path.GetTempPath(), $"newvistas-gpra-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        try
        {
            if (Directory.Exists(_outputDir))
                Directory.Delete(_outputDir, recursive: true);
        }
        catch { /* best-effort cleanup */ }
    }

    private IGpraReportGrain Report(string id) =>
        _cluster.GrainFactory.GetGrain<IGpraReportGrain>($"GPRA-REPORT:{id}");

    private IGpraSubmissionGrain Submission(string id) =>
        _cluster.GrainFactory.GetGrain<IGpraSubmissionGrain>($"GPRA-SUB:{id}");

    private async Task<string> CreateCompletedReportAsync(string suffix)
    {
        string reportId = $"fy2026-q1-{suffix}";
        await Report(reportId).CreateAsync(
            fiscalYear: 2026,
            reportingPeriod: GpraReportingPeriod.Quarter1,
            currentPeriodStart: new DateTime(2025, 10, 1),
            currentPeriodEnd: new DateTime(2025, 12, 31),
            baselinePeriodStart: new DateTime(2022, 10, 1),
            baselinePeriodEnd: new DateTime(2022, 12, 31),
            facilityId: "TRIBAL-HUB",
            facilityName: "Tribal Health Authority Hub",
            communityTaxonomy: "AUTTAX-DEMO",
            activeUserPopulation: 5000,
            generatedById: "QM1",
            generatedByName: "Quality Coordinator");

        await Report(reportId).AddIndicatorResultAsync(new GpraIndicatorResult
        {
            MeasureId = "GPRA-DM-01",
            Title = "Diabetes: HbA1c Testing",
            Category = GpraClinicalCategory.Diabetes,
            CurrentDenominator = 200,
            CurrentNumerator = 150,
            CurrentPerformanceRate = 75.0m,
            BaselineDenominator = 180,
            BaselineNumerator = 120,
            BaselinePerformanceRate = 66.67m,
            PercentagePointChange = 8.33m,
            IsImproved = true,
            TargetRate = 80m,
            TargetMet = false,
        });

        await Report(reportId).CompleteAsync();
        return reportId;
    }

    [Test]
    public async Task Package_OnCompletedReport_WritesFileAndPersistsState()
    {
        string reportId = await CreateCompletedReportAsync("happy");

        GpraSubmissionState submission = await Submission(reportId).PackageAsync(
            reportId, _outputDir, "QM1", "Quality Coordinator");

        Assert.That(submission.Status, Is.EqualTo(GpraSubmissionStatus.Packaged));
        Assert.That(submission.FilePath, Is.Not.Null);
        Assert.That(File.Exists(submission.FilePath!), Is.True,
            "Submission file should exist on disk after packaging.");
        Assert.That(submission.FormatVersion, Is.EqualTo("csv-v1"));
        Assert.That(submission.FileSizeBytes, Is.GreaterThan(0));
        Assert.That(submission.FileSha256, Has.Length.EqualTo(64));   // SHA-256 hex
        Assert.That(submission.PackagingAttempts, Is.EqualTo(1));
    }

    [Test]
    public async Task Package_FilenameEncodesFiscalYearAndAttempt()
    {
        string reportId = await CreateCompletedReportAsync("filename");
        GpraSubmissionState s = await Submission(reportId).PackageAsync(
            reportId, _outputDir, "QM1", "QC");

        string fileName = Path.GetFileName(s.FilePath!);
        Assert.That(fileName, Does.StartWith("gpra-fy2026-Quarter1-TRIBAL-HUB-attempt01-"));
        Assert.That(fileName, Does.EndWith(".csv"));
    }

    [Test]
    public async Task Package_RePackaging_IncrementsAttemptAndProducesNewFile()
    {
        string reportId = await CreateCompletedReportAsync("repackage");

        GpraSubmissionState first = await Submission(reportId).PackageAsync(
            reportId, _outputDir, "QM1", "QC");
        // Sleep just enough so the timestamp suffix differs.
        await Task.Delay(1100);
        GpraSubmissionState second = await Submission(reportId).PackageAsync(
            reportId, _outputDir, "QM1", "QC");

        Assert.That(second.PackagingAttempts, Is.EqualTo(2));
        Assert.That(second.FilePath, Is.Not.EqualTo(first.FilePath),
            "Re-packaging must produce a distinct filename so the prior submission file is preserved for audit.");
        Assert.That(File.Exists(first.FilePath!), Is.True,
            "Prior packaged file must remain on disk for audit.");
        Assert.That(File.Exists(second.FilePath!), Is.True);
    }

    [Test]
    public async Task Package_UnknownReport_Throws()
    {
        // No CreateAsync call → report grain has no state → formatter rejects (no indicators).
        const string reportId = "fy2026-nonexistent";
        Assert.That(
            async () => await Submission(reportId).PackageAsync(
                reportId, _outputDir, "QM1", "QC"),
            Throws.InstanceOf<ArgumentException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task RecordTransmission_AfterPackage_TransitionsToSubmitted()
    {
        string reportId = await CreateCompletedReportAsync("submit");
        await Submission(reportId).PackageAsync(reportId, _outputDir, "QM1", "QC");

        await Submission(reportId).RecordTransmissionAsync(
            DateTime.UtcNow, "IHS-PORTAL-RECEIPT-12345");

        GpraSubmissionState s = await Submission(reportId).GetAsync();
        Assert.That(s.Status, Is.EqualTo(GpraSubmissionStatus.Submitted));
        Assert.That(s.TransmissionTrackingId, Is.EqualTo("IHS-PORTAL-RECEIPT-12345"));
        Assert.That(s.TransmissionDate, Is.Not.Null);
    }

    [Test]
    public async Task RecordTransmission_BeforePackage_Throws()
    {
        string reportId = await CreateCompletedReportAsync("transmit-no-package");
        // Note: do NOT call PackageAsync.
        Assert.That(
            async () => await Submission(reportId).RecordTransmissionAsync(DateTime.UtcNow, null),
            Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task RecordIhsResponse_Accepted_TransitionsToAccepted()
    {
        string reportId = await CreateCompletedReportAsync("accepted");
        await Submission(reportId).PackageAsync(reportId, _outputDir, "QM1", "QC");
        await Submission(reportId).RecordTransmissionAsync(DateTime.UtcNow, "TRACK-01");

        await Submission(reportId).RecordIhsResponseAsync(
            DateTime.UtcNow, accepted: true, responseReceipt: "ACCEPTED. Confirmation 99887.");

        GpraSubmissionState s = await Submission(reportId).GetAsync();
        Assert.That(s.Status, Is.EqualTo(GpraSubmissionStatus.Accepted));
        Assert.That(s.IhsAccepted, Is.True);
        Assert.That(s.IhsResponseReceipt, Does.Contain("Confirmation 99887"));
    }

    [Test]
    public async Task RecordIhsResponse_Rejected_TransitionsToRejected()
    {
        string reportId = await CreateCompletedReportAsync("rejected");
        await Submission(reportId).PackageAsync(reportId, _outputDir, "QM1", "QC");
        await Submission(reportId).RecordTransmissionAsync(DateTime.UtcNow, "TRACK-02");

        await Submission(reportId).RecordIhsResponseAsync(
            DateTime.UtcNow, accepted: false, responseReceipt: "REJECTED: indicator GPRA-DM-01 numerator > denominator.");

        GpraSubmissionState s = await Submission(reportId).GetAsync();
        Assert.That(s.Status, Is.EqualTo(GpraSubmissionStatus.Rejected));
        Assert.That(s.IhsAccepted, Is.False);
    }

    [Test]
    public async Task RecordIhsResponse_BeforeTransmission_Throws()
    {
        string reportId = await CreateCompletedReportAsync("response-no-transmit");
        await Submission(reportId).PackageAsync(reportId, _outputDir, "QM1", "QC");
        // Do NOT call RecordTransmissionAsync.
        Assert.That(
            async () => await Submission(reportId).RecordIhsResponseAsync(DateTime.UtcNow, true, null),
            Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task RejectedSubmission_CanBeRePackaged_AndReSubmitted()
    {
        string reportId = await CreateCompletedReportAsync("retry");
        await Submission(reportId).PackageAsync(reportId, _outputDir, "QM1", "QC");
        await Submission(reportId).RecordTransmissionAsync(DateTime.UtcNow, "TRACK-A");
        await Submission(reportId).RecordIhsResponseAsync(DateTime.UtcNow, false, "REJECTED");

        // Re-package after rejection (operator fixed whatever IHS flagged).
        await Task.Delay(1100);
        GpraSubmissionState repackaged = await Submission(reportId).PackageAsync(
            reportId, _outputDir, "QM1", "QC");

        Assert.That(repackaged.Status, Is.EqualTo(GpraSubmissionStatus.Packaged));
        Assert.That(repackaged.PackagingAttempts, Is.EqualTo(2));

        // Re-record transmission and acceptance.
        await Submission(reportId).RecordTransmissionAsync(DateTime.UtcNow, "TRACK-B");
        await Submission(reportId).RecordIhsResponseAsync(DateTime.UtcNow, true, "ACCEPTED on retry");

        GpraSubmissionState final = await Submission(reportId).GetAsync();
        Assert.That(final.Status, Is.EqualTo(GpraSubmissionStatus.Accepted));
    }

    [Test]
    public async Task PackagedFile_ContainsExpectedHeader()
    {
        string reportId = await CreateCompletedReportAsync("filebody");
        GpraSubmissionState s = await Submission(reportId).PackageAsync(
            reportId, _outputDir, "QM1", "QC");

        string contents = await File.ReadAllTextAsync(s.FilePath!);
        Assert.That(contents, Does.Contain("# FiscalYear,2026"));
        Assert.That(contents, Does.Contain("# FacilityId,TRIBAL-HUB"));
        Assert.That(contents, Does.Contain("MeasureId,Title,Category"));
        Assert.That(contents, Does.Contain("GPRA-DM-01"));
    }
}
