# Inpatient Stay End-to-End -- Provider Human Test Script

**Purpose:** Verify the inpatient grains compose end-to-end for a single
patient: ADT (admission, transfer, discharge), bed management (assignment +
release + sibling-bed isolation), inpatient pharmacy (Unit Dose order +
verification), MAR sync, BCMA administration, and IV admixture pharmacy
(verify → compound → ready → dispense → administer). The corresponding
functional tests live in [`InpatientStayEndToEndTests`](../../../../../NewVistas.FunctionalTests/InpatientStayEndToEndTests.cs);
this script is the clinical-SME counterpart.

This is the "validation, not new build" round from the tribal-deployment
plan: every grain exercised here already exists and is covered by per-grain
unit + functional tests; the value here is confirming a real clinician's
inpatient day flows through the composition without surprises.

---

## Prerequisites

- **Login (provider):** `DOCTOR1` / Password: `smythVista1` (must hold the
  `ORES`, `PROVIDER`, and `DG_ADMIT` security keys).
- **Login (pharmacist):** `PHARM1` / Password: `smythVista1` (must hold
  `PSJ_RPHARM` for inpatient verification, `PSJ_RPHARM` + IV-admix mixin
  privileges for the IV scenario).
- **Login (nurse):** `NURSE1` / Password: `smythVista1` (must hold the
  BCMA-administer privileges; `DG_ADMIT` is **not** required to administer).
- **Site profile:** any profile that includes the inpatient stack
  (`IhsTribalSiteProfile`, `LocalhostDevProfile`, `RemoteOnlineProfile` are
  all sufficient — there are no inpatient-specific feature flags).
- **A registered patient** with an ICN (use the registration flow from
  [Doctors/13-Patient-Demographics.md](13-Patient-Demographics.md) or pick
  one out of the seeded demo set).

---

## Part A: Admission

### Scenario 1: Patient admitted from the ED

### Steps

1. As `DOCTOR1`, select the patient and navigate to the ADT/admission page.
2. Record the admission:
   - Movement date/time: now
   - Ward: `WARD-MED-3A` ("Medical Ward 3A")
   - Room/bed: `301-A`
   - Treating specialty: Internal Medicine
   - Attending physician: SMITH,JOHN A
   - Admission diagnosis: "Pneumonia, community-acquired"
   - Comments: "Patient presented to ED with fever, productive cough, hypoxia."

### Expected Result

- A new ADT movement is created with a movement id starting `ADT-`.
- The ward census for `WARD-MED-3A` now lists this patient.
- `wf.GetAdtMovementsAsync()` returns at least 1 entry; the most recent has
  `MovementType = "ADMISSION"`.

---

## Part B: Bed Assignment

### Scenario 2: Bed 301-A marked occupied

### Steps

1. From the bed-management page, look up bed `BED:MAIN:WARD-MED-3A:301-A`.
2. (If first time) "Setup bed" with ward `WARD-MED-3A`, room `301`,
   position `A`, type `MED-SURG`, facility `MAIN`.
3. "Assign patient" with the patient's ICN, expected discharge in 5 days.

### Expected Result

- Bed status flips from `AVAILABLE` to `OCCUPIED`.
- `BedState.PatientId` is the patient's ICN; `OccupiedSince` is set.
- A sibling bed (e.g., 301-B) in the same room remains `AVAILABLE` —
  bed grains are independent.

> Note: the bed grain's `SetupBedAsync` and `AssignPatientAsync` write only
> to the per-bed grain. The facility-wide `IBedBoardGrain` is a separate
> index that needs its own `AddOrUpdateBedAsync` push (or is auto-seeded
> with the demo set on first read). Production deployments wire a small
> "bed change → board update" workflow elsewhere; this script does not
> exercise the board.

---

## Part C: Inpatient Med Order + MAR Sync

### Scenario 3: Provider places a UNIT_DOSE order; pharmacy verifies; MAR picks it up

### Steps

1. As `DOCTOR1`, place an inpatient order for AZITHROMYCIN 500 mg PO QD ×
   5 days against the patient. Set ward `WARD-MED-3A`, room `301-A`,
   start now, stop in 5 days.
2. Pharmacy login (`PHARM1`): navigate to unverified inpatient orders;
   verify the new order.
3. Sync the order to the MAR:
   ```csharp
   await wf.SyncOrderToMARAsync(orderId);
   ```

### Expected Result

- Order state is `Verified`.
- `wf.GetPatientMARAsync()` includes a `MarEntry` whose `OrderId` matches
  the new order.

---

## Part D: BCMA Administration

### Scenario 4: Nurse scans + administers the first dose

### Steps

1. As `NURSE1`, open BCMA, scan/select the patient, scan the order's
   wristband barcode.
