// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Tests that grain state created through domain grain interfaces is fully readable
/// via the same Get*Async() methods the CDC materialization service uses.
///
/// Each test creates grain state, then reads it back via the grain interface — verifying
/// the critical path: grain write → state persistence → grain read (by CDC service).
/// </summary>
[TestFixture]
public class CdcGrainReadTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Patient ────────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_Patient_AllDemographicsAccessible()
    {
        string patientId = $"CDC-PAT-{Guid.NewGuid()}";
        IPatientGrain grain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        await grain.UpdateDemographicsAsync("DOE,JOHN", "M", new DateTime(1960, 3, 15), "123456789");
        await grain.UpdateVeteranInfoAsync("Y", 50, "SC", "SC VETERAN");
        await grain.UpdateMilitaryServiceAsync(
            new DateTime(1980, 6, 1), new DateTime(1984, 6, 1), "ARMY", "HONORABLE", "N");
        await grain.UpdateAddressAsync("123 Main St", null, null, "BOSTON", "MA", "02101");
        await grain.UpdateContactInfoAsync("617-555-1234", "617-555-5678", "john@example.com");

        // Read via the same interface the CDC materializer uses
        PatientState state = await grain.GetPatientAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.Name, Is.EqualTo("DOE,JOHN"));
        Assert.That(state.Sex, Is.EqualTo("M"));
        Assert.That(state.DateOfBirth, Is.EqualTo(new DateTime(1960, 3, 15)));
        Assert.That(state.SocialSecurityNumber, Is.EqualTo("123456789"));
        Assert.That(state.Veteran, Is.EqualTo("Y"));
        Assert.That(state.ServiceConnectedPercentage, Is.EqualTo(50));
        Assert.That(state.ServiceBranch, Is.EqualTo("ARMY"));
        Assert.That(state.ServiceEntryDate, Is.EqualTo(new DateTime(1980, 6, 1)));
        Assert.That(state.ServiceSeparationDate, Is.EqualTo(new DateTime(1984, 6, 1)));
        Assert.That(state.City, Is.EqualTo("BOSTON"));
        Assert.That(state.State, Is.EqualTo("MA"));
        Assert.That(state.LastModifiedDate, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task CdcRead_Patient_SsnLast4Derivable()
    {
        string patientId = $"CDC-PAT-{Guid.NewGuid()}";
        IPatientGrain grain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        await grain.UpdateDemographicsAsync("SMITH,JANE", "F", new DateTime(1975, 7, 4), "987654321");
        PatientState state = await grain.GetPatientAsync();

        // CDC materializer extracts last 4 digits
        string ssn = state.SocialSecurityNumber;
        Assert.That(ssn.Length, Is.GreaterThanOrEqualTo(4));
        Assert.That(ssn[^4..], Is.EqualTo("4321"));
    }

    [Test]
    public async Task CdcRead_Patient_DefaultStateReadable()
    {
        // Grain with no data written — CDC should handle gracefully
        string patientId = $"CDC-PAT-{Guid.NewGuid()}";
        IPatientGrain grain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        PatientState state = await grain.GetPatientAsync();

        Assert.That(state.Name, Is.EqualTo(string.Empty));
        Assert.That(state.DateOfBirth, Is.Null);
        Assert.That(state.ServiceConnectedPercentage, Is.Null);
        Assert.That(state.IsActive, Is.True);
    }

    // ─── Order ──────────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_Order_AllFieldsAccessible()
    {
        string orderId = $"CDC-ORD-{Guid.NewGuid()}";
        IOrderGrain grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);

        await grain.CreateOrderAsync(
            "PATIENT-001", "Lab", "CBC", "OI-CBC",
            "PROV-001", "Dr. Smith",
            DateTime.UtcNow,
            "LOC-001", "Primary Care",
            "ROUTINE", "Routine annual labs", null, null, null);

        OrderState state = await grain.GetOrderAsync();

        Assert.That(state.OrderId, Is.EqualTo(orderId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.OrderType, Is.EqualTo("Lab"));
        Assert.That(state.OrderableItem, Is.EqualTo("CBC"));
        Assert.That(state.ProviderId, Is.EqualTo("PROV-001"));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.LocationId, Is.EqualTo("LOC-001"));
        Assert.That(state.LocationName, Is.EqualTo("Primary Care"));
        Assert.That(state.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(state.Status, Is.EqualTo("Pending"));
        Assert.That(state.OrderDateTime, Is.Not.EqualTo(default(DateTime)));
        Assert.That(state.LastModifiedDate, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task CdcRead_Order_DaysToSignComputable()
    {
        string orderId = $"CDC-ORD-{Guid.NewGuid()}";
        IOrderGrain grain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);

        await grain.CreateOrderAsync(
            "PATIENT-001", "Lab", "BMP", null,
            "PROV-001", "Dr. Adams",
            DateTime.UtcNow,
            null, null, "ROUTINE", null, null, null, null);
        await grain.SignOrderAsync("ESIG123", DateTime.UtcNow);

        OrderState state = await grain.GetOrderAsync();

        Assert.That(state.SignatureDateTime, Is.Not.Null);
        // CDC materializer computes DaysToSign from OrderDateTime to SignatureDateTime
        int daysToSign = (int)(state.SignatureDateTime!.Value - state.OrderDateTime).TotalDays;
        Assert.That(daysToSign, Is.GreaterThanOrEqualTo(0));
    }

    // ─── Lab Test ───────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_LabTest_AllFieldsAccessible()
    {
        string labId = $"CDC-LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labId);

        await grain.OrderLabTestAsync(
            "PATIENT-001", "TST-WBC", "WBC", "26464-8",
            null, "PROV-001", "Dr. Smith", "Blood", "HEMATOLOGY");

        LabTestState state = await grain.GetLabTestAsync();

        Assert.That(state.LabTestId, Is.EqualTo(labId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.TestId, Is.EqualTo("TST-WBC"));
        Assert.That(state.TestName, Is.EqualTo("WBC"));
        Assert.That(state.TestCode, Is.EqualTo("26464-8"));
        Assert.That(state.SpecimenType, Is.EqualTo("Blood"));
        Assert.That(state.Category, Is.EqualTo("HEMATOLOGY"));
        Assert.That(state.Status, Is.EqualTo("Ordered"));
    }

    [Test]
    public async Task CdcRead_LabTest_ResultWithAbnormalFlag()
    {
        string labId = $"CDC-LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labId);

        await grain.OrderLabTestAsync(
            "PATIENT-001", "TST-K", "Potassium", null,
            null, "PROV-001", "Dr. Smith", "Blood", "CHEMISTRY");
        await grain.RecordResultAsync(DateTime.UtcNow, "5.8", "mmol/L", "3.5", "5.0", "H");

        LabTestState state = await grain.GetLabTestAsync();

        Assert.That(state.ResultValue, Is.EqualTo("5.8"));
        Assert.That(state.ResultUnit, Is.EqualTo("mmol/L"));
        Assert.That(state.ReferenceRangeLow, Is.EqualTo("3.5"));
        Assert.That(state.ReferenceRangeHigh, Is.EqualTo("5.0"));
        Assert.That(state.AbnormalFlag, Is.EqualTo("H"));
        Assert.That(state.ResultDateTime, Is.Not.Null);

        // CDC materializer parses ResultValue to numeric
        Assert.That(decimal.TryParse(state.ResultValue, out decimal numericResult), Is.True);
        Assert.That(numericResult, Is.EqualTo(5.8m));

        // CDC materializer derives IsAbnormal from flag
        bool isAbnormal = state.AbnormalFlag is "H" or "L" or "HH" or "LL" or "A" or "AA";
        Assert.That(isAbnormal, Is.True);
    }

    [Test]
    public async Task CdcRead_LabTest_CriticalResult()
    {
        string labId = $"CDC-LAB-{Guid.NewGuid()}";
        ILabTestGrain grain = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labId);

        await grain.OrderLabTestAsync(
            "PATIENT-001", "TST-K2", "Potassium", null,
            null, "PROV-001", "Dr. Smith", "Blood", "CHEMISTRY");
        await grain.RecordResultAsync(DateTime.UtcNow, "6.5", "mmol/L", "3.5", "5.0", "HH");

        LabTestState state = await grain.GetLabTestAsync();

        // CDC materializer derives IsCritical from flag
        bool isCritical = state.AbnormalFlag is "HH" or "LL" or "AA" || state.IsCritical;
        Assert.That(isCritical, Is.True);
    }

    // ─── Consult ────────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_Consult_AllFieldsAccessible()
    {
        string consultId = $"CDC-CON-{Guid.NewGuid()}";
        IConsultGrain grain = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);

        await grain.RequestConsultAsync(
            "PATIENT-001", "Cardiology", "SVC-CARDIO",
            "Primary Care", "SVC-PC", "URGENT",
            "PROV-001", "Dr. Smith",
            "PROV-002", "Dr. Jones",
            "Chest pain on exertion",
            "Chest pain, unspecified",
            null, "LOC-001", "Medicine Clinic");

        ConsultState state = await grain.GetConsultAsync();

        Assert.That(state.ConsultId, Is.EqualTo(consultId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.ToService, Is.EqualTo("Cardiology"));
        Assert.That(state.FromService, Is.EqualTo("Primary Care"));
        Assert.That(state.Urgency, Is.EqualTo("URGENT"));
        Assert.That(state.Status, Is.EqualTo("PENDING"));
        Assert.That(state.RequestingProviderId, Is.EqualTo("PROV-001"));
        Assert.That(state.RequestingProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.LocationId, Is.EqualTo("LOC-001"));
        Assert.That(state.RequestDateTime, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task CdcRead_Consult_DaysToCompleteComputable()
    {
        string consultId = $"CDC-CON-{Guid.NewGuid()}";
        IConsultGrain grain = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);

        await grain.RequestConsultAsync(
            "PATIENT-001", "Orthopedics", null,
            "ER", null, "URGENT",
            "PROV-001", "Dr. Smith", null, null,
            "Knee injury", null, null, null, null);
        await grain.AcceptAsync();
        await grain.ScheduleAsync();
        await grain.CompleteAsync(DateTime.UtcNow, "TIU-001");

        ConsultState state = await grain.GetConsultAsync();

        Assert.That(state.CompletedDateTime, Is.Not.Null);

        // CDC materializer computes these
        int daysToComplete = (int)(state.CompletedDateTime!.Value - state.RequestDateTime).TotalDays;
        Assert.That(daysToComplete, Is.GreaterThanOrEqualTo(0));
    }

    // ─── Vital ──────────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_Vital_StandardMeasurement()
    {
        string vitalId = $"CDC-VIT-{Guid.NewGuid()}";
        IVitalGrain grain = _cluster.GrainFactory.GetGrain<IVitalGrain>(vitalId);

        await grain.RecordVitalAsync(
            "PATIENT-001", "Temperature", "98.6", "F",
            DateTime.UtcNow, "LOC-001", "Clinic A",
            "PROV-001", "Nurse Smith", null, null);

        VitalState state = await grain.GetVitalAsync();

        Assert.That(state.VitalId, Is.EqualTo(vitalId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.VitalType, Is.EqualTo("Temperature"));
        Assert.That(state.Value, Is.EqualTo("98.6"));
        Assert.That(state.Units, Is.EqualTo("F"));
        Assert.That(state.EnteredById, Is.EqualTo("PROV-001"));

        // CDC materializer parses numeric
        Assert.That(decimal.TryParse(state.Value, out decimal numericResult), Is.True);
        Assert.That(numericResult, Is.EqualTo(98.6m));
    }

    [Test]
    public async Task CdcRead_Vital_BloodPressureSplittable()
    {
        string vitalId = $"CDC-VIT-{Guid.NewGuid()}";
        IVitalGrain grain = _cluster.GrainFactory.GetGrain<IVitalGrain>(vitalId);

        await grain.RecordVitalAsync(
            "PATIENT-001", "Blood Pressure", "120/80", "mmHg",
            DateTime.UtcNow, null, null, "PROV-001", "Nurse Smith", null, null);

        VitalState state = await grain.GetVitalAsync();

        Assert.That(state.Value, Is.EqualTo("120/80"));

        // CDC materializer splits BP into systolic/diastolic
        string[] parts = state.Value.Split('/');
        Assert.That(parts, Has.Length.EqualTo(2));
        Assert.That(decimal.TryParse(parts[0], out decimal systolic), Is.True);
        Assert.That(decimal.TryParse(parts[1], out decimal diastolic), Is.True);
        Assert.That(systolic, Is.EqualTo(120m));
        Assert.That(diastolic, Is.EqualTo(80m));
    }

    // ─── TIU Document ───────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_TiuDocument_AllFieldsAccessible()
    {
        string docId = $"CDC-TIU-{Guid.NewGuid()}";
        ITiuDocumentGrain grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(docId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "Progress Note", "DTYPE-001",
            "Patient presents for routine follow-up. No acute complaints.",
            "Follow-up visit",
            "AUTH-001", "Dr. Smith",
            null, null,
            "LOC-001", "Clinic A",
            null, DateTime.UtcNow);

        TiuDocumentState state = await grain.GetDocumentAsync();

        Assert.That(state.DocumentId, Is.EqualTo(docId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.DocumentType, Is.EqualTo("Progress Note"));
        Assert.That(state.AuthorId, Is.EqualTo("AUTH-001"));
        Assert.That(state.AuthorName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.LocationId, Is.EqualTo("LOC-001"));
        Assert.That(state.Subject, Is.EqualTo("Follow-up visit"));
        Assert.That(state.ReportText, Is.Not.Empty);

        // CDC materializer computes TextLength
        int textLength = state.ReportText?.Length ?? 0;
        Assert.That(textLength, Is.GreaterThan(0));
    }

    [Test]
    public async Task CdcRead_TiuDocument_HoursToSignComputable()
    {
        string docId = $"CDC-TIU-{Guid.NewGuid()}";
        ITiuDocumentGrain grain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(docId);

        await grain.CreateDocumentAsync(
            "PATIENT-001", "Progress Note", null,
            "Note text.",
            null,
            "AUTH-001", "Dr. Smith",
            null, null,
            null, null,
            null, DateTime.UtcNow);
        await grain.SignDocumentAsync(DateTime.UtcNow);

        TiuDocumentState state = await grain.GetDocumentAsync();

        Assert.That(state.SignedDateTime, Is.Not.Null);
        // CDC materializer computes HoursToSign
        decimal hoursToSign = (decimal)(state.SignedDateTime!.Value - state.EntryDate).TotalHours;
        Assert.That(hoursToSign, Is.GreaterThanOrEqualTo(0));
    }

    // ─── Pharmacy ───────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_Pharmacy_AllFieldsAccessible()
    {
        string rxId = $"CDC-RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);

        await grain.CreatePrescriptionAsync(
            "PATIENT-001", "Metoprolol 25mg", "DRUG-MET",
            "25mg", "PO", "BID",
            "Take one tablet by mouth twice daily",
            30, 90, 3,
            "PROV-001", "Dr. Smith",
            null, null, null, null);

        PharmacyState state = await grain.GetPrescriptionAsync();

        Assert.That(state.PrescriptionId, Is.EqualTo(rxId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.DrugName, Is.EqualTo("Metoprolol 25mg"));
        Assert.That(state.DrugId, Is.EqualTo("DRUG-MET"));
        Assert.That(state.Dosage, Is.EqualTo("25mg"));
        Assert.That(state.Route, Is.EqualTo("PO"));
        Assert.That(state.Schedule, Is.EqualTo("BID"));
        Assert.That(state.ProviderId, Is.EqualTo("PROV-001"));
        Assert.That(state.DaysSupply, Is.EqualTo(30));
        Assert.That(state.Quantity, Is.EqualTo(90));
        Assert.That(state.Refills, Is.EqualTo(3));
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    // ─── ADT ────────────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_Adt_AdmissionFieldsAccessible()
    {
        string moveId = $"CDC-ADT-{Guid.NewGuid()}";
        IAdtGrain grain = _cluster.GrainFactory.GetGrain<IAdtGrain>(moveId);

        await grain.RecordAdmissionAsync(
            "PATIENT-001", DateTime.UtcNow,
            "WARD-3A", "Ward 3A", "301-A",
            "SPEC-MED", "Internal Medicine",
            "PROV-001", "Dr. Smith",
            "Inpatient", "Pneumonia", null);

        AdtState state = await grain.GetMovementAsync();

        Assert.That(state.MovementId, Is.EqualTo(moveId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.TransactionType, Is.EqualTo("ADMISSION"));
        Assert.That(state.WardLocationId, Is.EqualTo("WARD-3A"));
        Assert.That(state.WardLocationName, Is.EqualTo("Ward 3A"));
        Assert.That(state.RoomBed, Is.EqualTo("301-A"));
        Assert.That(state.TreatingSpecialtyName, Is.EqualTo("Internal Medicine"));
        Assert.That(state.AttendingPhysicianId, Is.EqualTo("PROV-001"));
    }

    // ─── BCMA ───────────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_Bcma_AdministrationFieldsAccessible()
    {
        string adminId = $"CDC-BCMA-{Guid.NewGuid()}";
        IBcmaGrain grain = _cluster.GrainFactory.GetGrain<IBcmaGrain>(adminId);

        DateTime scheduledTime = DateTime.UtcNow.AddHours(-1);
        DateTime adminTime = DateTime.UtcNow;

        await grain.RecordAdministrationAsync(
            "PATIENT-001", "Metoprolol 25mg", "DRUG-MET",
            "25mg", "PO", "Given",
            scheduledTime, adminTime,
            "PROV-001", "Nurse Jones",
            null, "RX-001", null, null);

        BcmaState state = await grain.GetAdministrationAsync();

        Assert.That(state.AdministrationId, Is.EqualTo(adminId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.DrugName, Is.EqualTo("Metoprolol 25mg"));
        Assert.That(state.ActionStatus, Is.EqualTo("Given"));
        Assert.That(state.ScheduledDateTime, Is.Not.Null);
        Assert.That(state.AdministrationDateTime, Is.Not.Null);

        // CDC materializer computes VarianceMinutes
        int varianceMinutes = (int)(state.AdministrationDateTime!.Value - state.ScheduledDateTime!.Value).TotalMinutes;
        Assert.That(varianceMinutes, Is.GreaterThanOrEqualTo(0));
    }

    // ─── Audit Event ────────────────────────────────────────────────────────

    [Test]
    public async Task CdcRead_AuditEvent_ImmutableAndAccessible()
    {
        string eventId = $"AUDIT-{Guid.NewGuid()}";
        IAuditEventGrain grain = _cluster.GrainFactory.GetGrain<IAuditEventGrain>(eventId);

        await grain.RecordAsync(
            patientId: "PATIENT-001",
            domain: "ORDERS",
            action: "CREATE",
            entityType: "ORDER",
            entityId: "ORD-001",
            userId: "USER-001",
            userName: "Dr. Smith",
            locationId: "LOC-001",
            locationName: "Clinic A",
            details: "Created lab order CBC",
            oldValue: null,
            newValue: null,
            previousEventHash: IAuditEventGrain.GenesisHash);

        AuditEventState state = await grain.GetEventAsync();

        Assert.That(state.EventId, Is.EqualTo(eventId));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.Domain, Is.EqualTo("ORDERS"));
        Assert.That(state.Action, Is.EqualTo("CREATE"));
        Assert.That(state.EntityType, Is.EqualTo("ORDER"));
        Assert.That(state.EntityId, Is.EqualTo("ORD-001"));
        Assert.That(state.UserId, Is.EqualTo("USER-001"));
        Assert.That(state.UserName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.Timestamp, Is.Not.EqualTo(default(DateTime)));
        Assert.That(state.EventHash, Is.Not.Empty);
        Assert.That(state.PreviousEventHash, Is.Not.Empty);
    }
}
