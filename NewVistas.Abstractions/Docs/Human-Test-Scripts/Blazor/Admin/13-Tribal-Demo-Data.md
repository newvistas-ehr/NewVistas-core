# Tribal Demo Data Loader -- Administrator Human Test Script

**Purpose:** Load a small, self-contained tribal-flavored demo dataset
([`exports/TribalDemo/`](../../../../../exports/TribalDemo/README.md)) into a
running silo so a clinician/coordinator/operator can demo NewVistas in an
IHS / tribal context end-to-end:

- 50 patients with realistic IHS eligibility distribution (CHS-eligible,
  direct-care-only, by-category, walk-ins) → exercises `IhsTribalEligibilityPolicy`
- 8 CHS authorization requests at various lifecycle stages (approved,
  denied, pending) → exercises `IExternalReferralGrain` CHS workflow
- 1 completed FY2026 Q1 GPRA report with 11 indicators → exercises `IGpraReportGrain`
  and is ready for the GPRA submission packager (script `12-GPRA-Submission.md`)

The dataset is intentionally small and fully synthetic. ICNs are
deterministic per patient index, so re-running the loader produces no
duplicates.

---

## Prerequisites

- **Login (admin / seeding):** `ADMIN1` / Password: `smythVista1` (must hold `CanRegisterPatients`)
- **Site profile:** `IhsTribalSiteProfile` (which pre-enables the relevant
  features). Other profiles work but eligibility tiers will only be stamped
  if `IhsTribalEligibilityPolicy` is registered.
- **Manifest directory:** the `exports/TribalDemo/` directory must be
  reachable from the silo's filesystem. From a default checkout, that's
  `<repo-root>/exports/TribalDemo`.

---

## Part A: Load the Demo

### Scenario 1: Load via the seeder grain

### Steps

1. From a small operator harness or the (eventual) Blazor admin "Load Demo"
   page, invoke the seeder grain:
   ```csharp
   ITribalDemoSeederGrain seeder =
       grainFactory.GetGrain<ITribalDemoSeederGrain>("TRIBAL-DEMO-SEEDER");

   TribalDemoSeedResult result = await seeder.LoadAsync(
       manifestDirectory: @"C:\Source\NewVistas\exports\TribalDemo",
       seededByUserId: "ADMIN1",
       seededByUserName: "System Administrator");
   ```
   No REST endpoint exists by design — internal Blazor pages call the grain
   directly via `OrleansGrainService` (JWT identity flows through Orleans
   `RequestContext`; the grain-side `AuthorizationCallFilter` enforces the
   `CanRegisterPatients` gate via `[RequiresSecurityKey]` on `LoadAsync`).
2. Inspect `result`.

### Expected Result

- `result.PatientsRegistered = 50`
- `result.ChsReferralsCreated = 8`
- `result.ChsReferralsApproved = 6`
- `result.ChsReferralsDenied = 2`
- `result.GpraReportsCreated = 1`
- `result.PatientIcns` contains 50 ICNs all starting with the test/demo
  prefix `099` followed by a deterministic sequence.
- `result.Errors` is empty.

---

## Part B: Verify the Eligibility Distribution

### Scenario 2: Sample a CHS-eligible patient

### Steps

1. Pick the first ICN from `result.PatientIcns`.
2. View the patient demographics page in Blazor (or query
   `IPatientWorkflowGrain.GetPatientAsync(icn)`).

### Expected Result

- `Veteran = "N"`
- `PrimaryEligibilityCode = "IHS CHS"` for the first patient (the seed has
  the first patient as a 365-day CHSDA-resident tribal member).
- The corresponding enrollment record (via
  `IPatientWorkflowGrain.GetEnrollmentAsync`) has:
  - `EnrollmentStatus = Verified`
  - `PriorityGroup = "IHS-CHS"`
  - `PrioritySubgroup` set to the tribal affiliation (e.g., "Cherokee Nation")
  - `CopayExempt = true`, `CopayExemptionReason = "IHS_BENEFICIARY"`

### Scenario 3: Aggregate distribution check

### Steps

1. Walk all 50 ICNs and tally the `PrimaryEligibilityCode` values.

### Expected Result

Per the manifest README:

| Tier | Expected count |
|---|---|
| `IHS CHS` | 28 |
| `IHS DIRECT` | 15 (12 tribal-direct + 3 by-category) |
| _(empty / null)_ | 7 (walk-ins) |

---

## Part C: Verify CHS Referrals

### Scenario 4: Count by status

### Steps

1. Query the external-referral index: `IExternalReferralIndexGrain.GetByPatientAsync` for each patient ICN that has CHS referrals.
2. Or scan `EXT-REF:DEMO-001` through `EXT-REF:DEMO-008` directly via `IExternalReferralGrain.GetReferralAsync`.

### Expected Result

