# Clinical Event Sourcing -- Administrator + Clinician Human Test Script

**Purpose:** Verify the per-patient `IPatientClinicalEventStreamGrain`:
clinical actions append events with monotonic versions and a tamper-resistant
hash chain (§170.315(d)(2)), the stream replays deterministically to a given
version, idempotent appends do not duplicate, and chain verification detects
tampering.

This script tests commits **998c8263** (per-patient stream), **19b305e0**
(log consistency provider), **3673e424** (lab/consult/vital event sourcing),
**f93ede69** (allergies/MH), and **dccbe537** (event sourcing refactor).

---

## Prerequisites

- **Login (clinical):** `DOCTOR1` / Password: `smythVista1`
- **Login (admin/forensics):** `ADMIN1` / Password: `smythVista1`
- **Pre-conditions:**
  1. Single SiloHost + WebServer + BlazorWeb running (federation not required for this script).
  2. Demo data loaded for at least patient `P1`:
     ```
     POST /api/scheduling/demo/load?patientId=P1
     ```
  3. Optional: SQL Express for log-consistency provider; in-memory storage works for steady-state testing but does not survive restart.

---

## Part A: Append Events Across Domains

### Scenario 1: Append One Event per Domain

### Steps

1. As `DOCTOR1`, select patient P1 and perform one action in each of the following clinical domains -- record the cumulative event count after each:
   - Allergy: document a `Penicillin -- Rash, mild` allergy (per [Blazor/Doctors/14-Allergy-Documentation.md](../Doctors/14-Allergy-Documentation.md))
   - Lab: place a CBC order (per [Blazor/Doctors/06-Laboratory-Orders.md](../Doctors/06-Laboratory-Orders.md))
   - Consult: place a Cardiology consult (per [Blazor/Doctors/04-Consult-Management.md](../Doctors/04-Consult-Management.md))
   - Vital: capture BP 120/80, HR 72 (per [Blazor/Nurses/02-Vital-Signs-Recording.md](../Nurses/02-Vital-Signs-Recording.md))
   - MentalHealth: complete a PHQ-2 (per [Blazor/Doctors/10-Mental-Health-Screening.md](../Doctors/10-Mental-Health-Screening.md))
2. Query the patient's clinical event stream:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "DOCTOR1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $events = Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/P1/clinical-events?max=20" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   $events | Select-Object version, domain, eventId, appendedUtc, hash | Format-Table -AutoSize
   ```

### Expected Result

- 5 new events appended (one per domain).
- `version` is monotonic starting from the prior max + 1 (no gaps).
- `domain` field correctly identifies each (`Allergy`, `Lab`, `Consult`, `Vital`, `MentalHealth`).
- Each row has a unique `eventId` (GUID) and 64-char hex `hash`.
- Cross-ref: `ClinicalEventSourcingTests.Append_AssignsHashChainAndIncrementsVersion`.

### Scenario 2: Filter by Domain

### Steps

1. Query just the Allergy events:
   ```powershell
   Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/P1/clinical-events?domain=Allergy&max=20" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   ```

### Expected Result

- Only events with `domain = Allergy` returned.
- Includes all historical Allergy events plus the newly-added one.

---

## Part B: Hash Chain Determinism

### Scenario 3: Recompute Hash Externally

### Steps

