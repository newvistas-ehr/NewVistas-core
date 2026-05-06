// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ── Test Cluster Setup ────────────────────────────────────────────────────────

file class ROITestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("roiRequestStore");
        siloBuilder.AddMemoryGrainStorage("roiRequestIndexStore");
        siloBuilder.AddMemoryGrainStorage("roiDisclosureStore");
        siloBuilder.AddMemoryGrainStorage("roiDisclosureIndexStore");
    }
}

// ── ROIRequestGrain Tests ─────────────────────────────────────────────────────

[TestFixture]
public class ROIRequestGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IROIRequestGrain GetGrain(string id) => _cluster.GrainFactory.GetGrain<IROIRequestGrain>(id);

    private Task SubmitBasicRequest(IROIRequestGrain grain, string patientId, string patientName,
        RequesterType requesterType = RequesterType.Patient,
        ROIRequestPriority priority = ROIRequestPriority.Routine) =>
        grain.SubmitRequestAsync(
            patientId, patientName, new DateTime(1975, 6, 15),
            ROIRequestType.MedicalRecords, requesterType,
            "Jane Doe", "Self", "123 Main St", "555-0100", "555-0101", "jdoe@example.com",
            "Continuity of care", new List<string> { "Progress Notes", "Lab Results" },
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31),
            priority, "ROIStaff1");

    [Test]
    public async Task ROIRequestGrain_CanSubmitRequest()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        await SubmitBasicRequest(GetGrain(id), "PAT-001", "Alice Brown");

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PatientName, Is.EqualTo("Alice Brown"));
        Assert.That(state.RequestType, Is.EqualTo(ROIRequestType.MedicalRecords));
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Received));
        Assert.That(state.RecordsRequested, Has.Count.EqualTo(2));
        Assert.That(state.DueDate, Is.GreaterThan(DateTime.UtcNow));
    }

    [Test]
    public async Task ROIRequestGrain_SetsThirtyDayDueDate_Routine()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        DateTime before = DateTime.UtcNow;
        await SubmitBasicRequest(GetGrain(id), "PAT-002", "Bob Clark");

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.DueDate, Is.GreaterThanOrEqualTo(before.AddDays(29)));
        Assert.That(state.DueDate, Is.LessThanOrEqualTo(before.AddDays(31)));
    }

    [Test]
    public async Task ROIRequestGrain_SetsAuthorizationPending_ForNonPatient()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        await SubmitBasicRequest(GetGrain(id), "PAT-003", "Carol Davis",
            requesterType: RequesterType.Attorney);

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.AuthorizationStatus, Is.EqualTo(AuthorizationStatus.Pending));
    }

    [Test]
    public async Task ROIRequestGrain_SetsAuthorizationNotRequired_ForPatient()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        await SubmitBasicRequest(GetGrain(id), "PAT-004", "David Evans",
            requesterType: RequesterType.Patient);

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.AuthorizationStatus, Is.EqualTo(AuthorizationStatus.NotRequired));
    }

    [Test]
    public async Task ROIRequestGrain_CanAssignStaff()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        await SubmitBasicRequest(GetGrain(id), "PAT-005", "Eve Foster");

        await GetGrain(id).AssignStaffAsync("STAFF-101", "Mary Smith, ROI Specialist");

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.AssignedStaffId, Is.EqualTo("STAFF-101"));
        Assert.That(state.AssignedStaffName, Is.EqualTo("Mary Smith, ROI Specialist"));
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Acknowledged));
    }

    [Test]
    public async Task ROIRequestGrain_CanUpdateAuthorization()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        await SubmitBasicRequest(GetGrain(id), "PAT-006", "Frank Green",
            requesterType: RequesterType.InsuranceCompany);

        DateTime authDate = DateTime.UtcNow;
        DateTime expDate = authDate.AddYears(1);
        await GetGrain(id).UpdateAuthorizationAsync(AuthorizationStatus.Received, authDate, expDate);

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.AuthorizationStatus, Is.EqualTo(AuthorizationStatus.Received));
        Assert.That(state.AuthorizationDate, Is.Not.Null);
        Assert.That(state.AuthorizationExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task ROIRequestGrain_CanFulfillRequest()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        await SubmitBasicRequest(GetGrain(id), "PAT-007", "Grace Hall");
        await GetGrain(id).AssignStaffAsync("STAFF-102", "John Doe");

        await GetGrain(id).FulfillRequestAsync(FulfillmentMethod.Mail, "Sent via USPS certified mail.", 42, 12.60m);

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Fulfilled));
        Assert.That(state.FulfillmentMethod, Is.EqualTo(FulfillmentMethod.Mail));
        Assert.That(state.NumberOfPagesFulfilled, Is.EqualTo(42));
        Assert.That(state.FeeCharged, Is.EqualTo(12.60m));
        Assert.That(state.FulfillmentDate, Is.Not.Null);
    }

    [Test]
    public async Task ROIRequestGrain_CanDenyRequest()
    {
        string id = $"ROI-REQUEST:{Guid.NewGuid()}";
        await SubmitBasicRequest(GetGrain(id), "PAT-008", "Henry Irving",
            requesterType: RequesterType.LawEnforcement);

        await GetGrain(id).DenyRequestAsync("No valid court order presented. 45 CFR 164.512(f) requirements not met.");

        ROIRequestState state = await GetGrain(id).GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Denied));
        Assert.That(state.DenialReason, Is.Not.Empty);
    }

    [Test]
    public async Task ROIRequestGrain_UrgentRequestHasShorterDueDate()
    {
        string urgentId = $"ROI-REQUEST:{Guid.NewGuid()}";
        string routineId = $"ROI-REQUEST:{Guid.NewGuid()}";

        await SubmitBasicRequest(GetGrain(urgentId), "PAT-009", "Iris Jones", priority: ROIRequestPriority.Urgent);
        await SubmitBasicRequest(GetGrain(routineId), "PAT-010", "Jack King", priority: ROIRequestPriority.Routine);

        ROIRequestState urgent = await GetGrain(urgentId).GetRequestAsync();
        ROIRequestState routine = await GetGrain(routineId).GetRequestAsync();

        Assert.That(urgent.DueDate, Is.LessThan(routine.DueDate));
        Assert.That(urgent.DueDate, Is.LessThan(DateTime.UtcNow.AddDays(5)));
    }
}

// ── ROIRequestIndexGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class ROIRequestIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ROIRequestIndexEntry MakeEntry(string id, string patientId, ROIRequestStatus status,
        RequesterType requesterType = RequesterType.Patient,
        DateTime? dueDate = null) => new()
    {
        RequestId = id,
        PatientId = patientId,
        PatientName = "Test Patient",
        ReceivedDate = DateTime.UtcNow.AddDays(-5),
        RequestType = ROIRequestType.MedicalRecords,
        RequesterType = requesterType,
        RequesterName = "Test Requester",
        Status = status,
        DueDate = dueDate ?? DateTime.UtcNow.AddDays(25),
        AssignedStaffName = string.Empty,
        Priority = ROIRequestPriority.Routine
    };

    [Test]
    public async Task ROIRequestIndexGrain_CanUpsertAndRetrieve()
    {
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-IDX-TEST-1");

        await index.UpsertRequestAsync(MakeEntry("REQ-1", "PAT-A", ROIRequestStatus.Received));
        await index.UpsertRequestAsync(MakeEntry("REQ-2", "PAT-B", ROIRequestStatus.InProcess));

        List<ROIRequestIndexEntry> all = await index.GetAllRequestsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ROIRequestIndexGrain_UpdatesExistingEntry()
    {
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-IDX-TEST-2");

        await index.UpsertRequestAsync(MakeEntry("REQ-UPD", "PAT-C", ROIRequestStatus.Received));
        await index.UpsertRequestAsync(MakeEntry("REQ-UPD", "PAT-C", ROIRequestStatus.Fulfilled));

        List<ROIRequestIndexEntry> all = await index.GetAllRequestsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(ROIRequestStatus.Fulfilled));
    }

    [Test]
    public async Task ROIRequestIndexGrain_FiltersByStatus()
    {
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-IDX-TEST-3");

        await index.UpsertRequestAsync(MakeEntry("REQ-S1", "PAT-D", ROIRequestStatus.Received));
        await index.UpsertRequestAsync(MakeEntry("REQ-S2", "PAT-E", ROIRequestStatus.Fulfilled));
        await index.UpsertRequestAsync(MakeEntry("REQ-S3", "PAT-F", ROIRequestStatus.Received));

        List<ROIRequestIndexEntry> received = await index.GetRequestsByStatusAsync(ROIRequestStatus.Received);
        Assert.That(received, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ROIRequestIndexGrain_FiltersByPatient()
    {
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-IDX-TEST-4");

        await index.UpsertRequestAsync(MakeEntry("REQ-P1", "PAT-G", ROIRequestStatus.Received));
        await index.UpsertRequestAsync(MakeEntry("REQ-P2", "PAT-G", ROIRequestStatus.Fulfilled));
        await index.UpsertRequestAsync(MakeEntry("REQ-P3", "PAT-H", ROIRequestStatus.Received));

        List<ROIRequestIndexEntry> patG = await index.GetRequestsByPatientAsync("PAT-G");
        Assert.That(patG, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ROIRequestIndexGrain_FiltersByRequesterType()
    {
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-IDX-TEST-5");

        await index.UpsertRequestAsync(MakeEntry("REQ-R1", "PAT-I", ROIRequestStatus.Received, RequesterType.Attorney));
        await index.UpsertRequestAsync(MakeEntry("REQ-R2", "PAT-J", ROIRequestStatus.Received, RequesterType.InsuranceCompany));
        await index.UpsertRequestAsync(MakeEntry("REQ-R3", "PAT-K", ROIRequestStatus.Received, RequesterType.Attorney));

        List<ROIRequestIndexEntry> attorneys = await index.GetRequestsByRequesterTypeAsync(RequesterType.Attorney);
        Assert.That(attorneys, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ROIRequestIndexGrain_DetectsOverdueRequests()
    {
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-IDX-TEST-6");

        await index.UpsertRequestAsync(MakeEntry("REQ-OD1", "PAT-L", ROIRequestStatus.Received,
            dueDate: DateTime.UtcNow.AddDays(-5)));   // overdue
        await index.UpsertRequestAsync(MakeEntry("REQ-OD2", "PAT-M", ROIRequestStatus.Received,
            dueDate: DateTime.UtcNow.AddDays(10)));    // not overdue
        await index.UpsertRequestAsync(MakeEntry("REQ-OD3", "PAT-N", ROIRequestStatus.Fulfilled,
            dueDate: DateTime.UtcNow.AddDays(-5)));    // overdue but fulfilled (excluded)

        List<ROIRequestIndexEntry> overdue = await index.GetOverdueRequestsAsync();
        Assert.That(overdue, Has.Count.EqualTo(1));
        Assert.That(overdue[0].RequestId, Is.EqualTo("REQ-OD1"));
    }
}

// ── HIPAADisclosureGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class HIPAADisclosureGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IHIPAADisclosureGrain GetGrain(string id) => _cluster.GrainFactory.GetGrain<IHIPAADisclosureGrain>(id);

    private Task RecordDisclosure(IHIPAADisclosureGrain grain, string patientId,
        HIPAADisclosureType disclosureType, string linkedRequestId = "") =>
        grain.RecordDisclosureAsync(
            patientId, "Test Patient", disclosureType,
            "Recipient Name", "Recipient Org", "123 Recipient St",
            "For continuity of care", "Clinical summary",
            "2024-01-01 to 2024-12-31", 10, true,
            linkedRequestId, "Dr. Jones", "Attending Physician");

    [Test]
    public async Task HIPAADisclosureGrain_TPODisclosure_NotSubjectToAccounting()
    {
        string id = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
        await RecordDisclosure(GetGrain(id), "PAT-D1", HIPAADisclosureType.Treatment);

        HIPAADisclosureState state = await GetGrain(id).GetDisclosureAsync();
        Assert.That(state.IsSubjectToAccounting, Is.False);
        Assert.That(state.DisclosureType, Is.EqualTo(HIPAADisclosureType.Treatment));
    }

    [Test]
    public async Task HIPAADisclosureGrain_PaymentDisclosure_NotSubjectToAccounting()
    {
        string id = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
        await RecordDisclosure(GetGrain(id), "PAT-D2", HIPAADisclosureType.Payment);

        HIPAADisclosureState state = await GetGrain(id).GetDisclosureAsync();
        Assert.That(state.IsSubjectToAccounting, Is.False);
    }

    [Test]
    public async Task HIPAADisclosureGrain_LawEnforcementDisclosure_IsSubjectToAccounting()
    {
        string id = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
        await RecordDisclosure(GetGrain(id), "PAT-D3", HIPAADisclosureType.LawEnforcement);

        HIPAADisclosureState state = await GetGrain(id).GetDisclosureAsync();
        Assert.That(state.IsSubjectToAccounting, Is.True);
        Assert.That(state.DisclosedBy, Is.EqualTo("Dr. Jones"));
    }

    [Test]
    public async Task HIPAADisclosureGrain_PublicHealthDisclosure_IsSubjectToAccounting()
    {
        string id = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
        await RecordDisclosure(GetGrain(id), "PAT-D4", HIPAADisclosureType.PublicHealth);

        HIPAADisclosureState state = await GetGrain(id).GetDisclosureAsync();
        Assert.That(state.IsSubjectToAccounting, Is.True);
    }

    [Test]
    public async Task HIPAADisclosureGrain_PatientAuthorizationDisclosure_IsSubjectToAccounting()
    {
        string id = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
        await RecordDisclosure(GetGrain(id), "PAT-D5", HIPAADisclosureType.PatientAuthorization, "ROI-REQUEST:test-123");

        HIPAADisclosureState state = await GetGrain(id).GetDisclosureAsync();
        Assert.That(state.IsSubjectToAccounting, Is.True);
        Assert.That(state.LinkedRequestId, Is.EqualTo("ROI-REQUEST:test-123"));
    }

    [Test]
    public async Task HIPAADisclosureGrain_HealthcareOperations_NotSubjectToAccounting()
    {
        string id = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
        await RecordDisclosure(GetGrain(id), "PAT-D6", HIPAADisclosureType.HealthcareOperations);

        HIPAADisclosureState state = await GetGrain(id).GetDisclosureAsync();
        Assert.That(state.IsSubjectToAccounting, Is.False);
    }
}

// ── HIPAADisclosureIndexGrain Tests ──────────────────────────────────────────

[TestFixture]
public class HIPAADisclosureIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private HIPAADisclosureIndexEntry MakeEntry(string id, bool subjectToAccounting, DateTime? disclosureDate = null) => new()
    {
        DisclosureId = id,
        PatientId = "PAT-IDX-TEST",
        PatientName = "Index Test Patient",
        DisclosureDate = disclosureDate ?? DateTime.UtcNow.AddDays(-1),
        DisclosureType = subjectToAccounting ? HIPAADisclosureType.LawEnforcement : HIPAADisclosureType.Treatment,
        RecipientName = "Test Recipient",
        PurposeOfDisclosure = "Test purpose",
        IsSubjectToAccounting = subjectToAccounting,
        LinkedRequestId = string.Empty
    };

    [Test]
    public async Task HIPAADisclosureIndexGrain_CanUpsertAndRetrieve()
    {
        IHIPAADisclosureIndexGrain index = _cluster.GrainFactory.GetGrain<IHIPAADisclosureIndexGrain>("ROI-DISC-IDX:PAT-IX1");

        await index.UpsertDisclosureAsync(MakeEntry("DISC-1", false));
        await index.UpsertDisclosureAsync(MakeEntry("DISC-2", true));

        List<HIPAADisclosureIndexEntry> all = await index.GetAllDisclosuresAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task HIPAADisclosureIndexGrain_FiltersSubjectToAccounting()
    {
        IHIPAADisclosureIndexGrain index = _cluster.GrainFactory.GetGrain<IHIPAADisclosureIndexGrain>("ROI-DISC-IDX:PAT-IX2");

        await index.UpsertDisclosureAsync(MakeEntry("DISC-A1", true));
        await index.UpsertDisclosureAsync(MakeEntry("DISC-A2", false));  // TPO
        await index.UpsertDisclosureAsync(MakeEntry("DISC-A3", true));
        await index.UpsertDisclosureAsync(MakeEntry("DISC-A4", false));  // TPO

        List<HIPAADisclosureIndexEntry> accountable = await index.GetDisclosuresSubjectToAccountingAsync();
        Assert.That(accountable, Has.Count.EqualTo(2));
        Assert.That(accountable.All(d => d.IsSubjectToAccounting), Is.True);
    }

    [Test]
    public async Task HIPAADisclosureIndexGrain_FiltersByDateRange()
    {
        IHIPAADisclosureIndexGrain index = _cluster.GrainFactory.GetGrain<IHIPAADisclosureIndexGrain>("ROI-DISC-IDX:PAT-IX3");

        DateTime recent = DateTime.UtcNow.AddDays(-10);
        DateTime old = DateTime.UtcNow.AddDays(-400);

        await index.UpsertDisclosureAsync(MakeEntry("DISC-DR1", true, recent));
        await index.UpsertDisclosureAsync(MakeEntry("DISC-DR2", true, old));
        await index.UpsertDisclosureAsync(MakeEntry("DISC-DR3", false, recent));

        DateTime rangeStart = DateTime.UtcNow.AddDays(-30);
        DateTime rangeEnd = DateTime.UtcNow;
        List<HIPAADisclosureIndexEntry> ranged = await index.GetDisclosuresByDateRangeAsync(rangeStart, rangeEnd);

        Assert.That(ranged, Has.Count.EqualTo(2));
        Assert.That(ranged.All(d => d.DisclosureDate >= rangeStart && d.DisclosureDate <= rangeEnd), Is.True);
    }

    [Test]
    public async Task HIPAADisclosureIndexGrain_UpdatesExistingEntry()
    {
        IHIPAADisclosureIndexGrain index = _cluster.GrainFactory.GetGrain<IHIPAADisclosureIndexGrain>("ROI-DISC-IDX:PAT-IX4");

        await index.UpsertDisclosureAsync(MakeEntry("DISC-UPD", true));
        await index.UpsertDisclosureAsync(MakeEntry("DISC-UPD", false));  // update

        List<HIPAADisclosureIndexEntry> all = await index.GetAllDisclosuresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].IsSubjectToAccounting, Is.False);
    }
}

// ── ROI Integration Tests ─────────────────────────────────────────────────────

[TestFixture]
public class ROIIntegrationTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ROI_RequestFullLifecycle_PatientRequest()
    {
        string requestId = $"ROI-REQUEST:{Guid.NewGuid()}";
        IROIRequestGrain request = _cluster.GrainFactory.GetGrain<IROIRequestGrain>(requestId);
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-INT-IDX-1");

        // Submit
        await request.SubmitRequestAsync(
            "PAT-INT-1", "Nathan Brooks", new DateTime(1960, 3, 10),
            ROIRequestType.MedicalRecords, RequesterType.Patient,
            "Nathan Brooks", "Self", "456 Oak Ave", "555-1000", string.Empty, "nbrooks@email.com",
            "Personal review", new List<string> { "Complete medical record" },
            null, null, ROIRequestPriority.Routine, "ROIStaff1");

        ROIRequestState state = await request.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Received));
        Assert.That(state.AuthorizationStatus, Is.EqualTo(AuthorizationStatus.NotRequired));
        await index.UpsertRequestAsync(new ROIRequestIndexEntry
        {
            RequestId = state.RequestId, PatientId = state.PatientId, PatientName = state.PatientName,
            ReceivedDate = state.ReceivedDate, RequestType = state.RequestType,
            RequesterType = state.RequesterType, RequesterName = state.RequesterName,
            Status = state.Status, DueDate = state.DueDate, AssignedStaffName = state.AssignedStaffName,
            Priority = state.Priority
        });

        // Assign and process
        await request.AssignStaffAsync("STAFF-001", "Lisa Adams");
        await request.UpdateStatusAsync(ROIRequestStatus.InProcess, "Reviewing requested records.");

        // Fulfill
        await request.FulfillRequestAsync(FulfillmentMethod.Portal, "Uploaded to patient portal.", 85, 0m);
        state = await request.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Fulfilled));
        Assert.That(state.FeeCharged, Is.EqualTo(0m));
        Assert.That(state.FulfillmentDate, Is.Not.Null);
    }

    [Test]
    public async Task ROI_RequestFullLifecycle_ThirdPartyWithAuthorization()
    {
        string requestId = $"ROI-REQUEST:{Guid.NewGuid()}";
        IROIRequestGrain request = _cluster.GrainFactory.GetGrain<IROIRequestGrain>(requestId);

        await request.SubmitRequestAsync(
            "PAT-INT-2", "Olivia Grant", new DateTime(1985, 7, 20),
            ROIRequestType.BillingRecords, RequesterType.InsuranceCompany,
            "BlueCross BlueShield", "BCBS", "789 Insurance Blvd", "800-555-2000",
            "800-555-2001", "roi@bcbs.com",
            "Claims processing", new List<string> { "Billing records 2024" },
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31),
            ROIRequestPriority.Routine, "ROIStaff1");

        // Should require authorization for insurance company
        ROIRequestState state = await request.GetRequestAsync();
        Assert.That(state.AuthorizationStatus, Is.EqualTo(AuthorizationStatus.Pending));
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Received));

        // Record authorization received
        await request.UpdateAuthorizationAsync(AuthorizationStatus.Received, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        state = await request.GetRequestAsync();
        Assert.That(state.AuthorizationStatus, Is.EqualTo(AuthorizationStatus.Received));
    }

    [Test]
    public async Task ROI_DisclosureLinkedToRequest()
    {
        string requestId = $"ROI-REQUEST:{Guid.NewGuid()}";
        IROIRequestGrain request = _cluster.GrainFactory.GetGrain<IROIRequestGrain>(requestId);
        await request.SubmitRequestAsync(
            "PAT-INT-3", "Patricia Wang", null,
            ROIRequestType.LabRecords, RequesterType.HealthcareProvider,
            "Dr. Michael Lee", "City Hospital", "100 Hospital Way", "555-3000",
            string.Empty, "mlee@cityhospital.org",
            "Continuity of care — patient transferred", new List<string> { "Lab results 2024" },
            null, null, ROIRequestPriority.Expedited, "ROIStaff2");

        await request.FulfillRequestAsync(FulfillmentMethod.Fax, "Faxed to receiving facility.", 15, 4.50m);

        // Record the associated disclosure
        string disclosureId = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
        IHIPAADisclosureGrain disclosure = _cluster.GrainFactory.GetGrain<IHIPAADisclosureGrain>(disclosureId);
        await disclosure.RecordDisclosureAsync(
            "PAT-INT-3", "Patricia Wang", HIPAADisclosureType.Treatment,
            "Dr. Michael Lee", "City Hospital", "100 Hospital Way",
            "Continuity of care", "Lab results", "2024", 15,
            true, requestId, "ROIStaff2", "ROI Specialist");

        HIPAADisclosureState discState = await disclosure.GetDisclosureAsync();
        Assert.That(discState.LinkedRequestId, Is.EqualTo(requestId));
        Assert.That(discState.IsSubjectToAccounting, Is.False); // Treatment = TPO
    }

    [Test]
    public async Task ROI_PatientAccountingShowsOnlyAccountableDisclosures()
    {
        string patientId = "PAT-ACCT-TEST";
        IHIPAADisclosureIndexGrain index = _cluster.GrainFactory.GetGrain<IHIPAADisclosureIndexGrain>($"ROI-DISC-IDX:{patientId}");

        // TPO — excluded from accounting
        await index.UpsertDisclosureAsync(new HIPAADisclosureIndexEntry
        {
            DisclosureId = "DISC-TPO-1", PatientId = patientId, PatientName = "Accounting Patient",
            DisclosureDate = DateTime.UtcNow.AddDays(-30), DisclosureType = HIPAADisclosureType.Treatment,
            RecipientName = "Dr. Smith", PurposeOfDisclosure = "Treatment",
            IsSubjectToAccounting = false, LinkedRequestId = string.Empty
        });

        // Law enforcement — included in accounting
        await index.UpsertDisclosureAsync(new HIPAADisclosureIndexEntry
        {
            DisclosureId = "DISC-LAW-1", PatientId = patientId, PatientName = "Accounting Patient",
            DisclosureDate = DateTime.UtcNow.AddDays(-60), DisclosureType = HIPAADisclosureType.LawEnforcement,
            RecipientName = "Det. Johnson", PurposeOfDisclosure = "Law enforcement inquiry",
            IsSubjectToAccounting = true, LinkedRequestId = string.Empty
        });

        // Research with waiver — included
        await index.UpsertDisclosureAsync(new HIPAADisclosureIndexEntry
        {
            DisclosureId = "DISC-RES-1", PatientId = patientId, PatientName = "Accounting Patient",
            DisclosureDate = DateTime.UtcNow.AddDays(-90), DisclosureType = HIPAADisclosureType.ResearchWithWaiver,
            RecipientName = "University Research Team", PurposeOfDisclosure = "IRB-approved research",
            IsSubjectToAccounting = true, LinkedRequestId = string.Empty
        });

        List<HIPAADisclosureIndexEntry> accounting = await index.GetDisclosuresSubjectToAccountingAsync();
        Assert.That(accounting, Has.Count.EqualTo(2));
        Assert.That(accounting.All(d => d.IsSubjectToAccounting), Is.True);
    }

    [Test]
    public async Task ROI_MultipleRequestsForPatient_TrackedInIndex()
    {
        IROIRequestIndexGrain index = _cluster.GrainFactory.GetGrain<IROIRequestIndexGrain>("ROI-INT-MULTI-IDX");

        for (int i = 1; i <= 4; i++)
        {
            string id = $"ROI-REQUEST:{Guid.NewGuid()}";
            IROIRequestGrain grain = _cluster.GrainFactory.GetGrain<IROIRequestGrain>(id);
            await grain.SubmitRequestAsync(
                "PAT-MULTI", $"Multi Patient", null,
                ROIRequestType.MedicalRecords, RequesterType.Patient,
                $"Requester {i}", "Self", string.Empty, string.Empty, string.Empty, string.Empty,
                "Various purposes", new List<string> { "Records" },
                null, null, ROIRequestPriority.Routine, "ROIStaff1");
            ROIRequestState state = await grain.GetRequestAsync();
            await index.UpsertRequestAsync(new ROIRequestIndexEntry
            {
                RequestId = state.RequestId, PatientId = state.PatientId, PatientName = state.PatientName,
                ReceivedDate = state.ReceivedDate, RequestType = state.RequestType,
                RequesterType = state.RequesterType, RequesterName = state.RequesterName,
                Status = state.Status, DueDate = state.DueDate, AssignedStaffName = state.AssignedStaffName,
                Priority = state.Priority
            });
        }

        List<ROIRequestIndexEntry> patient = await index.GetRequestsByPatientAsync("PAT-MULTI");
        Assert.That(patient, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task ROI_DeniedRequest_RecordsReason()
    {
        string requestId = $"ROI-REQUEST:{Guid.NewGuid()}";
        IROIRequestGrain request = _cluster.GrainFactory.GetGrain<IROIRequestGrain>(requestId);

        await request.SubmitRequestAsync(
            "PAT-INT-4", "Quinn Morris", null,
            ROIRequestType.MentalHealthRecords, RequesterType.LawEnforcement,
            "Officer Davis", "Metro PD", "1 Police Plaza", "555-4000",
            string.Empty, string.Empty,
            "Criminal investigation", new List<string> { "Mental health records" },
            null, null, ROIRequestPriority.Urgent, "ROIStaff1");

        await request.DenyRequestAsync("Mental health records require a specific court order per state law. " +
            "No valid court order was presented. 42 CFR Part 2 protections apply.");

        ROIRequestState state = await request.GetRequestAsync();
        Assert.That(state.Status, Is.EqualTo(ROIRequestStatus.Denied));
        Assert.That(state.DenialReason, Does.Contain("court order"));
    }
}
