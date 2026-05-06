# NDW (National Data Warehouse) Export -- Data-Warehouse Coordinator Human Test Script

**Purpose:** Verify the IHS National Data Warehouse export lifecycle end-to-end:
- A coordinator selects a facility + reporting period and packages the
  export. The system selects the patient cohort via the registered
  `INdwExportSourceProvider` (default: every patient in the cluster's index)
  and runs the registered `INdwExportFormatter` (default: per-domain CSVs)
  to write the files to disk
- The lifecycle moves Pending → Packaged → Submitted → Accepted/Rejected,
  same shape as the GPRA submission workflow
- Per-attempt subdirectories preserve prior packaging runs for audit
- Auth gate (`CanSubmitNdw` security key) restricts packaging and
  transmission recording to the coordinator
- Same architecture as the GPRA submission packager (script 12) — pluggable
  formatter so deployments swap in the authoritative IHS NDW spec when
  available without touching grain code

The default CSV formatter writes three files per attempt:
`patients.csv`, `problems.csv`, `immunizations.csv`. Encounters, labs, and
pharmacy are deferred to the next iteration once the IHS NDW spec is in
hand.

---

## Prerequisites

- **Login (NDW coordinator):** an `ADMIN`-roled or `NdwCoordinator`-roled
  user holding the `CanSubmitNdw` security key. For the demo, `ADMIN1` is
  granted the key.
- **Login (negative test, no key):** `DOCTOR1` / Password: `smythVista1`
- **Site profile:** any profile with a registered patient population. The
  `IhsTribalSiteProfile` works; so does any profile after the tribal demo
  data has been loaded via [Admin/13-Tribal-Demo-Data.md](13-Tribal-Demo-Data.md).
- **Output directory:** a writable directory the silo can reach. Real
  deployments use a shared filesystem; the demo can use
  `C:\NewVistas-NdwExports\` (Windows) or `/var/lib/newvistas/ndw/`.

---

## Part A: Package an Export Run

### Scenario 1: Coordinator packages an FY2026 Q1 NDW export

### Steps

1. Optionally load the tribal demo data first (so there's a meaningful cohort) — see [Admin/13-Tribal-Demo-Data.md](13-Tribal-Demo-Data.md) Scenario 1.
2. From a small operator harness or the (eventual) Blazor admin "NDW Exports" page:
   ```csharp
   string runId = $"ndw-fy2026-q1-{Guid.NewGuid():N}";
   INdwExportRunGrain run = grainFactory.GetGrain<INdwExportRunGrain>($"NDW-EXPORT:{runId}");

   NdwExportRunState state = await run.PackageAsync(
       facilityId: "TRIBAL-HUB",
       periodStart: new DateTime(2026, 1, 1),
       periodEnd: new DateTime(2026, 3, 31),
       outputDirectory: @"C:\NewVistas-NdwExports",
       packagedById: "ADMIN1",
       packagedByName: "Data Warehouse Coordinator");
   ```

### Expected Result

- Returned `NdwExportRunState`:
  - `Status = Packaged`
  - `OutputDirectory` is a new subdirectory under the supplied directory, named like `ndw-TRIBAL-HUB-20260101-20260331-attempt01-...`
  - `Files` contains 3 entries: `patients.csv`, `problems.csv`, `immunizations.csv`
  - Each entry has `FileSizeBytes > 0` and a 64-char `Sha256` digest
  - `PatientCount` reflects the cohort size (e.g., 50 if the tribal demo was loaded)
  - `FormatVersion = "csv-v1"`
  - `PackagingAttempts = 1`
- Files exist on disk in the run subdirectory.

### Scenario 2: Inspect the per-domain CSV files

### Steps

1. Open `patients.csv` from the run directory.
2. Open `problems.csv`.
3. Open `immunizations.csv`.

### Expected Result

- `patients.csv` first line: `Icn,Dfn,Name,Sex,DateOfBirth,SsnLast4,Veteran,PrimaryEligibilityCode,IsActive`
- One row per patient in the cohort; `PrimaryEligibilityCode` shows `IHS CHS`/`IHS DIRECT`/empty per the registered eligibility policy.
- `problems.csv` first line: `Icn,ProblemId,Diagnosis,DiagnosisCode,Status,DateRecorded`
- `immunizations.csv` first line: `Icn,ImmunizationId,ImmunizationName,CvxCode,EventDateTime,Series`
- Problems and immunizations files contain rows only for entries falling within the requested period (entries with no recorded date pass through; entries with a date outside the period are filtered).

---

## Part B: Lifecycle Transitions

### Scenario 3: Operator records IHS transmission

### Steps

1. After uploading the files to IHS NDW out-of-band, record:
   ```csharp
   await run.RecordTransmissionAsync(
       transmissionDate: DateTime.UtcNow,
       trackingId: "NDW-PORTAL-TRACK-12345");
   ```

### Expected Result

- `Status` transitions `Packaged → Submitted`.
- `TransmissionTrackingId` populated.

### Scenario 4: IHS NDW accepts

### Steps

1. ```csharp
   await run.RecordIhsResponseAsync(
       responseDate: DateTime.UtcNow,
       accepted: true,
       responseReceipt: "NDW ACCEPTED. Confirmation NDW-2026-09988.");
   ```

### Expected Result

- `Status = Accepted`, `IhsAccepted = true`.

### Scenario 5: IHS NDW rejects → re-package and re-submit

### Steps

1. On a separate run, simulate rejection:
   ```csharp
   await run.RecordIhsResponseAsync(DateTime.UtcNow, accepted: false,
       responseReceipt: "REJECTED: patients.csv row 5 — invalid SSN format.");
   ```
2. Operator fixes the offending data, then re-packages:
   ```csharp
   await run.PackageAsync(facilityId, periodStart, periodEnd, outputDirectory, ...);
   ```

### Expected Result

- After rejection: `Status = Rejected`.
- After re-package: `Status = Packaged`, `PackagingAttempts = 2`. **A new attempt subdirectory** appears (e.g., `...attempt02-...`). **The original `attempt01` subdirectory is preserved** (audit trail). Re-record transmission and acceptance to walk to a successful outcome.

---

## Part C: State-Machine Guards

### Scenario 6: Cannot record transmission before packaging

### Steps

1. On a fresh run grain, call `RecordTransmissionAsync` directly.

### Expected Result

- Throws `InvalidOperationException` ("Cannot record transmission from status Pending; package the export first.").

### Scenario 7: Cannot record IHS response before transmission

### Steps

1. Package, but skip `RecordTransmissionAsync`. Try `RecordIhsResponseAsync`.

### Expected Result

- Throws `InvalidOperationException` ("Cannot record IHS response from status Packaged; record transmission first.").

### Scenario 8: Period end before start is rejected

### Steps

1. Call `PackageAsync` with `periodEnd < periodStart`.

### Expected Result

- Throws `ArgumentException` ("periodEnd must be >= periodStart.").

---

## Part D: Auth Gate

### Scenario 9: Non-coordinator cannot package

### Steps

1. As `DOCTOR1` (no `CanSubmitNdw` key), attempt `PackageAsync`.

### Expected Result

- `UnauthorizedAccessException` from `AuthorizationCallFilter`.
- No files written; run state unchanged.

---

## Part E: Verification Checklist

- [ ] Coordinator can package a run; per-domain CSV files appear in a per-attempt subdirectory
- [ ] Subdirectory name pattern: `ndw-{facility}-{startYYYYMMDD}-{endYYYYMMDD}-attempt{NN}-{timestamp}`
- [ ] `patients.csv` has expected header + one row per patient in the cohort
- [ ] `problems.csv` and `immunizations.csv` filter to entries within the requested period
- [ ] Each output file's `FileSizeBytes` and `Sha256` digest is recorded on the run state
- [ ] `RecordTransmissionAsync` transitions Packaged → Submitted with optional tracking id
- [ ] `RecordIhsResponseAsync(accepted=true)` transitions to Accepted; `accepted=false` to Rejected
- [ ] Re-package after Rejected produces a NEW subdirectory (preserves the prior one for audit) and increments `PackagingAttempts`
- [ ] State-machine guards reject out-of-order calls (transmit-before-package, response-before-transmit)
- [ ] `periodEnd < periodStart` rejected
- [ ] Empty `facilityId` rejected
- [ ] Non-coordinator (no `CanSubmitNdw` key) is rejected by the auth filter
- [ ] Audit log records each operator action via `[AuditAction]`

---

## Cross-References

- Grain interface: [`INdwExportRunGrain.cs`](../../../../GrainInterfaces/INdwExportRunGrain.cs)
- Implementation: [`NdwExportRunGrain.cs`](../../../../Grains/NdwExportRunGrain.cs)
- State: [`NdwExportRunState.cs`](../../../../GrainStates/NdwExportRunState.cs)
- Formatter: [`INdwExportFormatter.cs`](../../../../Reporting/INdwExportFormatter.cs), [`CsvNdwExportFormatter.cs`](../../../../Reporting/CsvNdwExportFormatter.cs)
- Source provider: [`INdwExportSourceProvider.cs`](../../../../Reporting/INdwExportSourceProvider.cs), [`PatientIndexNdwExportSourceProvider.cs`](../../../../Reporting/PatientIndexNdwExportSourceProvider.cs)
- Security key: [`SecurityKeys.cs`](../../../../Security/SecurityKeys.cs) `CanSubmitNdw`
- Functional tests: `NdwExportTests` (11 tests — packaging, file write, lifecycle, re-package, state-machine guards)
- Sister workflow: [`Admin/12-GPRA-Submission.md`](12-GPRA-Submission.md) — same architectural pattern, different content surface

> **Note:** the default per-domain CSV format (3 files: patients/problems/immunizations) is a stand-in. To match the authoritative IHS NDW spec — which covers more domains (encounters, labs, pharmacy, procedures), uses fixed-width or pipe-delimited records, and may require accompanying control files — register a spec-conformant `INdwExportFormatter` in DI before `AddCommonSiloServices` is called. Same for the source provider when active-user-by-encounter filtering is needed. The grain, lifecycle, and tests do not change.
