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
/// End-to-end inpatient stay scenario. The per-grain inpatient tests
/// (<see cref="AdtWorkflowTests"/>, <see cref="BcmaWorkflowTests"/>,
/// <see cref="BedManagementWorkflowTests"/>, <see cref="InpatientPharmacyWorkflowTests"/>,
/// <see cref="IVPharmacyWorkflowTests"/>, <see cref="WardStockWorkflowTests"/>)
/// each cover their own grain in isolation; this fixture runs a single
/// patient through the whole stay to confirm the grains compose end-to-end.
///
/// This is the "validation, not new build" round from the tribal-deployment
/// plan: if it passes, the existing inpatient surface works as a coherent
/// flow under the test cluster's site profile (which is `SharedCluster`,
/// matching the production cluster setup minus federation transport).
///
/// Stay narrative:
///   1. Patient arrives at the ED, gets admitted to Medical Ward 3A bed 301-A —
///      the admission itself occupies the bed (the unit owns bed truth)
///   2. Unit state confirms 301-A occupied; the unit census picks it up
///   3. Provider places an inpatient med order; pharmacy verifies; MAR syncs
///   4. Nurse administers the first scheduled dose via BCMA
///   5. Provider also orders an IV antibiotic; pharmacy compounds + dispenses
///   6. Patient transferred to Medical Ward 4B bed 401-B (old bed → Dirty)
///   7. Discharge after 5 days (bed → Dirty, census empties)
///   8. Verify the audit trail reflects the full journey
/// </summary>
[TestFixture]
public class InpatientStayEndToEndTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IInpatientUnitGrain Unit(string institutionId, string unitId) =>
        _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    /// <summary>Configures a fresh, isolated unit with the given beds (no rooms).</summary>
    private async Task<(string Inst, string UnitId)> NewUnitAsync(string name, params string[] bedIds)
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        string unitId = $"U-{Guid.NewGuid():N}";
        IInpatientUnitGrain unit = Unit(inst, unitId);
        await unit.ConfigureUnitAsync(name, "MedSurg", "Internal Medicine");
        foreach (string bedId in bedIds)
            await unit.AddBedAsync(bedId, null, BedType.Regular);
        return (inst, unitId);
    }

    private async Task<InpatientBed> BedAsync(string inst, string unitId, string bedId)
    {
        InpatientUnitState state = await Unit(inst, unitId).GetAsync();
        return state.Beds.First(b => b.BedId == bedId);
    }

    [Test]
    public async Task FullStay_AdmitOrderAdministerTransferDischarge_AllStepsSucceed()
    {
        var (inst3A, unitMed3A) = await NewUnitAsync("Medical Ward 3A", "301-A", "301-B");
        var (inst4B, unitMed4B) = await NewUnitAsync("Medical Ward 4B", "401-B");

        string patientId = $"INPATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        await wf.UpdateDemographicsAsync("TESTPATIENT,STAY", "M", new DateTime(1962, 4, 2), null);

        // ── Step 1: Admission (occupies the bed in the same call) ─────
        DateTime admitDate = DateTime.UtcNow.AddDays(-5);
        string admitMovementId = await wf.RecordAdmissionAsync(
            movementDateTime: admitDate,
            institutionId: inst3A,
            unitId: unitMed3A,
            bedId: "301-A",
            treatingSpecialtyName: "Internal Medicine",
            attendingPhysicianId: "DOCTOR1",
            attendingPhysicianName: "SMITH,JOHN A",
            admissionDiagnosis: "Pneumonia, community-acquired",
            comments: "Patient presented to ED with fever, productive cough, hypoxia.");
        Assert.That(admitMovementId, Does.StartWith("ADT-"),
            "Admission must return an ADT-prefixed movement id.");

        // ── Step 2: Bed truth + unit census (no explicit board sync) ──
        InpatientBed bed = await BedAsync(inst3A, unitMed3A, "301-A");
        Assert.That(bed.State, Is.EqualTo(BedLifecycleState.Occupied),
            "The admission itself must occupy the bed — the old bed-grain + board-sync gap is gone.");
        Assert.That(bed.PatientId, Is.EqualTo(patientId));

        List<UnitCensusEntry> census = await wf.GetUnitCensusAsync(inst3A, unitMed3A);
        Assert.That(census.Any(e => e.PatientId == patientId && e.BedId == "301-A"), Is.True,
            "Unit census is a projection of bed state and must show the admitted patient.");

        // ── Step 3: Inpatient med order + verification + MAR sync ─────
        // Use the lower-level grain directly since we don't need the full PSO
        // pharmacy verification dance for this validation.
        string orderId = $"PSJ-ORDER-{Guid.NewGuid():N}";
        IInpatientOrderGrain orderGrain = _cluster.GrainFactory.GetGrain<IInpatientOrderGrain>(orderId);
        await orderGrain.CreateOrderAsync(
            patientId: patientId,
            wardId: "WARD-MED-3A", wardName: "Medical Ward 3A", roomBed: "301-A",
            orderType: "UNIT_DOSE",
            drugName: "AZITHROMYCIN", drugId: $"DRUG-{Guid.NewGuid():N}",
            dosage: "500", doseUnit: "mg", route: "PO", schedule: "QD",
            priority: "ROUTINE",
            startDate: admitDate, stopDate: admitDate.AddDays(5),
            durationDays: 5, quantityPerDose: 1,
            providerId: "DOCTOR1", providerName: "SMITH,JOHN A",
            comments: "Community-acquired pneumonia",
            ivSolution: null, ivVolumeMl: null, infusionRateStr: null);
        await orderGrain.VerifyAsync("PHARM1", "WILLIAMS,ROBERT L");
        await wf.SyncOrderToMARAsync(orderId);

        List<MarEntry> mar = await wf.GetPatientMARAsync();
        Assert.That(mar.Any(e => e.OrderId == orderId), Is.True,
            "MAR should reflect the verified inpatient order after SyncOrderToMARAsync.");

        // ── Step 4: BCMA administration ───────────────────────────────
        string bcmaId = await wf.AdministerMedicationAsync(
            orderId: orderId,
            actionStatus: "GIVEN",
            administrationDateTime: admitDate.AddHours(2),
            administeredById: "NURSE1",
            administeredByName: "JOHNSON,MARY R",
            injectionSite: null,
            prnReason: null,
            comments: null);
        Assert.That(bcmaId, Is.Not.Null.And.Not.Empty);

        List<BcmaSummary> bcmaHistory = await wf.GetMedicationAdministrationsAsync(50);
        Assert.That(bcmaHistory.Any(b => b.BcmaId == bcmaId),
            Is.True, "BCMA history should record the administration just performed.");

        // ── Step 5: IV admixture order + compounding + dispensing ─────
        string ivOrderId = await wf.CreateIVAdmixOrderAsync(
            baseSolution: "0.9% Sodium Chloride",
            baseSolutionVolumeMl: 250,
            route: IVAdmixRoute.Peripheral,
            frequency: IVAdmixFrequency.Q12H,
            containerType: IVContainerType.Bag,
            containerCount: 1,
            priority: IVAdmixPriority.Routine,
            linkedInpatientOrderId: null,
            infusionRateStr: "100 mL/hr",
            infusionRateMlHr: 100m,
            infusionDurationHours: 2.5m,
            routeDescription: null,
            frequencyDescription: null,
            startDateTime: admitDate.AddHours(1),
            stopDateTime: admitDate.AddDays(5),
            providerId: "DOCTOR1",
            providerName: "SMITH,JOHN A",
            notes: "Pneumonia — IV antibiotic");

        await wf.AddIVAdmixAdditiveAsync(ivOrderId, new IVAdmixAdditive
        {
            DrugName = "CEFTRIAXONE",
            Dose = "1",
            DoseUnit = "g",
            DrugId = $"DRUG-{Guid.NewGuid():N}",
        });
        await wf.VerifyIVAdmixOrderAsync(ivOrderId, "PHARM1", "WILLIAMS,ROBERT L", DateTime.UtcNow);
        await wf.StartIVAdmixCompoundingAsync(ivOrderId, "PHARM1", "WILLIAMS,ROBERT L", DateTime.UtcNow);
        await wf.CompleteIVAdmixCompoundingAsync(ivOrderId, DateTime.UtcNow,
            lotNumber: "LOT-2026-001", expirationDate: DateTime.UtcNow.AddHours(24));
        await wf.DispenseIVAdmixOrderAsync(ivOrderId, DateTime.UtcNow);

        IVAdmixOrderState ivState = await wf.GetIVAdmixOrderAsync(ivOrderId);
        Assert.That(ivState.Status, Is.EqualTo(IVAdmixOrderStatus.Dispensed));
        Assert.That(ivState.LotNumber, Is.EqualTo("LOT-2026-001"));

        // ── Step 6: Transfer to a different ward ──────────────────────
        // One workflow call: occupies 401-B on Ward 4B and releases 301-A
        // (→ Dirty for EVS turnover) — no manual bed choreography.
        DateTime transferDate = admitDate.AddDays(2);
        string transferMovementId = await wf.RecordTransferAsync(
            currentMovementId: admitMovementId,
            transferDateTime: transferDate,
            toInstitutionId: inst4B,
            toUnitId: unitMed4B,
            toBedId: "401-B",
            toSpecialtyId: null,
            toSpecialtyName: "Internal Medicine",
            attendingPhysicianId: "DOCTOR1",
            attendingPhysicianName: "SMITH,JOHN A",
            comments: "Step-down to telemetry");
        Assert.That(transferMovementId, Does.StartWith("ADT-"));

        InpatientBed oldBed = await BedAsync(inst3A, unitMed3A, "301-A");
        InpatientBed newBed = await BedAsync(inst4B, unitMed4B, "401-B");
        Assert.That(oldBed.State, Is.EqualTo(BedLifecycleState.Dirty),
            "Original bed should go to Dirty (EVS turnover) after transfer.");
        Assert.That(oldBed.PatientId, Is.Null,
            "Original bed should be released after transfer.");
        Assert.That(newBed.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(newBed.PatientId, Is.EqualTo(patientId),
            "New bed should be occupied by the patient after transfer.");

        // ── Step 7: Discharge ─────────────────────────────────────────
        DateTime dischargeDate = admitDate.AddDays(5);
        await wf.RecordDischargeAsync(
            movementId: transferMovementId,
            dischargeDateTime: dischargeDate,
            dischargeDiagnosis: "Pneumonia, community-acquired — resolved",
            disposition: "HOME",
            comments: "Discharged home with 7-day course of azithromycin.");

        InpatientBed dischargedBed = await BedAsync(inst4B, unitMed4B, "401-B");
        Assert.That(dischargedBed.State, Is.EqualTo(BedLifecycleState.Dirty),
            "Discharge releases the bed to Dirty.");
        Assert.That(dischargedBed.PatientId, Is.Null);

        List<UnitCensusEntry> censusAfterDischarge = await wf.GetUnitCensusAsync(inst4B, unitMed4B);
        Assert.That(censusAfterDischarge.Any(e => e.PatientId == patientId), Is.False,
            "Census must be empty of the patient after discharge.");

        // ── Step 8: Audit-trail verification ──────────────────────────
        // Note: RecordDischargeAsync mutates the existing transfer movement
        // (sets MovementType=DISCHARGE on it) rather than appending a new
        // movement; admission + transfer-now-discharge = 2 entries.
        List<AdtSummary> movements = await wf.GetAdtMovementsAsync();
        Assert.That(movements.Count, Is.GreaterThanOrEqualTo(2),
            "ADT history should contain admission and transfer (latter mutates to DISCHARGE).");
        Assert.That(movements.Any(m => m.MovementId == admitMovementId), Is.True);
        AdtSummary? dischargeRow = movements.FirstOrDefault(m => m.MovementId == transferMovementId);
        Assert.That(dischargeRow, Is.Not.Null,
            "Transfer movement should be present (now reflecting discharge).");
        Assert.That(dischargeRow!.MovementType, Is.EqualTo("DISCHARGE"),
            "Transfer movement should have been mutated to DISCHARGE on RecordDischargeAsync.");
    }

    [Test]
    public async Task BcmaAndMar_AfterOrderSyncAndAdministration_AreConsistent()
    {
        string patientId = $"INPATIENT-MAR-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        var (inst, unitId) = await NewUnitAsync("Medical Ward 3A", "302-A");
        await wf.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "302-A",
            "Internal Medicine", null, null, null, null);

        string orderId = $"PSJ-ORDER-{Guid.NewGuid():N}";
        IInpatientOrderGrain orderGrain = _cluster.GrainFactory.GetGrain<IInpatientOrderGrain>(orderId);
        await orderGrain.CreateOrderAsync(
            patientId: patientId,
            wardId: "WARD-MED-3A", wardName: "Medical Ward 3A", roomBed: "302-A",
            orderType: "UNIT_DOSE",
            drugName: "ENOXAPARIN", drugId: $"DRUG-{Guid.NewGuid():N}",
            dosage: "40", doseUnit: "mg", route: "SC", schedule: "QD",
            priority: "ROUTINE",
            startDate: DateTime.UtcNow, stopDate: DateTime.UtcNow.AddDays(7),
            durationDays: 7, quantityPerDose: 1,
            providerId: "DOCTOR1", providerName: "SMITH,JOHN A",
            comments: "DVT prophylaxis",
            ivSolution: null, ivVolumeMl: null, infusionRateStr: null);
        await orderGrain.VerifyAsync("PHARM1", "WILLIAMS,ROBERT L");

        // MAR should sync first, BCMA admin should link cleanly.
        await wf.SyncOrderToMARAsync(orderId);
        string bcmaId = await wf.AdministerMedicationAsync(
            orderId: orderId, actionStatus: "GIVEN",
            administrationDateTime: DateTime.UtcNow,
            administeredById: "NURSE1", administeredByName: "JOHNSON,MARY R",
            injectionSite: "Left abdomen", prnReason: null, comments: null);

        // Both views observe the administration.
        List<MarEntry> mar = await wf.GetPatientMARAsync();
        List<BcmaSummary> bcma = await wf.GetMedicationAdministrationsAsync(50);
        Assert.That(mar.Any(e => e.OrderId == orderId), Is.True);
        Assert.That(bcma.Any(b => b.BcmaId == bcmaId), Is.True);
    }

    [Test]
    public async Task IvAdmixLifecycle_FullFlow_FromOrderToAdministration_TransitionsStatuses()
    {
        string patientId = $"INPATIENT-IV-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        var (inst, unitId) = await NewUnitAsync("Medical Ward 3A", "303-A");
        await wf.RecordAdmissionAsync(
            DateTime.UtcNow, inst, unitId, "303-A",
            "Internal Medicine", null, null, null, null);

        string orderId = await wf.CreateIVAdmixOrderAsync(
            baseSolution: "D5W",
            baseSolutionVolumeMl: 100,
            route: IVAdmixRoute.Peripheral,
            frequency: IVAdmixFrequency.Q8H,
            containerType: IVContainerType.Bag,
            containerCount: 1,
            priority: IVAdmixPriority.STAT,
            linkedInpatientOrderId: null,
            infusionRateStr: "50 mL/hr",
            infusionRateMlHr: 50m,
            infusionDurationHours: 2m,
            routeDescription: null,
            frequencyDescription: null,
            startDateTime: DateTime.UtcNow,
            stopDateTime: DateTime.UtcNow.AddHours(2),
            providerId: "DOCTOR1",
            providerName: "SMITH,JOHN A",
            notes: "Hydration + IV antibiotic");

        // Walk every state transition.
        IVAdmixOrderState created = await wf.GetIVAdmixOrderAsync(orderId);
        Assert.That(created.Status, Is.EqualTo(IVAdmixOrderStatus.Pending));

        await wf.AddIVAdmixAdditiveAsync(orderId, new IVAdmixAdditive
        {
            DrugName = "VANCOMYCIN",
            Dose = "1",
            DoseUnit = "g",
            DrugId = $"DRUG-{Guid.NewGuid():N}",
        });
        await wf.VerifyIVAdmixOrderAsync(orderId, "PHARM1", "WILLIAMS,ROBERT L", DateTime.UtcNow);
        Assert.That((await wf.GetIVAdmixOrderAsync(orderId)).Status, Is.EqualTo(IVAdmixOrderStatus.Verified));

        await wf.StartIVAdmixCompoundingAsync(orderId, "PHARM1", "WILLIAMS,ROBERT L", DateTime.UtcNow);
        Assert.That((await wf.GetIVAdmixOrderAsync(orderId)).Status, Is.EqualTo(IVAdmixOrderStatus.Compounding));

        await wf.CompleteIVAdmixCompoundingAsync(orderId, DateTime.UtcNow,
            lotNumber: "LOT-IV-2026-001", expirationDate: DateTime.UtcNow.AddDays(1));
        Assert.That((await wf.GetIVAdmixOrderAsync(orderId)).Status, Is.EqualTo(IVAdmixOrderStatus.Ready));

        await wf.DispenseIVAdmixOrderAsync(orderId, DateTime.UtcNow);
        Assert.That((await wf.GetIVAdmixOrderAsync(orderId)).Status, Is.EqualTo(IVAdmixOrderStatus.Dispensed));

        await wf.RecordIVAdmixAdministrationAsync(orderId, DateTime.UtcNow);
        Assert.That((await wf.GetIVAdmixOrderAsync(orderId)).Status, Is.EqualTo(IVAdmixOrderStatus.Administered));
    }

    [Test]
    public async Task UnitBeds_TrackOccupancyIndependently()
    {
        // The unit owns all its beds; this fixture verifies that occupying
        // one bed leaves a sibling bed unaffected, and that the release
        // transition (Occupied → Dirty) works without bleeding into the
        // available bed. There is no separate board to sync — the unit's
        // capacity rollup is pushed automatically on every mutation.
        var (inst, unitId) = await NewUnitAsync("Validation Ward", "VAL-101-A", "VAL-101-B");

        string patientId = $"PAT-{Guid.NewGuid()}";
        await Unit(inst, unitId).AdmitPatientAsync(new UnitAdmissionRequest
        {
            PatientId = patientId,
            PatientName = "OCCUPIED,PATIENT",
            MovementId = $"ADT-{Guid.NewGuid()}",
            BedId = "VAL-101-A",
            AdmitDate = DateTime.UtcNow
        });

        InpatientBed occupied = await BedAsync(inst, unitId, "VAL-101-A");
        InpatientBed available = await BedAsync(inst, unitId, "VAL-101-B");
        Assert.That(occupied.State, Is.EqualTo(BedLifecycleState.Occupied));
        Assert.That(occupied.PatientId, Is.EqualTo(patientId));
        Assert.That(available.State, Is.EqualTo(BedLifecycleState.Available));
        Assert.That(available.PatientId, Is.Null);

        // Release the occupied bed → Dirty; sibling unaffected.
        await Unit(inst, unitId).ReleasePatientAsync(patientId, $"ADT-{Guid.NewGuid()}");
        InpatientBed afterRelease = await BedAsync(inst, unitId, "VAL-101-A");
        InpatientBed siblingAfter = await BedAsync(inst, unitId, "VAL-101-B");
        Assert.That(afterRelease.State, Is.EqualTo(BedLifecycleState.Dirty));
        Assert.That(afterRelease.PatientId, Is.Null);
        Assert.That(siblingAfter.State, Is.EqualTo(BedLifecycleState.Available),
            "Releasing one bed should not affect a sibling bed on the same unit.");
    }
}
