// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ── Test Cluster Setup ────────────────────────────────────────────────────────

file class PATestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("paComplaintStore");
        siloBuilder.AddMemoryGrainStorage("paComplaintIndexStore");
        siloBuilder.AddMemoryGrainStorage("paCongressStore");
        siloBuilder.AddMemoryGrainStorage("paCongressIndexStore");
    }
}

// ── ComplaintGrain Tests ──────────────────────────────────────────────────────

[TestFixture]
public class ComplaintGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IComplaintGrain GetGrain(string id) => _cluster.GrainFactory.GetGrain<IComplaintGrain>(id);

    [Test]
    public async Task ComplaintGrain_CanFileComplaint()
    {
        string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
        IComplaintGrain grain = GetGrain(id);

        await grain.FileComplaintAsync(
            "PAT-001", "John Smith", DateTime.UtcNow.AddDays(-1),
            ComplaintType.Formal, ComplaintCategory.QualityOfCare, ComplaintPriority.Routine,
            InquirySource.PatientSelf, "Narrative of complaint.", "Specific concern.",
            "Primary Care", string.Empty, string.Empty, false, "Advocate1");

        ComplaintState state = await grain.GetComplaintAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PatientName, Is.EqualTo("John Smith"));
        Assert.That(state.ComplaintType, Is.EqualTo(ComplaintType.Formal));
        Assert.That(state.Category, Is.EqualTo(ComplaintCategory.QualityOfCare));
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Received));
        Assert.That(state.NarrativeDescription, Is.EqualTo("Narrative of complaint."));
        Assert.That(state.ResponseDue, Is.Not.Null);
    }

    [Test]
    public async Task ComplaintGrain_SetsCorrectDueDatesByPriority()
    {
        string routineId = $"PA-COMPLAINT:{Guid.NewGuid()}";
        string urgentId = $"PA-COMPLAINT:{Guid.NewGuid()}";

        await GetGrain(routineId).FileComplaintAsync(
            "PAT-002", "Jane Doe", DateTime.UtcNow,
            ComplaintType.Informal, ComplaintCategory.WaitTime, ComplaintPriority.Routine,
            InquirySource.PatientSelf, "Routine complaint.", string.Empty,
            "Pharmacy", string.Empty, string.Empty, false, "Advocate1");

        await GetGrain(urgentId).FileComplaintAsync(
            "PAT-003", "Bob Lee", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.QualityOfCare, ComplaintPriority.Urgent,
            InquirySource.PatientSelf, "Urgent complaint.", string.Empty,
            "Surgery", string.Empty, string.Empty, false, "Advocate1");

        ComplaintState routine = await GetGrain(routineId).GetComplaintAsync();
        ComplaintState urgent = await GetGrain(urgentId).GetComplaintAsync();

        Assert.That(routine.ResponseDue!.Value, Is.GreaterThan(DateTime.UtcNow.AddDays(25)));
        Assert.That(urgent.ResponseDue!.Value, Is.LessThan(DateTime.UtcNow.AddDays(10)));
    }

    [Test]
    public async Task ComplaintGrain_CanAssignAdvocate()
    {
        string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
        await GetGrain(id).FileComplaintAsync(
            "PAT-004", "Alice Wang", DateTime.UtcNow,
            ComplaintType.Grievance, ComplaintCategory.StaffConduct, ComplaintPriority.Routine,
            InquirySource.FamilyMember, "Grievance text.", string.Empty,
            "Nursing", "Alice's Son", "Son", false, "Advocate1");

        await GetGrain(id).AssignAdvocateAsync("ADV-101", "Mary Johnson, LCSW");

        ComplaintState state = await GetGrain(id).GetComplaintAsync();
        Assert.That(state.AssignedAdvocateId, Is.EqualTo("ADV-101"));
        Assert.That(state.AssignedAdvocateName, Is.EqualTo("Mary Johnson, LCSW"));
    }

    [Test]
    public async Task ComplaintGrain_CanAcknowledgeComplaint()
    {
        string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
        await GetGrain(id).FileComplaintAsync(
            "PAT-005", "Carlos Rivera", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.Billing, ComplaintPriority.Routine,
            InquirySource.PatientSelf, "Billing complaint.", string.Empty,
            "Finance", string.Empty, string.Empty, false, "Advocate1");

        await GetGrain(id).AcknowledgeComplaintAsync(DateTime.UtcNow);

        ComplaintState state = await GetGrain(id).GetComplaintAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Acknowledged));
        Assert.That(state.AcknowledgmentDate, Is.Not.Null);
    }

    [Test]
    public async Task ComplaintGrain_CanAddActionTaken_NoDuplicates()
    {
        string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
        await GetGrain(id).FileComplaintAsync(
            "PAT-006", "Diana Kim", DateTime.UtcNow,
            ComplaintType.Informal, ComplaintCategory.Communication, ComplaintPriority.Routine,
            InquirySource.PatientSelf, "Communication issue.", string.Empty,
            "Outpatient", string.Empty, string.Empty, false, "Advocate1");

        await GetGrain(id).AddActionTakenAsync("Contacted department head");
        await GetGrain(id).AddActionTakenAsync("Contacted department head"); // duplicate
        await GetGrain(id).AddActionTakenAsync("Issued written apology");

        ComplaintState state = await GetGrain(id).GetComplaintAsync();
        Assert.That(state.ActionsTaken, Has.Count.EqualTo(2));
        Assert.That(state.ActionsTaken, Contains.Item("Contacted department head"));
        Assert.That(state.ActionsTaken, Contains.Item("Issued written apology"));
    }

    [Test]
    public async Task ComplaintGrain_CanLogCorrespondence()
    {
        string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
        await GetGrain(id).FileComplaintAsync(
            "PAT-007", "Edward Park", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.PatientRights, ComplaintPriority.Urgent,
            InquirySource.Attorney, "Rights violation.", string.Empty,
            "Administration", "Attorney John Smith", "Attorney", true, "Advocate1");

        await GetGrain(id).LogCorrespondenceAsync("Inbound", "Phone", "Patient called to inquire about status.", "M.Johnson");
        await GetGrain(id).LogCorrespondenceAsync("Outbound", "Letter", "Acknowledgment letter sent.", "M.Johnson");

        ComplaintState state = await GetGrain(id).GetComplaintAsync();
        Assert.That(state.CorrespondenceLog, Has.Count.EqualTo(2));
        Assert.That(state.CorrespondenceLog[0].Direction, Is.EqualTo("Inbound"));
        Assert.That(state.CorrespondenceLog[1].Direction, Is.EqualTo("Outbound"));
        Assert.That(state.CorrespondenceLog[0].CorrespondenceId, Is.Not.Empty);
    }

    [Test]
    public async Task ComplaintGrain_CanResolveAndClose()
    {
        string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
        await GetGrain(id).FileComplaintAsync(
            "PAT-008", "Fiona Chen", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.QualityOfCare, ComplaintPriority.Routine,
            InquirySource.PatientSelf, "Quality complaint.", string.Empty,
            "Cardiology", string.Empty, string.Empty, false, "Advocate1");

        await GetGrain(id).ResolveComplaintAsync(ResolutionOutcome.Substantiated, "Care was suboptimal; corrective action taken.");

        ComplaintState afterResolve = await GetGrain(id).GetComplaintAsync();
        Assert.That(afterResolve.Status, Is.EqualTo(ComplaintStatus.Resolved));
        Assert.That(afterResolve.Outcome, Is.EqualTo(ResolutionOutcome.Substantiated));
        Assert.That(afterResolve.ResolvedDate, Is.Not.Null);

        await GetGrain(id).CloseComplaintAsync();

        ComplaintState afterClose = await GetGrain(id).GetComplaintAsync();
        Assert.That(afterClose.Status, Is.EqualTo(ComplaintStatus.Closed));
        Assert.That(afterClose.ClosedDate, Is.Not.Null);
    }

    [Test]
    public async Task ComplaintGrain_CanEscalate()
    {
        string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
        await GetGrain(id).FileComplaintAsync(
            "PAT-009", "George Patel", DateTime.UtcNow,
            ComplaintType.Grievance, ComplaintCategory.QualityOfCare, ComplaintPriority.Urgent,
            InquirySource.PatientSelf, "Serious complaint.", string.Empty,
            "ICU", string.Empty, string.Empty, false, "Advocate1");

        await GetGrain(id).UpdateStatusAsync(ComplaintStatus.Escalated);

        ComplaintState state = await GetGrain(id).GetComplaintAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Escalated));
    }
}

