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
/// Functional tests for Oncology / Tumor Registry — VistA Files #160-#165.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class OncologyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private Task<string> RegisterTumor(IPatientWorkflowGrain wf)
        => wf.RegisterOncologyTumorAsync(
            primarySite: "C34.1",
            primarySiteText: "Upper lobe, lung",
            histology: "8140/3",
            histologyText: "Adenocarcinoma, NOS",
            laterality: TumorLaterality.Right,
            dateOfDiagnosis: new DateTime(2025, 3, 15),
            diagnosisBasis: DiagnosisBasis.HistologyOfPrimary,
            sequenceNumber: 1,
            oncologistId: "ONC-001",
            oncologistName: "Dr. Rivera");

    private Task<string> CreateTreatment(IPatientWorkflowGrain wf, string tumorId)
        => wf.CreateOncologyTreatmentAsync(
            tumorId: tumorId,
            treatmentType: OncologyTreatmentType.Chemotherapy,
            agentName: "FOLFOX",
            doseDescription: "85 mg/m² oxaliplatin + 5-FU/LV q14d",
            providerId: "ONC-001",
            providerName: "Dr. Rivera",
            facilityName: "VA Oncology Center",
            notes: "First-line therapy for stage IIIA NSCLC.");

    // ── Tumor Registration Tests ───────────────────────────────────────────────

    [Test]
    public async Task RegisterTumor_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);

        Assert.That(tumorId, Is.Not.Null.And.Not.Empty);

        List<OncologyTumorIndexEntry> all = await wf.GetOncologyTumorsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TumorId, Is.EqualTo(tumorId));
        Assert.That(all[0].PrimarySite, Is.EqualTo("C34.1"));
        Assert.That(all[0].HistologyText, Is.EqualTo("Adenocarcinoma, NOS"));
        Assert.That(all[0].Status, Is.EqualTo(OncologyStatus.Active));
    }

    [Test]
    public async Task GetOncologyTumor_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);

        OncologyTumorState state = await wf.GetOncologyTumorAsync(tumorId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PrimarySite, Is.EqualTo("C34.1"));
        Assert.That(state.PrimarySiteText, Is.EqualTo("Upper lobe, lung"));
        Assert.That(state.Histology, Is.EqualTo("8140/3"));
        Assert.That(state.Laterality, Is.EqualTo(TumorLaterality.Right));
        Assert.That(state.DiagnosisBasis, Is.EqualTo(DiagnosisBasis.HistologyOfPrimary));
        Assert.That(state.SequenceNumber, Is.EqualTo(1));
        Assert.That(state.OncologistName, Is.EqualTo("Dr. Rivera"));
    }

    [Test]
    public async Task RecordOncologyStaging_SetsTnmAndStageGroup()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);

        await wf.RecordOncologyStagingAsync(
            tumorId,
            clinicalT: "cT2a",
            clinicalN: "cN2",
            clinicalM: "cM0",
            pathologicT: "pT2a",
            pathologicN: "pN2",
            pathologicM: "pM0",
            stageGroup: "IIIA",
            seerSummaryStage: "3");

        OncologyTumorState state = await wf.GetOncologyTumorAsync(tumorId);
        Assert.That(state.ClinicalT, Is.EqualTo("cT2a"));
        Assert.That(state.ClinicalN, Is.EqualTo("cN2"));
        Assert.That(state.PathologicT, Is.EqualTo("pT2a"));
        Assert.That(state.StageGroup, Is.EqualTo("IIIA"));
        Assert.That(state.SeerSummaryStage, Is.EqualTo("3"));
        Assert.That(state.StagingDate, Is.Not.Null);
    }

    [Test]
    public async Task UpdateOncologyStatus_ChangesStatusAndDate()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        DateTime remissionDate = new DateTime(2025, 9, 1);

        await wf.UpdateOncologyStatusAsync(tumorId, OncologyStatus.InRemission, remissionDate, "Complete response after 6 cycles.");

        OncologyTumorState state = await wf.GetOncologyTumorAsync(tumorId);
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.InRemission));
        Assert.That(state.StatusChangeDate, Is.EqualTo(remissionDate));

        List<OncologyTumorIndexEntry> index = await wf.GetOncologyTumorsAsync();
        Assert.That(index[0].Status, Is.EqualTo(OncologyStatus.InRemission));
    }

    [Test]
    public async Task RecordOncologyRecurrence_SetsRecurrenceFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        await wf.UpdateOncologyStatusAsync(tumorId, OncologyStatus.InRemission, DateTime.UtcNow, null);

        DateTime recurrenceDate = new DateTime(2026, 1, 15);
        await wf.RecordOncologyRecurrenceAsync(tumorId, recurrenceDate, "Liver", "CT-detected hepatic metastases.");

        OncologyTumorState state = await wf.GetOncologyTumorAsync(tumorId);
        Assert.That(state.Status, Is.EqualTo(OncologyStatus.Recurrence));
        Assert.That(state.RecurrenceDate, Is.EqualTo(recurrenceDate));
        Assert.That(state.RecurrenceSite, Is.EqualTo("Liver"));
    }

    [Test]
    public async Task GetActiveOncologyTumors_FiltersInRemissionAndDeceasedOut()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumor1 = await RegisterTumor(wf);
        string tumor2 = await wf.RegisterOncologyTumorAsync(
            "C18.0", "Cecum", "8140/3", "Adenocarcinoma", TumorLaterality.NotApplicable,
            new DateTime(2025, 6, 1), DiagnosisBasis.HistologyOfPrimary, 2, null, null);

        // Put tumor1 in remission
        await wf.UpdateOncologyStatusAsync(tumor1, OncologyStatus.InRemission, DateTime.UtcNow, null);

        List<OncologyTumorIndexEntry> active = await wf.GetActiveOncologyTumorsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].TumorId, Is.EqualTo(tumor2));
    }

    // ── Treatment Tests ────────────────────────────────────────────────────────

    [Test]
    public async Task CreateOncologyTreatment_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        string treatmentId = await CreateTreatment(wf, tumorId);

        Assert.That(treatmentId, Is.Not.Null.And.Not.Empty);

        List<OncologyTreatmentIndexEntry> all = await wf.GetOncologyTreatmentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TreatmentId, Is.EqualTo(treatmentId));
        Assert.That(all[0].TumorId, Is.EqualTo(tumorId));
        Assert.That(all[0].TreatmentType, Is.EqualTo(OncologyTreatmentType.Chemotherapy));
        Assert.That(all[0].AgentName, Is.EqualTo("FOLFOX"));
        Assert.That(all[0].Status, Is.EqualTo(OncologyTreatmentStatus.Planned));
    }

    [Test]
    public async Task StartOncologyTreatment_SetsActiveStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        string treatmentId = await CreateTreatment(wf, tumorId);
        DateTime startDate = new DateTime(2025, 4, 1);

        await wf.StartOncologyTreatmentAsync(treatmentId, startDate);

        OncologyTreatmentState state = await wf.GetOncologyTreatmentAsync(treatmentId);
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Active));
        Assert.That(state.StartDate, Is.EqualTo(startDate));
    }

    [Test]
    public async Task CompleteOncologyTreatment_SetsCompletedWithResponse()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        string treatmentId = await CreateTreatment(wf, tumorId);
        await wf.StartOncologyTreatmentAsync(treatmentId, new DateTime(2025, 4, 1));

        DateTime endDate = new DateTime(2025, 9, 15);
        await wf.CompleteOncologyTreatmentAsync(
            treatmentId, endDate,
            TreatmentResponseAssessment.PartialResponse,
            "CT showed 40% reduction in tumor burden.");

        OncologyTreatmentState state = await wf.GetOncologyTreatmentAsync(treatmentId);
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Completed));
        Assert.That(state.EndDate, Is.EqualTo(endDate));
        Assert.That(state.ResponseAssessment, Is.EqualTo(TreatmentResponseAssessment.PartialResponse));

        List<OncologyTreatmentIndexEntry> index = await wf.GetOncologyTreatmentsAsync();
        Assert.That(index[0].Status, Is.EqualTo(OncologyTreatmentStatus.Completed));
        Assert.That(index[0].ResponseAssessment, Is.EqualTo(TreatmentResponseAssessment.PartialResponse));
    }

    [Test]
    public async Task DiscontinueOncologyTreatment_SetsDiscontinuedWithReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        string treatmentId = await CreateTreatment(wf, tumorId);
        await wf.StartOncologyTreatmentAsync(treatmentId, DateTime.UtcNow);

        DateTime endDate = DateTime.UtcNow;
        await wf.DiscontinueOncologyTreatmentAsync(
            treatmentId, endDate, "Grade 4 neuropathy — intolerable toxicity", null);

        OncologyTreatmentState state = await wf.GetOncologyTreatmentAsync(treatmentId);
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Discontinued));
        Assert.That(state.DiscontinuationReason, Does.Contain("neuropathy"));
    }

    [Test]
    public async Task RecordOncologyResponse_RecordsInterimAssessment()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        string treatmentId = await CreateTreatment(wf, tumorId);
        await wf.StartOncologyTreatmentAsync(treatmentId, DateTime.UtcNow);

        DateTime assessmentDate = DateTime.UtcNow;
        await wf.RecordOncologyResponseAsync(
            treatmentId,
            TreatmentResponseAssessment.StableDisease,
            assessmentDate,
            "No measurable change per RECIST 1.1.");

        OncologyTreatmentState state = await wf.GetOncologyTreatmentAsync(treatmentId);
        Assert.That(state.ResponseAssessment, Is.EqualTo(TreatmentResponseAssessment.StableDisease));
        Assert.That(state.ResponseAssessmentDate, Is.Not.Null);
        Assert.That(state.Status, Is.EqualTo(OncologyTreatmentStatus.Active));
    }

    [Test]
    public async Task UpdateOncologyCycles_SetsCycleCount()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        string treatmentId = await CreateTreatment(wf, tumorId);
        await wf.StartOncologyTreatmentAsync(treatmentId, DateTime.UtcNow);

        await wf.UpdateOncologyCyclesAsync(treatmentId, 6);

        OncologyTreatmentState state = await wf.GetOncologyTreatmentAsync(treatmentId);
        Assert.That(state.CyclesCompleted, Is.EqualTo(6));
    }

    [Test]
    public async Task GetOncologyTreatmentsByTumor_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumor1 = await RegisterTumor(wf);
        string tumor2 = await wf.RegisterOncologyTumorAsync(
            "C18.0", "Cecum", "8140/3", "Adenocarcinoma", TumorLaterality.NotApplicable,
            new DateTime(2025, 6, 1), DiagnosisBasis.HistologyOfPrimary, 2, null, null);

        await CreateTreatment(wf, tumor1);
        await wf.CreateOncologyTreatmentAsync(
            tumor2, OncologyTreatmentType.Surgery, "Colectomy",
            null, null, null, null, null);

        List<OncologyTreatmentIndexEntry> t1Treatments = await wf.GetOncologyTreatmentsByTumorAsync(tumor1);
        List<OncologyTreatmentIndexEntry> t2Treatments = await wf.GetOncologyTreatmentsByTumorAsync(tumor2);

        Assert.That(t1Treatments, Has.Count.EqualTo(1));
        Assert.That(t1Treatments[0].AgentName, Is.EqualTo("FOLFOX"));
        Assert.That(t2Treatments, Has.Count.EqualTo(1));
        Assert.That(t2Treatments[0].TreatmentType, Is.EqualTo(OncologyTreatmentType.Surgery));
    }

    [Test]
    public async Task TumorRegistration_AddsTreatmentIdToTumorRecord()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string tumorId = await RegisterTumor(wf);
        string treatmentId = await CreateTreatment(wf, tumorId);

        OncologyTumorState tumor = await wf.GetOncologyTumorAsync(tumorId);
        Assert.That(tumor.TreatmentIds, Contains.Item(treatmentId));
    }

    [Test]
    public async Task MultiplePatients_IndependentTumorsAndTreatments()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        string t1 = await RegisterTumor(wf1);
        await CreateTreatment(wf1, t1);

        string t2 = await wf2.RegisterOncologyTumorAsync(
            "C50.9", "Breast, NOS", "8500/3", "Infiltrating duct carcinoma",
            TumorLaterality.Left, new DateTime(2025, 5, 1),
            DiagnosisBasis.HistologyOfPrimary, 1, null, null);

        List<OncologyTumorIndexEntry> p1Tumors = await wf1.GetOncologyTumorsAsync();
        List<OncologyTumorIndexEntry> p2Tumors = await wf2.GetOncologyTumorsAsync();

        Assert.That(p1Tumors, Has.Count.EqualTo(1));
        Assert.That(p2Tumors, Has.Count.EqualTo(1));
        Assert.That(p1Tumors[0].PrimarySite, Is.EqualTo("C34.1"));
        Assert.That(p2Tumors[0].PrimarySite, Is.EqualTo("C50.9"));
    }
}
