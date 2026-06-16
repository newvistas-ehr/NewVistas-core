// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Patient Advocate — VistA File #745.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class PatientAdvocateWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IComplaintGrain GetComplaint(string id)
        => _cluster.GrainFactory.GetGrain<IComplaintGrain>(id);

    private IComplaintIndexGrain GetComplaintIndex()
        => _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-COMPLAINT-IDX");

    private ICongressionalInquiryGrain GetCongressional(string id)
        => _cluster.GrainFactory.GetGrain<ICongressionalInquiryGrain>(id);

    private ICongressionalInquiryIndexGrain GetCongressionalIndex()
        => _cluster.GrainFactory.GetGrain<ICongressionalInquiryIndexGrain>("PA-CONGRESS-IDX");

    // ── Complaint Tests ──────────────────────────────────────────────────────

    [Test]
    public async Task FileComplaint_CreatesReceivedComplaint()
    {
        string complaintId = $"PA-COMP-{Guid.NewGuid():N}";
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IComplaintGrain grain = GetComplaint(complaintId);

        await grain.FileComplaintAsync(
            patientId, "DOE,JOHN", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.QualityOfCare,
            ComplaintPriority.Routine, InquirySource.PatientSelf,
            "Patient was not seen for scheduled appointment",
            "Patient wants explanation and apology",
            "Primary Care Clinic", "John Doe", "Self",
            isConfidential: false, createdBy: "ADV-001");

        ComplaintState state = await grain.GetComplaintAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.ComplaintType, Is.EqualTo(ComplaintType.Formal));
        Assert.That(state.Category, Is.EqualTo(ComplaintCategory.QualityOfCare));
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Received));
        Assert.That(state.NarrativeDescription, Does.Contain("scheduled appointment"));
    }

    [Test]
    public async Task AssignAdvocate_UpdatesAdvocateFields()
    {
        string complaintId = $"PA-COMP-{Guid.NewGuid():N}";
        IComplaintGrain grain = GetComplaint(complaintId);

        await grain.FileComplaintAsync(
            "PAT-001", "SMITH,JANE", DateTime.UtcNow,
            ComplaintType.Informal, ComplaintCategory.WaitTime,
            ComplaintPriority.Urgent, InquirySource.FamilyMember,
            "Long wait in ER", "Expedited care", "Emergency Dept",
            "Mary Smith", "Daughter", false, "ADV-002");

        await grain.AssignAdvocateAsync("ADV-100", "Susan Brown");

        ComplaintState state = await grain.GetComplaintAsync();
        Assert.That(state.AssignedAdvocateId, Is.EqualTo("ADV-100"));
        Assert.That(state.AssignedAdvocateName, Is.EqualTo("Susan Brown"));
    }

    [Test]
    public async Task AcknowledgeComplaint_SetsAcknowledgmentDate()
    {
        string complaintId = $"PA-COMP-{Guid.NewGuid():N}";
        IComplaintGrain grain = GetComplaint(complaintId);

        await grain.FileComplaintAsync(
            "PAT-002", "GREEN,BOB", DateTime.UtcNow,
            ComplaintType.Grievance, ComplaintCategory.StaffConduct,
            ComplaintPriority.Immediate, InquirySource.PatientSelf,
            "Rude staff member", "Apology", "Pharmacy",
            "Bob Green", "Self", false, "ADV-003");

        DateTime ackDate = DateTime.UtcNow;
        await grain.AcknowledgeComplaintAsync(ackDate);

        ComplaintState state = await grain.GetComplaintAsync();
        Assert.That(state.AcknowledgmentDate, Is.Not.Null);
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Acknowledged));
    }

    [Test]
    public async Task AddActionTaken_AppendsToActionsList()
    {
        string complaintId = $"PA-COMP-{Guid.NewGuid():N}";
        IComplaintGrain grain = GetComplaint(complaintId);

        await grain.FileComplaintAsync(
            "PAT-003", "WHITE,TOM", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.AccessToCare,
            ComplaintPriority.Routine, InquirySource.PatientSelf,
            "Cannot get appointment within 30 days", string.Empty, "Mental Health",
            "Tom White", "Self", false, "ADV-004");

        await grain.AddActionTakenAsync("Contacted MH scheduling to add appointment slots");
        await grain.AddActionTakenAsync("Patient offered next-day telehealth visit");

        ComplaintState state = await grain.GetComplaintAsync();
        Assert.That(state.ActionsTaken, Has.Count.EqualTo(2));
        Assert.That(state.ActionsTaken[0], Does.Contain("scheduling"));
    }

    [Test]
    public async Task LogCorrespondence_AddsToCorrespondenceLog()
    {
        string complaintId = $"PA-COMP-{Guid.NewGuid():N}";
        IComplaintGrain grain = GetComplaint(complaintId);

        await grain.FileComplaintAsync(
            "PAT-004", "BLACK,SUE", DateTime.UtcNow,
            ComplaintType.Informal, ComplaintCategory.Communication,
            ComplaintPriority.Routine, InquirySource.PatientSelf,
            "Not receiving appointment reminders", string.Empty, "Scheduling",
            "Sue Black", "Self", false, "ADV-005");

        await grain.LogCorrespondenceAsync("Outbound", "Phone", "Called patient to discuss concern", "ADV-005");

        ComplaintState state = await grain.GetComplaintAsync();
        Assert.That(state.CorrespondenceLog, Has.Count.EqualTo(1));
        Assert.That(state.CorrespondenceLog[0].Direction, Is.EqualTo("Outbound"));
        Assert.That(state.CorrespondenceLog[0].Method, Is.EqualTo("Phone"));
    }

    [Test]
    public async Task ResolveComplaint_SetsOutcomeAndResolutionSummary()
    {
        string complaintId = $"PA-COMP-{Guid.NewGuid():N}";
        IComplaintGrain grain = GetComplaint(complaintId);

        await grain.FileComplaintAsync(
            "PAT-005", "GRAY,ALICE", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.Billing,
            ComplaintPriority.Routine, InquirySource.PatientSelf,
            "Incorrect copay charged", "Refund of copay", "Business Office",
            "Alice Gray", "Self", false, "ADV-006");

        await grain.ResolveComplaintAsync(ResolutionOutcome.Substantiated, "Copay was incorrect; refund issued");

        ComplaintState state = await grain.GetComplaintAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Resolved));
        Assert.That(state.Outcome, Is.EqualTo(ResolutionOutcome.Substantiated));
        Assert.That(state.ResolutionSummary, Does.Contain("refund"));
        Assert.That(state.ResolvedDate, Is.Not.Null);
    }

    [Test]
    public async Task CloseComplaint_SetsClosedStatus()
    {
        string complaintId = $"PA-COMP-{Guid.NewGuid():N}";
        IComplaintGrain grain = GetComplaint(complaintId);

        await grain.FileComplaintAsync(
            "PAT-006", "KING,DAN", DateTime.UtcNow,
            ComplaintType.Informal, ComplaintCategory.FacilityEnvironment,
            ComplaintPriority.Routine, InquirySource.PatientSelf,
            "Broken elevator", string.Empty, "Building A",
            "Dan King", "Self", false, "ADV-007");

        await grain.ResolveComplaintAsync(ResolutionOutcome.NoActionRequired, "Elevator already scheduled for repair");
        await grain.CloseComplaintAsync();

        ComplaintState state = await grain.GetComplaintAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Closed));
        Assert.That(state.ClosedDate, Is.Not.Null);
    }

    // ── Complaint Index Tests ────────────────────────────────────────────────

    [Test]
    public async Task ComplaintIndex_UpsertAndQueryByStatus()
    {
        IComplaintIndexGrain index = GetComplaintIndex();

        string id1 = $"PA-COMP-{Guid.NewGuid():N}";
        string id2 = $"PA-COMP-{Guid.NewGuid():N}";

        await index.UpsertComplaintAsync(new ComplaintIndexEntry
        {
            ComplaintId = id1, PatientId = "PAT-IDX-1", PatientName = "DOE,JOHN",
            ReceivedDate = DateTime.UtcNow, ComplaintType = ComplaintType.Formal,
            Category = ComplaintCategory.QualityOfCare, Priority = ComplaintPriority.Routine,
            Status = ComplaintStatus.Received
        });

        await index.UpsertComplaintAsync(new ComplaintIndexEntry
        {
            ComplaintId = id2, PatientId = "PAT-IDX-2", PatientName = "SMITH,JANE",
            ReceivedDate = DateTime.UtcNow, ComplaintType = ComplaintType.Grievance,
            Category = ComplaintCategory.StaffConduct, Priority = ComplaintPriority.Urgent,
            Status = ComplaintStatus.Closed
        });

        List<ComplaintIndexEntry> received = await index.GetComplaintsByStatusAsync(ComplaintStatus.Received);
        Assert.That(received.Any(c => c.ComplaintId == id1), Is.True);
    }

    // ── Congressional Inquiry Tests ──────────────────────────────────────────

    [Test]
    public async Task CreateCongressionalInquiry_SetsReceivedStatus()
    {
        string inquiryId = $"PA-CONG-{Guid.NewGuid():N}";
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        ICongressionalInquiryGrain grain = GetCongressional(inquiryId);

        await grain.CreateInquiryAsync(
            patientId, "DOE,JOHN",
            CongressionalInquiryType.Senate,
            "Office of Sen. Smith", "Jane Aide",
            "202-555-1234", "aide@senate.gov",
            "Delayed benefits processing",
            "Veteran has been waiting 6 months for disability determination",
            string.Empty, "ADV-010");

        CongressionalInquiryState state = await grain.GetInquiryAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.InquiryType, Is.EqualTo(CongressionalInquiryType.Senate));
        Assert.That(state.CongressionalOfficeName, Is.EqualTo("Office of Sen. Smith"));
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Received));
        Assert.That(state.Subject, Does.Contain("Delayed benefits"));
    }

    [Test]
    public async Task AcknowledgeAndCompleteCongressionalInquiry_FullLifecycle()
    {
        string inquiryId = $"PA-CONG-{Guid.NewGuid():N}";
        ICongressionalInquiryGrain grain = GetCongressional(inquiryId);

        await grain.CreateInquiryAsync(
            "PAT-CONG-1", "SMITH,BOB",
            CongressionalInquiryType.HouseOfRepresentatives,
            "Office of Rep. Jones", "John Staffer",
            "202-555-5678", "staff@house.gov",
            "Access to care complaint",
            "Veteran unable to get appointment within 30 days",
            string.Empty, "ADV-011");

        await grain.AssignHandlerAsync("HANDLER-001", "Dr. Adams");
        await grain.AcknowledgeInquiryAsync(DateTime.UtcNow);

        CongressionalInquiryState stateAck = await grain.GetInquiryAsync();
        Assert.That(stateAck.AcknowledgmentDate, Is.Not.Null);
        Assert.That(stateAck.AssignedHandlerName, Is.EqualTo("Dr. Adams"));

        await grain.RecordInterimResponseAsync("Veteran has been scheduled for next available appointment");

        await grain.CompleteInquiryAsync(
            "Veteran was seen on 3/1 and received comprehensive care",
            ResolutionOutcome.Substantiated);

        CongressionalInquiryState stateFinal = await grain.GetInquiryAsync();
        Assert.That(stateFinal.Status, Is.EqualTo(ComplaintStatus.Resolved));
        Assert.That(stateFinal.FinalResponseText, Does.Contain("comprehensive care"));
        Assert.That(stateFinal.Outcome, Is.EqualTo(ResolutionOutcome.Substantiated));
    }

    [Test]
    public async Task CongressionalIndex_UpsertAndQueryPending()
    {
        ICongressionalInquiryIndexGrain index = GetCongressionalIndex();

        string id1 = $"PA-CONG-{Guid.NewGuid():N}";
        await index.UpsertInquiryAsync(new CongressionalInquiryIndexEntry
        {
            InquiryId = id1, PatientId = "PAT-CIDX-1", PatientName = "TEST,CONG",
            ReceivedDate = DateTime.UtcNow,
            InquiryType = CongressionalInquiryType.Senate,
            CongressionalOfficeName = "Office of Sen. Test",
            Status = ComplaintStatus.Received,
            AcknowledgmentDue = DateTime.UtcNow.AddDays(7),
            ResponseDue = DateTime.UtcNow.AddDays(20),
            IsAcknowledgmentOverdue = false, IsResponseOverdue = false
        });

        List<CongressionalInquiryIndexEntry> pending = await index.GetPendingInquiriesAsync();
        Assert.That(pending.Any(i => i.InquiryId == id1), Is.True);
    }
}