// ── ComplaintIndexGrain Tests ─────────────────────────────────────────────────

[TestFixture]
public class ComplaintIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ComplaintIndexEntry MakeEntry(string id, string patientId, ComplaintStatus status,
        ComplaintType type = ComplaintType.Formal, DateTime? responseDue = null) => new()
    {
        ComplaintId = id,
        PatientId = patientId,
        PatientName = "Test Patient",
        ReceivedDate = DateTime.UtcNow.AddDays(-5),
        ComplaintType = type,
        Category = ComplaintCategory.QualityOfCare,
        Priority = ComplaintPriority.Routine,
        Status = status,
        AssignedAdvocateName = string.Empty,
        ResponseDue = responseDue ?? DateTime.UtcNow.AddDays(25)
    };

    [Test]
    public async Task ComplaintIndexGrain_CanUpsertAndRetrieve()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-IDX-TEST-1");

        await index.UpsertComplaintAsync(MakeEntry("PA-C-1", "PAT-A", ComplaintStatus.Received));
        await index.UpsertComplaintAsync(MakeEntry("PA-C-2", "PAT-B", ComplaintStatus.Acknowledged));

        List<ComplaintIndexEntry> all = await index.GetAllComplaintsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ComplaintIndexGrain_UpdatesExistingEntry()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-IDX-TEST-2");

        ComplaintIndexEntry entry = MakeEntry("PA-C-UPD", "PAT-C", ComplaintStatus.Received);
        await index.UpsertComplaintAsync(entry);

        ComplaintIndexEntry updated = MakeEntry("PA-C-UPD", "PAT-C", ComplaintStatus.Resolved);
        await index.UpsertComplaintAsync(updated);

        List<ComplaintIndexEntry> all = await index.GetAllComplaintsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(ComplaintStatus.Resolved));
    }

    [Test]
    public async Task ComplaintIndexGrain_FiltersByStatus()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-IDX-TEST-3");

        await index.UpsertComplaintAsync(MakeEntry("PA-C-S1", "PAT-D", ComplaintStatus.Received));
        await index.UpsertComplaintAsync(MakeEntry("PA-C-S2", "PAT-E", ComplaintStatus.Acknowledged));
        await index.UpsertComplaintAsync(MakeEntry("PA-C-S3", "PAT-F", ComplaintStatus.Resolved));

        List<ComplaintIndexEntry> received = await index.GetComplaintsByStatusAsync(ComplaintStatus.Received);
        Assert.That(received, Has.Count.EqualTo(1));
        Assert.That(received[0].ComplaintId, Is.EqualTo("PA-C-S1"));
    }

    [Test]
    public async Task ComplaintIndexGrain_FiltersByPatient()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-IDX-TEST-4");

        await index.UpsertComplaintAsync(MakeEntry("PA-C-P1", "PAT-G", ComplaintStatus.Received));
        await index.UpsertComplaintAsync(MakeEntry("PA-C-P2", "PAT-G", ComplaintStatus.Acknowledged));
        await index.UpsertComplaintAsync(MakeEntry("PA-C-P3", "PAT-H", ComplaintStatus.Received));

        List<ComplaintIndexEntry> patientG = await index.GetComplaintsByPatientAsync("PAT-G");
        Assert.That(patientG, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ComplaintIndexGrain_FiltersByType()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-IDX-TEST-5");

        await index.UpsertComplaintAsync(MakeEntry("PA-C-T1", "PAT-I", ComplaintStatus.Received, ComplaintType.Formal));
        await index.UpsertComplaintAsync(MakeEntry("PA-C-T2", "PAT-J", ComplaintStatus.Received, ComplaintType.Grievance));
        await index.UpsertComplaintAsync(MakeEntry("PA-C-T3", "PAT-K", ComplaintStatus.Received, ComplaintType.Formal));

        List<ComplaintIndexEntry> formal = await index.GetComplaintsByTypeAsync(ComplaintType.Formal);
        Assert.That(formal, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ComplaintIndexGrain_DetectsOverdueComplaints()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-IDX-TEST-6");

        await index.UpsertComplaintAsync(MakeEntry("PA-C-OD1", "PAT-L", ComplaintStatus.Received,
            responseDue: DateTime.UtcNow.AddDays(-5)));  // overdue
        await index.UpsertComplaintAsync(MakeEntry("PA-C-OD2", "PAT-M", ComplaintStatus.Received,
            responseDue: DateTime.UtcNow.AddDays(10)));  // not overdue
        await index.UpsertComplaintAsync(MakeEntry("PA-C-OD3", "PAT-N", ComplaintStatus.Resolved,
            responseDue: DateTime.UtcNow.AddDays(-5)));  // overdue but resolved (excluded)

        List<ComplaintIndexEntry> overdue = await index.GetOverdueComplaintsAsync();
        Assert.That(overdue, Has.Count.EqualTo(1));
        Assert.That(overdue[0].ComplaintId, Is.EqualTo("PA-C-OD1"));
    }
}