1. From the event list in Scenario 1, pick the most recent event. Record its `version`, `previousHash`, `eventId`, `payload`, and `hash`.
2. Recompute the hash externally (PowerShell uses the same SHA-256 algorithm as the grain's hash chain):
   ```powershell
   $evt = $events[0]   # most recent
   $canonical = (@{
     version = $evt.version
     previousHash = $evt.previousHash
     eventId = $evt.eventId
     domain = $evt.domain
     payload = $evt.payload
     sourceClusterId = $evt.sourceClusterId
     appendedUtc = $evt.appendedUtc
   } | ConvertTo-Json -Compress -Depth 10)
   $bytes = [Text.Encoding]::UTF8.GetBytes($canonical)
   $sha = [Security.Cryptography.SHA256]::Create()
   $computed = -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })
   "Computed: $computed"
   "Stored:   $($evt.hash)"
   "Match?    $($computed -eq $evt.hash)"
   ```

### Expected Result

- Match? `True`. (If False, the canonical-form fields used by the grain differ from what's in this script -- inspect `EventEnvelope.cs` for the canonical serializer and adjust.)
- Cross-ref: `ClinicalEventSourcingTests.HashChain_Compute_IsDeterministic`.

### Scenario 4: Hash Differs When Previous Hash Differs

### Steps

1. Compare the `hash` of two adjacent events: `events[1].hash` (the previousHash for events[0]) is part of the input that produced `events[0].hash`. Conceptually verify:
   - Event N's hash includes Event N-1's hash as input
   - Therefore tampering with Event N-1 would invalidate Event N's hash

### Expected Result

- This is structural -- confirmed by `ClinicalEventSourcingTests.HashChain_Compute_DiffersForDifferentPreviousHash`. No manual validation required for this scenario.

---

## Part C: Replay & Reconstruction

### Scenario 5: Replay Stream to a Specific Version

### Steps

1. Pick an intermediate version from Scenario 1 (e.g., `version = 3` of the 5 just-appended).
2. Replay:
   ```powershell
   Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/P1/clinical-events/replay?untilVersion=3" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   ```

### Expected Result

- Returns a snapshot of patient state as it was at version 3.
- The Allergy and Lab events (versions 1, 2) are present in the snapshot; Consult/Vital/MentalHealth (versions 4, 5) are **not**.
- This is the §170.315(d)(2) "replay to a point in time" guarantee.

### Scenario 6: Idempotent Append (Same eventId Twice)

### Steps

1. Capture an existing event's `eventId`.
2. Attempt to re-append the same `eventId` via the appropriate grain method (e.g., the federation inbound applier path -- not normally exposed to clinicians but reachable via `/api/federation/inbound`).
3. Re-query the stream.

### Expected Result

- Stream length **unchanged**; the duplicate `eventId` is a no-op (or returns "already applied").
- Cross-ref: `FederationInboundApplierTests.ApplyBatch_FreshEnvelopes_AllApplied_VersionAdvances` covers the deduplication path.

---

## Part D: Chain Verification

### Scenario 7: Verify Full Chain (Untampered)

### Steps

1. Call:
   ```powershell
   Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/P1/clinical-events/verify-chain" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   ```

### Expected Result

- Response: `{ "valid": true, "lastVerifiedVersion": <N>, "errors": [] }`.
- All versions from 1..N verified.

### Scenario 8: Detect Tampering (DESTRUCTIVE -- last test)

> **Warning:** This scenario writes directly to the SQL store to simulate
> tampering. Run only on a disposable test database. Restore from backup or
> re-seed demo data afterward.

### Steps

1. Locate the event row in SQL (table name varies by storage provider; for `AdoNetGrainStorage` it is `OrleansStorage`):
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasHub -Q "SELECT TOP 1 PayloadXml, PayloadBinary FROM OrleansStorage WHERE GrainTypeString LIKE '%PatientClinicalEventStreamGrain%' AND GrainIdString = 'P1'"
   ```
2. Manipulate one byte of the payload (an UPDATE that flips a value but does not regenerate hashes).
3. Re-call `/clinical-events/verify-chain`.

### Expected Result

- Response: `{ "valid": false, "lastVerifiedVersion": <K-1>, "errors": [ "Hash mismatch at version K" ] }`.
- The chain detects the tampering at the version where the recomputed hash no longer matches the stored hash.
- Restore the database from backup (or `TRUNCATE TABLE OrleansStorage` and re-seed) before further tests.

---

## Part E: Verification Checklist

- [ ] One event per clinical domain appends correctly (5 domains validated)
- [ ] Versions are monotonic with no gaps
- [ ] Each event has unique `eventId` and 64-char hex `hash`
- [ ] Filter by domain returns only that domain's events
- [ ] External SHA-256 computation matches the stored hash (deterministic)
- [ ] Replay to an intermediate version reconstructs correct snapshot
- [ ] Re-appending the same `eventId` is idempotent
- [ ] Full chain verification returns `valid: true` on untampered stream
- [ ] Chain verification returns `valid: false` after deliberate SQL tampering
- [ ] Database restored to a clean state after tampering test

---

## Cross-References

- Grain interface: [IPatientClinicalEventStreamGrain.cs](../../../../NewVistas.Abstractions/GrainInterfaces/IPatientClinicalEventStreamGrain.cs)
- Implementation: [PatientClinicalEventStreamGrain.cs](../../../../NewVistas.Abstractions/Grains/PatientClinicalEventStreamGrain.cs)
- Envelope: [EventEnvelope.cs](../../../../NewVistas.Abstractions/Events/EventEnvelope.cs)
- Domain event types: [NewVistas.Abstractions/Events/Clinical/](../../../../NewVistas.Abstractions/Events/Clinical/)
- Snapshot: [PatientStateSnapshot.cs](../../../../NewVistas.Abstractions/GrainStates/PatientStateSnapshot.cs)
- Functional / unit tests:
  - `ClinicalEventSourcingTests.HashChain_Compute_IsDeterministic`
  - `ClinicalEventSourcingTests.HashChain_Compute_DiffersForDifferentPreviousHash`
  - `ClinicalEventSourcingTests.Append_AssignsHashChainAndIncrementsVersion`
  - `ClinicalEventReplicationSinkTests.Append_FreshEvent_InvokesSinkOnce_WithSealedEnvelope`
- Regulatory anchor: ONC §170.315(d)(2) -- Auditable Events and Tamper-Resistance
