# Patient Merge -- Administrator Human Test Script

**Purpose:** Verify the duplicate-patient merge workflow end-to-end:
- Two patients are registered, suspected to be the same person
- Admin merges the duplicate (source) into the surviving (target) record
- All clinical data (allergies, problems, immunizations, lab/order/note IDs, etc.) moves from source → target with no loss and no duplicates
- Source patient is deactivated and stamped with `MergedIntoPatientId`
- Source ICN's MPI correlation grain and MPI search-index entry are aliased to the target ICN, so future searches by the source ICN flag the alias
- Auth gate (`CanMergePatients` security key) blocks unauthorized callers

The merge feature is **per-site opt-in** via `ISiteParametersGrain` feature
flag `PATIENT_MERGE`. Tribal / VA-aligned deployments will pre-enable it via
their site profile; this script enables it manually for the test.

---

## Prerequisites

- **Login (admin):** `ADMIN1` / Password: `smythVista1` (must hold `Administrator`)
- **Login (non-admin, for negative test):** `DOCTOR1` / Password: `smythVista1`
- **Pre-conditions:**
  1. SiloHost, WebServer, BlazorWeb running on a single-silo dev profile (no federation needed for this script).
  2. `CanMergePatients` security key issued to `ADMIN1`'s role mapping (the demo seed should include it; if not, the negative test in Scenario 5 will be a false positive — see [Demo Users & Login Reference](../../../../../NewVistas.BlazorWeb/UserManual/admin/demo-users.md)).
  3. PowerShell session with cleaned variables: `Remove-Variable jwt, srcIcn, tgtIcn -ErrorAction SilentlyContinue`

---

## Part A: Enable the Feature

### Scenario 1: Enable PATIENT_MERGE for the test site

### Steps

1. Get an admin JWT:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "ADMIN1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $jwt = $login.token
   ```
2. Enable the feature:
   ```powershell
   Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/site-parameters/features/enable `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{ featureName = "PATIENT_MERGE" } | ConvertTo-Json) `
     -ContentType "application/json"
   ```

### Expected Result

- HTTP 200; response indicates feature is now enabled.
- Subsequent calls to `IPatientWorkflowGrain.MergePatientAsync` will not short-circuit on the feature gate.

---

## Part B: Set Up Two Duplicate Patients

### Scenario 2: Register two patients that turn out to be the same person

### Steps

1. Register the **target** (the record that will survive):
   ```powershell
   $tgt = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/registration/register `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{
       patientName  = "MERGEDEMO,JOHN A"
       ssn          = "111223333"
       dateOfBirth  = "1960-01-01"
       sex          = "M"
       facilityDfn  = "DFN-100100"
     } | ConvertTo-Json) -ContentType "application/json"
   $tgtIcn = $tgt.icn
   "Target ICN: $tgtIcn"
   ```
2. Register the **source** (the duplicate):
   ```powershell
   $src = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/registration/register `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{
       patientName  = "MERGEDEMO,JOHNNY"
       ssn          = "111223333"
       dateOfBirth  = "1960-01-01"
       sex          = "M"
       facilityDfn  = "DFN-100200"
     } | ConvertTo-Json) -ContentType "application/json"
   $srcIcn = $src.icn
   "Source ICN: $srcIcn"
   ```
3. Add some clinical data on each so we can prove it moves on merge.
   - Login as `DOCTOR1` on `https://localhost:7137`, select the **target** patient by ICN, document a Penicillin allergy.
   - Select the **source** patient by ICN, document a Sulfa allergy and a Hypertension problem.

### Expected Result

- Two distinct ICNs returned (e.g., `0000000037V045712` and `0000000038V...`).
- Both patients exist in MPI search:
  ```powershell
  Invoke-RestMethod -Uri "https://localhost:7127/api/mpi/search?q=MERGEDEMO" `
    -Headers @{ Authorization = "Bearer $jwt" }
  ```
  returns at least 2 hits.
- Target has: Penicillin allergy.
- Source has: Sulfa allergy + Hypertension problem.

---

## Part C: Execute the Merge

### Scenario 3: Merge source into target (happy path)

### Steps

> **Note:** there is no REST endpoint for merge by design — workflow grains
> are invoked from the Blazor UI directly via `OrleansGrainService` (which
> propagates the JWT identity through Orleans `RequestContext`). For this
> manual test you can either:
>
> - Use the Blazor "Patient Merge" admin page (when built), OR
> - From a small test harness / `dotnet` script that creates an Orleans
>   client, sets `RequestContext` from the ADMIN1 JWT, and calls
>   `IPatientWorkflowGrain.MergePatientAsync(...)` on the target ICN.
>
> The functional test fixture
> [`PatientMergeMpiPropagationTests`](../../../../../NewVistas.UnitTests/PatientMergeMpiPropagationTests.cs)
> exercises the same workflow programmatically.

### Expected Result

- `PatientMergeResult.Success == true`.
- `result.ItemsMoved` shows non-zero counts for `Allergies` and `Problems`
  (and any other categories with source-only data).

---

## Part D: Verify the Aftermath

### Scenario 4: Confirm clinical data moved + MPI alias is in place

### Steps

1. Reload the **target** patient in Blazor as `DOCTOR1`. Cover sheet should show:
   - Allergies: Penicillin **and** Sulfa
   - Problems: Hypertension
2. Reload the **source** patient in Blazor. Cover sheet (or a banner) should indicate the patient has been merged into `<target ICN>`.
3. Query the MPI for the source ICN:
   ```powershell
   Invoke-RestMethod -Uri "https://localhost:7127/api/mpi/icn/$srcIcn" `
     -Headers @{ Authorization = "Bearer $jwt" }
   ```