// ── CongressionalInquiryGrain Tests ──────────────────────────────────────────

[TestFixture]
public class CongressionalInquiryGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICongressionalInquiryGrain GetGrain(string id)
        => _cluster.GrainFactory.GetGrain<ICongressionalInquiryGrain>(id);

    private Task CreateInquiry(ICongressionalInquiryGrain grain, string patientId, string patientName)
        => grain.CreateInquiryAsync(
            patientId, patientName, CongressionalInquiryType.Senate,
            "Sen. John Doe Office", "Staff Contact", "202-555-0100", "jdoe@senate.gov",
            "Delay in benefits processing", "Veteran has been waiting 18 months for disability rating.",
            string.Empty, "Advocate1");

    [Test]
    public async Task CongressionalInquiryGrain_CanCreateInquiry()
    {
        string id = $"PA-CONGRESS:{Guid.NewGuid()}";
        await CreateInquiry(GetGrain(id), "PAT-010", "Henry Wilson");

        CongressionalInquiryState state = await GetGrain(id).GetInquiryAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-010"));
        Assert.That(state.PatientName, Is.EqualTo("Henry Wilson"));
        Assert.That(state.InquiryType, Is.EqualTo(CongressionalInquiryType.Senate));
        Assert.That(state.CongressionalOfficeName, Is.EqualTo("Sen. John Doe Office"));
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Received));
    }

    [Test]
    public async Task CongressionalInquiryGrain_SetsFederalTimelines()
    {
        string id = $"PA-CONGRESS:{Guid.NewGuid()}";
        DateTime before = DateTime.UtcNow;
        await CreateInquiry(GetGrain(id), "PAT-011", "Irene Foster");

        CongressionalInquiryState state = await GetGrain(id).GetInquiryAsync();
        // 7-day acknowledgment requirement
        Assert.That(state.AcknowledgmentDue, Is.GreaterThanOrEqualTo(before.AddDays(6)));
        Assert.That(state.AcknowledgmentDue, Is.LessThanOrEqualTo(before.AddDays(8)));
        // 20-day response requirement
        Assert.That(state.ResponseDue, Is.GreaterThanOrEqualTo(before.AddDays(19)));
        Assert.That(state.ResponseDue, Is.LessThanOrEqualTo(before.AddDays(21)));
    }

    [Test]
    public async Task CongressionalInquiryGrain_CanAssignHandler()
    {
        string id = $"PA-CONGRESS:{Guid.NewGuid()}";
        await CreateInquiry(GetGrain(id), "PAT-012", "James Carter");

        await GetGrain(id).AssignHandlerAsync("HANDLER-201", "Dr. Sarah Mills");

        CongressionalInquiryState state = await GetGrain(id).GetInquiryAsync();
        Assert.That(state.AssignedHandlerId, Is.EqualTo("HANDLER-201"));
        Assert.That(state.AssignedHandlerName, Is.EqualTo("Dr. Sarah Mills"));
    }

    [Test]
    public async Task CongressionalInquiryGrain_CanAcknowledgeInquiry()
    {
        string id = $"PA-CONGRESS:{Guid.NewGuid()}";
        await CreateInquiry(GetGrain(id), "PAT-013", "Karen Thomas");

        DateTime ackDate = DateTime.UtcNow;
        await GetGrain(id).AcknowledgeInquiryAsync(ackDate);

        CongressionalInquiryState state = await GetGrain(id).GetInquiryAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Acknowledged));
        Assert.That(state.AcknowledgmentDate, Is.Not.Null);
    }

    [Test]
    public async Task CongressionalInquiryGrain_CanRecordInterimResponse()
    {
        string id = $"PA-CONGRESS:{Guid.NewGuid()}";
        await CreateInquiry(GetGrain(id), "PAT-014", "Louis Martin");
        await GetGrain(id).AcknowledgeInquiryAsync(DateTime.UtcNow);

        await GetGrain(id).RecordInterimResponseAsync("We are investigating the matter and will respond within 20 days.");

        CongressionalInquiryState state = await GetGrain(id).GetInquiryAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.UnderInvestigation));
        Assert.That(state.InterimResponseText, Is.Not.Empty);
        Assert.That(state.InterimResponseDate, Is.Not.Null);
    }

    [Test]
    public async Task CongressionalInquiryGrain_CanCompleteInquiry()
    {
        string id = $"PA-CONGRESS:{Guid.NewGuid()}";
        await CreateInquiry(GetGrain(id), "PAT-015", "Maria Lopez");
        await GetGrain(id).AcknowledgeInquiryAsync(DateTime.UtcNow);

        await GetGrain(id).CompleteInquiryAsync(
            "After thorough review, we determined the delay was due to missing documentation. Corrective action was taken.",
            ResolutionOutcome.Substantiated);

        CongressionalInquiryState state = await GetGrain(id).GetInquiryAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Resolved));
        Assert.That(state.Outcome, Is.EqualTo(ResolutionOutcome.Substantiated));
        Assert.That(state.FinalResponseText, Is.Not.Empty);
        Assert.That(state.FinalResponseDate, Is.Not.Null);
    }
}

