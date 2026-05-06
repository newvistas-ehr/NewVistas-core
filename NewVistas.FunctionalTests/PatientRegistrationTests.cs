// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text.RegularExpressions;
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end registration tests for <see cref="IPatientRegistrationGrain"/>.
/// Verifies the per-cluster ICN issuance flow described in
/// <c>Docs/Architect-decisions/ADR-001-Patient-Identity-Strategy.md</c>:
///   - ICN is issued from the local cluster's range
///   - PatientGrain is keyed by the ICN
///   - DFN is preserved on PatientState as legacy data
///   - MpiCorrelationGrain is created and indexed in MpiSearchGrain
///   - The local facility is recorded in the correlation's facility list
/// </summary>
[TestFixture]
public class PatientRegistrationTests
{
    private TestCluster _cluster = null!;

    /// <summary>
    /// The 3-digit cluster prefix configured on the SharedCluster fixture.
    /// Every issued ICN must start with this value.
    /// </summary>
    public const string TestClusterPrefix = "099";

    /// <summary>
    /// The local cluster id configured on the SharedCluster fixture.
    /// Every facility-correlation row added at registration must reference this id.
    /// </summary>
    public const string TestLocalClusterId = "TEST-CLUSTER";

    private static readonly Regex IcnPattern =
        new(@"^[0-9]{10}V[0-9]{6}$", RegexOptions.Compiled);

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientRegistrationGrain Registration() =>
        _cluster.GrainFactory.GetGrain<IPatientRegistrationGrain>("REGISTRATION");

    private static RegistrationRequest NewRequest(string? dfnSuffix = null)
    {
        string suffix = dfnSuffix ?? Guid.NewGuid().ToString("N").Substring(0, 6);
        return new RegistrationRequest
        {
            PatientName = $"TESTPATIENT,REG-{suffix}",
            Ssn = "111223333",
            DateOfBirth = new DateTime(1955, 3, 12),
            Sex = "M",
            FacilityDfn = $"DFN-{suffix}",
        };
    }

    [Test]
    public async Task RegisterPatient_ReturnsIcnInExpectedFormat()
    {
        string icn = await Registration().RegisterPatientAsync(NewRequest());
        Assert.That(IcnPattern.IsMatch(icn), Is.True,
            $"Expected {{10digit}}V{{6digit}} ICN, got '{icn}'.");
    }

    [Test]
    public async Task RegisterPatient_IcnStartsWithLocalClusterPrefix()
    {
        string icn = await Registration().RegisterPatientAsync(NewRequest());
        Assert.That(icn.StartsWith(TestClusterPrefix), Is.True,
            $"ICN '{icn}' should start with cluster prefix '{TestClusterPrefix}'.");
    }

    [Test]
    public async Task RegisterPatient_PatientGrainKeyedByIcn()
    {
        RegistrationRequest req = NewRequest();
        string icn = await Registration().RegisterPatientAsync(req);

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(icn);
        PatientState state = await workflow.GetPatientAsync();

        Assert.That(state.Name, Is.EqualTo(req.PatientName));
    }

    [Test]
    public async Task RegisterPatient_DfnPreservedInPatientState()
    {
        RegistrationRequest req = NewRequest();
        string icn = await Registration().RegisterPatientAsync(req);

        PatientState state = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn).GetPatientAsync();

