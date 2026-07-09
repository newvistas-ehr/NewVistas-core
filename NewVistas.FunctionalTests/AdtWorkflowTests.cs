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
/// Functional tests for VistA ADT — Admission / Discharge / Transfer.
/// File #405 (Patient Movement) against the unit-owns-beds model:
/// admissions require a configured, active IInpatientUnitGrain; the census is a
/// projection of unit state (GetUnitCensusAsync) and the unit directory is the
/// per-institution capacity rollup (GetUnitDirectoryAsync).
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

    private IInpatientUnitGrain Unit(string institutionId, string unitId)
        => _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    /// <summary>Configures a fresh, isolated unit with beds B1..Bn (no rooms).</summary>
    private async Task<(string Inst, string UnitId)> NewUnitAsync(int beds = 4, string name = "Test Ward")
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        string unitId = $"U-{Guid.NewGuid():N}";
        IInpatientUnitGrain unit = Unit(inst, unitId);
        await unit.ConfigureUnitAsync(name, "MEDICINE", "Internal Medicine");
        for (int i = 1; i <= beds; i++)
            await unit.AddBedAsync($"B{i}", null, BedType.Regular);
        return (inst, unitId);
    }

    // ─── Admission Tests ──────────────────────────────────────────────────

    [Test]
    public async Task RecordAdmission_ReturnsIdWithAdtPrefix()
    {
        var (inst, unitId) = await NewUnitAsync();
        IPatientWorkflowGrain w = NewWorkflow();

        string id = await w.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "B1",
            "Internal Medicine", "PROV-001", "Dr. Smith", "Pneumonia", null);

        Assert.That(id, Does.StartWith("ADT-"));
    }

    [Test]
    public async Task RecordAdmission_CreatesMovementWithAdmissionType()
    {
        var (inst, unitId) = await NewUnitAsync();
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "B1",
            "Internal Medicine", null, null, "CHF", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements, Has.Count.EqualTo(1));
        Assert.That(movements[0].MovementType, Is.EqualTo("ADMISSION"));
    }

    [Test]
    public async Task RecordAdmission_GetAdtMovements_ShowsAdmittedStatus()
    {
        var (inst, unitId) = await NewUnitAsync();
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "B2",
            "Cardiology", null, null, "Chest pain", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements[0].Status, Is.EqualTo("ADMITTED"));
    }

    [Test]
    public async Task RecordAdmission_AddsPatientToUnitCensus()
    {
        var (inst, unitId) = await NewUnitAsync();
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "B1",
            "Surgery", null, null, "Appendicitis", null);

        List<UnitCensusEntry> census = await w.GetUnitCensusAsync(inst, unitId);
        Assert.That(census.Any(e => e.PatientId == patientId), Is.True);
        Assert.That(census.First(e => e.PatientId == patientId).BedId, Is.EqualTo("B1"));
    }

    [Test]
    public async Task RecordAdmission_UnknownUnit_Throws()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        Assert.ThrowsAsync<InvalidOperationException>(() => w.RecordAdmissionAsync(
            DateTime.UtcNow, $"INST-{Guid.NewGuid():N}", $"U-{Guid.NewGuid():N}", null,
            "Medicine", null, null, "dx", null));
    }

    // ─── Discharge Tests ──────────────────────────────────────────────────

    [Test]
    public async Task RecordDischarge_SetsDischargedStatus()
    {
        var (inst, unitId) = await NewUnitAsync();
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-5), inst, unitId, "B1",
            "Internal Medicine", null, null, "COPD exacerbation", null);

        await w.RecordDischargeAsync(admitId, DateTime.UtcNow, "COPD, improved", "REGULAR", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements[0].Status, Is.EqualTo("DISCHARGED"));
    }

    [Test]
    public async Task RecordDischarge_CalculatesLengthOfStay()
    {
        var (inst, unitId) = await NewUnitAsync();
        IPatientWorkflowGrain w = NewWorkflow();
        DateTime admitDate = DateTime.UtcNow.AddDays(-7);
        // Boarder admission — the unit is required, a bed is not.
        string admitId = await w.RecordAdmissionAsync(
            admitDate, inst, unitId, null, null, null, null, "Elective surgery", null);

        await w.RecordDischargeAsync(admitId, DateTime.UtcNow, null, "REGULAR", null);

        IAdtGrain adtGrain = _cluster.GrainFactory.GetGrain<IAdtGrain>(admitId);
        AdtState state = await adtGrain.GetMovementAsync();
        Assert.That(state.LengthOfStay, Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    public async Task RecordDischarge_RemovesPatientFromUnitCensus_AndDirtiesBed()
    {
        var (inst, unitId) = await NewUnitAsync();
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "B1",
            "Critical Care", null, null, "Sepsis", null);

        // Verify patient is on census
        List<UnitCensusEntry> before = await w.GetUnitCensusAsync(inst, unitId);
        Assert.That(before.Any(e => e.PatientId == patientId), Is.True);

        await w.RecordDischargeAsync(admitId, DateTime.UtcNow.AddDays(3), "Sepsis, resolved", "REGULAR", null);

        List<UnitCensusEntry> after = await w.GetUnitCensusAsync(inst, unitId);
        Assert.That(after.Any(e => e.PatientId == patientId), Is.False);

        InpatientUnitState unitState = await Unit(inst, unitId).GetAsync();
        Assert.That(unitState.Beds.First(b => b.BedId == "B1").State,
            Is.EqualTo(BedLifecycleState.Dirty), "The vacated bed awaits EVS turnover.");
    }

    // ─── Transfer Tests ───────────────────────────────────────────────────

    [Test]
    public async Task RecordTransfer_ReturnsNewAdtId()
    {
        var (instA, unitA) = await NewUnitAsync();
        var (instB, unitB) = await NewUnitAsync(name: "ICU");
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), instA, unitA, "B1",
            "Internal Medicine", null, null, "Pneumonia", null);

        string transferId = await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            instB, unitB, "B1",
            null, "Critical Care", null, "Dr. Jones", "Deteriorating");

        Assert.That(transferId, Does.StartWith("ADT-"));
        Assert.That(transferId, Is.Not.EqualTo(admitId));
    }

    [Test]
    public async Task RecordTransfer_CreatesNewMovementRecord_OriginalRemainsInList()
    {
        var (instA, unitA) = await NewUnitAsync();
        var (instB, unitB) = await NewUnitAsync(name: "Surgery Ward");
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-2), instA, unitA, "B1",
            "Medicine", null, null, "UTI", null);

        await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            instB, unitB, "B2",
            null, "Surgery", null, null, null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements, Has.Count.EqualTo(2));
        Assert.That(movements.Any(m => m.MovementId == admitId), Is.True);
    }

    [Test]
    public async Task RecordTransfer_NewMovementShowsTransferredType()
    {
        var (instA, unitA) = await NewUnitAsync();
        var (instB, unitB) = await NewUnitAsync(name: "Ortho Ward");
        IPatientWorkflowGrain w = NewWorkflow();
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), instA, unitA, "B1",
            "Internal Medicine", null, null, "Fall", null);

        string transferId = await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            instB, unitB, "B1",
            null, "Orthopedics", null, null, null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        AdtSummary? transferSummary = movements.FirstOrDefault(m => m.MovementId == transferId);
        Assert.That(transferSummary, Is.Not.Null);
        Assert.That(transferSummary!.MovementType, Is.EqualTo("TRANSFER"));
        Assert.That(transferSummary.Status, Is.EqualTo("TRANSFERRED"));
    }

    [Test]
    public async Task RecordTransfer_MovesPatientBetweenUnitCensuses()
    {
        var (instA, unitA) = await NewUnitAsync();
        var (instB, unitB) = await NewUnitAsync(name: "ICU");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-1), instA, unitA, "B1",
            "Internal Medicine", null, null, "Observation", null);

        await w.RecordTransferAsync(
            admitId, DateTime.UtcNow,
            instB, unitB, "B1",
            null, "Critical Care", null, null, null);

        List<UnitCensusEntry> source = await w.GetUnitCensusAsync(instA, unitA);
        List<UnitCensusEntry> destination = await w.GetUnitCensusAsync(instB, unitB);

        Assert.That(source.Any(e => e.PatientId == patientId), Is.False, "Patient should be removed from source unit");
        Assert.That(destination.Any(e => e.PatientId == patientId), Is.True, "Patient should be on destination unit");
    }

    // ─── Unit Directory Tests ─────────────────────────────────────────────

    [Test]
    public async Task UnitDirectory_ListsConfiguredUnitsWithCapacity()
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        string medId = $"U-{Guid.NewGuid():N}";
        string icuId = $"U-{Guid.NewGuid():N}";
        IInpatientUnitGrain med = Unit(inst, medId);
        IInpatientUnitGrain icu = Unit(inst, icuId);
        await med.ConfigureUnitAsync("Medicine 3A", "MEDICINE", null);
        await icu.ConfigureUnitAsync("Intensive Care Unit", "ICU", null);
        await med.AddBedAsync("B1", null, BedType.Regular);
        await med.AddBedAsync("B2", null, BedType.Regular);
        await icu.AddBedAsync("I1", null, BedType.Icu);

        IPatientWorkflowGrain w = NewWorkflow();
        List<UnitCapacitySummary> directory = await w.GetUnitDirectoryAsync(inst);

        Assert.That(directory, Has.Count.EqualTo(2));
        UnitCapacitySummary medEntry = directory.Single(u => u.UnitId == medId);
        UnitCapacitySummary icuEntry = directory.Single(u => u.UnitId == icuId);
        Assert.That(medEntry.Name, Is.EqualTo("Medicine 3A"));
        Assert.That(medEntry.TotalBeds, Is.EqualTo(2));
        Assert.That(medEntry.Available, Is.EqualTo(2));
        Assert.That(icuEntry.UnitType, Is.EqualTo("ICU"));
        Assert.That(icuEntry.TotalBeds, Is.EqualTo(1));
    }

    [Test]
    public async Task UnitDirectory_ReflectsOccupancyAfterAdmission()
    {
        var (inst, unitId) = await NewUnitAsync(beds: 3);
        IPatientWorkflowGrain w = NewWorkflow();
        await w.RecordAdmissionAsync(DateTime.UtcNow, inst, unitId, "B1",
            "Medicine", null, null, "Obs", null);

        List<UnitCapacitySummary> directory = await w.GetUnitDirectoryAsync(inst);
        UnitCapacitySummary entry = directory.Single(u => u.UnitId == unitId);
        Assert.That(entry.Occupied, Is.EqualTo(1));
        Assert.That(entry.Available, Is.EqualTo(2));
    }

    // ─── Sorting + Multi-admission Tests ─────────────────────────────────

    [Test]
    public async Task GetAdtMovements_SortedDescendingByMovementDate()
    {
        var (instA, unitA) = await NewUnitAsync(name: "Ward A");
        var (instB, unitB) = await NewUnitAsync(name: "Ward B");
        var (instC, unitC) = await NewUnitAsync(name: "Ward C");
        IPatientWorkflowGrain w = NewWorkflow();
        await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-10), instA, unitA, null,
            "Medicine", null, null, "Episode 1", null);
        await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-5), instB, unitB, null,
            "Medicine", null, null, "Episode 2", null);
        await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-1), instC, unitC, null,
            "Medicine", null, null, "Episode 3", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();

        Assert.That(movements, Has.Count.EqualTo(3));
        Assert.That(movements[0].MovementDateTime, Is.GreaterThan(movements[1].MovementDateTime));
        Assert.That(movements[1].MovementDateTime, Is.GreaterThan(movements[2].MovementDateTime));
    }

    [Test]
    public async Task MultipleAdmissions_AllAppearInMovementList()
    {
        var (instA, unitA) = await NewUnitAsync(name: "Ward A");
        var (instB, unitB) = await NewUnitAsync(name: "Ward B");
        IPatientWorkflowGrain w = NewWorkflow();

        string id1 = await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-30),
            instA, unitA, null, "Medicine", null, null, "First admission", null);
        string id2 = await w.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-15),
            instB, unitB, null, "Cardiology", null, null, "Second admission", null);

        List<AdtSummary> movements = await w.GetAdtMovementsAsync();

        Assert.That(movements, Has.Count.EqualTo(2));
        Assert.That(movements.Any(m => m.MovementId == id1), Is.True);
        Assert.That(movements.Any(m => m.MovementId == id2), Is.True);
    }

    // ─── Full Workflow Test ───────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_AdmitTransferDischarge_CensusCorrectAtEachStep()
    {
        var (instMed, unitMed) = await NewUnitAsync(name: "Medical Ward 3A");
        var (instIcu, unitIcu) = await NewUnitAsync(name: "Intensive Care Unit");
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        IPatientGrain patient = GetPatient(patientId);
        await patient.UpdateDemographicsAsync("Jones, Robert", "M", new DateTime(1960, 5, 15), null);

        // Step 1: Admit to Medical Ward 3A
        string admitId = await w.RecordAdmissionAsync(
            DateTime.UtcNow.AddDays(-5), instMed, unitMed, "B1",
            "Internal Medicine", "PROV-001", "Dr. Adams", "Community pneumonia", null);

        List<UnitCensusEntry> censusAfterAdmit = await w.GetUnitCensusAsync(instMed, unitMed);
        Assert.That(censusAfterAdmit.Any(e => e.PatientId == patientId), Is.True, "Patient should be on Med 3A after admission");
        Assert.That(censusAfterAdmit.First(e => e.PatientId == patientId).PatientName, Is.EqualTo("Jones, Robert"));

        // Step 2: Transfer to ICU
        await w.RecordTransferAsync(
            admitId, DateTime.UtcNow.AddDays(-3),
            instIcu, unitIcu, "B1",
            null, "Critical Care", "PROV-002", "Dr. Baker", "Worsening resp status");

        List<UnitCensusEntry> medAfterTransfer = await w.GetUnitCensusAsync(instMed, unitMed);
        List<UnitCensusEntry> icuAfterTransfer = await w.GetUnitCensusAsync(instIcu, unitIcu);
        Assert.That(medAfterTransfer.Any(e => e.PatientId == patientId), Is.False, "Patient should NOT be on Med 3A after transfer");
        Assert.That(icuAfterTransfer.Any(e => e.PatientId == patientId), Is.True, "Patient should be on ICU after transfer");

        // The vacated Med 3A bed awaits EVS turnover.
        Assert.That((await Unit(instMed, unitMed).GetAsync()).Beds.First(b => b.BedId == "B1").State,
            Is.EqualTo(BedLifecycleState.Dirty));

        // Movements list should show 2 records
        List<AdtSummary> movements = await w.GetAdtMovementsAsync();
        Assert.That(movements, Has.Count.EqualTo(2));
        Assert.That(movements[0].MovementType, Is.EqualTo("TRANSFER")); // most recent first
        Assert.That(movements[1].MovementType, Is.EqualTo("ADMISSION"));

        // Step 3: Discharge from ICU (use transfer movement ID)
        string transferId = movements[0].MovementId;
        await w.RecordDischargeAsync(transferId, DateTime.UtcNow, "Pneumonia resolved", "REGULAR", null);

        List<UnitCensusEntry> icuAfterDischarge = await w.GetUnitCensusAsync(instIcu, unitIcu);
        Assert.That(icuAfterDischarge.Any(e => e.PatientId == patientId), Is.False, "Patient should NOT be on ICU after discharge");

        movements = await w.GetAdtMovementsAsync();
        Assert.That(movements[0].Status, Is.EqualTo("DISCHARGED"));
    }
}
