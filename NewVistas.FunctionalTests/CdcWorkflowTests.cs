// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the CDC materialization pipeline — verifies that data created
/// through workflow grains is accessible via domain grain interfaces, which is the
/// exact path the CdcMaterializationService uses.
///
/// The CDC service:
///   1. Queries OrleansStorage for changed grain keys (by ModifiedOn)
///   2. Calls domain grain Get*Async() via IGrainFactory
///   3. Maps state to star schema and writes via ADO.NET
///
/// These tests validate step 2: after clinical workflows, domain grain state is
/// correctly populated and accessible through the Get*Async() interface.
/// </summary>
[TestFixture]
public class CdcWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> CreateTestPatientAsync()
    {
        string patientId = $"CDC-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.UpdateDemographicsAsync(
            "CDCTEST,PATIENT", "M", new DateTime(1955, 11, 11), "111223333");
        await workflow.UpdateVeteranInfoAsync("Y", 30, "SC", "SC VETERAN");
        return patientId;
    }

    // ─── Patient Demographics via Workflow → Domain Grain ────────────────────

    [Test]
    public async Task CdcWorkflow_PatientCreatedViaWorkflow_ReadableViaDomainGrain()
    {
        string patientId = await CreateTestPatientAsync();

        // CDC path: read via domain grain directly (not via workflow)
        IPatientGrain domainGrain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        PatientState state = await domainGrain.GetPatientAsync();

        Assert.That(state.Name, Is.EqualTo("CDCTEST,PATIENT"));
        Assert.That(state.Sex, Is.EqualTo("M"));
        Assert.That(state.DateOfBirth, Is.EqualTo(new DateTime(1955, 11, 11)));
        Assert.That(state.SocialSecurityNumber, Is.EqualTo("111223333"));
        Assert.That(state.Veteran, Is.EqualTo("Y"));
        Assert.That(state.ServiceConnectedPercentage, Is.EqualTo(30));
    }

    // ─── Order Placed via Workflow → Domain Grain ───────────────────────────

    [Test]
    public async Task CdcWorkflow_OrderPlacedViaWorkflow_ReadableViaDomainGrain()
    {
        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        string orderId = await workflow.PlaceOrderAsync(
            "Lab", "CBC with Differential", "OI-CBC",
            "PROV-001", "Dr. Smith",
            "LOC-001", "Primary Care Clinic",
            "ROUTINE", "Annual screening", null);

        // CDC path: read the order grain directly
        IOrderGrain orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
        OrderState orderState = await orderGrain.GetOrderAsync();

        Assert.That(orderState.PatientId, Is.EqualTo(patientId));
        Assert.That(orderState.OrderType, Is.EqualTo("Lab"));
        Assert.That(orderState.OrderableItem, Is.EqualTo("CBC with Differential"));
        Assert.That(orderState.ProviderId, Is.EqualTo("PROV-001"));
        Assert.That(orderState.ProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(orderState.LocationId, Is.EqualTo("LOC-001"));
        Assert.That(orderState.Status, Is.EqualTo("Pending"));
    }

    [Test]
    public async Task CdcWorkflow_OrderSigned_SignatureFieldsPopulated()
    {
        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        string orderId = await workflow.PlaceOrderAsync(
            "Lab", "BMP", null,
            "PROV-001", "Dr. Adams",
            null, null, "ROUTINE", null, null);
        await workflow.SignOrderAsync(orderId, "ESIG-ADAMS");

        IOrderGrain orderGrain = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
        OrderState state = await orderGrain.GetOrderAsync();

        Assert.That(state.SignatureDateTime, Is.Not.Null);
        Assert.That(state.ElectronicSignature, Is.Not.Null.And.Not.Empty);
    }

    // ─── Lab Result via Workflow → Domain Grain ─────────────────────────────

    [Test]
    public async Task CdcWorkflow_LabOrdered_ReadableViaDomainGrain()
    {
        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // OrderLabTestAsync params: testId, testName, testCode, orderId,
        //   orderingProviderId, orderingProviderName, specimenType, category
        string labId = await workflow.OrderLabTestAsync(
            "TST-GLU", "Glucose", "2345-7",
            null, "PROV-001", "Dr. Smith", "Blood", "CHEMISTRY");

        ILabTestGrain labGrain = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labId);
        LabTestState state = await labGrain.GetLabTestAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.TestName, Is.EqualTo("Glucose"));
        Assert.That(state.TestCode, Is.EqualTo("2345-7"));
        Assert.That(state.Category, Is.EqualTo("CHEMISTRY"));
        Assert.That(state.Status, Is.EqualTo("Ordered"));
    }

    [Test]
    public async Task CdcWorkflow_LabResulted_ResultFieldsAccessible()
    {
        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        string labId = await workflow.OrderLabTestAsync(
            "TST-HGB", "Hemoglobin", null,
            null, "PROV-001", "Dr. Smith", "Blood", "HEMATOLOGY");
        // RecordLabResultAsync params: labTestId, resultDateTime, resultValue, resultUnit,
        //   referenceLow, referenceHigh, abnormalFlag
        await workflow.RecordLabResultAsync(labId, DateTime.UtcNow, "14.2", "g/dL", "12.0", "17.5", null);

        ILabTestGrain labGrain = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labId);
        LabTestState state = await labGrain.GetLabTestAsync();

        Assert.That(state.ResultValue, Is.EqualTo("14.2"));
        Assert.That(state.ResultUnit, Is.EqualTo("g/dL"));
        Assert.That(state.ReferenceRangeLow, Is.EqualTo("12.0"));
        Assert.That(state.ReferenceRangeHigh, Is.EqualTo("17.5"));
        Assert.That(state.ResultDateTime, Is.Not.Null);
    }

    // ─── Vital via Workflow → Domain Grain ──────────────────────────────────

    [Test]
    public async Task CdcWorkflow_VitalRecorded_ReadableViaDomainGrain()
    {
        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // RecordVitalsAsync (plural) takes a dictionary of vitals
        await workflow.RecordVitalsAsync(
            "LOC-001", "Clinic A",
            "PROV-001", "Nurse Jones",
            DateTime.UtcNow,
            new Dictionary<string, string> { { "Blood Pressure", "130/85" } },
            null);

        // The workflow creates individual VitalGrain per vital type.
        // Read the patient's recent vitals to find the vital ID.
        List<VitalSummary> vitals = await workflow.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.GreaterThanOrEqualTo(1));

        VitalSummary bpVital = vitals.First(v => v.VitalType == "Blood Pressure");
        IVitalGrain vitalGrain = _cluster.GrainFactory.GetGrain<IVitalGrain>(bpVital.VitalId);
        VitalState state = await vitalGrain.GetVitalAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.VitalType, Is.EqualTo("Blood Pressure"));
        Assert.That(state.Value, Is.EqualTo("130/85"));
    }

    // ─── TIU Document via Workflow → Domain Grain ───────────────────────────

    [Test]
    public async Task CdcWorkflow_NoteCreated_ReadableViaDomainGrain()
    {
        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // CreateNoteAsync params: documentType, documentTypeId, reportText, subject,
        //   authorId, authorName, cosignerId, cosignerName, locationId, locationName,
        //   visitId, referenceDate
        string noteId = await workflow.CreateNoteAsync(
            "Progress Note", null,
            "Patient presents for annual physical exam. All findings normal.",
            "Annual physical",
            "AUTH-001", "Dr. Smith",
            null, null,
            "LOC-001", "Clinic A",
            null, DateTime.UtcNow);

        ITiuDocumentGrain noteGrain = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(noteId);
        TiuDocumentState state = await noteGrain.GetDocumentAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.DocumentType, Is.EqualTo("Progress Note"));
        Assert.That(state.AuthorId, Is.EqualTo("AUTH-001"));
        Assert.That(state.Subject, Is.EqualTo("Annual physical"));
        Assert.That(state.ReportText, Does.Contain("annual physical"));
    }

    // ─── Consult via Workflow → Domain Grain ────────────────────────────────

    [Test]
    public async Task CdcWorkflow_ConsultRequested_ReadableViaDomainGrain()
    {
        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // RequestConsultAsync params: toService, toServiceId, fromService, fromServiceId,
        //   urgency, requestingProviderId, requestingProviderName,
        //   attentionProviderId, attentionProviderName,
        //   reasonForRequest, provisionalDiagnosis, orderId, locationId, locationName
        string consultId = await workflow.RequestConsultAsync(
            "Cardiology", null,
            null, null,
            "ROUTINE",
            "PROV-001", "Dr. Smith",
            null, null,
            "Chest pain evaluation",
            "Chest pain, unspecified",
            null, "LOC-001", "Primary Care Clinic");

        IConsultGrain consultGrain = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);
        ConsultState state = await consultGrain.GetConsultAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.ToService, Is.EqualTo("Cardiology"));
        Assert.That(state.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(state.Status, Is.EqualTo("PENDING"));
        Assert.That(state.RequestingProviderId, Is.EqualTo("PROV-001"));
    }

    // ─── Prescription via Domain Grain (no workflow prescribe method) ───────

    [Test]
    public async Task CdcWorkflow_PrescriptionCreated_ReadableViaDomainGrain()
    {
        string patientId = await CreateTestPatientAsync();

        // Create prescription directly via domain grain (the workflow handles pharmacy
        // through order placement; direct prescription creation tests the CDC read path)
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain rxGrain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await rxGrain.CreatePrescriptionAsync(
            patientId, "Lisinopril 10mg", "DRUG-LIS",
            "10mg", "PO", "QD",
            "Take one tablet by mouth daily",
            30, 90, 3,
            "PROV-001", "Dr. Smith",
            null, null, null, null);

        PharmacyState state = await rxGrain.GetPrescriptionAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.DrugName, Is.EqualTo("Lisinopril 10mg"));
        Assert.That(state.DrugId, Is.EqualTo("DRUG-LIS"));
        Assert.That(state.Dosage, Is.EqualTo("10mg"));
        Assert.That(state.Route, Is.EqualTo("PO"));
        Assert.That(state.ProviderId, Is.EqualTo("PROV-001"));
        Assert.That(state.DaysSupply, Is.EqualTo(30));
        Assert.That(state.Refills, Is.EqualTo(3));
    }

    // ─── Multi-Domain Workflow (Clinical Encounter) ─────────────────────────

    [Test]
    public async Task CdcWorkflow_FullEncounter_AllDomainGrainsReadable()
    {
        // Simulates a complete clinical encounter and verifies all domain grains
        // are accessible via the CDC read path

        string patientId = await CreateTestPatientAsync();
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // 1. Record vitals
        await workflow.RecordVitalsAsync(
            "LOC-ER", "Emergency Room",
            "PROV-NURSE", "Nurse Davis",
            DateTime.UtcNow,
            new Dictionary<string, string> { { "Temperature", "101.2" } },
            null);

        // 2. Place lab order
        string orderId = await workflow.PlaceOrderAsync(
            "Lab", "CBC", null,
            "PROV-DOC", "Dr. Johnson",
            "LOC-ER", "Emergency Room",
            "STAT", "Evaluate for infection", null);

        // 3. Order the lab test
        string labId = await workflow.OrderLabTestAsync(
            "TST-CBC", "CBC", null,
            null, "PROV-DOC", "Dr. Johnson", "Blood", "HEMATOLOGY");

        // 4. Create a note
        string noteId = await workflow.CreateNoteAsync(
            "ER Note", null,
            "Patient evaluation for fever.",
            "Fever evaluation",
            "PROV-DOC", "Dr. Johnson",
            null, null,
            "LOC-ER", "Emergency Room",
            null, DateTime.UtcNow);

        // Verify all domain grains are independently readable (CDC path)
        OrderState order = await _cluster.GrainFactory
            .GetGrain<IOrderGrain>(orderId).GetOrderAsync();
        Assert.That(order.PatientId, Is.EqualTo(patientId));
        Assert.That(order.OrderType, Is.EqualTo("Lab"));
        Assert.That(order.Urgency, Is.EqualTo("STAT"));

        LabTestState lab = await _cluster.GrainFactory
            .GetGrain<ILabTestGrain>(labId).GetLabTestAsync();
        Assert.That(lab.PatientId, Is.EqualTo(patientId));
        Assert.That(lab.TestName, Is.EqualTo("CBC"));

        TiuDocumentState note = await _cluster.GrainFactory
            .GetGrain<ITiuDocumentGrain>(noteId).GetDocumentAsync();
        Assert.That(note.PatientId, Is.EqualTo(patientId));
        Assert.That(note.DocumentType, Is.EqualTo("ER Note"));

        // Patient grain should also reflect the IDs
        PatientState patient = await _cluster.GrainFactory
            .GetGrain<IPatientGrain>(patientId).GetPatientAsync();
        Assert.That(patient.OrderIds, Does.Contain(orderId));
        Assert.That(patient.LabTestIds, Does.Contain(labId));
        Assert.That(patient.TiuDocumentIds, Does.Contain(noteId));
    }
}
