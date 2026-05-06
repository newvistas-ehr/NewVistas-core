// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Audit Report grains.
/// §170.315(d)(3) — Audit Report(s).
/// </summary>
[TestFixture]
public class AuditReportGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Report Grain CRUD ────────────────────────────────────────────────────

    [Test]
    public async Task ReportGrain_CanSaveAndRetrieve()
    {
        string reportId = Guid.NewGuid().ToString("N");
        IAuditReportGrain grain = _cluster.GrainFactory.GetGrain<IAuditReportGrain>($"AUDIT-REPORT:{reportId}");

        var report = new AuditReportState
        {
            ReportId = reportId,
            Title = "Test Report",
            ReportType = "patient",
            PatientId = "PAT-001",
            PeriodStart = DateTime.UtcNow.AddDays(-30),
            PeriodEnd = DateTime.UtcNow,
            GeneratedDate = DateTime.UtcNow,
            TotalEvents = 42,
            EventsByDomain = new Dictionary<string, int> { { "ORDERS", 20 }, { "LABS", 22 } },
            EventsByAction = new Dictionary<string, int> { { "VIEW", 30 }, { "UPDATE", 12 } },
            IntegrityStatus = "verified",
            IntegrityPassCount = 42,
            IntegrityFailCount = 0
        };

        await grain.SaveReportAsync(report);
        AuditReportState result = await grain.GetReportAsync();

        Assert.That(result.ReportId, Is.EqualTo(reportId));
        Assert.That(result.Title, Is.EqualTo("Test Report"));
        Assert.That(result.TotalEvents, Is.EqualTo(42));
        Assert.That(result.EventsByDomain["ORDERS"], Is.EqualTo(20));
        Assert.That(result.IntegrityStatus, Is.EqualTo("verified"));
    }

    [Test]
    public async Task ReportGrain_StoresIntegrityFailures()
    {
        string reportId = Guid.NewGuid().ToString("N");
        IAuditReportGrain grain = _cluster.GrainFactory.GetGrain<IAuditReportGrain>($"AUDIT-REPORT:{reportId}");

        await grain.SaveReportAsync(new AuditReportState
        {
            ReportId = reportId,
            Title = "Tampered Report",
            ReportType = "patient",
            IntegrityStatus = "tamper-detected",
            IntegrityPassCount = 38,
            IntegrityFailCount = 2,
            IntegrityFailures = new List<string> { "AUDIT-BAD1", "AUDIT-BAD2" }
        });

        AuditReportState result = await grain.GetReportAsync();
        Assert.That(result.IntegrityStatus, Is.EqualTo("tamper-detected"));
        Assert.That(result.IntegrityFailCount, Is.EqualTo(2));
        Assert.That(result.IntegrityFailures, Has.Count.EqualTo(2));
        Assert.That(result.IntegrityFailures, Contains.Item("AUDIT-BAD1"));
    }

    // ─── Report Index ─────────────────────────────────────────────────────────

    [Test]
    public async Task ReportIndex_CanAddAndList()
    {
        IAuditReportIndexGrain index = _cluster.GrainFactory.GetGrain<IAuditReportIndexGrain>(
            $"AUDIT-REPORT-INDEX-{Guid.NewGuid():N}");

        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-001", Title = "Report 1", ReportType = "patient",
            PatientId = "PAT-A", GeneratedDate = DateTime.UtcNow.AddHours(-2),
            TotalEvents = 10, IntegrityStatus = "verified"
        });
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-002", Title = "Report 2", ReportType = "patient",
            PatientId = "PAT-B", GeneratedDate = DateTime.UtcNow.AddHours(-1),
            TotalEvents = 25, IntegrityStatus = "not-checked"
        });

        List<AuditReportSummary> all = await index.GetAllReportsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].ReportId, Is.EqualTo("RPT-002")); // newest first
    }

    [Test]
    public async Task ReportIndex_FiltersByPatient()
    {
        IAuditReportIndexGrain index = _cluster.GrainFactory.GetGrain<IAuditReportIndexGrain>(
            $"AUDIT-REPORT-INDEX-{Guid.NewGuid():N}");

        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-P1", PatientId = "PAT-X", ReportType = "patient",
            GeneratedDate = DateTime.UtcNow
        });
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-P2", PatientId = "PAT-Y", ReportType = "patient",
            GeneratedDate = DateTime.UtcNow
        });
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-P3", PatientId = "PAT-X", ReportType = "patient",
            GeneratedDate = DateTime.UtcNow
        });

        List<AuditReportSummary> patX = await index.GetReportsByPatientAsync("PAT-X");
        Assert.That(patX, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ReportIndex_FiltersByType()
    {
        IAuditReportIndexGrain index = _cluster.GrainFactory.GetGrain<IAuditReportIndexGrain>(
            $"AUDIT-REPORT-INDEX-{Guid.NewGuid():N}");

        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-T1", ReportType = "patient", GeneratedDate = DateTime.UtcNow
        });
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-T2", ReportType = "system", GeneratedDate = DateTime.UtcNow
        });

        List<AuditReportSummary> patient = await index.GetReportsByTypeAsync("patient");
        Assert.That(patient, Has.Count.EqualTo(1));
        Assert.That(patient[0].ReportId, Is.EqualTo("RPT-T1"));
    }

    [Test]
    public async Task ReportIndex_DeduplicatesOnReportId()
    {
        IAuditReportIndexGrain index = _cluster.GrainFactory.GetGrain<IAuditReportIndexGrain>(
            $"AUDIT-REPORT-INDEX-{Guid.NewGuid():N}");

        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-DUP", Title = "First", ReportType = "patient",
            GeneratedDate = DateTime.UtcNow, TotalEvents = 5
        });
        await index.AddReportAsync(new AuditReportSummary
        {
            ReportId = "RPT-DUP", Title = "Updated", ReportType = "patient",
            GeneratedDate = DateTime.UtcNow, TotalEvents = 10
        });

        List<AuditReportSummary> all = await index.GetAllReportsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TotalEvents, Is.EqualTo(10));
    }

    // ─── Report Generator ─────────────────────────────────────────────────────

    [Test]
    public async Task Generator_GeneratesReportFromAuditEvents()
    {
        string patientId = $"AUDIT-RPT-{Guid.NewGuid():N}";

        // Create audit events via the workflow grain
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-001",
            "DR-SMITH", "Smith, John", null, null, "New order placed");
        await workflow.LogAuditEventAsync("LABS", "VIEW", "LabTestState", "LAB-001",
            "DR-SMITH", "Smith, John", null, null, "Lab result viewed");
        await workflow.LogAuditEventAsync("ORDERS", "UPDATE", "OrderState", "ORD-001",
            "DR-JONES", "Jones, Mary", null, null, "Order modified");

        // Generate report
        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
            null, null, null, false, "ADMIN");

        Assert.That(report.TotalEvents, Is.EqualTo(3));
        Assert.That(report.EventsByDomain["ORDERS"], Is.EqualTo(2));
        Assert.That(report.EventsByDomain["LABS"], Is.EqualTo(1));
        Assert.That(report.EventsByAction["CREATE"], Is.EqualTo(1));
        Assert.That(report.EventsByAction["VIEW"], Is.EqualTo(1));
        Assert.That(report.EventsByAction["UPDATE"], Is.EqualTo(1));
        Assert.That(report.EventsByUser["Smith, John"], Is.EqualTo(2));
        Assert.That(report.EventsByUser["Jones, Mary"], Is.EqualTo(1));
        Assert.That(report.ReportType, Is.EqualTo("patient"));
        Assert.That(report.PatientId, Is.EqualTo(patientId));
        Assert.That(report.IntegrityStatus, Is.EqualTo("not-checked"));
    }

    [Test]
    public async Task Generator_FiltersByDomain()
    {
        string patientId = $"AUDIT-RPT-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-A",
            null, null, null, null, "Order A");
        await workflow.LogAuditEventAsync("LABS", "VIEW", "LabTestState", "LAB-A",
            null, null, null, null, "Lab A");
        await workflow.LogAuditEventAsync("ORDERS", "UPDATE", "OrderState", "ORD-B",
            null, null, null, null, "Order B");

        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
            "ORDERS", null, null, false, null);

        Assert.That(report.TotalEvents, Is.EqualTo(2));
        Assert.That(report.EventsByDomain.ContainsKey("LABS"), Is.False);
        Assert.That(report.DomainFilter, Is.EqualTo("ORDERS"));
    }

    [Test]
    public async Task Generator_FiltersByAction()
    {
        string patientId = $"AUDIT-RPT-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-C",
            null, null, null, null, "Created");
        await workflow.LogAuditEventAsync("ORDERS", "VIEW", "OrderState", "ORD-C",
            null, null, null, null, "Viewed");
        await workflow.LogAuditEventAsync("ORDERS", "DELETE", "OrderState", "ORD-C",
            null, null, null, null, "Deleted");

        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
            null, "DELETE", null, false, null);

        Assert.That(report.TotalEvents, Is.EqualTo(1));
        Assert.That(report.ActionFilter, Is.EqualTo("DELETE"));
    }

    [Test]
    public async Task Generator_VerifiesIntegrity()
    {
        string patientId = $"AUDIT-RPT-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-INT",
            "DR-A", "Dr. A", null, null, "Test event 1");
        await workflow.LogAuditEventAsync("LABS", "VIEW", "LabTestState", "LAB-INT",
            "DR-A", "Dr. A", null, null, "Test event 2");

        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
            null, null, null, true, null);

        Assert.That(report.IntegrityStatus, Is.EqualTo("verified"));
        Assert.That(report.IntegrityPassCount, Is.EqualTo(2));
        Assert.That(report.IntegrityFailCount, Is.EqualTo(0));
        Assert.That(report.IntegrityFailures, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task Generator_EmptyDateRange_ReturnsZeroEvents()
    {
        string patientId = $"AUDIT-RPT-{Guid.NewGuid():N}";

        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        await workflow.LogAuditEventAsync("ORDERS", "CREATE", "OrderState", "ORD-EMPTY",
            null, null, null, null, "Event outside range");

        IAuditReportGeneratorGrain generator = _cluster.GrainFactory.GetGrain<IAuditReportGeneratorGrain>(
            $"AUDIT-REPORT-GEN:{patientId}");
        AuditReportState report = await generator.GenerateReportAsync(
            DateTime.UtcNow.AddYears(-10), DateTime.UtcNow.AddYears(-9),
            null, null, null, false, null);

        Assert.That(report.TotalEvents, Is.EqualTo(0));
        Assert.That(report.Events, Has.Count.EqualTo(0));
    }
}