2. Confirm "GIVEN", record administration time, leave injection site blank
   for an oral med.

### Expected Result

- A BCMA record is returned with a non-empty BcmaId.
- `wf.GetMedicationAdministrationsAsync(50)` includes the new BCMA id.
- The MAR entry for this order shows the most-recent administration time.

---

## Part E: IV Admixture Lifecycle

### Scenario 5: Order, verify, compound, dispense, administer an IV antibiotic

### Steps

1. As `DOCTOR1`, place an IV admixture order: 0.9% Sodium Chloride 250 mL
   peripheral, Q12H, 1 bag, infusion rate 100 mL/hr, infusion duration 2.5
   hours, route Peripheral, frequency Q12H, priority Routine, start now,
   stop in 5 days, notes: "Pneumonia — IV antibiotic".
2. Add an additive: CEFTRIAXONE 1 g.
3. As `PHARM1`:
   - Verify the IV order.
   - Start compounding.
   - Complete compounding with lot `LOT-2026-001` and 24-hour expiration.
   - Dispense to the floor.
4. As `NURSE1`: record administration.

### Expected Result

- Status transitions: `Pending → Verified → Compounding → Ready → Dispensed → Administered`.
- After dispense, `IVAdmixOrderState.Status = Dispensed`, `LotNumber = "LOT-2026-001"`.
- After administration: `Status = Administered`, `AdministrationDateTime` set.

> Note: status name is `Ready` (not `Compounded`) once compounding completes
> — that is the canonical name of the post-compounding pre-dispense state
> in `IVAdmixOrderStatus`.

---

## Part F: Transfer

### Scenario 6: Patient stepped down to telemetry on day 2

### Steps

1. As `DOCTOR1`, open the ADT/transfer page for the patient's current
   admission movement.
2. Transfer to ward `WARD-MED-4B` ("Medical Ward 4B"), room/bed `401-B`,
   specialty Internal Medicine, attending SMITH,JOHN A, comments
   "Step-down to telemetry".
3. From bed management:
   - Discharge the old bed `BED:MAIN:WARD-MED-3A:301-A` (status →
     `CLEANING`, patient released).
   - Setup + assign new bed `BED:MAIN:WARD-MED-4B:401-B`.

### Expected Result

- New ADT movement returned with id `ADT-...`.
- Old bed: `Status = CLEANING`, `PatientId = null`.
- New bed: `Status = OCCUPIED`, `PatientId = patient ICN`.
- Ward census for the old ward no longer lists the patient; new ward does.

---

## Part G: Discharge

### Scenario 7: Discharge home on day 5

### Steps

1. As `DOCTOR1`, open the ADT/discharge page using the **transfer** movement
   id from Scenario 6 (the discharge mutates the most-recent movement; it
   does not create a fresh ADT row).
2. Set discharge date/time, diagnosis "Pneumonia, community-acquired —
   resolved", disposition "HOME", comments
   "Discharged home with 7-day course of azithromycin."
3. From bed management, discharge bed `BED:MAIN:WARD-MED-4B:401-B`.

### Expected Result

- The transfer movement now has `MovementType = "DISCHARGE"` (mutated in
  place — `RecordDischargeAsync` sets `TransactionType = "DISCHARGE"` on
  the existing movement; it does **not** add a new movement).
- `LengthOfStay` is computed (admission → discharge in days).
- Ward census no longer lists the patient.
- Bed: `Status = CLEANING`, `PatientId = null`.

---

## Part H: Audit-Trail Verification

### Scenario 8: Confirm the stay's audit trail is complete and signable

### Steps

1. As `DOCTOR1`, open the patient's ADT history.

### Expected Result

- At least 2 ADT movements: the original `ADMISSION` and the
  transfer-now-`DISCHARGE` (per the in-place mutation noted in Scenario 7).
- The audit log (`auditEventStore`) contains entries for each
  `IsClinicalWrite = true` action: admission, transfer, discharge, order
  create, order verify, BCMA administration, IV verify/compound/dispense.

---

## Part I: Negative Tests

### Scenario 9: Discharge without DG_ADMIT key is rejected

### Steps

1. Log in as a user without `DG_ADMIT` (e.g., a clerk).
2. Attempt `RecordDischargeAsync` on the patient's transfer movement.

### Expected Result

- `UnauthorizedAccessException` from `AuthorizationCallFilter`.
- No state change on the ADT movement.

### Scenario 10: BCMA administration links to a discontinued order is blocked

### Steps

1. Place + verify a second inpatient order (e.g., MORPHINE 4mg IV Q4H PRN).
2. Discontinue the order: `await orderGrain.DiscontinueAsync("Patient
   refused")`.
