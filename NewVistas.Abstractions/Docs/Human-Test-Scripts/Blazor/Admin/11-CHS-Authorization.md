# Contract Health Services (CHS) Authorization -- Administrator Human Test Script

**Purpose:** Verify the IHS Contract Health Services / Purchased and Referred
Care (PRC) authorization workflow end-to-end:
- A clinician creates an external referral and marks it as needing CHS funding
- A CHS coordinator reviews the request, verifies eligibility, and approves
  or denies it under 25 CFR Part 136
- The audit trail records who requested, who approved, and the dollar amount
- The eligibility gate prevents direct-care-only patients from being CHS-funded
- The auth gate (`CanAuthorizeChs` security key) restricts approval to the
  designated CHS coordinator role

This is feature-gated by the `EXTERNAL_REFERRAL_TRACKING` site flag (the
`IhsTribalSiteProfile` pre-enables it).

---

## Prerequisites

- **Login (CHS coordinator):** an `ADMIN`-roled or `CHSCoordinator`-roled
  user holding the `CanAuthorizeChs` security key. For the demo, `ADMIN1` is
  granted the key by default.
- **Login (clinician):** `DOCTOR1` / Password: `smythVista1`
- **Login (negative test, no key):** `DOCTOR2` / Password: `smythVista1`
- **Site profile:** `IhsTribalSiteProfile` (or any profile that has
  `EXTERNAL_REFERRAL_TRACKING` enabled and `IhsTribalEligibilityPolicy`
  registered).
- **Two test patients:**
  - `P-CHS-ELIG` registered as a tribal member with **180+ days CHSDA
    residency** so `IhsTribalEligibilityPolicy` stamps `PrimaryEligibilityCode = "IHS CHS"`
  - `P-DIRECT-ONLY` registered as a tribal member with **0 CHSDA residency
    days** (or `ResidesInChsda = false`) so the policy stamps
    `PrimaryEligibilityCode = "IHS DIRECT"` only

---

## Part A: Patient Setup

### Scenario 1: Register the two test patients

### Steps

1. Get a JWT for `ADMIN1` and register the CHS-eligible patient:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "ADMIN1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $jwt = $login.token

   $chsResp = Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/registration/register `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{
       patientName        = "TRIBAL,CHS-ELIGIBLE"
       ssn                = "111223333"
       dateOfBirth        = "1970-01-01"
       sex                = "F"
       facilityDfn        = "DFN-CHS-001"
       isTribalMember     = $true
       tribalAffiliation  = "Cherokee Nation"
       residesInChsda     = $true
       chsdaResidencyDays = 365
     } | ConvertTo-Json) -ContentType "application/json"
   $chsIcn = $chsResp.icn
   ```
