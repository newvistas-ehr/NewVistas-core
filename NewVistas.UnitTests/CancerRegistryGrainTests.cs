// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Cancer Registry grains.
/// §170.315(f)(4) — Transmission to cancer registries.
/// </summary>
[TestFixture]
public class CancerRegistryGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> SetupPatientWithTumor(string patientId)
    {
        IPatientWorkflowGrain w = Workflow(patientId);
        await w.UpdateDemographicsAsync("Smith, John", "M", new DateTime(1955, 8, 20), null);

        string tumorId = await w.RegisterOncologyTumorAsync(
            "C34.1", "Upper lobe, right lung",
            "8140/3", "Adenocarcinoma, NOS",
            TumorLaterality.Right,
            new DateTime(2025, 6, 15),
            DiagnosisBasis.HistologyOfPrimary,
            1, "ONC-001", "Dr. Oncologist");

        await w.RecordOncologyStagingAsync(tumorId,
            "T2a", "N1", "M0",
            "pT2a", "pN1", "pM0",
            "IIB", "3");

        return tumorId;
    }

    // ─── Report Generation ───────────────────────────────────────────────

    [Test]
    public async Task CancerRegistry_CanGenerateReport()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-001", "Jane Registrar");

        Assert.That(reportId, Does.StartWith("CR-REPORT:"));
    }

    [Test]
    public async Task CancerRegistry_ReportContainsPatientData()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-001", "Jane Registrar");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.PatientName, Is.EqualTo("Smith, John"));
        Assert.That(report.Sex, Is.EqualTo("M"));
        Assert.That(report.PatientId, Is.EqualTo(patientId));
    }

    [Test]
    public async Task CancerRegistry_ReportContainsTumorData()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-002", "Bob Registrar");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.PrimarySite, Is.EqualTo("C34.1"));
        Assert.That(report.PrimarySiteText, Is.EqualTo("Upper lobe, right lung"));
        Assert.That(report.Histology, Is.EqualTo("8140/3"));
        Assert.That(report.HistologyText, Is.EqualTo("Adenocarcinoma, NOS"));
        Assert.That(report.Laterality, Is.EqualTo("Right"));
        Assert.That(report.DateOfDiagnosis.Year, Is.EqualTo(2025));
    }

    [Test]
    public async Task CancerRegistry_ReportContainsStagingData()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-003", "Carol Registrar");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.ClinicalT, Is.EqualTo("T2a"));
        Assert.That(report.ClinicalN, Is.EqualTo("N1"));
        Assert.That(report.ClinicalM, Is.EqualTo("M0"));
        Assert.That(report.PathologicT, Is.EqualTo("pT2a"));
        Assert.That(report.StageGroup, Is.EqualTo("IIB"));
        Assert.That(report.SeerSummaryStage, Is.EqualTo("3"));
    }

    [Test]
    public async Task CancerRegistry_ReportHasGeneratedStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-004", "Dave Registrar");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Generated));
        Assert.That(report.ReportingFacility, Is.EqualTo("VA-508"));
        Assert.That(report.RegistrarName, Is.EqualTo("Dave Registrar"));
    }

    // ─── NAACCR Abstract ─────────────────────────────────────────────────

    [Test]
    public async Task CancerRegistry_NaaccrAbstractContainsExpectedFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-005", "Eve Registrar");

        string naaccr = await Workflow(patientId).GetCancerRegistryNaaccrAbstractAsync(reportId);

        Assert.That(naaccr, Does.Contain("NAACCR|V24|ABSTRACT"));
        Assert.That(naaccr, Does.Contain("PATIENT_NAME|Smith, John"));
        Assert.That(naaccr, Does.Contain("PRIMARY_SITE|C34.1"));
        Assert.That(naaccr, Does.Contain("HISTOLOGIC_TYPE|8140/3"));
        Assert.That(naaccr, Does.Contain("CLINICAL_T|T2a"));
        Assert.That(naaccr, Does.Contain("AJCC_STAGE_GROUP|IIB"));
        Assert.That(naaccr, Does.Contain("REPORTING_FACILITY|VA-508"));
    }

    [Test]
    public async Task CancerRegistry_NaaccrAbstractContainsSexCode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-006", "Frank Registrar");

        string naaccr = await Workflow(patientId).GetCancerRegistryNaaccrAbstractAsync(reportId);

        Assert.That(naaccr, Does.Contain("SEX|1")); // Male = 1
    }

    // ─── Report Lifecycle ────────────────────────────────────────────────

    [Test]
    public async Task CancerRegistry_CanSubmitReport()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-007", "Grace Registrar");

        await Workflow(patientId).SubmitCancerRegistryReportAsync(
            reportId, "State Cancer Registry", "CONF-12345");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Submitted));
        Assert.That(report.RegistryName, Is.EqualTo("State Cancer Registry"));
        Assert.That(report.ConfirmationNumber, Is.EqualTo("CONF-12345"));
        Assert.That(report.SubmittedDate, Is.Not.Null);
    }

    [Test]
    public async Task CancerRegistry_CanAcceptReport()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-008", "Heidi Registrar");

        await Workflow(patientId).SubmitCancerRegistryReportAsync(
            reportId, "Central Registry", null);
        await Workflow(patientId).AcceptCancerRegistryReportAsync(
            reportId, "Record accepted and imported.");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Accepted));
        Assert.That(report.RegistryResponse, Is.EqualTo("Record accepted and imported."));
    }

    [Test]
    public async Task CancerRegistry_CanRejectReport()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-009", "Ivan Registrar");

        await Workflow(patientId).SubmitCancerRegistryReportAsync(
            reportId, "State Registry", null);
        await Workflow(patientId).RejectCancerRegistryReportAsync(
            reportId, "Missing SEER summary stage.");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Rejected));
        Assert.That(report.RejectionReason, Is.EqualTo("Missing SEER summary stage."));
    }

    // ─── Index Operations ────────────────────────────────────────────────

    [Test]
    public async Task CancerRegistry_IndexTracksReports()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-010", "Julia Registrar");

        ICancerRegistryReportIndexGrain index = _cluster.GrainFactory
            .GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

        List<CancerRegistryReportIndexEntry> all = await index.GetAllReportsAsync();
        Assert.That(all.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task CancerRegistry_IndexFiltersByPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-011", "Kyle Registrar");

        ICancerRegistryReportIndexGrain index = _cluster.GrainFactory
            .GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

        List<CancerRegistryReportIndexEntry> reports = await index.GetReportsByPatientAsync(patientId);
        Assert.That(reports.Count, Is.GreaterThan(0));
        Assert.That(reports.All(r => r.PatientId == patientId), Is.True);
    }

    [Test]
    public async Task CancerRegistry_IndexFiltersByStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-012", "Laura Registrar");

        ICancerRegistryReportIndexGrain index = _cluster.GrainFactory
            .GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

        List<CancerRegistryReportIndexEntry> pending = await index.GetPendingReportsAsync();
        Assert.That(pending.Count, Is.GreaterThan(0));
        Assert.That(pending.All(r => r.Status == CancerRegistryReportStatus.Generated), Is.True);
    }

    [Test]
    public async Task CancerRegistry_IndexUpdatesOnStatusChange()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-013", "Mike Registrar");

        await Workflow(patientId).SubmitCancerRegistryReportAsync(
            reportId, "State Registry", "CONF-99999");

        ICancerRegistryReportIndexGrain index = _cluster.GrainFactory
            .GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

        List<CancerRegistryReportIndexEntry> submitted = await index.GetReportsByStatusAsync("Submitted");
        Assert.That(submitted.Any(r => r.ReportId == reportId), Is.True);
    }

    // ─── Treatment Integration ───────────────────────────────────────────

    [Test]
    public async Task CancerRegistry_ReportIncludesTreatmentData()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        // Add a treatment
        string txId = await Workflow(patientId).CreateOncologyTreatmentAsync(
            tumorId, OncologyTreatmentType.Chemotherapy, "Cisplatin",
            "75mg/m2 IV", "PROV-001", "Dr. Chemo", "VA Medical Center", null);
        await Workflow(patientId).StartOncologyTreatmentAsync(txId, new DateTime(2025, 7, 1));

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-014", "Nancy Registrar");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.TreatmentSummary, Does.Contain("Chemotherapy"));
        Assert.That(report.TreatmentSummary, Does.Contain("Cisplatin"));
        Assert.That(report.FirstTreatmentDate, Is.EqualTo(new DateTime(2025, 7, 1)));
    }

    [Test]
    public async Task CancerRegistry_NaaccrAbstractIncludesTreatment()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string tumorId = await SetupPatientWithTumor(patientId);

        string txId = await Workflow(patientId).CreateOncologyTreatmentAsync(
            tumorId, OncologyTreatmentType.Radiation, "External beam",
            "60 Gy / 30 fractions", "PROV-002", "Dr. Rad", "VA Rad Center", null);
        await Workflow(patientId).StartOncologyTreatmentAsync(txId, new DateTime(2025, 8, 1));

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-015", "Oscar Registrar");

        string naaccr = await Workflow(patientId).GetCancerRegistryNaaccrAbstractAsync(reportId);

        Assert.That(naaccr, Does.Contain("TREATMENT_SUMMARY|Radiation: External beam"));
        Assert.That(naaccr, Does.Contain("DATE_FIRST_TREATMENT|20250801"));
    }
}
