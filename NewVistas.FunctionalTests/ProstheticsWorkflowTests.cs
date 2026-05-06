// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Prosthetics — File #669.1.
/// Prosthetics items are now embedded on the patient grain as ProstheticsEntry.
/// Tests exercise the workflow grain methods for issuing, returning, repairing,
/// cost tracking, warranty, delivery, fitting, satisfaction, and maintenance workflows.
/// </summary>
[TestFixture]
public class ProstheticsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private async Task<string> IssueStandardItemAsync(IPatientWorkflowGrain w,
        string itemDescription = "BELOW KNEE PROSTHESIS, LEFT")
    {
        return await w.IssueProstheticAsync(
            itemDescription, "L5301", "PROSTHETIC",
            DateTime.UtcNow, 1, 5200.00m,
            "PROV-001", "Dr. Smith",
            "LOC-001", "Prosthetics Lab",
            true, "Standard issue");
    }

    // ─── 1. Issue Item ───────────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanIssueItem()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await IssueStandardItemAsync(w);

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Status, Is.EqualTo("ISSUED"));
        Assert.That(entry.ItemDescription, Is.EqualTo("BELOW KNEE PROSTHESIS, LEFT"));
        Assert.That(entry.HcpcsCode, Is.EqualTo("L5301"));
        Assert.That(entry.Quantity, Is.EqualTo(1));
    }

    // ─── 2. Get Item ─────────────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanGetItem()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await IssueStandardItemAsync(w);

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ProstheticsId, Is.Not.Empty);
        Assert.That(entry.ItemCategory, Is.EqualTo("PROSTHETIC"));
        Assert.That(entry.IsServiceConnected, Is.True);
    }

    // ─── 3. Set HCPCS Code ──────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanSetHcpcsCode()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);

        await w.SetProstheticsHcpcsCodeAsync(id, "L5321");

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.HcpcsCode, Is.EqualTo("L5321"));
    }

    // ─── 4. Record Cost ─────────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanRecordCost()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);

        await w.RecordProstheticsCostAsync(id, 7500.00m, "ProTech Solutions", "VEND-002");

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Cost, Is.EqualTo(7500.00m));
        Assert.That(entry.Vendor, Is.EqualTo("ProTech Solutions"));
        Assert.That(entry.VendorId, Is.EqualTo("VEND-002"));
    }

    // ─── 5. Set Warranty ─────────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanSetWarranty()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);
        DateTime warrantyStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime warrantyEnd = new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await w.SetProstheticsWarrantyAsync(id, warrantyStart, warrantyEnd);

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.WarrantyStartDate, Is.EqualTo(warrantyStart));
        Assert.That(entry.WarrantyEndDate, Is.EqualTo(warrantyEnd));
    }

    // ─── 6. Record Delivery ─────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanRecordDelivery()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);
        DateTime deliveryDate = DateTime.UtcNow;

        await w.RecordProstheticsDeliveryAsync(id, deliveryDate, "SHIPPING", "1Z999AA10123456784");

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.DeliveryDate, Is.Not.Null);
        Assert.That(entry.DeliveryDate!.Value, Is.EqualTo(deliveryDate).Within(TimeSpan.FromSeconds(1)));
        Assert.That(entry.DeliveryMethod, Is.EqualTo("SHIPPING"));
        Assert.That(entry.TrackingNumber, Is.EqualTo("1Z999AA10123456784"));
    }

    // ─── 7. Record Fitting ──────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanRecordFitting()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);

        await w.RecordProstheticsFittingAsync(id, "Socket adjusted for comfort. Alignment checked.", "John Carter CPO");

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.FittingNotes, Is.EqualTo("Socket adjusted for comfort. Alignment checked."));
        Assert.That(entry.FittedByName, Is.EqualTo("John Carter CPO"));
        Assert.That(entry.FittingDate, Is.Not.Null);
    }

    // ─── 8. Record Patient Satisfaction ─────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanRecordPatientSatisfaction()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);

        await w.RecordProstheticsSatisfactionAsync(id, 4);

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.PatientSatisfaction, Is.EqualTo(4));
    }

    // ─── 9. Schedule Maintenance ────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanScheduleMaintenance()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);
        DateTime nextMaintenance = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        await w.ScheduleProstheticsMaintenanceAsync(id, nextMaintenance, "6-month routine check");

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.NextMaintenanceDate, Is.EqualTo(nextMaintenance));
        Assert.That(entry.MaintenanceNotes, Is.EqualTo("6-month routine check"));
    }

    // ─── 10. Add Maintenance Record ─────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanAddMaintenanceRecord()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);

        await w.AddProstheticsMaintenanceRecordAsync(id, "ROUTINE", "Tech Williams", "Inspected socket and alignment", null);

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.MaintenanceHistory, Has.Count.EqualTo(1));
        Assert.That(entry.MaintenanceHistory[0].MaintenanceType, Is.EqualTo("ROUTINE"));
        Assert.That(entry.MaintenanceHistory[0].TechnicianName, Is.EqualTo("Tech Williams"));
        Assert.That(entry.MaintenanceHistory[0].Notes, Is.EqualTo("Inspected socket and alignment"));
    }

    // ─── 11. Get Maintenance History ─────────────────────────────────────────

    [Test]
    public async Task Prosthetics_CanGetMaintenanceHistory()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);

        await w.AddProstheticsMaintenanceRecordAsync(id, "ROUTINE", "Tech A", "Initial inspection", null);
        await w.AddProstheticsMaintenanceRecordAsync(id, "ADJUSTMENT", "Tech B", "Socket tightened", null);
        await w.AddProstheticsMaintenanceRecordAsync(id, "REPAIR", "Tech A", "Replaced liner", null);

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.MaintenanceHistory, Has.Count.EqualTo(3));
        Assert.That(entry.MaintenanceHistory[0].MaintenanceType, Is.EqualTo("ROUTINE"));
        Assert.That(entry.MaintenanceHistory[1].MaintenanceType, Is.EqualTo("ADJUSTMENT"));
        Assert.That(entry.MaintenanceHistory[2].MaintenanceType, Is.EqualTo("REPAIR"));
    }

    // ─── 12. Maintenance With Cost ───────────────────────────────────────────

    [Test]
    public async Task Prosthetics_MaintenanceWithCost()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await IssueStandardItemAsync(w);

        await w.AddProstheticsMaintenanceRecordAsync(id, "REPAIR", "Tech Martinez", "Replaced knee joint mechanism", 1250.00m);

        ProstheticsEntry? entry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.MaintenanceHistory, Has.Count.EqualTo(1));
        Assert.That(entry.MaintenanceHistory[0].Cost, Is.EqualTo(1250.00m));
        Assert.That(entry.MaintenanceHistory[0].MaintenanceType, Is.EqualTo("REPAIR"));
        Assert.That(entry.MaintenanceHistory[0].TechnicianName, Is.EqualTo("Tech Martinez"));
    }

    // ─── 13. List Prosthetics ────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_ListReturnsAll()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await IssueStandardItemAsync(w, "BELOW KNEE PROSTHESIS, LEFT");
        await IssueStandardItemAsync(w, "ABOVE KNEE PROSTHESIS, RIGHT");

        List<ProstheticsSummary> list = await w.GetProstheticsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
    }

    // ─── 14. Patient Linkage ──────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_LinksToPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await IssueStandardItemAsync(w);

        List<ProstheticsEntry> entries = await GetPatient(patientId).GetProstheticsItemsAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ProstheticsId, Is.Not.Empty);
    }

    // ─── 15. Multiple Patients Independent ────────────────────────────────────

    [Test]
    public async Task Prosthetics_MultiplePatients_Independent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await IssueStandardItemAsync(w1);

        List<ProstheticsSummary> list2 = await w2.GetProstheticsAsync();
        Assert.That(list2, Is.Empty);
    }

    // ─── 16. Full Workflow ───────────────────────────────────────────────────

    [Test]
    public async Task Prosthetics_FullWorkflow()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Issue
        string id = await w.IssueProstheticAsync(
            "ABOVE KNEE PROSTHESIS, RIGHT", "L5301", "PROSTHETIC",
            DateTime.UtcNow, 1, 5200.00m,
            "PROV-001", "Dr. Rehab",
            "LOC-001", "Prosthetics Lab",
            true, null);

        ProstheticsEntry? s1 = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(s1!.Status, Is.EqualTo("ISSUED"));
        Assert.That(s1.ItemDescription, Is.EqualTo("ABOVE KNEE PROSTHESIS, RIGHT"));

        // Set HCPCS
        await w.SetProstheticsHcpcsCodeAsync(id, "L5312");
        ProstheticsEntry? s2 = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(s2!.HcpcsCode, Is.EqualTo("L5312"));

        // Record cost
        await w.RecordProstheticsCostAsync(id, 12000.00m, "Advanced Prosthetics Inc", "VEND-010");
        ProstheticsEntry? s3 = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(s3!.Cost, Is.EqualTo(12000.00m));

        // Set warranty
        DateTime wStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime wEnd = new DateTime(2029, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await w.SetProstheticsWarrantyAsync(id, wStart, wEnd);

        // Record delivery
        await w.RecordProstheticsDeliveryAsync(id, DateTime.UtcNow, "IN_PERSON", null);
        ProstheticsEntry? s4 = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(s4!.DeliveryMethod, Is.EqualTo("IN_PERSON"));

        // Record fitting
        await w.RecordProstheticsFittingAsync(id, "Perfect fit after socket adjustment", "Sarah Johnson CPO");
        ProstheticsEntry? s5 = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(s5!.FittedByName, Is.EqualTo("Sarah Johnson CPO"));

        // Record satisfaction
        await w.RecordProstheticsSatisfactionAsync(id, 5);

        // Schedule and record maintenance
        await w.ScheduleProstheticsMaintenanceAsync(id, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), "6-month check");
        await w.AddProstheticsMaintenanceRecordAsync(id, "ROUTINE", "Tech Davis", "Initial post-fitting check", 0.00m);

        // Final assertions
        ProstheticsEntry? finalEntry = await GetPatient(patientId).GetProstheticsItemAsync(id);
        Assert.That(finalEntry, Is.Not.Null);
        Assert.That(finalEntry!.Status, Is.EqualTo("ISSUED"));
        Assert.That(finalEntry.HcpcsCode, Is.EqualTo("L5312"));
        Assert.That(finalEntry.Cost, Is.EqualTo(12000.00m));
        Assert.That(finalEntry.WarrantyStartDate, Is.EqualTo(wStart));
        Assert.That(finalEntry.WarrantyEndDate, Is.EqualTo(wEnd));
        Assert.That(finalEntry.PatientSatisfaction, Is.EqualTo(5));
        Assert.That(finalEntry.MaintenanceHistory, Has.Count.EqualTo(1));
        Assert.That(finalEntry.FittingNotes, Does.Contain("socket adjustment"));
    }
}