4. Query the MPI correlation grain directly for the source ICN:
   ```powershell
   Invoke-RestMethod -Uri "https://localhost:7127/api/mpi/correlation/$srcIcn" `
     -Headers @{ Authorization = "Bearer $jwt" }
   ```

### Expected Result

- Target patient has merged data, no duplicates.
- Source patient is marked merged (`MergedIntoPatientId == $tgtIcn`, `IsActive == false`).
- MPI search result for source ICN includes `mergedIntoIcn = $tgtIcn` (new field).
- MPI correlation grain for source ICN has `mergedIntoIcn = $tgtIcn`.
- MPI correlation grain for target ICN has `mergedIntoIcn = null`.

---

## Part E: Negative Tests

### Scenario 5: Non-admin cannot merge (auth gate)

### Steps

1. Login as `DOCTOR1` and obtain their JWT (DOCTOR1 should NOT hold `CanMergePatients`).
2. From a test harness with `RequestContext.UserId = "DOCTOR1"`, call
   `IPatientWorkflowGrain.MergePatientAsync(...)` on a fresh target patient.

### Expected Result

- Call throws `UnauthorizedAccessException` (the grain-side `AuthorizationCallFilter` rejects on the `[RequiresSecurityKey(SecurityKeys.CanMergePatients)]` attribute).
- No clinical data moves; no MPI changes.
- See [AuthorizationCallFilter.cs](../../../../../NewVistas.Abstractions/Security/AuthorizationCallFilter.cs).

### Scenario 6: Re-merging a different target is refused

### Steps

1. After Scenario 3 (source merged into target), attempt a second merge of the same source into a **different** target.

### Expected Result

- Throws `InvalidOperationException` from `MpiCorrelationGrain.MarkAsMergedAsync`: "ICN ... is already merged into ...; refusing to remerge into ...".
- Source's `MergedIntoIcn` remains pointed at the first target — the alias chain is not re-routed.

### Scenario 7: Idempotent re-merge to the same target is allowed

### Steps

1. Re-issue the same merge request (same source, same target) immediately after Scenario 3.

### Expected Result

- Result is success (no clinical data is moved a second time because the dedup-by-ID logic skips already-present entries).
- `MergedIntoIcn` is unchanged.
- No exception.

### Scenario 8: Feature disabled → merge refused

### Steps

1. Disable the feature:
   ```powershell
   Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/site-parameters/features/disable `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{ featureName = "PATIENT_MERGE" } | ConvertTo-Json) `
     -ContentType "application/json"
   ```
2. Try Scenario 3 again on a fresh source/target pair.

### Expected Result

- `PatientMergeResult.Success == false`.
- `result.ErrorMessage` says `"Patient merge is not enabled for this site. Enable the PATIENT_MERGE feature in Site Parameters."`
- No data moves; no MPI changes.

---

## Part F: Verification Checklist

- [ ] PATIENT_MERGE feature can be enabled and disabled at runtime
- [ ] Two duplicate patients can be registered with distinct ICNs
- [ ] Merge moves all clinical data (allergies, problems, immunizations, ID lists) from source to target
- [ ] No duplicate clinical entries created when both patients had overlapping data
- [ ] Source patient is marked `IsActive = false` with `MergedIntoPatientId` set
- [ ] Source ICN's MPI correlation grain has `MergedIntoIcn` = target ICN
- [ ] Source ICN's MPI search entry has `MergedIntoIcn` = target ICN
- [ ] Target ICN's MPI correlation/search records remain unaliased
- [ ] Non-admin (no `CanMergePatients` key) is rejected with `UnauthorizedAccessException`
- [ ] Re-merging the same source to a *different* target is refused
- [ ] Idempotent re-merge (same source, same target) is allowed
- [ ] Disabling the feature blocks subsequent merge calls

---

## Cross-References

- Workflow method: [`IPatientWorkflowGrain.MergePatientAsync`](../../../../GrainInterfaces/IPatientWorkflowGrain.cs) (lines around `[RequiresSecurityKey(CanMergePatients)]`)
- Implementation: [`PatientMergeGrain.cs`](../../../../Grains/PatientMergeGrain.cs)
- MPI propagation: [`PatientMergeGrain.cs`](../../../../Grains/PatientMergeGrain.cs) Phase 4b
- Security key: [`SecurityKeys.cs`](../../../../Security/SecurityKeys.cs) `CanMergePatients`
- Functional tests:
  - `PatientMergeGrainTests` (existing — covers data-movement basics)
  - `PatientMergeMpiPropagationTests` (new — covers MPI correlation alias + search index update + idempotency + re-merge refusal)
- Architecture: [`ADR-001 — Patient Identity Strategy`](../../../Architect-decisions/ADR-001-Patient-Identity-Strategy.md) -- merge is the recovery path for cross-cluster duplicates