- 8 referrals created with grain keys `EXT-REF:DEMO-001` through `EXT-REF:DEMO-008`.
- 6 are in `AUTHORIZED` status with non-null `AuthorizedAmount` and `AuthorizationNumber`.
- 2 are in `DENIED` status with `StatusReason` populated:
  - `DEMO-005` (cosmetic, Class V) — denial reason mentions "excluded"
  - `DEMO-006` (screening colonoscopy, alternate resources not verified) — denial reason mentions "Alternate resources"
- All 8 have `IsChsReferral = true`, `MedicalPriorityClass` populated, and `EstimatedCost > 0`.
- Referral index entries (`ExternalReferralIndexEntry`) carry `IsChsReferral`, `MedicalPriorityClass`, and `AuthorizedAmount` so the CHS coordinator dashboard can filter on them.

---

## Part D: Verify GPRA Report

### Scenario 5: Inspect the seeded report

### Steps

1. Query `IGpraReportGrain` keyed `GPRA-REPORT:fy2026-q1-tribal-hub`.

### Expected Result

- `Status = Completed`
- `FiscalYear = 2026`, `ReportingPeriod = Quarter1`
- `FacilityId = "TRIBAL-HUB"`, `ActiveUserPopulation = 5000`
- `Indicators.Count = 11` covering 6 categories (Diabetes, CardiovascularDisease, Immunizations, BehavioralHealth, WomensHealth, PreventiveCare)
- One indicator hits its target: `GPRA-CV-01` (BP control at 70%, target 70%) — `TargetMet = true`. The other 10 are improving but not yet at target.

### Scenario 6: Hand off to the GPRA submission packager

### Steps

1. Continue with [Admin/12-GPRA-Submission.md](12-GPRA-Submission.md) Scenario 1 using `reportId = "fy2026-q1-tribal-hub"`.

### Expected Result

- The seeded report packages cleanly via `IGpraSubmissionGrain.PackageAsync` and walks the rest of the submission lifecycle.

---

## Part E: Idempotency

### Scenario 7: Re-run the loader

### Steps

1. Re-invoke `LoadAsync` with the same manifest directory.

### Expected Result

- Returns again with the same patient ICNs, CHS referrals, and GPRA report.
- No duplicate patient records (deterministic ICN derivation by manifest index).
- CHS referrals re-create over the same grain keys; status is `AUTHORIZED` or `DENIED` again (re-running approve/deny on an already-finalized referral throws inside the grain — this is expected and surfaces in `result.Errors` as a per-referral note, not a load failure).
- GPRA report is unchanged.

---

## Part F: Negative Tests

### Scenario 8: Missing manifest directory

### Steps

1. Invoke with a non-existent directory.

### Expected Result

- Throws `DirectoryNotFoundException`.

### Scenario 9: Non-admin cannot load

### Steps

1. Login as `DOCTOR1` (no `CanRegisterPatients`).
2. Attempt `LoadAsync`.

### Expected Result

- `UnauthorizedAccessException` from the grain-side `AuthorizationCallFilter`.

---

## Part G: Verification Checklist

- [ ] 50 patients registered with deterministic ICNs prefixed by the cluster's IcnPrefix
- [ ] Eligibility distribution matches README (28/15/7)
- [ ] First patient's primary eligibility code is "IHS CHS"
- [ ] Enrollment record for a CHS-eligible patient has PriorityGroup "IHS-CHS"
- [ ] 8 CHS referrals created at the documented grain keys
- [ ] 6 approved + 2 denied; all have `IsChsReferral = true`
- [ ] Denial reasons capture the documented IHS rationale
- [ ] GPRA report seeded in `Completed` status with 11 indicators
- [ ] Re-running the loader produces the same ICNs (idempotent)
- [ ] Missing directory throws cleanly
- [ ] Non-admin is rejected by the auth gate

---

## Cross-References

- Dataset: [`exports/TribalDemo/README.md`](../../../../../exports/TribalDemo/README.md)
- Seeder grain: [`ITribalDemoSeederGrain.cs`](../../../../GrainInterfaces/ITribalDemoSeederGrain.cs), [`TribalDemoSeederGrain.cs`](../../../../Grains/TribalDemoSeederGrain.cs)
- Eligibility policy that classifies patients: [`IhsTribalEligibilityPolicy.cs`](../../../../Eligibility/IhsTribalEligibilityPolicy.cs)
- CHS workflow: [`Admin/11-CHS-Authorization.md`](11-CHS-Authorization.md)
- GPRA submission: [`Admin/12-GPRA-Submission.md`](12-GPRA-Submission.md)
- Site profile: [`IhsTribalSiteProfile.cs`](../../../../../NewVistas.SiloHost/Infrastructure/Profiles/IhsTribalSiteProfile.cs)
- Functional tests: `TribalDemoSeederTests` (synthetic-manifest tests + a real-manifest end-to-end test that loads `exports/TribalDemo/` and pins the documented eligibility distribution)
