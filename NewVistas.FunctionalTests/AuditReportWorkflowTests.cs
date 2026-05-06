// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Audit Report generation end-to-end workflows.
/// §170.315(d)(3) — Audit Report(s).
/// </summary>
[TestFixture]
public class AuditReportWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task FullLifecycle_GenerateReport_PersistAndRetrieve()
    {
        string patientId = $"AUDIT-FUNC-{Guid.NewGuid():N}";

        // 1. Create audit trail
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-F1",
            "DR-SMITH", "Smith, John", "LOC-1", "Ward 3A", "New medication order");
        await workflow.LogAuditEventAsync("PHARMACY", "VIEW", "PrescriptionState", "RX-F1",
            "DR-SMITH", "Smith, John", "LOC-1", "Ward 3A", "Reviewed prescription");
        await workflow.LogAuditEventAsync("ORDERS", "SIGN", "OrderState", "ORD-F1",
            "DR-SMITH", "Smith, John", "LOC-1", "Ward 3A", "Order signed");
        await workflow.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-F1",
            "DR-JONES", "Jones, Mary", "LOC-2", "Lab", "Lab order placed");
        await workflow.LogAuditEventAsync("NOTES", "CREATE", "TiuDocumentState", "NOTE-F1",
            "DR-JONES", "Jones, Mary", "LOC-2", "Lab", "Progress note created");

        // 2. Generate report with integrity verification
        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
            null, null, null, true, "ADMIN-USER");

        Assert.That(report.TotalEvents, Is.EqualTo(5));
        Assert.That(report.IntegrityStatus, Is.EqualTo("verified"));
        Assert.That(report.IntegrityPassCount, Is.EqualTo(5));
        Assert.That(report.GeneratedBy, Is.EqualTo("ADMIN-USER"));

        // Verify aggregation stats
        Assert.That(report.EventsByDomain["ORDERS"], Is.EqualTo(2));
        Assert.That(report.EventsByDomain["PHARMACY"], Is.EqualTo(1));
        Assert.That(report.EventsByDomain["LABS"], Is.EqualTo(1));
        Assert.That(report.EventsByDomain["NOTES"], Is.EqualTo(1));
        Assert.That(report.EventsByAction["CREATE"], Is.EqualTo(3));
        Assert.That(report.EventsByUser["Smith, John"], Is.EqualTo(3));
        Assert.That(report.EventsByUser["Jones, Mary"], Is.EqualTo(2));

        // 3. Persist the report
        string reportGrainId = $"AUDIT-REPORT:{report.ReportId}";
        IAuditReportGrain reportGrain = _cluster.GrainFactory.GetGrain<IAuditReportGrain>(reportGrainId);
        await reportGrain.SaveReportAsync(report);

        // 4. Add to index
        IAuditReportIndexGrain index = _cluster.GrainFactory.GetGrain<IAuditReportIndexGrain>("AUDIT-REPORT-INDEX");
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = report.ReportId,
            Title = report.Title,
            ReportType = report.ReportType,
            PatientId = patientId,
            PeriodStart = report.PeriodStart,
            PeriodEnd = report.PeriodEnd,
            GeneratedDate = report.GeneratedDate,
            TotalEvents = report.TotalEvents,
            IntegrityStatus = report.IntegrityStatus
        });

        // 5. Retrieve from persistence
        AuditReportState retrieved = await reportGrain.GetReportAsync();
        Assert.That(retrieved.ReportId, Is.EqualTo(report.ReportId));
        Assert.That(retrieved.TotalEvents, Is.EqualTo(5));
        Assert.That(retrieved.Events, Has.Count.EqualTo(5));

        // 6. Verify index listing
        List<AuditReportSummary> patientReports = await index.GetReportsByPatientAsync(patientId);
        Assert.That(patientReports, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(patientReports.Any(r => r.ReportId == report.ReportId), Is.True);
    }

    [Test]
    public async Task FilteredReport_DomainAndAction()
    {
        string patientId = $"AUDIT-FUNC-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-FA",
            "DR-A", "Dr. A", null, null, "Order created");
        await workflow.LogAuditEventAsync("ORDERS", "UPDATE", "OrderState", "ORD-FA",
            "DR-A", "Dr. A", null, null, "Order updated");
        await workflow.LogAuditEventAsync("ORDERS", "VIEW", "OrderState", "ORD-FA",
            "DR-B", "Dr. B", null, null, "Order viewed");
        await workflow.LogAuditEventAsync("LABS", "CREATE", "LabTestState", "LAB-FA",
            "DR-A", "Dr. A", null, null, "Lab created");

        // Filter: ORDERS domain only, CREATE action only
        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
            "ORDERS", "CREATE", null, false, null);

        Assert.That(report.TotalEvents, Is.EqualTo(1));
        Assert.That(report.DomainFilter, Is.EqualTo("ORDERS"));
        Assert.That(report.ActionFilter, Is.EqualTo("CREATE"));
    }

    [Test]
    public async Task ReportTitle_IncludesDateRangeAndFilters()
    {
        string patientId = $"AUDIT-FUNC-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("PHARMACY", "VIEW", "PrescriptionState", "RX-T",
            null, null, null, null, "Viewed");

        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            "PHARMACY", null, null, false, null);

        Assert.That(report.Title, Does.Contain("2026-01-01"));
        Assert.That(report.Title, Does.Contain("2026-12-31"));
        Assert.That(report.Title, Does.Contain("PHARMACY"));
    }

    [Test]
    public async Task IntegrityVerification_AllEventsPass()
    {
        string patientId = $"AUDIT-FUNC-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-V1",
            "DR-V", "Dr. Verify", null, null, "Event 1");
        await workflow.LogAuditEventAsync("ORDERS", "UPDATE", "OrderState", "ORD-V1",
            "DR-V", "Dr. Verify", null, null, "Event 2");
        await workflow.LogAuditEventAsync("ORDERS", "SIGN", "OrderState", "ORD-V1",
            "DR-V", "Dr. Verify", null, null, "Event 3");

        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
            null, null, null, true, null);

        Assert.That(report.IntegrityStatus, Is.EqualTo("verified"));
        Assert.That(report.IntegrityPassCount, Is.EqualTo(3));
        Assert.That(report.IntegrityFailCount, Is.EqualTo(0));
    }

    [Test]
    public async Task MultipleReports_IndexTracksAll()
    {
        IAuditReportIndexGrain index = _cluster.GrainFactory.GetGrain<IAuditReportIndexGrain>(
            $"AUDIT-REPORT-INDEX-MULTI-{Guid.NewGuid():N}");

        string pat1 = $"PAT-MULTI-{Guid.NewGuid():N}";
        string pat2 = $"PAT-MULTI-{Guid.NewGuid():N}";

        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-M1", Title = "Report 1", ReportType = "patient",
            PatientId = pat1, GeneratedDate = DateTime.UtcNow.AddMinutes(-3),
            TotalEvents = 10, IntegrityStatus = "verified"
        });
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-M2", Title = "Report 2", ReportType = "patient",
            PatientId = pat2, GeneratedDate = DateTime.UtcNow.AddMinutes(-2),
            TotalEvents = 20, IntegrityStatus = "not-checked"
        });
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-M3", Title = "Report 3", ReportType = "patient",
            PatientId = pat1, GeneratedDate = DateTime.UtcNow.AddMinutes(-1),
            TotalEvents = 5, IntegrityStatus = "verified"
        });

        // All
        List<AuditReportSummary> all = await index.GetAllReportsAsync();
        Assert.That(all, Has.Count.EqualTo(3));
        Assert.That(all[0].ReportId, Is.EqualTo("RPT-M3")); // newest first

        // By patient
        List<AuditReportSummary> pat1Reports = await index.GetReportsByPatientAsync(pat1);
        Assert.That(pat1Reports, Has.Count.EqualTo(2));

        // By type
        List<AuditReportSummary> patientType = await index.GetReportsByTypeAsync("patient");
        Assert.That(patientType, Has.Count.EqualTo(3));
    }
}