// ── CongressionalInquiryIndexGrain Tests ─────────────────────────────────────

[TestFixture]
public class CongressionalInquiryIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private CongressionalInquiryIndexEntry MakeEntry(string id, string patientId,
        ComplaintStatus status, bool responseOverdue = false) => new()
    {
        InquiryId = id,
        PatientId = patientId,
        PatientName = "Test Patient",
        ReceivedDate = DateTime.UtcNow.AddDays(-10),
        InquiryType = CongressionalInquiryType.Senate,
        CongressionalOfficeName = "Sen. Test Office",
        Status = status,
        AcknowledgmentDue = DateTime.UtcNow.AddDays(responseOverdue ? -15 : 3),
        ResponseDue = DateTime.UtcNow.AddDays(responseOverdue ? -5 : 10),
        IsAcknowledgmentOverdue = false,
        IsResponseOverdue = responseOverdue
    };

    [Test]
    public async Task CongressionalInquiryIndexGrain_CanUpsertAndRetrieve()
    {
        ICongressionalInquiryIndexGrain index = _cluster.GrainFactory.GetGrain<ICongressionalInquiryIndexGrain>("PA-CONGRESS-IDX-TEST-1");

        await index.UpsertInquiryAsync(MakeEntry("PA-CI-1", "PAT-Q", ComplaintStatus.Received));
        await index.UpsertInquiryAsync(MakeEntry("PA-CI-2", "PAT-R", ComplaintStatus.Acknowledged));

        List<CongressionalInquiryIndexEntry> all = await index.GetAllInquiriesAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CongressionalInquiryIndexGrain_FiltersPending()
    {
        ICongressionalInquiryIndexGrain index = _cluster.GrainFactory.GetGrain<ICongressionalInquiryIndexGrain>("PA-CONGRESS-IDX-TEST-2");

        await index.UpsertInquiryAsync(MakeEntry("PA-CI-P1", "PAT-S", ComplaintStatus.Received));
        await index.UpsertInquiryAsync(MakeEntry("PA-CI-P2", "PAT-T", ComplaintStatus.UnderInvestigation));
        await index.UpsertInquiryAsync(MakeEntry("PA-CI-P3", "PAT-U", ComplaintStatus.Resolved));

        List<CongressionalInquiryIndexEntry> pending = await index.GetPendingInquiriesAsync();
        Assert.That(pending, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CongressionalInquiryIndexGrain_FiltersByPatient()
    {
        ICongressionalInquiryIndexGrain index = _cluster.GrainFactory.GetGrain<ICongressionalInquiryIndexGrain>("PA-CONGRESS-IDX-TEST-3");

        await index.UpsertInquiryAsync(MakeEntry("PA-CI-PAT1", "PAT-V", ComplaintStatus.Received));
        await index.UpsertInquiryAsync(MakeEntry("PA-CI-PAT2", "PAT-V", ComplaintStatus.Acknowledged));
        await index.UpsertInquiryAsync(MakeEntry("PA-CI-PAT3", "PAT-W", ComplaintStatus.Received));

        List<CongressionalInquiryIndexEntry> patV = await index.GetInquiriesByPatientAsync("PAT-V");
        Assert.That(patV, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CongressionalInquiryIndexGrain_FiltersOverdue()
    {
        ICongressionalInquiryIndexGrain index = _cluster.GrainFactory.GetGrain<ICongressionalInquiryIndexGrain>("PA-CONGRESS-IDX-TEST-4");

        await index.UpsertInquiryAsync(MakeEntry("PA-CI-OD1", "PAT-X", ComplaintStatus.Received, responseOverdue: true));
        await index.UpsertInquiryAsync(MakeEntry("PA-CI-OD2", "PAT-Y", ComplaintStatus.Received, responseOverdue: false));
        await index.UpsertInquiryAsync(MakeEntry("PA-CI-OD3", "PAT-Z", ComplaintStatus.Resolved, responseOverdue: true)); // resolved = excluded

        List<CongressionalInquiryIndexEntry> overdue = await index.GetOverdueInquiriesAsync();
        Assert.That(overdue, Has.Count.EqualTo(1));
        Assert.That(overdue[0].InquiryId, Is.EqualTo("PA-CI-OD1"));
    }
}

// ── PA Integration Tests ──────────────────────────────────────────────────────

[TestFixture]
public class PAIntegrationTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task PA_ComplaintFullLifecycle()
    {
        string complaintId = $"PA-COMPLAINT:{Guid.NewGuid()}";
        IComplaintGrain complaint = _cluster.GrainFactory.GetGrain<IComplaintGrain>(complaintId);
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-INT-IDX-1");

        // File
        await complaint.FileComplaintAsync(
            "PAT-INT-1", "Nathan Brooks", DateTime.UtcNow,
            ComplaintType.Formal, ComplaintCategory.QualityOfCare, ComplaintPriority.Routine,
            InquirySource.PatientSelf, "Patient experienced delayed care.", "Wants formal apology.",
            "Emergency Dept", string.Empty, string.Empty, false, "Advocate1");
        ComplaintState state = await complaint.GetComplaintAsync();
        await index.UpsertComplaintAsync(new ComplaintIndexEntry
        {
            ComplaintId = state.ComplaintId, PatientId = state.PatientId, PatientName = state.PatientName,
            ReceivedDate = state.ReceivedDate, ComplaintType = state.ComplaintType, Category = state.Category,
            Priority = state.Priority, Status = state.Status, AssignedAdvocateName = state.AssignedAdvocateName,
            ResponseDue = state.ResponseDue
        });

        // Assign
        await complaint.AssignAdvocateAsync("ADV-001", "Dr. Susan Reed");

        // Acknowledge
        await complaint.AcknowledgeComplaintAsync(DateTime.UtcNow);
        state = await complaint.GetComplaintAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Acknowledged));

        // Add actions
        await complaint.AddActionTakenAsync("Spoke with ED director");
        await complaint.AddActionTakenAsync("Reviewed patient timeline");

        // Resolve
        await complaint.ResolveComplaintAsync(ResolutionOutcome.Substantiated, "Delay confirmed; staff counseled.");
        state = await complaint.GetComplaintAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Resolved));
        Assert.That(state.ActionsTaken, Has.Count.EqualTo(2));

        // Close
        await complaint.CloseComplaintAsync();
        state = await complaint.GetComplaintAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Closed));
        Assert.That(state.ClosedDate, Is.Not.Null);
    }

    [Test]
    public async Task PA_CongressionalInquiryFullLifecycle()
    {
        string inquiryId = $"PA-CONGRESS:{Guid.NewGuid()}";
        ICongressionalInquiryGrain inquiry = _cluster.GrainFactory.GetGrain<ICongressionalInquiryGrain>(inquiryId);
        ICongressionalInquiryIndexGrain index = _cluster.GrainFactory.GetGrain<ICongressionalInquiryIndexGrain>("PA-CONGRESS-INT-1");

        // Create
        await inquiry.CreateInquiryAsync(
            "PAT-INT-2", "Oliver Grant", CongressionalInquiryType.HouseOfRepresentatives,
            "Rep. Jane Smith Office", "John Adams", "703-555-0200", "jsmith@house.gov",
            "Delayed disability rating decision", "Veteran waiting for 24 months.",
            string.Empty, "Advocate1");

        CongressionalInquiryState state = await inquiry.GetInquiryAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Received));

        // Assign
        await inquiry.AssignHandlerAsync("H-001", "Director of Patient Advocacy");

        // Acknowledge within 7 days
        await inquiry.AcknowledgeInquiryAsync(DateTime.UtcNow);
        state = await inquiry.GetInquiryAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Acknowledged));
        Assert.That(state.AcknowledgmentDate, Is.Not.Null);

        // Interim response
        await inquiry.RecordInterimResponseAsync("Under investigation. Full response by deadline.");
        state = await inquiry.GetInquiryAsync();
        Assert.That(state.InterimResponseDate, Is.Not.Null);

        // Complete
        await inquiry.CompleteInquiryAsync("Rating processed; delays due to system error; corrective action taken.",
            ResolutionOutcome.Substantiated);
        state = await inquiry.GetInquiryAsync();
        Assert.That(state.Status, Is.EqualTo(ComplaintStatus.Resolved));
        Assert.That(state.FinalResponseDate, Is.Not.Null);
    }

    [Test]
    public async Task PA_MultipleComplaintsTrackedInIndex()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-INT-MULTI-IDX");

        for (int i = 1; i <= 5; i++)
        {
            string id = $"PA-COMPLAINT:{Guid.NewGuid()}";
            IComplaintGrain grain = _cluster.GrainFactory.GetGrain<IComplaintGrain>(id);
            await grain.FileComplaintAsync(
                $"PAT-MULTI-{i}", $"Patient {i}", DateTime.UtcNow,
                ComplaintType.Informal, ComplaintCategory.WaitTime, ComplaintPriority.Routine,
                InquirySource.PatientSelf, "Wait time issue.", string.Empty,
                "Outpatient", string.Empty, string.Empty, false, "Advocate1");
            ComplaintState state = await grain.GetComplaintAsync();
            await index.UpsertComplaintAsync(new ComplaintIndexEntry
            {
                ComplaintId = state.ComplaintId, PatientId = state.PatientId, PatientName = state.PatientName,
                ReceivedDate = state.ReceivedDate, ComplaintType = state.ComplaintType, Category = state.Category,
                Priority = state.Priority, Status = state.Status, AssignedAdvocateName = state.AssignedAdvocateName,
                ResponseDue = state.ResponseDue
            });
        }

        List<ComplaintIndexEntry> all = await index.GetAllComplaintsAsync();
        Assert.That(all, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task PA_CongressionalLinkedToComplaint()
    {
        // Create a complaint first
        string complaintId = $"PA-COMPLAINT:{Guid.NewGuid()}";
        IComplaintGrain complaint = _cluster.GrainFactory.GetGrain<IComplaintGrain>(complaintId);
        await complaint.FileComplaintAsync(
            "PAT-INT-3", "Patricia Wang", DateTime.UtcNow,
            ComplaintType.CongressionalInquiry, ComplaintCategory.BenefitsDetermination, ComplaintPriority.Urgent,
            InquirySource.CongressionalOffice, "Benefits denial complaint.", string.Empty,
            "Benefits", string.Empty, string.Empty, false, "Advocate1");

        // Create linked congressional inquiry
        string inquiryId = $"PA-CONGRESS:{Guid.NewGuid()}";
        ICongressionalInquiryGrain inquiry = _cluster.GrainFactory.GetGrain<ICongressionalInquiryGrain>(inquiryId);
        await inquiry.CreateInquiryAsync(
            "PAT-INT-3", "Patricia Wang", CongressionalInquiryType.Senate,
            "Sen. Robert Hall", "Staff Member", "202-555-0300", string.Empty,
            "Benefits denial", "Constituent inquiry about denied benefits claim.",
            complaintId, "Advocate1");

        CongressionalInquiryState state = await inquiry.GetInquiryAsync();
        Assert.That(state.LinkedComplaintId, Is.EqualTo(complaintId));
        Assert.That(state.PatientId, Is.EqualTo("PAT-INT-3"));
    }

    [Test]
    public async Task PA_OverdueComplaintsDetectedInIndex()
    {
        IComplaintIndexGrain index = _cluster.GrainFactory.GetGrain<IComplaintIndexGrain>("PA-INT-OVERDUE-IDX");

        // Past due complaint
        await index.UpsertComplaintAsync(new ComplaintIndexEntry
        {
            ComplaintId = "PA-OVERDUE-1", PatientId = "PAT-OD", PatientName = "Overdue Patient",
            ReceivedDate = DateTime.UtcNow.AddDays(-40), ComplaintType = ComplaintType.Formal,
            Category = ComplaintCategory.QualityOfCare, Priority = ComplaintPriority.Routine,
            Status = ComplaintStatus.Acknowledged, AssignedAdvocateName = "Advocate",
            ResponseDue = DateTime.UtcNow.AddDays(-10)
        });

        // On-time complaint
        await index.UpsertComplaintAsync(new ComplaintIndexEntry
        {
            ComplaintId = "PA-ONTIME-1", PatientId = "PAT-OT", PatientName = "On Time Patient",
            ReceivedDate = DateTime.UtcNow.AddDays(-5), ComplaintType = ComplaintType.Informal,
            Category = ComplaintCategory.WaitTime, Priority = ComplaintPriority.Routine,
            Status = ComplaintStatus.Received, AssignedAdvocateName = string.Empty,
            ResponseDue = DateTime.UtcNow.AddDays(20)
        });

        List<ComplaintIndexEntry> overdue = await index.GetOverdueComplaintsAsync();
        Assert.That(overdue, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(overdue.Any(o => o.ComplaintId == "PA-OVERDUE-1"), Is.True);
    }
}