3. Attempt `wf.AdministerMedicationAsync(orderId, "GIVEN", ...)`.

### Expected Result

- BCMA administration is rejected (or marked HELD) — the workflow refuses
  to administer against a discontinued order.

### Scenario 11: IV admixture cannot skip compounding

### Steps

1. Create an IV admixture order.
2. Verify it.
3. Skip `StartIVAdmixCompoundingAsync` and immediately call
   `CompleteIVAdmixCompoundingAsync`.

### Expected Result

- The grain rejects the out-of-order transition (status stays `Verified`).

---

## Part J: Verification Checklist

- [ ] Admission creates an ADT movement with id starting `ADT-`
- [ ] Ward census picks up the patient on admission
- [ ] Bed grain transitions AVAILABLE → OCCUPIED with patient id stamped
- [ ] Sibling bed in the same room remains AVAILABLE during the stay
- [ ] UNIT_DOSE order verification + `SyncOrderToMARAsync` populates the MAR
- [ ] BCMA `AdministerMedicationAsync` records non-empty BcmaId, MAR + BCMA history both reflect it
- [ ] IV admixture status walks Pending → Verified → Compounding → **Ready** → Dispensed → Administered (note `Ready` not `Compounded`)
- [ ] IV additive added before verification persists on the order state
- [ ] IV lot number + expiration date persist on the order state after compounding
- [ ] Transfer creates a new ADT movement; old bed released; new bed occupied
- [ ] Discharge mutates the most-recent movement (`MovementType = DISCHARGE`); does **not** add a new ADT row
- [ ] Bed flips OCCUPIED → CLEANING on discharge; patient id cleared
- [ ] LengthOfStay is computed on discharge
- [ ] Audit trail contains entries for each `IsClinicalWrite = true` action
- [ ] DG_ADMIT-required calls are rejected for users without the key
- [ ] BCMA against a discontinued order is rejected/held
- [ ] IV out-of-order transitions are rejected

---

## Cross-References

- **Functional test:** [`InpatientStayEndToEndTests`](../../../../../NewVistas.FunctionalTests/InpatientStayEndToEndTests.cs) — 4 tests covering the full stay, BCMA-MAR consistency, IV admixture lifecycle, and bed-grain independence.
- **Per-grain test fixtures:** [`AdtWorkflowTests`](../../../../../NewVistas.FunctionalTests/AdtWorkflowTests.cs), [`BcmaWorkflowTests`](../../../../../NewVistas.FunctionalTests/BcmaWorkflowTests.cs), [`BedManagementWorkflowTests`](../../../../../NewVistas.FunctionalTests/BedManagementWorkflowTests.cs), [`InpatientPharmacyWorkflowTests`](../../../../../NewVistas.FunctionalTests/InpatientPharmacyWorkflowTests.cs), [`IVPharmacyWorkflowTests`](../../../../../NewVistas.FunctionalTests/IVPharmacyWorkflowTests.cs), [`WardStockWorkflowTests`](../../../../../NewVistas.FunctionalTests/WardStockWorkflowTests.cs).
- **Grain interfaces:** [`IAdtGrain.cs`](../../../../GrainInterfaces/IAdtGrain.cs), [`IBedManagementGrain.cs`](../../../../GrainInterfaces/IBedManagementGrain.cs) (declares `IBedGrain` + `IBedBoardGrain`), [`IBcmaGrain.cs`](../../../../GrainInterfaces/IBcmaGrain.cs), [`IInpatientOrderGrain.cs`](../../../../GrainInterfaces/IInpatientOrderGrain.cs), [`IIVAdmixOrderGrain.cs`](../../../../GrainInterfaces/IIVAdmixOrderGrain.cs).
- **Workflow partials:** [`PatientWorkflowGrain.AdtAdmin.cs`](../../../../Grains/PatientWorkflowGrain.AdtAdmin.cs), [`PatientWorkflowGrain.Bcma.cs`](../../../../Grains/PatientWorkflowGrain.Bcma.cs), inpatient-pharmacy + IV partials.
- **Security keys:** [`SecurityKeys.cs`](../../../../Security/SecurityKeys.cs) — `DG_ADMIT`, `ORES`, `PROVIDER`, `PSJ_RPHARM`.
- **Tribal-flavor coverage:** registration end-to-end is exercised by [`IhsTribalRegistrationEligibilityTests`](../../../../../NewVistas.FunctionalTests/IhsTribalRegistrationEligibilityTests.cs); the new `Register_TribalMember_IcnStartsWithTribalPrefix` test pins that the ICN format produced by the tribal cluster is one the inpatient stack accepts (the inpatient grains are key-agnostic, so SharedCluster validation transitively proves tribal-cluster usability).
