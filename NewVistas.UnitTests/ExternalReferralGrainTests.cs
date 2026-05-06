// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for External Referral grains — IHS RPMS RCIS and VA Community Care
/// referral tracking. Tests the IExternalReferralGrain and IExternalReferralIndexGrain
/// directly, verifying referral lifecycle, status transitions, follow-ups, documents,
/// and index queries.
/// </summary>
[TestFixture]
public class ExternalReferralGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IExternalReferralGrain GetReferralGrain(string referralId) =>
        _cluster.GrainFactory.GetGrain<IExternalReferralGrain>(referralId);

    private IExternalReferralIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IExternalReferralIndexGrain>("EXT-REF-IDX");

    private async Task<ExternalReferralState> CreateTestReferralAsync(
        string referralId,
        string patientId = "PATIENT-1",
        string patientName = "DOE,JOHN",
        string referralType = "SPECIALTY",
        string facilityName = "Community Hospital",
        string urgency = "ROUTINE")
    {
        IExternalReferralGrain grain = GetReferralGrain(referralId);
        ExternalReferralState result = await grain.CreateReferralAsync(
            patientId, patientName, referralType,
            facilityName, "FAC-001",
            "Dr. Smith", "NPI-123",
            "Cardiology consultation", "Chest pain", urgency,
            "PROV-1", "Dr. Jones",
            null, null, null, null);
        return result;
    }

    [Test]
    public async Task ExternalReferralGrain_CreatesReferral()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";

        // Act
        ExternalReferralState result = await CreateTestReferralAsync(referralId);

        // Assert
        Assert.That(result.ReferralId, Is.EqualTo(referralId));
        Assert.That(result.PatientId, Is.EqualTo("PATIENT-1"));
        Assert.That(result.PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(result.ReferralType, Is.EqualTo("SPECIALTY"));
        Assert.That(result.ExternalFacilityName, Is.EqualTo("Community Hospital"));
        Assert.That(result.ExternalFacilityId, Is.EqualTo("FAC-001"));
        Assert.That(result.ExternalProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(result.ExternalProviderId, Is.EqualTo("NPI-123"));
        Assert.That(result.Purpose, Is.EqualTo("Cardiology consultation"));
        Assert.That(result.Diagnosis, Is.EqualTo("Chest pain"));
        Assert.That(result.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(result.Status, Is.EqualTo("SUBMITTED"));
        Assert.That(result.ReferredByProviderId, Is.EqualTo("PROV-1"));
        Assert.That(result.ReferredByProviderName, Is.EqualTo("Dr. Jones"));
        Assert.That(result.RequiresFollowUp, Is.True);
    }

    [Test]
    public async Task ExternalReferralGrain_UpdatesStatus()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        await CreateTestReferralAsync(referralId);
        IExternalReferralGrain grain = GetReferralGrain(referralId);

        // Act
        await grain.UpdateStatusAsync("AUTHORIZED", "Approved by utilization review", "ADMIN-1", "Admin User");

        // Assert
        ExternalReferralState state = await grain.GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo("AUTHORIZED"));
        Assert.That(state.StatusReason, Is.EqualTo("Approved by utilization review"));
        Assert.That(state.FollowUps, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task ExternalReferralGrain_RecordsAppointment()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        await CreateTestReferralAsync(referralId);
        IExternalReferralGrain grain = GetReferralGrain(referralId);
        DateTime apptDate = new DateTime(2026, 4, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        await grain.RecordAppointmentAsync(apptDate, "CONF-12345");

        // Assert
        ExternalReferralState state = await grain.GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(state.AppointmentDateTime, Is.EqualTo(apptDate));
        Assert.That(state.ConfirmationNumber, Is.EqualTo("CONF-12345"));
    }

    [Test]
    public async Task ExternalReferralGrain_RecordsCompletion()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        await CreateTestReferralAsync(referralId);
        IExternalReferralGrain grain = GetReferralGrain(referralId);
        DateTime completionDate = new DateTime(2026, 4, 20, 14, 0, 0, DateTimeKind.Utc);

        // Act
        await grain.RecordCompletionAsync(completionDate, "Patient tolerated procedure well", "Normal sinus rhythm");

        // Assert
        ExternalReferralState state = await grain.GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.CompletionDate, Is.EqualTo(completionDate));
        Assert.That(state.OutcomeNotes, Is.EqualTo("Patient tolerated procedure well"));
        Assert.That(state.ClinicalFindings, Is.EqualTo("Normal sinus rhythm"));
        Assert.That(state.RequiresFollowUp, Is.False);
    }

    [Test]
    public async Task ExternalReferralGrain_RecordsDenial()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        await CreateTestReferralAsync(referralId);
        IExternalReferralGrain grain = GetReferralGrain(referralId);

        // Act
        await grain.RecordDenialAsync("Not medically necessary", "ADMIN-2", "Review Officer");

        // Assert
        ExternalReferralState state = await grain.GetReferralAsync();
        Assert.That(state.Status, Is.EqualTo("DENIED"));
        Assert.That(state.StatusReason, Is.EqualTo("Not medically necessary"));
    }

    [Test]
    public async Task ExternalReferralGrain_AddsFollowUp()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        await CreateTestReferralAsync(referralId);
        IExternalReferralGrain grain = GetReferralGrain(referralId);

        // Act
        await grain.AddFollowUpAsync("Called facility to confirm appointment", "Nurse Williams");

        // Assert
        ExternalReferralState state = await grain.GetReferralAsync();
        Assert.That(state.FollowUps, Has.Count.GreaterThanOrEqualTo(1));
        ReferralFollowUp followUp = state.FollowUps.Last();
        Assert.That(followUp.Note, Is.EqualTo("Called facility to confirm appointment"));
        Assert.That(followUp.AuthorName, Is.EqualTo("Nurse Williams"));
    }

    [Test]
    public async Task ExternalReferralGrain_AttachesDocument()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        await CreateTestReferralAsync(referralId);
        IExternalReferralGrain grain = GetReferralGrain(referralId);

        // Act
        await grain.AttachDocumentAsync("DOC-001", "CONSULT_REPORT", "Cardiology consultation report");

        // Assert
        ExternalReferralState state = await grain.GetReferralAsync();
        Assert.That(state.Documents, Has.Count.EqualTo(1));
        ReferralDocument doc = state.Documents[0];
        Assert.That(doc.DocumentId, Is.EqualTo("DOC-001"));
        Assert.That(doc.DocumentType, Is.EqualTo("CONSULT_REPORT"));
        Assert.That(doc.Description, Is.EqualTo("Cardiology consultation report"));
    }

    [Test]
    public async Task ExternalReferralGrain_IndexUpdatedOnCreate()
    {
        // Arrange
        string referralId = $"EXT-REF:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        // Act
        await CreateTestReferralAsync(referralId, patientId: patientId);

        // Assert
        IExternalReferralIndexGrain index = GetIndex();
        List<ExternalReferralIndexEntry> entries = await index.GetByPatientAsync(patientId);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ReferralId, Is.EqualTo(referralId));
        Assert.That(entries[0].PatientId, Is.EqualTo(patientId));
        Assert.That(entries[0].Status, Is.EqualTo("SUBMITTED"));
    }

    [Test]
    public async Task ExternalReferralGrain_IndexSearchByStatus()
    {
        // Arrange — create two referrals with different statuses
        string referralId1 = $"EXT-REF:{Guid.NewGuid()}";
        string referralId2 = $"EXT-REF:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        await CreateTestReferralAsync(referralId1, patientId: patientId);
        await CreateTestReferralAsync(referralId2, patientId: patientId);

        // Deny the second referral (which updates its status in the index)
        IExternalReferralGrain grain2 = GetReferralGrain(referralId2);
        await grain2.RecordDenialAsync("Not eligible", "ADMIN-1", "Admin");

        // Update the index for the denied referral
        IExternalReferralIndexGrain index = GetIndex();
        await index.AddOrUpdateAsync(new ExternalReferralIndexEntry
        {
            ReferralId = referralId2,
            PatientId = patientId,
            PatientName = "DOE,JOHN",
            ReferralType = "SPECIALTY",
            ExternalFacilityName = "Community Hospital",
            Status = "DENIED",
            Urgency = "ROUTINE",
            ReferralDate = DateTime.UtcNow,
            RequiresFollowUp = false
        });

        // Act
        List<ExternalReferralIndexEntry> submitted = await index.GetByStatusAsync("SUBMITTED");
        List<ExternalReferralIndexEntry> denied = await index.GetByStatusAsync("DENIED");

        // Assert
        Assert.That(submitted.Any(e => e.ReferralId == referralId1), Is.True);
        Assert.That(denied.Any(e => e.ReferralId == referralId2), Is.True);
        Assert.That(submitted.Any(e => e.ReferralId == referralId2), Is.False);
    }

    [Test]
    public async Task ExternalReferralGrain_IndexPendingFollowUps()
    {
        // Arrange — create one completed (no follow-up) and one submitted (has follow-up)
        string completedId = $"EXT-REF:{Guid.NewGuid()}";
        string submittedId = $"EXT-REF:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        await CreateTestReferralAsync(completedId, patientId: patientId);
        await CreateTestReferralAsync(submittedId, patientId: patientId);

        // Complete the first referral
        IExternalReferralGrain completedGrain = GetReferralGrain(completedId);
        await completedGrain.RecordCompletionAsync(DateTime.UtcNow, "Done", "Normal");

        // Update the index for the completed referral
        IExternalReferralIndexGrain index = GetIndex();
        await index.AddOrUpdateAsync(new ExternalReferralIndexEntry
        {
            ReferralId = completedId,
            PatientId = patientId,
            PatientName = "DOE,JOHN",
            ReferralType = "SPECIALTY",
            ExternalFacilityName = "Community Hospital",
            Status = "COMPLETED",
            Urgency = "ROUTINE",
            ReferralDate = DateTime.UtcNow,
            RequiresFollowUp = false
        });

        // Act
        List<ExternalReferralIndexEntry> pending = await index.GetPendingFollowUpsAsync();

        // Assert — only the submitted referral should require follow-up
        Assert.That(pending.Any(e => e.ReferralId == submittedId), Is.True);
        Assert.That(pending.Any(e => e.ReferralId == completedId), Is.False);
    }
}
