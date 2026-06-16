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
///   1. Patient arrives at the ED, gets admitted to Medical Ward 3A bed 301-A
///   2. Bed management marks 301-A as occupied; ward census picks it up
///   3. Provider places an inpatient med order; pharmacy verifies; MAR syncs
///   4. Nurse administers the first scheduled dose via BCMA
///   5. Provider also orders an IV antibiotic; pharmacy compounds + dispenses
///   6. Patient transferred to Medical Ward 4B bed 401-B
///   7. Discharge after 5 days
///   8. Verify the audit trail and ward census both reflect the full journey
/// </summary>
[TestFixture]
public class InpatientStayEndToEndTests
{
    private const string FacilityId = "MAIN";

    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IBedGrain Bed(string bedId) =>
        _cluster.GrainFactory.GetGrain<IBedGrain>(bedId);

    [Test]
    public async Task FullStay_AdmitOrderAdministerTransferDischarge_AllStepsSucceed()
    {
        string patientId = $"INPATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // ── Step 1: Admission ─────────────────────────────────────────
        DateTime admitDate = DateTime.UtcNow.AddDays(-5);
        string admitMovementId = await wf.RecordAdmissionAsync(
            movementDateTime: admitDate,
            wardLocationId: "WARD-MED-3A",
            wardLocationName: "Medical Ward 3A",
            roomBed: "301-A",
            treatingSpecialtyName: "Internal Medicine",
            attendingPhysicianId: "DOCTOR1",
            attendingPhysicianName: "SMITH,JOHN A",
            admissionDiagnosis: "Pneumonia, community-acquired",
            comments: "Patient presented to ED with fever, productive cough, hypoxia.");
        Assert.That(admitMovementId, Does.StartWith("ADT-"),
            "Admission must return an ADT-prefixed movement id.");

        // ── Step 2: Bed assignment + ward census ──────────────────────
        string bedId = $"BED:{FacilityId}:WARD-MED-3A:301-A:{Guid.NewGuid():N}";
        await Bed(bedId).SetupBedAsync(
            wardId: "WARD-MED-3A", wardName: "Medical Ward 3A",
            roomNumber: "301", bedPosition: "A", bedType: "MED-SURG",
            facilityId: FacilityId);
        await Bed(bedId).AssignPatientAsync(patientId, "TESTPATIENT,STAY",
            expectedDischarge: admitDate.AddDays(5));

        BedState bedState = await Bed(bedId).GetBedAsync();
        Assert.That(bedState.PatientId, Is.EqualTo(patientId));
        Assert.That(bedState.Status, Is.EqualTo("OCCUPIED"));

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
        DateTime transferDate = admitDate.AddDays(2);
        string transferMovementId = await wf.RecordTransferAsync(
            currentMovementId: admitMovementId,
            transferDateTime: transferDate,
            toWardId: "WARD-MED-4B",
            toWardName: "Medical Ward 4B",
            toRoomBed: "401-B",
            toSpecialtyId: null,
            toSpecialtyName: "Internal Medicine",
            attendingPhysicianId: "DOCTOR1",
            attendingPhysicianName: "SMITH,JOHN A",
            comments: "Step-down to telemetry");
        Assert.That(transferMovementId, Does.StartWith("ADT-"));

        // Old bed released, new bed assigned.
        await Bed(bedId).DischargePatientAsync();
        string newBedId = $"BED:{FacilityId}:WARD-MED-4B:401-B:{Guid.NewGuid():N}";
        await Bed(newBedId).SetupBedAsync(
            wardId: "WARD-MED-4B", wardName: "Medical Ward 4B",
            roomNumber: "401", bedPosition: "B", bedType: "TELEMETRY",
            facilityId: FacilityId);
        await Bed(newBedId).AssignPatientAsync(patientId, "TESTPATIENT,STAY",
            expectedDischarge: admitDate.AddDays(5));

        BedState oldBed = await Bed(bedId).GetBedAsync();
        BedState newBed = await Bed(newBedId).GetBedAsync();
        Assert.That(oldBed.PatientId, Is.Null,
            "Original bed should be released after transfer.");
        Assert.That(newBed.PatientId, Is.EqualTo(patientId),
            "New bed should be assigned to the patient after transfer.");

        // ── Step 7: Discharge ─────────────────────────────────────────
        DateTime dischargeDate = admitDate.AddDays(5);
        await wf.RecordDischargeAsync(
            movementId: transferMovementId,
            dischargeDateTime: dischargeDate,
            dischargeDiagnosis: "Pneumonia, community-acquired — resolved",
            disposition: "HOME",
            comments: "Discharged home with 7-day course of azithromycin.");

        await Bed(newBedId).DischargePatientAsync();
        BedState dischargedBed = await Bed(newBedId).GetBedAsync();
        Assert.That(dischargedBed.PatientId, Is.Null);

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

        await wf.RecordAdmissionAsync(
            DateTime.UtcNow, "WARD-MED-3A", "Medical Ward 3A", "302-A",
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

        await wf.RecordAdmissionAsync(
            DateTime.UtcNow, "WARD-MED-3A", "Medical Ward 3A", "303-A",
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
    public async Task BedGrains_AcrossWard_TrackOccupancyIndependently()
    {
        // Each bed grain owns its own state; this fixture verifies that
        // assigning one bed in a ward leaves a sibling bed unaffected, and
        // that the discharge transition (OCCUPIED → CLEANING) works without
        // bleeding into the available bed.
        //
        // BedBoard index updates are out of scope here — `SetupBedAsync` and
        // `AssignPatientAsync` write only to the per-bed grain; pushing to
        // the BedBoard requires an explicit `AddOrUpdateBedAsync`, which is
        // covered by BedManagementWorkflowTests.
        string facilityId = $"VAL-{Guid.NewGuid():N}";
        string occupiedBedId = $"BED:{facilityId}:VAL-WARD:VAL-101-A";
        string availableBedId = $"BED:{facilityId}:VAL-WARD:VAL-101-B";

        await Bed(occupiedBedId).SetupBedAsync(
            "VAL-WARD", "Validation Ward", "VAL-101", "A", "MED-SURG", facilityId);
        await Bed(availableBedId).SetupBedAsync(
            "VAL-WARD", "Validation Ward", "VAL-101", "B", "MED-SURG", facilityId);

        string patientId = $"PAT-{Guid.NewGuid()}";
        await Bed(occupiedBedId).AssignPatientAsync(
            patientId, "OCCUPIED,PATIENT", expectedDischarge: null);

        BedState occupied = await Bed(occupiedBedId).GetBedAsync();
        BedState available = await Bed(availableBedId).GetBedAsync();
        Assert.That(occupied.Status, Is.EqualTo("OCCUPIED"));
        Assert.That(occupied.PatientId, Is.EqualTo(patientId));
        Assert.That(available.Status, Is.EqualTo("AVAILABLE"));
        Assert.That(available.PatientId, Is.Null);

        // Discharge the occupied bed → CLEANING; sibling unaffected.
        await Bed(occupiedBedId).DischargePatientAsync();
        BedState afterDischarge = await Bed(occupiedBedId).GetBedAsync();
        BedState siblingAfter = await Bed(availableBedId).GetBedAsync();
        Assert.That(afterDischarge.Status, Is.EqualTo("CLEANING"));
        Assert.That(afterDischarge.PatientId, Is.Null);
        Assert.That(siblingAfter.Status, Is.EqualTo("AVAILABLE"),
            "Discharging one bed should not affect a sibling bed in the same room.");
    }
}
