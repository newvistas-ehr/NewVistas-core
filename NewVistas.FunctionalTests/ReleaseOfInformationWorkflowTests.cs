// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Release of Information — VistA File #195.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class ReleaseOfInformationWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IROIRequestGrain GetRequest(string id)
        => _cluster.GrainFactory.GetGrain<IROIRequestGrain>(id);

    private IROIRequestIndexGrain GetRequestIndex()
        => _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-REQUEST-IDX");

    private IHIPAADisclosureGrain GetDisclosure(string id)
        => _cluster.GrainFactory.GetGrain<IHIPAADisclosureGrain>(id);

    private IHIPAADisclosureIndexGrain GetDisclosureIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IHIPAADisclosureIndexGrain>($"ROI-DISC-IDX:{patientId}");

    // ── ROI Request Tests ────────────────────────────────────────────────────

    [Test]
    public async Task SubmitRequest_SetsReceivedStatus_AndRecordsFields()
    {
        string requestId = $"ROI-REQ-{Guid.NewGuid():N}";
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IROIRequestGrain grain = GetRequest(requestId);

        await grain.SubmitRequestAsync(
            patientId, "DOE,JOHN", new DateTime(1950, 3, 15),
            ROIRequestType.MedicalRecords, RequesterType.Patient,
            "John Doe", string.Empty, "123 Main St", "555-1234", string.Empty, "john@test.com",
            "Personal use", new List<string> { "Progress Notes", "Lab Results" },
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31),
            ROIRequestPriority.Routine, "ROI-CLERK-001");

        ROIRequestState state = await grain.GetRequestAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(state.RequestType, Is.EqualTo(ROIRequestType.MedicalRecords));
        Assert.That(state.RequesterType, Is.EqualTo(RequesterType.Patient));
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Received));
        Assert.That(state.RecordsRequested, Has.Count.EqualTo(2));
        Assert.That(state.Priority, Is.EqualTo(ROIRequestPriority.Routine));
    }

    [Test]
    public async Task AssignStaff_UpdatesAssignedFields()
    {
        string requestId = $"ROI-REQ-{Guid.NewGuid():N}";
        IROIRequestGrain grain = GetRequest(requestId);

        await grain.SubmitRequestAsync(
            "PAT-001", "SMITH,JANE", null,
            ROIRequestType.BillingRecords, RequesterType.InsuranceCompany,
            "Aetna Claims", "Aetna Inc", "PO Box 999", "800-555-0000", "800-555-0001", string.Empty,
            "Insurance claim processing", new List<string> { "Billing Records" },
            null, null, ROIRequestPriority.Expedited, "CLERK-002");

        await grain.AssignStaffAsync("STAFF-100", "Mary Jones");

        ROIRequestState state = await grain.GetRequestAsync();
        Assert.That(state.AssignedStaffId, Is.EqualTo("STAFF-100"));
        Assert.That(state.AssignedStaffName, Is.EqualTo("Mary Jones"));
    }

    [Test]
    public async Task UpdateAuthorization_SetsAuthFields()
    {
        string requestId = $"ROI-REQ-{Guid.NewGuid():N}";
        IROIRequestGrain grain = GetRequest(requestId);

        await grain.SubmitRequestAsync(
            "PAT-002", "BROWN,BOB", null,
            ROIRequestType.WholeRecord, RequesterType.Attorney,
            "Attorney Smith", "Smith Law LLC", "456 Legal Ave", "555-9876", string.Empty, string.Empty,
            "Legal proceeding", new List<string> { "Entire Record" },
            null, null, ROIRequestPriority.Urgent, "CLERK-003");

        DateTime authDate = DateTime.UtcNow;
        DateTime authExpiration = authDate.AddMonths(6);
        await grain.UpdateAuthorizationAsync(AuthorizationStatus.Received, authDate, authExpiration);

        ROIRequestState state = await grain.GetRequestAsync();
        Assert.That(state.AuthorizationStatus, Is.EqualTo(AuthorizationStatus.Received));
        Assert.That(state.AuthorizationDate, Is.Not.Null);
        Assert.That(state.AuthorizationExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task FulfillRequest_SetsStatusFulfilled_AndRecordsFulfillmentDetails()
    {
        string requestId = $"ROI-REQ-{Guid.NewGuid():N}";
        IROIRequestGrain grain = GetRequest(requestId);

        await grain.SubmitRequestAsync(
            "PAT-003", "GREEN,ALICE", null,
            ROIRequestType.LabRecords, RequesterType.HealthcareProvider,
            "Dr. Wilson", "VA Primary Care", "789 Health Blvd", "555-4444", string.Empty, string.Empty,
            "Continuity of care", new List<string> { "Lab Results 2024" },
            null, null, ROIRequestPriority.Routine, "CLERK-004");

        await grain.FulfillRequestAsync(FulfillmentMethod.ElectronicTransfer, "Sent via secure email", 42, 0m);

        ROIRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Fulfilled));
        Assert.That(state.FulfillmentMethod, Is.EqualTo(FulfillmentMethod.ElectronicTransfer));
        Assert.That(state.NumberOfPagesFulfilled, Is.EqualTo(42));
        Assert.That(state.FeeCharged, Is.EqualTo(0m));
        Assert.That(state.FulfillmentDate, Is.Not.Null);
    }

    [Test]
    public async Task DenyRequest_SetsStatusDenied_WithReason()
    {
        string requestId = $"ROI-REQ-{Guid.NewGuid():N}";
        IROIRequestGrain grain = GetRequest(requestId);

        await grain.SubmitRequestAsync(
            "PAT-004", "WHITE,TOM", null,
            ROIRequestType.SubstanceAbuseRecords, RequesterType.LawEnforcement,
            "Det. Johnson", "Local PD", "100 Police Ln", "555-0000", string.Empty, string.Empty,
            "Investigation", new List<string> { "42 CFR Part 2 Records" },
            null, null, ROIRequestPriority.Routine, "CLERK-005");

        await grain.DenyRequestAsync("42 CFR Part 2 records require specific court order");

        ROIRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Denied));
        Assert.That(state.DenialReason, Does.Contain("42 CFR Part 2"));
    }

    [Test]
    public async Task UpdateStatus_TransitionsCorrectly()
    {
        string requestId = $"ROI-REQ-{Guid.NewGuid():N}";
        IROIRequestGrain grain = GetRequest(requestId);

        await grain.SubmitRequestAsync(
            "PAT-005", "KING,SUE", null,
            ROIRequestType.ImagingRecords, RequesterType.PatientRepresentative,
            "Tom King", string.Empty, string.Empty, "555-1111", string.Empty, string.Empty,
            "Patient representative request", new List<string> { "MRI Results" },
            null, null, ROIRequestPriority.Routine, "CLERK-006");

        await grain.UpdateStatusAsync(ROIRequestStatus.InProcess, "Pulling imaging records");

        ROIRequestState state = await grain.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.InProcess));
        Assert.That(state.ProcessingNotes, Does.Contain("Pulling imaging"));
    }

    // ── Index Tests ──────────────────────────────────────────────────────────

    [Test]
    public async Task RequestIndex_UpsertAndQueryByStatus()
    {
        IROIRequestIndexGrain index = GetRequestIndex();

        string id1 = $"ROI-REQ-{Guid.NewGuid():N}";
        string id2 = $"ROI-REQ-{Guid.NewGuid():N}";

        await index.UpsertRequestAsync(new ROIRequestIndexEntry
        {
            RequestId = id1, PatientId = "PAT-IDX-1", PatientName = "DOE,JOHN",
            ReceivedDate = DateTime.UtcNow, RequestType = ROIRequestType.MedicalRecords,
            RequesterType = RequesterType.Patient, RequesterName = "John Doe",
            Status = ROIRequestStatus.Received, DueDate = DateTime.UtcNow.AddDays(30),
            Priority = ROIRequestPriority.Routine
        });

        await index.UpsertRequestAsync(new ROIRequestIndexEntry
        {
            RequestId = id2, PatientId = "PAT-IDX-2", PatientName = "SMITH,JANE",
            ReceivedDate = DateTime.UtcNow, RequestType = ROIRequestType.BillingRecords,
            RequesterType = RequesterType.InsuranceCompany, RequesterName = "Aetna",
            Status = ROIRequestStatus.Fulfilled, DueDate = DateTime.UtcNow.AddDays(30),
            Priority = ROIRequestPriority.Expedited
        });

        List<ROIRequestIndexEntry> received = await index.GetRequestsByStatusAsync(ROIRequestStatus.Received);
        Assert.That(received.Any(r => r.RequestId == id1), Is.True);

        List<ROIRequestIndexEntry> fulfilled = await index.GetRequestsByStatusAsync(ROIRequestStatus.Fulfilled);
        Assert.That(fulfilled.Any(r => r.RequestId == id2), Is.True);
    }

    [Test]
    public async Task RequestIndex_QueryByPatient()
    {
        IROIRequestIndexGrain index = GetRequestIndex();
        string patientId = $"PAT-QUERY-{Guid.NewGuid():N}";

        await index.UpsertRequestAsync(new ROIRequestIndexEntry
        {
            RequestId = $"ROI-REQ-{Guid.NewGuid():N}", PatientId = patientId,
            PatientName = "TEST,PATIENT", ReceivedDate = DateTime.UtcNow,
            RequestType = ROIRequestType.WholeRecord, RequesterType = RequesterType.Patient,
            RequesterName = "Test Patient", Status = ROIRequestStatus.Received,
            DueDate = DateTime.UtcNow.AddDays(30), Priority = ROIRequestPriority.Routine
        });

        List<ROIRequestIndexEntry> results = await index.GetRequestsByPatientAsync(patientId);
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0].PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task RequestIndex_QueryByRequesterType()
    {
        IROIRequestIndexGrain index = GetRequestIndex();
        string reqId = $"ROI-REQ-{Guid.NewGuid():N}";

        await index.UpsertRequestAsync(new ROIRequestIndexEntry
        {
            RequestId = reqId, PatientId = "PAT-RTYPE-1", PatientName = "TEST,RTYPE",
            ReceivedDate = DateTime.UtcNow, RequestType = ROIRequestType.MedicalRecords,
            RequesterType = RequesterType.ResearchInstitution, RequesterName = "NIH",
            Status = ROIRequestStatus.Received, DueDate = DateTime.UtcNow.AddDays(30),
            Priority = ROIRequestPriority.Routine
        });

        List<ROIRequestIndexEntry> results = await index.GetRequestsByRequesterTypeAsync(RequesterType.ResearchInstitution);
        Assert.That(results.Any(r => r.RequestId == reqId), Is.True);
    }

    // ── HIPAA Disclosure Tests ───────────────────────────────────────────────

    [Test]
    public async Task RecordDisclosure_CreatesDisclosureRecord()
    {
        string disclosureId = $"ROI-DISC-{Guid.NewGuid():N}";
        string patientId = $"PAT-DISC-{Guid.NewGuid():N}";
        IHIPAADisclosureGrain grain = GetDisclosure(disclosureId);

        await grain.RecordDisclosureAsync(
            patientId, "DOE,JOHN",
            HIPAADisclosureType.LawEnforcement,
            "Det. Johnson", "Local Police Dept", "100 Police Ln",
            "Court-ordered disclosure", "Progress notes Jan-Jun 2024",
            "01/2024 - 06/2024", 15, false,
            string.Empty, "ROI Clerk Smith", "HIM Technician");

        HIPAADisclosureState state = await grain.GetDisclosureAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.DisclosureType, Is.EqualTo(HIPAADisclosureType.LawEnforcement));
        Assert.That(state.RecipientName, Is.EqualTo("Det. Johnson"));
        Assert.That(state.NumberOfPages, Is.EqualTo(15));
        Assert.That(state.IsSubjectToAccounting, Is.True);
    }

    [Test]
    public async Task RecordTPODisclosure_IsNotSubjectToAccounting()
    {
        string disclosureId = $"ROI-DISC-{Guid.NewGuid():N}";
        string patientId = $"PAT-TPO-{Guid.NewGuid():N}";
        IHIPAADisclosureGrain grain = GetDisclosure(disclosureId);

        await grain.RecordDisclosureAsync(
            patientId, "SMITH,JANE",
            HIPAADisclosureType.Treatment,
            "Dr. Wilson", "VA Cardiology", "VA Medical Center",
            "Referral for cardiac consult", "Cardiology records",
            "2024", 5, false,
            string.Empty, "Dr. Brown", "Attending Physician");

        HIPAADisclosureState state = await grain.GetDisclosureAsync();
        Assert.That(state.IsSubjectToAccounting, Is.False);
    }

    [Test]
    public async Task DisclosureIndex_QueryByDateRange()
    {
        string patientId = $"PAT-DIDX-{Guid.NewGuid():N}";
        IHIPAADisclosureIndexGrain index = GetDisclosureIndex(patientId);

        DateTime now = DateTime.UtcNow;
        await index.UpsertDisclosureAsync(new HIPAADisclosureIndexEntry
        {
            DisclosureId = $"DISC-{Guid.NewGuid():N}",
            PatientId = patientId, PatientName = "TEST,DISC",
            DisclosureDate = now.AddDays(-10),
            DisclosureType = HIPAADisclosureType.PublicHealth,
            RecipientName = "State Health Dept",
            PurposeOfDisclosure = "Disease reporting",
            IsSubjectToAccounting = true
        });

        List<HIPAADisclosureIndexEntry> results = await index.GetDisclosuresByDateRangeAsync(
            now.AddDays(-30), now);
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
    }
}
