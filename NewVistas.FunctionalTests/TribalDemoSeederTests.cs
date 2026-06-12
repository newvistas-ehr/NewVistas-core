// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Eligibility;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for <see cref="ITribalDemoSeederGrain"/>. Two flavors:
///
///   • Synthetic-manifest tests write small fixed JSON to a temp directory and
///     validate the seeder behaves correctly (idempotent ICN derivation,
///     CHS approve/deny, GPRA report creation, error tolerance).
///   • Real-manifest test loads the actual <c>exports/TribalDemo/</c> manifest
///     from the repository to confirm it is well-formed and the documented
///     eligibility distribution matches what the policy actually stamps.
///
/// Uses a dedicated <see cref="TestCluster"/> wired with
/// <see cref="IhsTribalEligibilityPolicy"/> so the full pipeline is exercised.
/// </summary>
[TestFixture, NonParallelizable]
public class TribalDemoSeederTests
{
    public const string TestClusterPrefix = "910";
    public const string TestLocalClusterId = "TRIBAL-DEMO-SEEDER-TEST";

    private TestCluster _cluster = null!;
    private string _tempManifestDir = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<TribalSeederSiloConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();

        _tempManifestDir = Path.Combine(Path.GetTempPath(), $"newvistas-tribal-demo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempManifestDir);
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
        try
        {
            if (Directory.Exists(_tempManifestDir))
                Directory.Delete(_tempManifestDir, recursive: true);
        }
        catch { /* best-effort */ }
    }

    private ITribalDemoSeederGrain Seeder() =>
        _cluster.GrainFactory.GetGrain<ITribalDemoSeederGrain>("TRIBAL-DEMO-SEEDER");