        Assert.That(state.Dfn, Is.EqualTo(req.FacilityDfn));
    }

    [Test]
    public async Task RegisterPatient_IcnPersistedInPatientState()
    {
        string icn = await Registration().RegisterPatientAsync(NewRequest());

        PatientState state = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn).GetPatientAsync();

        Assert.That(state.Icn, Is.EqualTo(icn));
    }

    [Test]
    public async Task RegisterPatient_DemographicsPopulated()
    {
        RegistrationRequest req = NewRequest();
        string icn = await Registration().RegisterPatientAsync(req);

        PatientState state = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn).GetPatientAsync();

        Assert.That(state.Name, Is.EqualTo(req.PatientName));
        Assert.That(state.SocialSecurityNumber, Is.EqualTo(req.Ssn));
        Assert.That(state.DateOfBirth, Is.EqualTo(req.DateOfBirth));
        Assert.That(state.Sex, Is.EqualTo(req.Sex));
    }

    [Test]
    public async Task RegisterPatient_CreatesMpiCorrelationRecord()
    {
        RegistrationRequest req = NewRequest();
        string icn = await Registration().RegisterPatientAsync(req);

        IMpiCorrelationGrain mpi = _cluster.GrainFactory
            .GetGrain<IMpiCorrelationGrain>($"MPI:{icn}");
        MpiCorrelationState state = await mpi.GetCorrelationAsync();

        Assert.That(state.Icn, Is.EqualTo(icn));
        Assert.That(state.PatientName, Is.EqualTo(req.PatientName));
        Assert.That(state.Ssn, Is.EqualTo(req.Ssn));
    }

    [Test]
    public async Task RegisterPatient_CorrelationContainsLocalFacility()
    {
        RegistrationRequest req = NewRequest();
        string icn = await Registration().RegisterPatientAsync(req);

        List<MpiLocalCorrelation> facilities = await _cluster.GrainFactory
            .GetGrain<IMpiCorrelationGrain>($"MPI:{icn}")
            .GetTreatingFacilitiesAsync();

        Assert.That(facilities, Has.Count.EqualTo(1));
        Assert.That(facilities[0].FacilityId, Is.EqualTo(TestLocalClusterId));
        Assert.That(facilities[0].LocalDfn, Is.EqualTo(req.FacilityDfn));
    }

    [Test]
    public async Task RegisterPatient_AddedToMpiSearchIndex()
    {
        RegistrationRequest req = NewRequest();
        string icn = await Registration().RegisterPatientAsync(req);

        MpiSearchResult? hit = await _cluster.GrainFactory
            .GetGrain<IMpiSearchGrain>("MPI-INDEX")
            .LookupByIcnAsync(icn);

        Assert.That(hit, Is.Not.Null);
        Assert.That(hit!.Icn, Is.EqualTo(icn));
        Assert.That(hit.PatientName, Is.EqualTo(req.PatientName));
    }

    [Test]
    public async Task RegisterPatient_TwoCallsProduceDistinctIcns()
    {
        string icnA = await Registration().RegisterPatientAsync(NewRequest());
        string icnB = await Registration().RegisterPatientAsync(NewRequest());

        Assert.That(icnB, Is.Not.EqualTo(icnA));
    }

    [Test]
    public async Task RegisterPatient_ExternallySuppliedIcn_BypassesIssuer()
    {
        const string externalIcn = "1234567890V999999";
        long sequenceBefore = await _cluster.GrainFactory
            .GetGrain<IIcnIssuerGrain>("ICN-ISSUER").PeekNextSequenceAsync();

        RegistrationRequest req = NewRequest();
        req.ExternallySuppliedIcn = externalIcn;
        string returned = await Registration().RegisterPatientAsync(req);

        long sequenceAfter = await _cluster.GrainFactory
            .GetGrain<IIcnIssuerGrain>("ICN-ISSUER").PeekNextSequenceAsync();

        Assert.That(returned, Is.EqualTo(externalIcn));
        Assert.That(sequenceAfter, Is.EqualTo(sequenceBefore),
            "Issuer sequence must not advance when an ICN is externally supplied.");
    }

    [Test]
    public async Task RegisterPatient_NoOpPolicy_LeavesEnrollmentUntouched_EvenForVeteran()
    {
        // SharedCluster registers NoOpRegistrationEligibilityPolicy. Even
        // when the request says IsVeteran with a 70% SC rating, the policy
        // must NOT initialize the enrollment record on a non-VA deployment.
        RegistrationRequest req = NewRequest();
        req.IsVeteran = true;
        req.ServiceConnectedPercentage = 70;
        req.IsPurpleHeart = true;

        string icn = await Registration().RegisterPatientAsync(req);

        PatientEnrollmentState enrollment = await _cluster.GrainFactory
            .GetGrain<IPatientWorkflowGrain>(icn)
            .GetEnrollmentAsync();

        Assert.That(enrollment.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Unverified),
            "NoOp policy must not touch enrollment status.");
        Assert.That(enrollment.PriorityGroup, Is.Null.Or.Empty,
            "NoOp policy must not assign a priority group.");
    }

    [Test]
    public void RegisterPatient_RejectsEmptyName()
    {
        RegistrationRequest req = NewRequest();
        req.PatientName = "";
        Assert.That(
            async () => await Registration().RegisterPatientAsync(req),
            Throws.InstanceOf<ArgumentException>()
                .Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public void RegisterPatient_RejectsEmptyDfn()
    {
        RegistrationRequest req = NewRequest();
        req.FacilityDfn = "";
        Assert.That(
            async () => await Registration().RegisterPatientAsync(req),
            Throws.InstanceOf<ArgumentException>()
                .Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }
}