2. Register the direct-care-only patient:
   ```powershell
   $directResp = Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/registration/register `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{
       patientName        = "TRIBAL,DIRECT-CARE-ONLY"
       ssn                = "111224444"
       dateOfBirth        = "1980-05-15"
       sex                = "M"
       facilityDfn        = "DFN-DIRECT-001"
       isTribalMember     = $true
       tribalAffiliation  = "Cherokee Nation"
       residesInChsda     = $false
     } | ConvertTo-Json) -ContentType "application/json"
   $directIcn = $directResp.icn
   ```
3. Verify the two PrimaryEligibilityCode values via the patient grain (Blazor:
   navigate to each patient's demographics page and check the eligibility
   field):
   - `$chsIcn` → `IHS CHS`
   - `$directIcn` → `IHS DIRECT`

### Expected Result

- Both patients registered with distinct ICNs starting with the cluster prefix (e.g., `9100000001V…`).
- `$chsIcn` is CHS-eligible; `$directIcn` is direct-care only.

---

## Part B: CHS Approval Happy Path

### Scenario 2: Clinician creates a referral and marks it CHS

### Steps

1. As `DOCTOR1` in Blazor:
   - Select patient `$chsIcn`.
   - Open the **Referrals** page.
   - Create an external referral: cardiology consult, External Facility = "Tulsa Cardiology", purpose "New-onset chest pain", urgency = ROUTINE.
   - Note the referral ID (form `EXT-REF:{guid}`).
2. From the same Referrals page, click **Request CHS Authorization**.
3. Fill the request:
   - Estimated cost: `$1500.00`
   - Medical Priority Class: `II` (acute)
   - Alternate resources checked: `Yes`
   - Note: `No alternate coverage on file`
4. Submit.

### Expected Result

- Referral status changes to `PENDING_CHS_AUTH`.
- A follow-up note appears on the referral: "CHS authorization requested. Priority class II, estimated cost $1500.00. Alternate resources checked: yes."

### Scenario 3: CHS coordinator approves

### Steps

1. As `ADMIN1` in Blazor:
   - Open the CHS coordinator dashboard (or the Referrals page filtered by status `PENDING_CHS_AUTH`).
   - Open the request from Scenario 2.
   - Approve: authorized amount `$1600.00`, authorization number `CHS-2026-00045`.

### Expected Result

- Status transitions to `AUTHORIZED`.
- `AuthorizedAmount = 1600.00`, `AuthorizationNumber = "CHS-2026-00045"`.
- `ChsAuthorizedById = ADMIN1`, `ChsAuthorizationDate ≈ now`.
- A follow-up entry: "CHS authorization approved: $1600.00, auth# CHS-2026-00045."
- The audit log records the action (Domain=CHS, Action=APPROVE_AUTH, EntityType=ExternalReferral) per the `[AuditAction]` attribute on `IPatientWorkflowGrain.ApproveChsAuthorizationAsync`.

---

## Part C: Eligibility Gate

### Scenario 4: Approval blocked for direct-care-only patient

### Steps

1. As `DOCTOR1`, repeat Scenario 2 against patient `$directIcn` (the direct-care-only patient).
2. As `ADMIN1`, attempt to approve the new request.

### Expected Result

- The approval is rejected with an `InvalidOperationException` (or HTTP 400/500 if surfaced via the REST layer):
  > "Patient {ICN} does not hold CHS eligibility ('IHS DIRECT' on file; expected 'IHS CHS'). Re-run eligibility determination or deny this request."
- The referral remains in `PENDING_CHS_AUTH` so the coordinator can decide to deny it instead.

---

## Part D: Denial Workflow

### Scenario 5: Coordinator denies a low-priority request

### Steps

1. As `DOCTOR1`, create another referral for `$chsIcn` and mark it CHS:
   - Estimated cost: `$5000.00`
   - Medical Priority Class: `IV` (chronic tertiary)
   - Alternate resources checked: `No`
   - Note: `Patient declined to disclose insurance status`
2. As `ADMIN1`, deny the request:
   - Reason: "Priority IV deferred for FY2026; alternate resources not verified."

### Expected Result

- Status transitions to `DENIED`.
- `StatusReason` matches the supplied text.
- `RequiresFollowUp = false` — denied requests don't need clinician follow-up.
- `AuthorizedAmount` remains `null`.
- A follow-up entry: "CHS authorization denied: Priority IV deferred for FY2026; alternate resources not verified."

---

## Part E: Auth Gate

### Scenario 6: Non-coordinator cannot approve

### Steps

1. As `DOCTOR2` (no `CanAuthorizeChs` key), attempt to approve a fresh pending CHS request.

### Expected Result

- Call rejected with `UnauthorizedAccessException` from the grain-side `AuthorizationCallFilter`.
- The referral state is unchanged.
- Audit log records the denied attempt (the filter logs unauthorized access attempts).

---

## Part F: Negative State-Transition Tests

### Scenario 7: Cannot approve a non-CHS referral

### Steps

1. Create a regular external referral via `CreateExternalReferralAsync` and **do NOT** call `RequestChsAuthorizationAsync`.
2. As `ADMIN1`, attempt to approve.

### Expected Result

- Throws `InvalidOperationException`: "This referral was not submitted as a CHS request; call RequestChsAuthorizationAsync first."

### Scenario 8: Cannot re-approve an already-authorized referral

### Steps

1. Take a referral already in `AUTHORIZED` status (Scenario 3).
2. Attempt a second approval with a different amount.

### Expected Result

- Throws `InvalidOperationException`: "CHS authorization can only be approved from PENDING_CHS_AUTH status (currently AUTHORIZED)."

---

## Part G — alt: WpfDelphiUI Consults Tab (CPRS-style frontend)

The same workflow lives in the WpfDelphiUI's Consults tab, lower pane.
Validation steps mirror the Blazor flow above — with these UI specifics:

### Scenario: CHS action bar visible only when applicable

### Steps

1. Launch `NewVistas.WpfDelphiUI` and log in as `ADMIN1` (holds `CanAuthorizeChs`).
2. Select patient `$chsIcn` and open the **Consults** tab.
3. The tab splits horizontally: internal Consults (top) + External Referrals (bottom).
4. Select a referral that is `IsChsReferral = true`.

### Expected Result

- A pale-amber action bar appears above the External Referrals list, headed
  with the referral details (CHS Priority Class, Authorized Amount,
  Alternate Resources Checked, decision date + coordinator).
- Three buttons: **Request CHS Auth**, **Approve**, **Deny**.
- The action bar is hidden when:
  - No referral is selected, OR
  - The selected referral is `IsChsReferral = false`, OR
  - The user does NOT hold `CanAuthorizeChs` (e.g., logged in as `DOCTOR2`).

### Scenario: Single form, branched by action

### Steps

1. Click **Request CHS Auth** → form shows estimated-cost, priority class,
   alternate-resources checkbox + note.
2. Click **Approve** → form shows authorized amount + optional auth#.
3. Click **Deny** → form shows the denial-reason text area.
4. The single Submit button maps to the correct workflow command via a
   `DataTrigger` on the action mode (Request → `RequestChsCommand`,
   Approve → `ApproveChsCommand`, Deny → `DenyChsCommand`).

### Expected Result

- Submit calls the corresponding `api/externalreferral/{patientId}/referrals/{referralId}/chs/{action}` endpoint.
- The referrals list reloads after a successful submission; the selected
  referral's CHS status fields update accordingly.
- Validation errors (non-numeric amount, missing denial reason) surface in
  the shared `ErrorText` strip without submitting the request.

### Scenario: Direct-care-only eligibility gate (UI surface)

### Steps

1. As `ADMIN1`, select a referral against `$directIcn` (direct-care-only)
   and approve.

### Expected Result

- The Submit fails with a 400 surfaced as `Approve failed: BadRequest`.
- The error message describes the eligibility mismatch (per the
  `InvalidOperationException` thrown by `ApproveChsAuthorizationAsync`).
- The referral remains in `PENDING_CHS_AUTH` so the coordinator can choose
  to deny it.

---

## Part G: Verification Checklist

- [ ] CHS-eligible patient registered with PrimaryEligibilityCode = "IHS CHS"
- [ ] Direct-care-only patient registered with PrimaryEligibilityCode = "IHS DIRECT"
- [ ] CHS request marks referral PENDING_CHS_AUTH and records cost + priority + alternate-resources status
- [ ] Approval transitions to AUTHORIZED with the dollar amount + auth number
- [ ] Approval is blocked for direct-care-only patients with a clear error
- [ ] Denial transitions to DENIED with the reason captured
- [ ] Approval requires the `CanAuthorizeChs` security key (rejected for non-coordinators)
- [ ] Cannot approve a referral that was never marked as CHS
- [ ] Cannot re-approve an already-AUTHORIZED referral
- [ ] Audit log records each CHS action via `[AuditAction]` on the workflow methods
- [ ] Referral index reflects the CHS fields (IsChsReferral, MedicalPriorityClass, AuthorizedAmount)
- [ ] WpfDelphiUI Consults tab CHS action bar appears only for CHS-flagged referrals when the user holds `CanAuthorizeChs`
- [ ] The single Submit button branches to Request / Approve / Deny based on the selected action mode
- [ ] REST endpoints `api/externalreferral/{patientId}/referrals/{referralId}/chs/request` + `/approve` + `/deny` map 1:1 to the workflow methods

---

## Cross-References

- Workflow methods: [`IPatientWorkflowGrain.RequestChsAuthorizationAsync`](../../../../GrainInterfaces/IPatientWorkflowGrain.cs), `ApproveChsAuthorizationAsync`, `DenyChsAuthorizationAsync`
- Implementation: [`PatientWorkflowGrain.Referral.cs`](../../../../Grains/PatientWorkflowGrain.Referral.cs) (eligibility check) and [`ExternalReferralGrain.cs`](../../../../Grains/ExternalReferralGrain.cs) (state transitions)
- Eligibility policy that stamps the IHS CHS code: [`IhsTribalEligibilityPolicy.cs`](../../../../Eligibility/IhsTribalEligibilityPolicy.cs)
- Security key: [`SecurityKeys.cs`](../../../../Security/SecurityKeys.cs) `CanAuthorizeChs`
- Functional tests: `ChsAuthorizationTests` (8 scenarios — happy path, eligibility gate, state transitions, index propagation)
- Architecture: [`ADR-001 — Patient Identity Strategy`](../../../Architect-decisions/ADR-001-Patient-Identity-Strategy.md), tribal-deployment plan (CHS authorization is a critical-path Phase 1 item)
- REST controller: [`ExternalReferralController.cs`](../../../../../NewVistas.WebServer/Controllers/ExternalReferralController.cs) — CHS endpoints under `api/externalreferral/{patientId}/referrals/{referralId}/chs/{request|approve|deny}`
- WpfDelphiUI Consults tab: [`ConsultsViewModel.cs`](../../../../../NewVistas.WpfDelphiUI/ViewModels/ConsultsViewModel.cs) (CHS commands + form-mode branching) and [`ConsultsView.xaml`](../../../../../NewVistas.WpfDelphiUI/Views/ConsultsView.xaml) (action bar in the External Referrals lower pane)
