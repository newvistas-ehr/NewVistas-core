// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the NDW export run grain: package patient data into
/// per-domain CSVs, walk the lifecycle to Accepted/Rejected, and re-package
/// after rejection.
/// </summary>
[TestFixture]
public class NdwExportTests
{
    private TestCluster _cluster = null!;
    private string _outputDir = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
        _outputDir = Path.Combine(Path.GetTempPath(), $"newvistas-ndw-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        try { if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true); }
        catch { }
    }

    private INdwExportRunGrain Run(string id) =>
        _cluster.GrainFactory.GetGrain<INdwExportRunGrain>($"NDW-EXPORT:{id}");

    private async Task<string> SeedTwoPatientsAsync(string suffix)
    {
        // Use the index directly so we don't entangle with IhsTribalEligibilityPolicy.
        // SharedCluster runs NoOp, so registrations succeed without enrollment side
        // effects. The NDW export reads from patient state + index — that's all we need.
        var idx = _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");
        for (int i = 0; i < 2; i++)
        {
            string icn = $"NDW-PAT-{suffix}-{i}";
            await _cluster.GrainFactory.GetGrain<IPatientGrain>(icn).UpdateDemographicsAsync(
                $"NDWTEST,P{i}", "M", new DateTime(1970, 1, 1 + i), $"11122333{i}");
            await idx.AddOrUpdateAsync(new PatientIndexEntry
            {
                PatientId = icn,
                Name = $"NDWTEST,P{i}",
                DateOfBirth = new DateTime(1970, 1, 1 + i),
                Sex = "M",
                Icn = icn,
                IsActive = true,
            });
        }
        return suffix;
    }

    [Test]
    public async Task Package_OnSeededCohort_WritesThreeCsvFilesAndPersistsState()
    {
        await SeedTwoPatientsAsync("happy");
        string runId = $"happy-{Guid.NewGuid():N}";

        NdwExportRunState state = await Run(runId).PackageAsync(
            facilityId: "TRIBAL-HUB",
            periodStart: new DateTime(2026, 1, 1),
            periodEnd: new DateTime(2026, 12, 31),
            outputDirectory: _outputDir,
            packagedById: "NDW1", packagedByName: "NDW Coordinator");

        Assert.That(state.Status, Is.EqualTo(NdwExportRunStatus.Packaged));
        Assert.That(state.OutputDirectory, Is.Not.Null);
        Assert.That(Directory.Exists(state.OutputDirectory!), Is.True);
        Assert.That(state.Files.Select(f => f.FileName),
            Is.EquivalentTo(new[] { "patients.csv", "problems.csv", "immunizations.csv" }));
        foreach (NdwExportFile f in state.Files)
        {
            Assert.That(File.Exists(Path.Combine(state.OutputDirectory!, f.FileName)), Is.True,
                $"{f.FileName} should exist on disk");
            Assert.That(f.Sha256, Has.Length.EqualTo(64));
            Assert.That(f.FileSizeBytes, Is.GreaterThan(0));
        }
        Assert.That(state.PatientCount, Is.GreaterThanOrEqualTo(2));
        Assert.That(state.PackagingAttempts, Is.EqualTo(1));
        Assert.That(state.FormatVersion, Is.EqualTo("csv-v1"));
    }

    [Test]
    public async Task Package_OutputDirectoryNamePatternEncodesFacilityAndPeriod()
    {
        await SeedTwoPatientsAsync("dirname");
        string runId = $"dirname-{Guid.NewGuid():N}";

        NdwExportRunState state = await Run(runId).PackageAsync(
            "BEDFORD", new DateTime(2026, 1, 1), new DateTime(2026, 3, 31),
            _outputDir, "NDW1", "Coord");

        string runDirName = Path.GetFileName(state.OutputDirectory!);
        Assert.That(runDirName, Does.StartWith("ndw-BEDFORD-20260101-20260331-attempt01"));
    }

