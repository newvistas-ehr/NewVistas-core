// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Eligibility;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;
using NewVistas.Abstractions.Security;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for the VA-aligned registration flow: registration with
/// <see cref="VaRegistrationEligibilityPolicy"/> wired into the silo. Verifies
/// that veteran-status hints on the <see cref="RegistrationRequest"/> drive
/// the §17.36 priority-group determination via
/// <see cref="IAutoEligibilityDeterminationGrain"/> and apply the result to
/// the patient's enrollment record automatically.
///
/// Uses a dedicated <see cref="TestCluster"/> (not <see cref="SharedCluster"/>)
/// so the VA policy can be wired in without affecting other tests.
/// </summary>
[TestFixture, NonParallelizable]
public class VaPatientRegistrationEligibilityTests
{
    public const string TestClusterPrefix = "099";
    public const string TestLocalClusterId = "VA-TEST-CLUSTER";

    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<VaPolicySiloConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
    }

    private IPatientRegistrationGrain Registration() =>
        _cluster.GrainFactory.GetGrain<IPatientRegistrationGrain>("REGISTRATION");

    private static RegistrationRequest VeteranRequest(int? scPercent = null)
    {
        string suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        return new RegistrationRequest
        {
            PatientName = $"VETERAN,SC{scPercent ?? 0}-{suffix}",
            Ssn = "111223333",
            DateOfBirth = new DateTime(1955, 3, 12),
            Sex = "M",
            FacilityDfn = $"DFN-{suffix}",
            IsVeteran = true,
            ServiceConnectedPercentage = scPercent,
        };
    }

    [Test]
    public async Task Register_ServiceConnected70Percent_AssignsPriorityGroup()
    {
        // SC ≥ 50% should land in Priority Group 1 (catastrophically rated).
        string icn = await Registration().RegisterPatientAsync(VeteranRequest(scPercent: 70));

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.PriorityGroup, Is.Not.Null.And.Not.Empty,
            "VA policy should assign a priority group for SC ≥ 50% veteran.");
        Assert.That(enrollment.CopayExempt, Is.True,
            "SC ≥ 50% veterans are copay-exempt.");
    }

    [Test]
    public async Task Register_NonVeteran_LeavesEnrollmentUntouched()
    {
        // VA policy short-circuits when IsVeteran is not true. Enrollment
        // record stays in its default Unverified state with no priority group.
        RegistrationRequest req = VeteranRequest();
        req.IsVeteran = false;

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Unverified));
        Assert.That(enrollment.PriorityGroup, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Register_VeteranWithIsVeteranNull_LeavesEnrollmentUntouched()
    {
        // The policy treats null IsVeteran the same as false — no enrollment
        // changes unless the caller explicitly opts in.
        RegistrationRequest req = VeteranRequest();
        req.IsVeteran = null;

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Unverified));
        Assert.That(enrollment.PriorityGroup, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Register_VeteranSetsVeteranInfoOnPatientState()
    {
        RegistrationRequest req = VeteranRequest(scPercent: 30);
        req.PrimaryEligibilityCode = "SC LESS THAN 50%";

        string icn = await Registration().RegisterPatientAsync(req);

        PatientState state = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn).GetPatientAsync();

        Assert.That(state.Veteran, Is.EqualTo("Y"),
            "Veteran flag should be stamped on patient state by VA policy.");
        Assert.That(state.ServiceConnectedPercentage, Is.EqualTo(30));
        Assert.That(state.PrimaryEligibilityCode, Is.EqualTo("SC LESS THAN 50%"));
    }

    [Test]
    public async Task Register_DeterminationRecordPersisted()
    {
        // The VA policy runs IAutoEligibilityDeterminationGrain.DetermineAsync,
        // which persists a determination record keyed "ELIG-DET:{icn}".
        string icn = await Registration().RegisterPatientAsync(VeteranRequest(scPercent: 60));

        AutoEligibilityDeterminationState det = await _cluster.GrainFactory
            .GetGrain<IAutoEligibilityDeterminationGrain>($"ELIG-DET:{icn}")
            .GetAsync();

        Assert.That(det.PatientId, Is.EqualTo(icn));
        Assert.That(det.IsServiceConnected50Plus, Is.True);
        Assert.That(det.ServiceConnectedPercent, Is.EqualTo(60));
    }

    // ── Configuration plumbing ───────────────────────────────────────────────

    private sealed class VaPolicySiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryGrainStorage("icnIssuerStore");
            siloBuilder.AddMemoryGrainStorage("mpiCorrelationStore");
            siloBuilder.AddMemoryGrainStorage("mpiSearchStore");
            siloBuilder.AddMemoryGrainStorage("patientStore");
            siloBuilder.AddMemoryGrainStorage("patientIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");
            siloBuilder.AddMemoryGrainStorage("patientEnrollmentStore");
            siloBuilder.AddMemoryGrainStorage("autoEligibilityDeterminationStore");
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");

            siloBuilder.Services.AddSingleton<IClusterIdentity>(
                new StaticClusterIdentity(TestLocalClusterId, TestClusterPrefix));
            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, NullClinicalEventReplicationSink>();
            siloBuilder.Services.AddSingleton<IDrugInteractionCacheService, DrugInteractionCacheService>();

            // The thing under test: VA-aligned eligibility policy.
            siloBuilder.Services.AddSingleton<IRegistrationEligibilityPolicy, VaRegistrationEligibilityPolicy>();
            siloBuilder.Services.AddSingleton<IMpiFederationAnnouncer, NoOpMpiFederationAnnouncer>();
            siloBuilder.Services.AddSingleton<IMpiInboundHandler, DefaultMpiInboundHandler>();
        }
    }
}
