# Diabetes Registry -- Provider Human Test Script

**Purpose:** Verify the per-patient diabetes registry end-to-end:

- Enroll a patient in the diabetes registry (type 1, type 2, etc.)
- Record diabetes-specific measurements: HbA1c, foot exam, eye exam, eGFR, ACR
- View a computed snapshot showing HbA1c control status, exam due-status, and kidney function tier
- Generate a pre-visit plan listing what's due, overdue, and up-to-date for the patient on a given visit date
- Enumerate the diabetic cohort via the registry index (population-health workflows + GPRA aggregation)

This is the disease-specific clinical depth on top of the generic
`IClinicalRegistryEntryGrain` — equivalent to the per-patient subset of
RPMS BDM (Diabetes Management). Diabetes is the #1 chronic-disease focus at
most IHS facilities (prevalence 2-3× the US average), so this is one of
the higher-value pieces for tribal deployments.

---

## Prerequisites

- **Login (provider):** `DOCTOR1` / Password: `smythVista1` (must hold the
  `CanManageDiabetesRegistry` security key for mutating operations; reads
  are open to any authenticated clinician).
- **Site profile:** `IhsTribalSiteProfile` pre-enables `DIABETES_REGISTRY`.
  Other profiles need to enable it manually:
  ```powershell
  Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/site-parameters/features/enable `
    -Headers @{ Authorization = "Bearer $jwt" } `
    -Body (@{ featureName = "DIABETES_REGISTRY" } | ConvertTo-Json) `
    -ContentType "application/json"
  ```
- **A registered patient** with an ICN (use the registration flow from
  [Doctors/13-Patient-Demographics.md](13-Patient-Demographics.md) or the
  tribal demo seed from [Admin/13-Tribal-Demo-Data.md](../Admin/13-Tribal-Demo-Data.md)).

---

## Part A: Enrollment

### Scenario 1: Enroll a Type-2 diabetic

### Steps

1. As `DOCTOR1`, select the patient's ICN.
2. Invoke the workflow grain. Blazor does not yet host a dedicated enrollment
   form; for now enrollment is a grain-direct call from the dev harness or
   admin page (the WpfDelphiUI Cover Sheet panel is read-only and surfaces
   the snapshot + pre-visit plan once the patient is enrolled — see Part G).
   ```csharp
   await workflow.EnrollInDiabetesRegistryAsync(
       diabetesType: "TYPE_2",
       enrollmentDate: DateTime.UtcNow);
   ```

### Expected Result

- The patient is enrolled in the `IDiabetesRegistryGrain` keyed `"DM-REG:{icn}"`.
- The patient's ICN appears in the singleton `IDiabetesRegistryIndexGrain` cohort list (used by population-health and GPRA aggregation).
- Re-enrolling the same patient is idempotent: enrollment date does not regress.

---

## Part B: Recording Measurements

### Scenario 2: Record HbA1c trending

### Steps

1. Record three sequential HbA1c results spanning the past 12 months (e.g., 8.9 a year ago, 7.8 six months ago, 6.9 last month).
   ```csharp
   await workflow.RecordDiabetesHbA1cAsync(8.9m, DateTime.UtcNow.AddMonths(-12));
   await workflow.RecordDiabetesHbA1cAsync(7.8m, DateTime.UtcNow.AddMonths(-6));
   await workflow.RecordDiabetesHbA1cAsync(6.9m, DateTime.UtcNow.AddDays(-15));
   ```
2. Get the snapshot.

### Expected Result

- Snapshot's `LastHbA1cValue = 6.9`, `LastHbA1cDate ≈ now-15 days`.
- `HbA1cControl = Good` (per ADA threshold &lt;7.0).
- The grain's persisted history retains all three readings (oldest-first), bounded at 24 entries.

### Scenario 3: Record annual exams

### Steps

1. Record a foot exam, an eye exam, and a kidney function panel:
   ```csharp
   await workflow.RecordDiabetesFootExamAsync(DateTime.UtcNow.AddMonths(-3), "Dr. Begay");
   await workflow.RecordDiabetesEyeExamAsync(DateTime.UtcNow.AddMonths(-14), "Dr. Yazzie");  // 14 months ago → Due
   await workflow.RecordDiabetesAcrAsync(15m, DateTime.UtcNow.AddMonths(-18));               // 18 months ago → Overdue
   await workflow.RecordDiabetesEgfrAsync(45m, DateTime.UtcNow.AddMonths(-2));               // CKD G3 (Reduced)
   ```
2. Get the snapshot.

### Expected Result

- `FootExamStatus = UpToDate` (within 12 months)
- `EyeExamStatus = Due` (12–15 months)
- `AcrStatus = Overdue` (> 15 months)
- `KidneyFunction = Reduced` (eGFR 30–59)
- `LastEgfrValue = 45`, `LastAcrValue = 15`

---

## Part C: Pre-Visit Plan

### Scenario 4: Generate a pre-visit plan

### Steps

1. Get the pre-visit plan for today:
   ```csharp
   DiabetesPreVisitPlan plan = await workflow.GetDiabetesPreVisitPlanAsync(DateTime.UtcNow);
   ```
2. Inspect the three lists.

### Expected Result (continuing from Scenario 3 setup)

- `ItemsUpToDate` includes the foot exam ("Annual diabetic foot exam up to date (last performed 3 mo ago).") and the recent HbA1c.
- `ItemsDue` includes "Annual dilated retinal eye exam due (last performed 14 mo ago)." and "Reduced kidney function: last eGFR 45 (CKD G3)."
- `ItemsOverdue` includes "Annual urine albumin/creatinine ratio (nephropathy screen) overdue (last performed 18 mo ago)."
- `Snapshot` is the same data as Scenario 3 returned.

### Scenario 5: Pre-visit plan flags poor HbA1c control regardless of recency

### Steps

1. Record a recent HbA1c of 9.4:
   ```csharp
   await workflow.RecordDiabetesHbA1cAsync(9.4m, DateTime.UtcNow.AddMonths(-2));
   ```
2. Re-generate the pre-visit plan.

### Expected Result

- The plan's `ItemsOverdue` now includes "HbA1c poor control: last value 9.4% (≥9.0). Discuss intensification of therapy."
- The plan's `ItemsUpToDate` still includes the HbA1c itself (recent test) — both signals are surfaced.

---

## Part D: Population-Health Cohort

### Scenario 6: Enumerate the diabetic cohort

### Steps

1. ```csharp
   IDiabetesRegistryIndexGrain idx = grainFactory.GetGrain<IDiabetesRegistryIndexGrain>("DM-REGISTRY-IDX");
   List<string> diabeticIcns = await idx.GetEnrolledIcnsAsync();
   int count = await idx.GetCountAsync();
   ```

### Expected Result

- The list contains the ICN(s) enrolled in this and any prior scenario.
- Future GPRA aggregation iterates this cohort to compute denominators for the GPRA-DM-* indicators.

---

## Part E: Negative Tests

### Scenario 7: Mutating call without `CanManageDiabetesRegistry` is rejected

### Steps

1. As a user without the security key, attempt `RecordDiabetesHbA1cAsync`.

### Expected Result

- `UnauthorizedAccessException` from the grain-side `AuthorizationCallFilter`.
- No state change.

### Scenario 8: Feature disabled — read returns empty, write throws

### Steps

1. Disable the feature: `POST /api/site-parameters/features/disable` with `{ "featureName": "DIABETES_REGISTRY" }`.
2. Call `GetDiabetesRegistrySnapshotAsync` on any patient.
3. Call `EnrollInDiabetesRegistryAsync`.

### Expected Result

- Snapshot call returns an empty snapshot (`IsEnrolled = false`, `HbA1cControl = NoData`, all dates null) — does **not** throw.
- Enroll call throws `InvalidOperationException` with the message:
  > "Diabetes registry is not enabled for this site. Enable the DIABETES_REGISTRY feature in Site Parameters."

### Scenario 9: Out-of-range HbA1c value rejected

### Steps

1. Try `RecordDiabetesHbA1cAsync(99m, ...)` (HbA1c is a percentage; 99 is impossible).

### Expected Result

- `ArgumentOutOfRangeException` ("HbA1c value must be between 0 and 25 (percent).").

### Scenario 10: Out-of-order exam recording does not regress most-recent date

### Steps

1. Record a foot exam dated 2026-01-01 with provider "Dr. New".
2. Record a foot exam dated 2024-01-01 with provider "Dr. Old".
3. Get the snapshot.

### Expected Result

- `LastFootExamDate = 2026-01-01`, `LastFootExamProviderName = "Dr. New"`.
- The older exam is silently ignored (no error, no state regression). This protects against late-arriving lab interface messages overwriting the truth.

---

## Part G: WpfDelphiUI Cover Sheet Panel (CPRS-style frontend)

### Scenario 11: Enrolled patient surfaces on the Cover Sheet

### Steps

1. Launch `NewVistas.WpfDelphiUI` and log in as `DOCTOR1`.
2. Select the patient enrolled and instrumented above.
3. Stay on the **Cover Sheet** tab (default).

### Expected Result

- A 9th panel appears below the standard 8-panel grid, headed with the
  patient's diabetes type (e.g., "Diabetes Registry — TYPE_2"). The panel
  is *only* visible when `snapshot.IsEnrolled == true` — for non-enrolled
  patients it collapses (Auto-height row → 0 px) so the layout stays
  CPRS-faithful.
- **Left column** (snapshot summary):
  - HbA1c value + control label, color-coded: maroon for Poor (≥9.0),
    navy for Good/AtTarget, grey when no data.
  - Kidney function: eGFR + label, color-coded by status (Severe → red,
    Reduced → amber, Normal → navy).
  - Annual exams: foot / eye / ACR each labelled "up to date / due /
    overdue / never recorded".
- **Right column** (pre-visit plan, today's date):
  - Three sub-sections — **Overdue** (red), **Due**, **Up to date** (grey)
    — populated from `GetDiabetesPreVisitPlanAsync(DateTime.UtcNow)`.

### Scenario 12: Non-enrolled patient — panel hidden

### Steps

1. Select a patient NOT enrolled in the diabetes registry.

### Expected Result

- The Cover Sheet shows the standard 8 panels only; the diabetes row has
  zero height (the conditional `Visibility="{Binding HasDiabetesRegistry}"`
  on its Border collapses the entire Auto-height row).
- No 404 spam in the API server log: the WpfDelphiUI Cover Sheet
  swallows snapshot/pre-visit-plan errors so a disabled feature flag or
  missing registry record doesn't break the rest of the cover sheet load.

### Scenario 13: Feature disabled — panel hidden

### Steps

1. Disable the feature: `POST /api/site-parameters/features/disable` with
   `{ "featureName": "DIABETES_REGISTRY" }`.
2. Select an *enrolled* patient.

### Expected Result

- Snapshot endpoint returns the empty snapshot (`IsEnrolled = false`),
  pre-visit-plan endpoint returns the empty plan — both per the workflow
  grain's feature-disabled behavior. Panel stays hidden.

---

## Part F: Verification Checklist

- [ ] DIABETES_REGISTRY feature flag enables/disables the workflow grain methods
- [ ] `IhsTribalSiteProfile` pre-enables the feature
- [ ] EnrollInDiabetesRegistryAsync persists state and adds to the cohort index
- [ ] Re-enrolling does not regress the enrollment date but updates type
- [ ] HbA1c history is bounded (oldest dropped beyond capacity)
- [ ] Snapshot HbA1cControl: Good &lt;7.0 / AtTarget 7.0–8.9 / Poor ≥9.0
- [ ] Snapshot exam status: UpToDate ≤12mo / Due 12–15mo / Overdue &gt;15mo
- [ ] Snapshot kidney function: Normal ≥60 / Reduced 30–59 / Severe &lt;30
- [ ] Pre-visit plan separates ItemsUpToDate / ItemsDue / ItemsOverdue correctly
- [ ] Poor HbA1c surfaces on the overdue list regardless of test recency
- [ ] Severe CKD surfaces on the overdue list with referral suggestion
- [ ] `IDiabetesRegistryIndexGrain.GetEnrolledIcnsAsync` enumerates the cohort
- [ ] Mutating calls without `CanManageDiabetesRegistry` are rejected
- [ ] Out-of-range HbA1c rejected
- [ ] Out-of-order exam recording does not regress dates
- [ ] WpfDelphiUI Cover Sheet shows the diabetes panel only when enrolled
- [ ] HbA1c color-coding matches control status (maroon Poor / navy Good)
- [ ] Pre-visit plan items split into Overdue / Due / Up-to-date columns
- [ ] Read endpoints `api/diabetesregistry/{patientId}/snapshot` + `/previsit-plan` return the expected DTO shape

---

## Cross-References

- Per-patient grain: [`IDiabetesRegistryGrain.cs`](../../../../GrainInterfaces/IDiabetesRegistryGrain.cs), [`DiabetesRegistryGrain.cs`](../../../../Grains/DiabetesRegistryGrain.cs)
- Cohort index: same files (singleton `IDiabetesRegistryIndexGrain`)
- State + snapshot + pre-visit plan + status enums: [`DiabetesRegistryState.cs`](../../../../GrainStates/DiabetesRegistryState.cs)
- Pure-function rules (status classifiers, pre-visit plan composition): [`DiabetesRegistryRules.cs`](../../../../Helpers/DiabetesRegistryRules.cs)
- Workflow methods: [`PatientWorkflowGrain.DiabetesRegistry.cs`](../../../../Grains/PatientWorkflowGrain.DiabetesRegistry.cs)
- Security key: [`SecurityKeys.cs`](../../../../Security/SecurityKeys.cs) `CanManageDiabetesRegistry`
- Site flavor pre-enable: [`IhsTribalSiteProfile.cs`](../../../../../NewVistas.SiloHost/Infrastructure/Profiles/IhsTribalSiteProfile.cs) — `DIABETES_REGISTRY` in `PreEnabledFeatures`
- Functional tests: `DiabetesRegistryRulesTests` (16 unit tests for the rules helper) + `DiabetesRegistryWorkflowTests` (10 functional tests for the workflow path)
- Sister GPRA indicators (downstream consumer): the GPRA report's `GPRA-DM-01..04` indicators draw their per-patient denominators/numerators from this registry
- REST controller (read-side, chart-tab consumer): [`DiabetesRegistryController.cs`](../../../../../NewVistas.WebServer/Controllers/DiabetesRegistryController.cs)
- WpfDelphiUI Cover Sheet panel: [`CoverSheetViewModel.cs`](../../../../../NewVistas.WpfDelphiUI/ViewModels/CoverSheetViewModel.cs), [`CoverSheetView.xaml`](../../../../../NewVistas.WpfDelphiUI/Views/CoverSheetView.xaml) (the conditional 9th panel)