    [Test]
    public async Task Package_PatientsCsv_HasHeaderAndOneRowPerPatient()
    {
        await SeedTwoPatientsAsync("patientscsv");
        string runId = $"patientscsv-{Guid.NewGuid():N}";

        NdwExportRunState state = await Run(runId).PackageAsync(
            "TRIBAL-HUB", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");

        string patientsCsv = await File.ReadAllTextAsync(Path.Combine(state.OutputDirectory!, "patients.csv"));
        string[] lines = patientsCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Header + at least 2 patient rows (could be more from other tests; index is shared).
        Assert.That(lines[0], Does.Contain("Icn,Dfn,Name,Sex"));
        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public async Task RePackage_ProducesNewSubdirectory_AndPreservesPriorRun()
    {
        await SeedTwoPatientsAsync("repackage");
        string runId = $"repackage-{Guid.NewGuid():N}";

        NdwExportRunState first = await Run(runId).PackageAsync(
            "TRIBAL-HUB", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");
        NdwExportRunState second = await Run(runId).PackageAsync(
            "TRIBAL-HUB", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");

        Assert.That(second.PackagingAttempts, Is.EqualTo(2));
        Assert.That(second.OutputDirectory, Is.Not.EqualTo(first.OutputDirectory));
        Assert.That(Directory.Exists(first.OutputDirectory!), Is.True,
            "Prior attempt directory must remain on disk for audit.");
        Assert.That(Path.GetFileName(second.OutputDirectory!), Does.Contain("attempt02"));
    }

    [Test]
    public async Task RecordTransmission_AfterPackage_TransitionsToSubmitted()
    {
        await SeedTwoPatientsAsync("submit");
        string runId = $"submit-{Guid.NewGuid():N}";
        await Run(runId).PackageAsync("TRIBAL-HUB",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");

        await Run(runId).RecordTransmissionAsync(DateTime.UtcNow, "NDW-TRACK-XYZ");

        NdwExportRunState s = await Run(runId).GetAsync();
        Assert.That(s.Status, Is.EqualTo(NdwExportRunStatus.Submitted));
        Assert.That(s.TransmissionTrackingId, Is.EqualTo("NDW-TRACK-XYZ"));
    }

    [Test]
    public async Task RecordTransmission_BeforePackage_Throws()
    {
        string runId = $"no-package-{Guid.NewGuid():N}";
        Assert.That(
            async () => await Run(runId).RecordTransmissionAsync(DateTime.UtcNow, null),
            Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task RecordIhsResponse_Accepted_TransitionsToAccepted()
    {
        await SeedTwoPatientsAsync("accept");
        string runId = $"accept-{Guid.NewGuid():N}";
        await Run(runId).PackageAsync("TRIBAL-HUB",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");
        await Run(runId).RecordTransmissionAsync(DateTime.UtcNow, "NDW-T-1");

        await Run(runId).RecordIhsResponseAsync(DateTime.UtcNow, accepted: true,
            responseReceipt: "NDW ACCEPTED. Confirmation NDW-2026-09988.");

        NdwExportRunState s = await Run(runId).GetAsync();
        Assert.That(s.Status, Is.EqualTo(NdwExportRunStatus.Accepted));
        Assert.That(s.IhsAccepted, Is.True);
    }

    [Test]
    public async Task RecordIhsResponse_Rejected_TransitionsToRejected_AndAllowsRePackage()
    {
        await SeedTwoPatientsAsync("rejected");
        string runId = $"rejected-{Guid.NewGuid():N}";
        await Run(runId).PackageAsync("TRIBAL-HUB",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");
        await Run(runId).RecordTransmissionAsync(DateTime.UtcNow, "NDW-T-1");
        await Run(runId).RecordIhsResponseAsync(DateTime.UtcNow, accepted: false,
            responseReceipt: "REJECTED: patients.csv row 5 — invalid SSN format.");

        NdwExportRunState rejected = await Run(runId).GetAsync();
        Assert.That(rejected.Status, Is.EqualTo(NdwExportRunStatus.Rejected));

        // Re-package after rejection should work and increment the attempt counter.
        NdwExportRunState repackaged = await Run(runId).PackageAsync(
            "TRIBAL-HUB", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");
        Assert.That(repackaged.Status, Is.EqualTo(NdwExportRunStatus.Packaged));
        Assert.That(repackaged.PackagingAttempts, Is.EqualTo(2));
    }

    [Test]
    public async Task RecordIhsResponse_BeforeTransmission_Throws()
    {
        await SeedTwoPatientsAsync("response-no-transmit");
        string runId = $"resp-no-transmit-{Guid.NewGuid():N}";
        await Run(runId).PackageAsync("TRIBAL-HUB",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            _outputDir, "NDW1", "Coord");
        Assert.That(
            async () => await Run(runId).RecordIhsResponseAsync(DateTime.UtcNow, true, null),
            Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public void Package_PeriodEndBeforeStart_Throws()
    {
        string runId = $"bad-period-{Guid.NewGuid():N}";
        Assert.That(
            async () => await Run(runId).PackageAsync(
                "TRIBAL-HUB", new DateTime(2026, 12, 31), new DateTime(2026, 1, 1),
                _outputDir, "NDW1", "Coord"),
            Throws.InstanceOf<ArgumentException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public void Package_EmptyFacilityId_Throws()
    {
        string runId = $"no-facility-{Guid.NewGuid():N}";
        Assert.That(
            async () => await Run(runId).PackageAsync(
                "", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
                _outputDir, "NDW1", "Coord"),
            Throws.InstanceOf<ArgumentException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }
}
