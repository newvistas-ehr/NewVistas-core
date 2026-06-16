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
/// Functional tests for Cancer Registry reporting workflows.
/// §170.315(f)(4) — Transmission to cancer registries.
///
/// Tests end-to-end workflows through the PatientWorkflowGrain:
/// tumor registration → staging → treatment → NAACCR abstract generation →
/// report submission → acceptance/rejection lifecycle.
/// </summary>
[TestFixture]
public class CancerRegistryWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<(string patientId, string tumorId)> SetupPatientWithTumor()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = Workflow(patientId);

        await w.UpdateDemographicsAsync("Johnson, Mary", "F", new DateTime(1960, 3, 22), null);

        string tumorId = await w.RegisterOncologyTumorAsync(
            "C50.4", "Upper-outer quadrant of breast",
            "8500/3", "Infiltrating duct carcinoma, NOS",
            TumorLaterality.Left,
            new DateTime(2025, 4, 10),
            DiagnosisBasis.HistologyOfPrimary,
            1, "ONC-100", "Dr. Breast Oncologist");

        await w.RecordOncologyStagingAsync(tumorId,
            "T1c", "N0", "M0",
            "pT1c", "pN0(sn)", "pM0",
            "IA", "1");

        return (patientId, tumorId);
    }

    // ─── Full Workflow: Generate → Submit → Accept ───────────────────────

    [Test]
    public async Task CrWorkflow_FullLifecycleGenerateSubmitAccept()
    {
        (string patientId, string tumorId) = await SetupPatientWithTumor();

        // Generate
        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-MDACC", "REG-100", "Mary Registrar");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);
        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Generated));
        Assert.That(report.PatientName, Is.EqualTo("Johnson, Mary"));
        Assert.That(report.PrimarySite, Is.EqualTo("C50.4"));

        // Submit
        await Workflow(patientId).SubmitCancerRegistryReportAsync(
            reportId, "Maryland Cancer Registry", "MCR-2025-0001");

        report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);
        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Submitted));
        Assert.That(report.RegistryName, Is.EqualTo("Maryland Cancer Registry"));

        // Accept
        await Workflow(patientId).AcceptCancerRegistryReportAsync(
            reportId, "Abstract received and validated successfully.");

        report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);
        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Accepted));
    }

    [Test]
    public async Task CrWorkflow_FullLifecycleGenerateSubmitReject()
    {
        (string patientId, string tumorId) = await SetupPatientWithTumor();

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-MDACC", "REG-101", "Alice Registrar");

        await Workflow(patientId).SubmitCancerRegistryReportAsync(
            reportId, "Central Cancer Registry", null);

        await Workflow(patientId).RejectCancerRegistryReportAsync(
            reportId, "Histology code does not match primary site topography.");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);
        Assert.That(report.Status, Is.EqualTo(CancerRegistryReportStatus.Rejected));
        Assert.That(report.RejectionReason, Does.Contain("Histology code"));
    }

    // ─── NAACCR Abstract Validation ──────────────────────────────────────

    [Test]
    public async Task CrWorkflow_NaaccrAbstractHasRequiredFields()
    {
        (string patientId, string tumorId) = await SetupPatientWithTumor();

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-MDACC", "REG-102", "Bob Registrar");

        string naaccr = await Workflow(patientId).GetCancerRegistryNaaccrAbstractAsync(reportId);

        // NAACCR header
        Assert.That(naaccr, Does.Contain("NAACCR|V24|ABSTRACT"));

        // Patient demographics
        Assert.That(naaccr, Does.Contain("PATIENT_NAME|Johnson, Mary"));
        Assert.That(naaccr, Does.Contain("SEX|2")); // Female = 2

        // Tumor data
        Assert.That(naaccr, Does.Contain("PRIMARY_SITE|C50.4"));
        Assert.That(naaccr, Does.Contain("HISTOLOGIC_TYPE|8500/3"));
        Assert.That(naaccr, Does.Contain("LATERALITY|Left"));
        Assert.That(naaccr, Does.Contain("DATE_OF_DIAGNOSIS|20250410"));

        // Staging
        Assert.That(naaccr, Does.Contain("CLINICAL_T|T1c"));
        Assert.That(naaccr, Does.Contain("CLINICAL_N|N0"));
        Assert.That(naaccr, Does.Contain("AJCC_STAGE_GROUP|IA"));
        Assert.That(naaccr, Does.Contain("SEER_SUMMARY_STAGE|1"));

        // Reporting
        Assert.That(naaccr, Does.Contain("REPORTING_FACILITY|VA-MDACC"));
    }

    // ─── Treatment Integration ───────────────────────────────────────────

    [Test]
    public async Task CrWorkflow_ReportIncludesMultipleTreatments()
    {
        (string patientId, string tumorId) = await SetupPatientWithTumor();

        // Add surgery
        string surgTxId = await Workflow(patientId).CreateOncologyTreatmentAsync(
            tumorId, OncologyTreatmentType.Surgery, "Lumpectomy",
            null, "SURG-001", "Dr. Surgeon", "VA Surgical Center", null);
        await Workflow(patientId).StartOncologyTreatmentAsync(surgTxId, new DateTime(2025, 5, 1));

        // Add chemo
        string chemoTxId = await Workflow(patientId).CreateOncologyTreatmentAsync(
            tumorId, OncologyTreatmentType.Chemotherapy, "Doxorubicin/Cyclophosphamide",
            "AC regimen", "ONCO-001", "Dr. Chemo", "VA Infusion Center", null);
        await Workflow(patientId).StartOncologyTreatmentAsync(chemoTxId, new DateTime(2025, 6, 15));

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-MDACC", "REG-103", "Carol Registrar");

        CancerRegistryReportState report = await Workflow(patientId).GetCancerRegistryReportAsync(reportId);

        Assert.That(report.TreatmentSummary, Does.Contain("Surgery"));
        Assert.That(report.TreatmentSummary, Does.Contain("Chemotherapy"));
        Assert.That(report.FirstTreatmentDate, Is.EqualTo(new DateTime(2025, 5, 1)));
    }

    // ─── Index Queries ───────────────────────────────────────────────────

    [Test]
    public async Task CrWorkflow_IndexTracksMultipleReports()
    {
        (string patient1, string tumor1) = await SetupPatientWithTumor();
        (string patient2, string tumor2) = await SetupPatientWithTumor();

        await Workflow(patient1).GenerateCancerRegistryReportAsync(
            tumor1, "VA-508", "REG-200", "Registrar A");
        await Workflow(patient2).GenerateCancerRegistryReportAsync(
            tumor2, "VA-508", "REG-201", "Registrar B");

        ICancerRegistryReportIndexGrain index = _cluster.GrainFactory
            .GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

        List<CancerRegistryReportIndexEntry> all = await index.GetAllReportsAsync();
        Assert.That(all.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task CrWorkflow_IndexFiltersByPatient()
    {
        (string patientId, string tumorId) = await SetupPatientWithTumor();

        await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-202", "Registrar C");

        ICancerRegistryReportIndexGrain index = _cluster.GrainFactory
            .GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

        List<CancerRegistryReportIndexEntry> reports = await index.GetReportsByPatientAsync(patientId);
        Assert.That(reports.Count, Is.GreaterThan(0));
        Assert.That(reports.All(r => r.PatientId == patientId), Is.True);
    }

    [Test]
    public async Task CrWorkflow_IndexPendingFiltersCorrectly()
    {
        (string patientId, string tumorId) = await SetupPatientWithTumor();

        string reportId = await Workflow(patientId).GenerateCancerRegistryReportAsync(
            tumorId, "VA-508", "REG-203", "Registrar D");

        ICancerRegistryReportIndexGrain index = _cluster.GrainFactory
            .GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

        // Should appear in pending
        List<CancerRegistryReportIndexEntry> pending = await index.GetPendingReportsAsync();
        Assert.That(pending.Any(r => r.ReportId == reportId), Is.True);

        // After submission, should no longer be pending
        await Workflow(patientId).SubmitCancerRegistryReportAsync(
            reportId, "State Registry", null);

        pending = await index.GetPendingReportsAsync();
        Assert.That(pending.All(r => r.ReportId != reportId), Is.True);
    }

    // ─── Multi-Patient Isolation ─────────────────────────────────────────

    [Test]
    public async Task CrWorkflow_ReportsIsolatedPerPatient()
    {
        (string patient1, string tumor1) = await SetupPatientWithTumor();
        (string patient2, string tumor2) = await SetupPatientWithTumor();

        string report1 = await Workflow(patient1).GenerateCancerRegistryReportAsync(
            tumor1, "VA-508", "REG-300", "Registrar X");

        CancerRegistryReportState state1 = await Workflow(patient1).GetCancerRegistryReportAsync(report1);
        Assert.That(state1.PatientId, Is.EqualTo(patient1));

        string report2 = await Workflow(patient2).GenerateCancerRegistryReportAsync(
            tumor2, "VA-508", "REG-301", "Registrar Y");

        CancerRegistryReportState state2 = await Workflow(patient2).GetCancerRegistryReportAsync(report2);
        Assert.That(state2.PatientId, Is.EqualTo(patient2));
        Assert.That(state1.PatientId, Is.Not.EqualTo(state2.PatientId));
    }
}
