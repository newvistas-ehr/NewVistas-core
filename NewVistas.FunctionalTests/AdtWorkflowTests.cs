// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA ADT — Admission / Discharge / Transfer.
/// File #405 (Patient Movement), File #42 (Ward Location), Ward Census.
/// </summary>
[TestFixture]
public class AdtWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private IWardLocationIndexGrain GetWardIndex()
        => _cluster.GrainFactory.GetGrain<IWardLocationIndexGrain>("WARD-LOCATION-INDEX");

    private IWardCensusGrain GetCensus(string wardId)
        => _cluster.GrainFactory.GetGrain<IWardCensusGrain>($"WARD-CENSUS:{wardId}");

    // ─── Admission Tests ──────────────────────────────────────────────────

    [Test]
    public async Task RecordAdmission_ReturnsIdWithAdtPrefix()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.RecordAdmissionAsync(
            DateTime.UtcNow, "WARD-MED-3A", "Medical Ward 3A", "301-A",
            "Internal Medicine", "PROV-001", "Dr. Smith", "Pneumonia", null);

        Assert.That(id, Does.StartWith("ADT-"));
    }

    [Test]
    public async Task RecordAdmission_CreatesMovementWithAdmissionType()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAdmissionAsync(
            DateTime.UtcNow, "WARD-MED-3A", "Medical Ward 3A", "301-A",
            "Internal Medicine", null, null, "CHF", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements, Has.Count.EqualTo(1));
        Assert.That(movements[0].MovementType, Is.EqualTo("ADMISSION"));
    }

    [Test]
    public async Task RecordAdmission_GetAdtMovements_ShowsAdmittedStatus()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAdmissionAsync(
            DateTime.UtcNow, "WARD-MED-4B", "Medical Ward 4B", "402-B",
            "Cardiology", null, null, "Chest pain", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements[0].Status, Is.EqualTo("ADMITTED"));
    }

    [Test]
    public async Task RecordAdmission_AddsPatientToWardCensus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAdmissionAsync(
            DateTime.UtcNow, "WARD-SURG-2C", "Surgery Ward 2C", "215-A",
            "Surgery", null, null, "Appendicitis", null);

        List<WardCensusEntry> census = await GetCensus("WARD-SURG-2C").GetCensusAsync();
        Assert.That(census.Any(e => e.PatientId == patientId), Is.True);
    }

    // ─── Discharge Tests ──────────────────────────────────────────────────

    [Test]
    public async Task RecordDischarge_SetsDischargedStatus()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-5), "WARD-MED-3A", "Medical Ward 3A", "302-A",
            "Internal Medicine", null, null, "COPD exacerbation", null);

        await w.RecordDischargeAsync(admitId, DateTime.UtcNow, "COPD, improved", "REGULAR", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements[0].Status, Is.EqualTo("DISCHARGED"));
    }

    [Test]
    public async Task RecordDischarge_CalculatesLengthOfStay()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        DateTime admitDate = DateTime.UtcNow.AddDays(-7);
        string admitId = await w.RecordAdmissionAsync(
            admitDate, null, null, null, null, null, null, "Elective surgery", null);

        await w.RecordDischargeAsync(admitId, DateTime.UtcNow, null, "REGULAR", null);

        IAdtGrain adtGrain = _cluster.GrainFactory.GetGrain<IAdtGrain>(admitId);
        AdtState state = await adtGrain.GetMovementAsync();
        Assert.That(state.LengthOfStay, Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    public async Task RecordDischarge_RemovesPatientFromWardCensus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow, "WARD-ICU-1", "Intensive Care Unit", "ICU-3",
            "Critical Care", null, null, "Sepsis", null);

        // Verify patient is on census
        List<WardCensusEntry> before = await GetCensus("WARD-ICU-1").GetCensusAsync();
        Assert.That(before.Any(e => e.PatientId == patientId), Is.True);

        await w.RecordDischargeAsync(admitId, DateTime.UtcNow.AddDays(3), "Sepsis, resolved", "REGULAR", null);

        List<WardCensusEntry> after = await GetCensus("WARD-ICU-1").GetCensusAsync();
        Assert.That(after.Any(e => e.PatientId == patientId), Is.False);
    }

    // ─── Transfer Tests ───────────────────────────────────────────────────

    [Test]
    public async Task RecordTransfer_ReturnsNewAdtId()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), "WARD-MED-3A", "Medical Ward 3A", "305-A",
            "Internal Medicine", null, null, "Pneumonia", null);

        string transferId = await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            "WARD-ICU-1", "Intensive Care Unit", "ICU-2",
            null, "Critical Care", null, "Dr. Jones", "Deteriorating");

        Assert.That(transferId, Does.StartWith("ADT-"));
        Assert.That(transferId, Is.Not.EqualTo(admitId));
    }

    [Test]
    public async Task RecordTransfer_CreatesNewMovementRecord_OriginalRemainsInList()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-2), "WARD-MED-4B", "Medical Ward 4B", "410-A",
            "Medicine", null, null, "UTI", null);

        await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            "WARD-SURG-2C", "Surgery Ward 2C", "220-B",
            null, "Surgery", null, null, null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements, Has.Count.EqualTo(2));
        Assert.That(movements.Any(m => m.MovementId == admitId), Is.True);
    }

    [Test]
    public async Task RecordTransfer_NewMovementShowsTransferredType()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), "WARD-MED-3A", "Medical Ward 3A", "308-A",
            "Internal Medicine", null, null, "Fall", null);

        string transferId = await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            "WARD-SURG-2C", "Surgery Ward 2C", "225-A",
            null, "Orthopedics", null, null, null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        AdtSummary? transferSummary = movements.FirstOrDefault(m => m.MovementId == transferId);
        Assert.That(transferSummary, Is.Not.Null);
        Assert.That(transferSummary!.MovementType, Is.EqualTo("TRANSFER"));
        Assert.That(transferSummary.Status, Is.EqualTo("TRANSFERRED"));
    }

    [Test]
    public async Task RecordTransfer_MovesPatientBetweenWardCensuses()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), "WARD-MED-3A", "Medical Ward 3A", "310-A",
            "Internal Medicine", null, null, "Observation", null);

        await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            "WARD-ICU-1", "Intensive Care Unit", "ICU-4",
            null, "Critical Care", null, null, null);

        List<WardCensusEntry> med3a = await GetCensus("WARD-MED-3A").GetCensusAsync();
        List<WardCensusEntry> icu = await GetCensus("WARD-ICU-1").GetCensusAsync();

        Assert.That(med3a.Any(e => e.PatientId == patientId), Is.False, "Patient should be removed from source ward");
        Assert.That(icu.Any(e => e.PatientId == patientId), Is.True, "Patient should be on destination ward");
    }

    // ─── Ward Location Index Tests ────────────────────────────────────────

    [Test]
    public async Task WardLocationIndex_GetAllWards_ReturnsSeedData()
    {
        IWardLocationIndexGrain idx = GetWardIndex();

        List<WardLocationEntry> wards = await idx.GetAllWardsAsync();

        Assert.That(wards, Has.Count.GreaterThanOrEqualTo(6));
        Assert.That(wards.Any(w => w.WardId == "WARD-ICU-1"), Is.True);
        Assert.That(wards.Any(w => w.WardType == "MEDICINE"), Is.True);
    }

    [Test]
    public async Task WardLocationIndex_SearchByName_Filters()
    {
        IWardLocationIndexGrain idx = GetWardIndex();

        List<WardLocationEntry> results = await idx.SearchWardsAsync("ICU");

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.All(w => w.WardType.Contains("ICU") || w.Name.Contains("ICU")), Is.True);
    }

    // ─── Sorting + Multi-admission Tests ─────────────────────────────────

    [Test]
    public async Task GetAdtMovements_SortedDescendingByMovementDate()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-10), null, "Ward A", null,
            "Medicine", null, null, "Episode 1", null);
        await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-5), null, "Ward B", null,
            "Medicine", null, null, "Episode 2", null);
        await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-1), null, "Ward C", null,
            "Medicine", null, null, "Episode 3", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();

        Assert.That(movements, Has.Count.EqualTo(3));
        Assert.That(movements[0].MovementDateTime, Is.GreaterThan(movements[1].MovementDateTime));
        Assert.That(movements[1].MovementDateTime, Is.GreaterThan(movements[2].MovementDateTime));
    }

    [Test]
    public async Task MultipleAdmissions_AllAppearInMovementList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string id1 = await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-30),
            null, "Ward A", null, "Medicine", null, null, "First admission", null);
        string id2 = await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-15),
            null, "Ward B", null, "Cardiology", null, null, "Second admission", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();

        Assert.That(movements, Has.Count.EqualTo(2));
        Assert.That(movements.Any(m => m.MovementId == id1), Is.True);
        Assert.That(movements.Any(m => m.MovementId == id2), Is.True);
    }

    // ─── Full Workflow Test ───────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_AdmitTransferDischarge_CensusCorrectAtEachStep()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("Jones, Robert", "M", new DateTime(1960, 5, 15), null);

        // Step 1: Admit to Medical Ward 3A
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-5), "WARD-MED-3A", "Medical Ward 3A", "305-B",
            "Internal Medicine", "PROV-001", "Dr. Adams", "Community pneumonia", null);

        List<WardCensusEntry> censusAfterAdmit = await GetCensus("WARD-MED-3A").GetCensusAsync();
        Assert.That(censusAfterAdmit.Any(e => e.PatientId == patientId), Is.True, "Patient should be on Med 3A after admission");
        Assert.That(censusAfterAdmit.First(e => e.PatientId == patientId).PatientName, Is.EqualTo("Jones, Robert"));

        // Step 2: Transfer to ICU
        await w.RecordTransferAsync(
            admitId, DateTime.UtcNow.AddDays(-3),
            "WARD-ICU-1", "Intensive Care Unit", "ICU-1",
            null, "Critical Care", "PROV-002", "Dr. Baker", "Worsening resp status");

        List<WardCensusEntry> med3aAfterTransfer = await GetCensus("WARD-MED-3A").GetCensusAsync();
        List<WardCensusEntry> icuAfterTransfer = await GetCensus("WARD-ICU-1").GetCensusAsync();
        Assert.That(med3aAfterTransfer.Any(e => e.PatientId == patientId), Is.False, "Patient should NOT be on Med 3A after transfer");
        Assert.That(icuAfterTransfer.Any(e => e.PatientId == patientId), Is.True, "Patient should be on ICU after transfer");

        // Movements list should show 2 records
        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements, Has.Count.EqualTo(2));
        Assert.That(movements[0].MovementType, Is.EqualTo("TRANSFER")); // most recent first
        Assert.That(movements[1].MovementType, Is.EqualTo("ADMISSION"));

        // Step 3: Discharge from ICU (use transfer movement ID)
        string transferId = movements[0].MovementId;
        await w.RecordDischargeAsync(transferId, DateTime.UtcNow, "Pneumonia resolved", "REGULAR", null);

        List<WardCensusEntry> icuAfterDischarge = await GetCensus("WARD-ICU-1").GetCensusAsync();
        Assert.That(icuAfterDischarge.Any(e => e.PatientId == patientId), Is.False, "Patient should NOT be on ICU after discharge");

        movements = await w.GetAdtMovementsAsync();
        Assert.That(movements[0].Status, Is.EqualTo("DISCHARGED"));
    }
}