    private string WriteSyntheticManifest(string subdir)
    {
        string dir = Path.Combine(_tempManifestDir, subdir);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public void Load_NonExistentDirectory_Throws()
    {
        Assert.That(
            async () => await Seeder().LoadAsync(
                Path.Combine(_tempManifestDir, "does-not-exist"),
                "ADMIN1", "Admin"),
            Throws.InstanceOf<DirectoryNotFoundException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task Load_PatientsOnly_RegistersAllAndAssignsDeterministicIcns()
    {
        string dir = WriteSyntheticManifest("patients-only");
        var patients = new object[]
        {
            new { patientName = "BEGAY,JOHN A", ssn = "111223333", dateOfBirth = "1965-03-12",
                  sex = "M", facilityDfn = "DFN-001",
                  isTribalMember = true, tribalAffiliation = "Navajo Nation",
                  residesInChsda = true, chsdaResidencyDays = 365 },
            new { patientName = "WALKER,MARY B", ssn = "222334444", dateOfBirth = "1978-08-22",
                  sex = "F", facilityDfn = "DFN-002",
                  isTribalMember = true, tribalAffiliation = "Cherokee Nation",
                  residesInChsda = false },
        };
        await File.WriteAllTextAsync(Path.Combine(dir, "patients.json"),
            JsonSerializer.Serialize(patients));

        TribalDemoSeedResult result = await Seeder().LoadAsync(dir, "ADMIN1", "Admin");

        Assert.That(result.PatientsRegistered, Is.EqualTo(2));
        Assert.That(result.PatientIcns, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Is.Empty);
        // ICNs are deterministic from index → re-loading the same manifest produces the same ICNs.
        Assert.That(result.PatientIcns[0], Does.StartWith(TestClusterPrefix).Or.Match("^099"));
    }

    [Test]
    public async Task Load_TribalMemberWithChsdaResidency_StampsIhsChsEligibility()
    {
        string dir = WriteSyntheticManifest("chs-eligibility");
        await File.WriteAllTextAsync(Path.Combine(dir, "patients.json"),
            JsonSerializer.Serialize(new[]
            {
                new { patientName = "CHSELIGIBLE,TEST", ssn = "111223333",
                      dateOfBirth = "1960-01-01", sex = "F", facilityDfn = "DFN-CHS",
                      isTribalMember = true, tribalAffiliation = "Navajo Nation",
                      residesInChsda = true, chsdaResidencyDays = 365 }
            }));

        TribalDemoSeedResult result = await Seeder().LoadAsync(dir, "ADMIN1", "Admin");

        string icn = result.PatientIcns[0];
        PatientState state = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn).GetPatientAsync();
        Assert.That(state.PrimaryEligibilityCode, Is.EqualTo(IhsTribalEligibilityPolicy.ChsEligibleCode));
    }

    [Test]
    public async Task Load_TribalMemberWithoutChsdaResidency_StampsDirectCare()
    {
        string dir = WriteSyntheticManifest("direct-only");
        await File.WriteAllTextAsync(Path.Combine(dir, "patients.json"),
            JsonSerializer.Serialize(new[]
            {
                new { patientName = "DIRECTONLY,TEST", ssn = "111223334",
                      dateOfBirth = "1970-01-01", sex = "M", facilityDfn = "DFN-DIRECT",
                      isTribalMember = true, tribalAffiliation = "Cherokee Nation",
                      residesInChsda = false }
            }));

        TribalDemoSeedResult result = await Seeder().LoadAsync(dir, "ADMIN1", "Admin");

        PatientState state = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(result.PatientIcns[0]).GetPatientAsync();
        Assert.That(state.PrimaryEligibilityCode, Is.EqualTo(IhsTribalEligibilityPolicy.DirectCareCode));
    }

    [Test]
    public async Task Load_ChsReferrals_CreatesAndApprovesAndDenies()
    {
        string dir = WriteSyntheticManifest("chs-referrals");
        // Need at least 3 patients to support patientIndex 1, 2, 3 in the referrals.
        await File.WriteAllTextAsync(Path.Combine(dir, "patients.json"),
            JsonSerializer.Serialize(new[]
            {
                new { patientName = "P1,A", ssn = "111223335", dateOfBirth = "1960-01-01",
                      sex = "M", facilityDfn = "D1",
                      isTribalMember = true, tribalAffiliation = "Cherokee",
                      residesInChsda = true, chsdaResidencyDays = 200 },
                new { patientName = "P2,B", ssn = "111223336", dateOfBirth = "1965-01-01",
                      sex = "F", facilityDfn = "D2",
                      isTribalMember = true, tribalAffiliation = "Cherokee",
                      residesInChsda = true, chsdaResidencyDays = 200 },
                new { patientName = "P3,C", ssn = "111223337", dateOfBirth = "1970-01-01",
                      sex = "M", facilityDfn = "D3",
                      isTribalMember = true, tribalAffiliation = "Cherokee",
                      residesInChsda = true, chsdaResidencyDays = 200 },
            }));
        await File.WriteAllTextAsync(Path.Combine(dir, "chs-referrals.json"),
            JsonSerializer.Serialize(new object[]
            {
                new { patientIndex = 1, referralType = "SPECIALTY",
                      externalFacilityName = "ExtFac", purpose = "Test",
                      urgency = "ROUTINE", estimatedCost = 100m,
                      medicalPriorityClass = "II", alternateResourcesChecked = true,
                      approve = true, authorizedAmount = 100m,
                      authorizationNumber = "CHS-T-1" },
                new { patientIndex = 2, referralType = "SPECIALTY",
                      externalFacilityName = "ExtFac", purpose = "Test denied",
                      urgency = "ROUTINE", estimatedCost = 5000m,
                      medicalPriorityClass = "V", alternateResourcesChecked = false,
                      approve = false, denialReason = "Class V excluded" },
                new { patientIndex = 3, referralType = "SPECIALTY",
                      externalFacilityName = "ExtFac", purpose = "Test pending",
                      urgency = "ROUTINE", estimatedCost = 200m,
                      medicalPriorityClass = "III", alternateResourcesChecked = true,
                      approve = (bool?)null },
            }));

        TribalDemoSeedResult result = await Seeder().LoadAsync(dir, "ADMIN1", "Admin");

        Assert.That(result.PatientsRegistered, Is.EqualTo(3));
        Assert.That(result.ChsReferralsCreated, Is.EqualTo(3));
        Assert.That(result.ChsReferralsApproved, Is.EqualTo(1));
        Assert.That(result.ChsReferralsDenied, Is.EqualTo(1));
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public async Task Load_GpraReport_CreatesCompletedReport()
    {
        string dir = WriteSyntheticManifest("gpra-report");
        var report = new
        {
            reportId = "fy2026-q1-test",
            fiscalYear = 2026,
            reportingPeriod = "Quarter1",
            currentPeriodStart = "2025-10-01",
            currentPeriodEnd = "2025-12-31",
            baselinePeriodStart = "2022-10-01",
            baselinePeriodEnd = "2022-12-31",
            facilityId = "TEST-FAC",
            facilityName = "Test Facility",
            communityTaxonomy = (string?)null,
            activeUserPopulation = 1000,
            generatedById = "QM1",
            generatedByName = "QC",
            indicators = new[]
            {
                new {
                    measureId = "GPRA-T-01", title = "Test indicator",
                    category = "Diabetes",
                    currentDenominator = 100, currentNumerator = 75, currentPerformanceRate = 75.0m,
                    baselineDenominator = 90, baselineNumerator = 60, baselinePerformanceRate = 66.67m,
                    percentagePointChange = 8.33m, isImproved = true,
                    targetRate = (decimal?)80m, targetMet = false
                }
            }
        };
        await File.WriteAllTextAsync(Path.Combine(dir, "gpra-report.json"),
            JsonSerializer.Serialize(report));

        TribalDemoSeedResult result = await Seeder().LoadAsync(dir, "ADMIN1", "Admin");

        Assert.That(result.GpraReportsCreated, Is.EqualTo(1));
        Assert.That(result.Errors, Is.Empty);

        GpraReportState state = await _cluster.GrainFactory
            .GetGrain<IGpraReportGrain>("GPRA-REPORT:fy2026-q1-test").GetAsync();
        Assert.That(state.Status, Is.EqualTo(GpraReportStatus.Completed));
        Assert.That(state.Indicators, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Load_IsIdempotent_SameManifestProducesSameIcns()
    {
        string dir = WriteSyntheticManifest("idempotent");
        await File.WriteAllTextAsync(Path.Combine(dir, "patients.json"),
            JsonSerializer.Serialize(new[]
            {
                new { patientName = "IDEMP,A", ssn = "111223338",
                      dateOfBirth = "1960-01-01", sex = "M", facilityDfn = "D1" },
                new { patientName = "IDEMP,B", ssn = "111223339",
                      dateOfBirth = "1965-01-01", sex = "F", facilityDfn = "D2" },
            }));

        TribalDemoSeedResult first = await Seeder().LoadAsync(dir, "ADMIN1", "Admin");
        TribalDemoSeedResult second = await Seeder().LoadAsync(dir, "ADMIN1", "Admin");

        Assert.That(second.PatientIcns, Is.EqualTo(first.PatientIcns),
            "Same manifest must produce same ICNs (deterministic externally-supplied ICN by index).");
    }

    [Test]
    public async Task Load_RealRepoManifest_LoadsWithoutErrorsAndMatchesDocumentedDistribution()
    {
        // Locate exports/TribalDemo from the test runtime working directory.
        // Tests run from NewVistas.FunctionalTests/bin/Debug/net10.0; repo
        // root is 4 levels up.
        string testBaseDir = AppContext.BaseDirectory;
        string repoRoot = Path.GetFullPath(Path.Combine(testBaseDir, "..", "..", "..", ".."));
        string manifestDir = Path.Combine(repoRoot, "exports", "TribalDemo");

        if (!Directory.Exists(manifestDir))
            Assert.Inconclusive($"Real manifest directory not found at {manifestDir}; skipping.");

        TribalDemoSeedResult result = await Seeder().LoadAsync(manifestDir, "ADMIN1", "Admin");

        Assert.That(result.Errors, Is.Empty,
            "Real manifest should load without errors. Errors: " + string.Join("; ", result.Errors));
        Assert.That(result.PatientsRegistered, Is.EqualTo(50),
            "patients.json should contain 50 patients per the README.");
        Assert.That(result.ChsReferralsCreated, Is.EqualTo(8),
            "chs-referrals.json should contain 8 referrals per the README.");
        Assert.That(result.ChsReferralsApproved, Is.EqualTo(6),
            "6 of the 8 demo referrals are marked approve=true per the README.");
        Assert.That(result.ChsReferralsDenied, Is.EqualTo(2),
            "2 of the 8 demo referrals are marked approve=false per the README.");
        Assert.That(result.GpraReportsCreated, Is.EqualTo(1));

        // Eligibility distribution check: count patients by their stamped code.
        int chsEligible = 0, directCare = 0, noEnrollment = 0;
        foreach (string icn in result.PatientIcns)
        {
            PatientState pat = await _cluster.GrainFactory
                .GetGrain<IPatientWorkflowGrain>(icn).GetPatientAsync();
            string code = pat.PrimaryEligibilityCode ?? string.Empty;
            if (code == IhsTribalEligibilityPolicy.ChsEligibleCode) chsEligible++;
            else if (code == IhsTribalEligibilityPolicy.DirectCareCode) directCare++;
            else noEnrollment++;
        }

        // Per the README: 28 CHS-eligible, 12 direct-only, 3 by-category (also direct), 7 walk-in.
        Assert.That(chsEligible, Is.EqualTo(28), "CHS-eligible count should match README documented distribution.");
        Assert.That(directCare, Is.EqualTo(15), "Direct-care count should be 12 tribal-direct + 3 by-category = 15.");
        Assert.That(noEnrollment, Is.EqualTo(7), "Walk-in count should match README.");
    }

    // ── Configuration plumbing ───────────────────────────────────────────────

    private sealed class TribalSeederSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryGrainStorage("icnIssuerStore");
            siloBuilder.AddMemoryGrainStorage("mpiCorrelationStore");
            siloBuilder.AddMemoryGrainStorage("mpiSearchStore");
            siloBuilder.AddMemoryGrainStorage("patientStore");
            siloBuilder.AddMemoryGrainStorage("patientHistoryIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");
            siloBuilder.AddMemoryGrainStorage("patientEnrollmentStore");
            siloBuilder.AddMemoryGrainStorage("autoEligibilityDeterminationStore");
            siloBuilder.AddMemoryGrainStorage("externalReferralStore");
            siloBuilder.AddMemoryGrainStorage("externalReferralIndexStore");
            siloBuilder.AddMemoryGrainStorage("gpraReportStore");
            siloBuilder.AddMemoryGrainStorage("gpraReportIndexStore");
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");

            siloBuilder.Services.AddSingleton<IClusterIdentity>(
                new StaticClusterIdentity(TestLocalClusterId, TestClusterPrefix));
            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, NullClinicalEventReplicationSink>();
            siloBuilder.Services.AddSingleton<IDrugInteractionCacheService, DrugInteractionCacheService>();
            siloBuilder.Services.AddSingleton<IRegistrationEligibilityPolicy, IhsTribalEligibilityPolicy>();
            siloBuilder.Services.AddSingleton<IMpiFederationAnnouncer, NoOpMpiFederationAnnouncer>();
            siloBuilder.Services.AddSingleton<IMpiInboundHandler, DefaultMpiInboundHandler>();
        }
    }
}
