// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for External Referral Tracking via the PatientWorkflowGrain.
/// Verifies the Site Flavor Architecture (Option 4 — Composition) feature gate,
/// referral creation, listing, and completion through the workflow orchestration layer.
/// Maps to IHS RPMS RCIS and VA Community Care referral tracking.
/// </summary>
[TestFixture]
public class ExternalReferralWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private ISiteParametersGrain GetSiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private IPatientIndexGrain GetPatientIndex() =>
        _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

    /// <summary>
    /// Helper: create a patient with demographics and register in the patient index.
    /// </summary>
    private async Task<string> CreatePatientAsync(string name, string sex, DateTime dob, string ssn)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain grain = GetPatient(patientId);
        await grain.UpdateDemographicsAsync(name, sex, dob, ssn);

        IPatientIndexGrain index = GetPatientIndex();
        await index.AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = patientId,
            Name = name,
            DateOfBirth = dob,
            Sex = sex,
            SsnLast4 = ssn.Length >= 4 ? ssn[^4..] : string.Empty,
            IsActive = true
        });

        return patientId;
    }

    [Test, Order(1)]
    public async Task WorkflowReferral_FailsWhenFeatureDisabled()
    {
        // Arrange — explicitly disable the feature to ensure clean state
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("EXTERNAL_REFERRAL_TRACKING");

        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act & Assert — should throw InvalidOperationException because feature is not enabled
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.CreateExternalReferralAsync(
                "SPECIALTY", "Community Hospital", "FAC-001",
                "Dr. Smith", "NPI-123",
                "Cardiology consultation", "Chest pain", "ROUTINE",
                "PROV-1", "Dr. Jones",
                null, null, null, null);
        });
    }

    [Test, Order(2)]
    public async Task WorkflowReferral_CreatesReferralWhenEnabled()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("EXTERNAL_REFERRAL_TRACKING");

        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act
        ExternalReferralState referral = await workflow.CreateExternalReferralAsync(
            "SPECIALTY", "Community Hospital", "FAC-001",
            "Dr. Smith", "NPI-123",
            "Cardiology consultation", "Chest pain", "ROUTINE",
            "PROV-1", "Dr. Jones",
            null, null, null, null);

        // Assert
        Assert.That(referral, Is.Not.Null);
        Assert.That(referral.PatientId, Is.EqualTo(patientId));
        Assert.That(referral.Status, Is.EqualTo("SUBMITTED"));
        Assert.That(referral.ReferralType, Is.EqualTo("SPECIALTY"));
        Assert.That(referral.ExternalFacilityName, Is.EqualTo("Community Hospital"));
        Assert.That(referral.Purpose, Is.EqualTo("Cardiology consultation"));
    }

    [Test, Order(3)]
    public async Task WorkflowReferral_ListsPatientReferrals()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("EXTERNAL_REFERRAL_TRACKING");

        string patientId = await CreatePatientAsync("JONES,MARY", "F", new DateTime(1980, 3, 10), "777-88-9999");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        await workflow.CreateExternalReferralAsync(
            "SPECIALTY", "Community Hospital", "FAC-001",
            "Dr. Smith", "NPI-123",
            "Cardiology consultation", "Chest pain", "ROUTINE",
            "PROV-1", "Dr. Jones",
            null, null, null, null);

        await workflow.CreateExternalReferralAsync(
            "DIAGNOSTIC", "Regional Lab Center", "FAC-002",
            "Dr. Lee", "NPI-456",
            "MRI Brain", "Headaches", "URGENT",
            "PROV-1", "Dr. Jones",
            null, null, null, null);

        // Act
        List<ExternalReferralIndexEntry> referrals = await workflow.GetExternalReferralsAsync();

        // Assert
        Assert.That(referrals, Has.Count.EqualTo(2));
    }

    [Test, Order(4)]
    public async Task WorkflowReferral_CompletesReferral()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("EXTERNAL_REFERRAL_TRACKING");

        string patientId = await CreatePatientAsync("BROWN,ROBERT", "M", new DateTime(1955, 12, 1), "222-33-4444");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        ExternalReferralState referral = await workflow.CreateExternalReferralAsync(
            "CONSULTATION", "VA Partner Clinic", "FAC-003",
            "Dr. Adams", "NPI-789",
            "Orthopedic evaluation", "Knee pain", "ROUTINE",
            "PROV-2", "Dr. Wilson",
            null, null, null, null);

        DateTime completionDate = new DateTime(2026, 4, 20, 14, 0, 0, DateTimeKind.Utc);

        // Act
        await workflow.CompleteExternalReferralAsync(
            referral.ReferralId, completionDate,
            "Surgery not indicated", "Mild degenerative changes");

        // Assert
        ExternalReferralState completed = await workflow.GetExternalReferralAsync(referral.ReferralId);
        Assert.That(completed.Status, Is.EqualTo("COMPLETED"));
        Assert.That(completed.CompletionDate, Is.EqualTo(completionDate));
        Assert.That(completed.OutcomeNotes, Is.EqualTo("Surgery not indicated"));
        Assert.That(completed.ClinicalFindings, Is.EqualTo("Mild degenerative changes"));
        Assert.That(completed.RequiresFollowUp, Is.False);
    }
}
