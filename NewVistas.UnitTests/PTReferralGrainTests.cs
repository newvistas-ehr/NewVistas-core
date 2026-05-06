// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for PT Referral grains — external referring provider referrals with
/// visit authorization tracking. Tests the IPTReferralGrain, IPTReferralIndexGrain,
/// and the referral-linked session workflow through IPTWorkflowGrain.
/// </summary>
[TestFixture]
public class PTReferralGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPTWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPTWorkflowGrain>(patientId);

    private IPTReferralGrain GetReferralGrain(string key) =>
        _cluster.GrainFactory.GetGrain<IPTReferralGrain>(key);

    private async Task<string> CreateTestReferralAsync(
        string patientId,
        string? diagnosis = "M54.5 Low back pain",
        int authorizedVisits = 12,
        List<BodyGroup>? bodyGroups = null)
    {
        IPTWorkflowGrain workflow = GetWorkflow(patientId);
        return await workflow.CreateReferralAsync(
            patientName: "DOE,JOHN",
            referringProviderName: "Dr. Smith",
            referringProviderId: "NPI-1234567890",
            referringProviderSpecialty: "Orthopedics",
            referringFacilityName: "Community Orthopedic Associates",
            diagnosis: diagnosis,
            diagnosisCode: "M54.5",
            bodyGroups: bodyGroups ?? new List<BodyGroup> { BodyGroup.LumbarSpine },
            reasonForReferral: "Evaluate and treat for chronic low back pain",
            precautions: null,
            authorizedVisits: authorizedVisits,
            authorizationExpirationDate: DateTime.UtcNow.AddMonths(3),
            referralDate: DateTime.UtcNow.AddDays(-2),
            receivedDate: DateTime.UtcNow,
            notes: "Patient prefers morning appointments");
    }

    [Test]
    public async Task PTReferralGrain_CreatesReferral()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";

        // Act
        string referralKey = await CreateTestReferralAsync(patientId);

        // Assert
        PTReferralState state = await GetReferralGrain(referralKey).GetReferralAsync();
        Assert.That(state.ReferralId, Is.EqualTo(referralKey));
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(state.ReferringProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.ReferringProviderId, Is.EqualTo("NPI-1234567890"));
        Assert.That(state.ReferringProviderSpecialty, Is.EqualTo("Orthopedics"));
        Assert.That(state.ReferringFacilityName, Is.EqualTo("Community Orthopedic Associates"));
        Assert.That(state.Diagnosis, Is.EqualTo("M54.5 Low back pain"));
        Assert.That(state.DiagnosisCode, Is.EqualTo("M54.5"));
        Assert.That(state.BodyGroups, Has.Count.EqualTo(1));
        Assert.That(state.BodyGroups, Contains.Item(BodyGroup.LumbarSpine));
        Assert.That(state.ReasonForReferral, Is.EqualTo("Evaluate and treat for chronic low back pain"));
        Assert.That(state.AuthorizedVisits, Is.EqualTo(12));
        Assert.That(state.UsedVisits, Is.EqualTo(0));
        Assert.That(state.Status, Is.EqualTo(PTReferralStatus.Active));
        Assert.That(state.Notes, Is.EqualTo("Patient prefers morning appointments"));
    }

    [Test]
    public async Task PTReferralGrain_GetReferralAsync()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        string referralKey = await CreateTestReferralAsync(patientId);

        // Act
        PTReferralState state = await GetWorkflow(patientId).GetReferralAsync(referralKey);

        // Assert
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.Diagnosis, Is.EqualTo("M54.5 Low back pain"));
    }

    [Test]
    public async Task PTReferralGrain_IncrementVisitCount()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        string referralKey = await CreateTestReferralAsync(patientId, authorizedVisits: 12);

        // Act
        IPTReferralGrain grain = GetReferralGrain(referralKey);
        int count1 = await grain.IncrementVisitCountAsync();
        int count2 = await grain.IncrementVisitCountAsync();
        int count3 = await grain.IncrementVisitCountAsync();

        // Assert
        Assert.That(count1, Is.EqualTo(1));
        Assert.That(count2, Is.EqualTo(2));
        Assert.That(count3, Is.EqualTo(3));

        PTReferralState state = await grain.GetReferralAsync();
        Assert.That(state.UsedVisits, Is.EqualTo(3));
    }

    [Test]
    public async Task PTReferralGrain_RecordSessionWithReferral()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        string referralKey = await CreateTestReferralAsync(patientId, authorizedVisits: 12);

        // Act — record a session linked to the referral
        IPTWorkflowGrain workflow = GetWorkflow(patientId);
        string sessionKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.LumbarSpine,
            DateTime.UtcNow,
            "THER-001", "Jane PT",
            "LOC-001", "Main Clinic",
            Laterality.Bilateral,
            new List<RomMeasurement>
            {
                new() { Movement = Movement.Flexion, ActiveRom = 40m }
            },
            new List<StrengthMeasurement>(),
            "Initial evaluation",
            referralKey);

        // Assert — session has referral link
        IPTSessionGrain sessionGrain = _cluster.GrainFactory.GetGrain<IPTSessionGrain>(sessionKey);
        PTSessionState sessionState = await sessionGrain.GetSessionAsync();
        Assert.That(sessionState.ReferralId, Is.EqualTo(referralKey));

        // Assert — referral visit count incremented
        PTReferralState referralState = await GetReferralGrain(referralKey).GetReferralAsync();
        Assert.That(referralState.UsedVisits, Is.EqualTo(1));
    }

    [Test]
    public async Task PTReferralGrain_RecordSessionWithoutReferral()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";

        // Act — record a session with no referral
        IPTWorkflowGrain workflow = GetWorkflow(patientId);
        string sessionKey = await workflow.RecordBodyGroupSessionAsync(
            BodyGroup.Shoulder,
            DateTime.UtcNow,
            "THER-001", "Jane PT",
            "LOC-001", "Main Clinic",
            Laterality.Left,
            new List<RomMeasurement>
            {
                new() { Movement = Movement.Flexion, ActiveRom = 150m }
            },
            new List<StrengthMeasurement>(),
            "Follow-up",
            null);

        // Assert — session has no referral link
        IPTSessionGrain sessionGrain = _cluster.GrainFactory.GetGrain<IPTSessionGrain>(sessionKey);
        PTSessionState sessionState = await sessionGrain.GetSessionAsync();
        Assert.That(sessionState.ReferralId, Is.Null);
    }

    [Test]
    public async Task PTReferralGrain_UpdateStatus()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        string referralKey = await CreateTestReferralAsync(patientId);

        // Act
        await GetWorkflow(patientId).UpdateReferralStatusAsync(
            referralKey, PTReferralStatus.Completed, "Episode of care complete");

        // Assert
        PTReferralState state = await GetReferralGrain(referralKey).GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo(PTReferralStatus.Completed));
        Assert.That(state.Notes, Is.EqualTo("Episode of care complete"));
    }

    [Test]
    public async Task PTReferralGrain_UpdateAuthorization()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        string referralKey = await CreateTestReferralAsync(patientId, authorizedVisits: 12);
        DateTime newExpiration = DateTime.UtcNow.AddMonths(6);

        // Act
        await GetWorkflow(patientId).UpdateReferralAuthorizationAsync(referralKey, 24, newExpiration);

        // Assert
        PTReferralState state = await GetReferralGrain(referralKey).GetReferralAsync();
        Assert.That(state.AuthorizedVisits, Is.EqualTo(24));
        Assert.That(state.AuthorizationExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task PTReferralGrain_IndexActiveReferrals()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        string ref1 = await CreateTestReferralAsync(patientId, diagnosis: "Neck pain");
        string ref2 = await CreateTestReferralAsync(patientId, diagnosis: "Shoulder impingement");

        // Cancel the first referral
        await GetWorkflow(patientId).UpdateReferralStatusAsync(
            ref1, PTReferralStatus.Cancelled, "Patient declined");

        // Act
        List<PTReferralState> activeReferrals = await GetWorkflow(patientId).GetActiveReferralsAsync();

        // Assert
        Assert.That(activeReferrals, Has.Count.EqualTo(1));
        Assert.That(activeReferrals[0].Diagnosis, Is.EqualTo("Shoulder impingement"));
    }

    [Test]
    public async Task PTReferralGrain_IndexAllReferrals()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        await CreateTestReferralAsync(patientId, diagnosis: "Low back pain");
        await CreateTestReferralAsync(patientId, diagnosis: "Knee pain");

        // Act
        List<PTReferralState> allReferrals = await GetWorkflow(patientId).GetAllReferralsAsync();

        // Assert
        Assert.That(allReferrals, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PTReferralGrain_VisitCountExceedsAuthorized_DoesNotThrow()
    {
        // Arrange — create referral with only 2 authorized visits
        string patientId = $"PAT-{Guid.NewGuid()}";
        string referralKey = await CreateTestReferralAsync(patientId, authorizedVisits: 2);

        // Act — record 3 sessions (exceeds authorization)
        IPTReferralGrain grain = GetReferralGrain(referralKey);
        await grain.IncrementVisitCountAsync();
        await grain.IncrementVisitCountAsync();
        int count = await grain.IncrementVisitCountAsync(); // exceeds authorized

        // Assert — no exception, count continues beyond authorized
        Assert.That(count, Is.EqualTo(3));
        PTReferralState state = await grain.GetReferralAsync();
        Assert.That(state.UsedVisits, Is.EqualTo(3));
        Assert.That(state.AuthorizedVisits, Is.EqualTo(2));
    }
}
