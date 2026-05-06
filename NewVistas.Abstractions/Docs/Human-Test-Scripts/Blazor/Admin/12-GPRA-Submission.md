# GPRA Submission Packaging -- Quality Coordinator Human Test Script

**Purpose:** Verify the GPRA submission lifecycle end-to-end:
- A completed `IGpraReportGrain` (built by the existing GPRA reporting
  workflow) can be packaged into a submission file by a quality coordinator
- The submission state tracks the file path, format version, SHA-256, and
  packaging-attempt count
- Operator-recorded transmission and IHS response transitions move the state
  through Packaged → Submitted → Accepted (or Rejected)
- A rejected submission can be re-packaged and re-submitted without losing
  the original audit trail
- Auth gate (`CanSubmitGpra` security key) restricts packaging and
  transmission recording to the designated coordinator

This is the bridge between NewVistas's existing GPRA computation
(`IGpraReportGrain`) and IHS national-office transmission. The default
formatter is CSV; deployments with the authoritative IHS GPRA+ submission
spec register their own `IGpraSubmissionFormatter` in DI.

---

## Prerequisites

- **Login (quality coordinator):** an `ADMIN`-roled or `QualityCoordinator`-roled
  user holding the `CanSubmitGpra` security key. For the demo, `ADMIN1` and
  `QM1` are granted the key.
- **Login (negative test, no key):** `DOCTOR1` / Password: `smythVista1`
- **Site profile:** `IhsTribalSiteProfile` (or any profile with
  `GPRA_REPORTING` enabled).
- **Submission output directory:** a writable directory the silo can reach.
  Real deployments use a shared filesystem; the demo can use
  `C:\NewVistas-GpraSubmissions\` (Windows) or `/var/lib/newvistas/gpra/`.
- **A completed GPRA report:** an `IGpraReportGrain` in `Completed` status
  with at least one indicator. Build via the existing GPRA reporting page
  or the API:
  ```powershell
  # Out of scope for this script — see the GPRA Reporting test script for
  # how to build a report. This script assumes one already exists with
  # ReportId = "fy2026-q1-tribal-hub" (suffix only — full grain key is
  # "GPRA-REPORT:fy2026-q1-tribal-hub").
  ```

---

## Part A: Package the Submission

### Scenario 1: Quality coordinator packages a completed report

### Steps

1. Get an admin JWT:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "QM1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $jwt = $login.token
   ```
2. From a small test harness (or the Blazor "GPRA Submissions" admin page,
   when built), call the workflow:
   ```
   await Submission("fy2026-q1-tribal-hub").PackageAsync(
       reportId: "fy2026-q1-tribal-hub",
       outputDirectory: "C:\\NewVistas-GpraSubmissions",
       packagedById: "QM1",
       packagedByName: "Quality Coordinator");
   ```
   (The grain interface is `IGpraSubmissionGrain`; key is `"GPRA-SUB:fy2026-q1-tribal-hub"`.
   No REST endpoint exists by design — Blazor invokes the grain directly via
   `OrleansGrainService`, which propagates the JWT identity through Orleans
   `RequestContext` and the grain-side `AuthorizationCallFilter` enforces
   the `CanSubmitGpra` gate.)
3. Inspect the result.

### Expected Result

- Returned `GpraSubmissionState`:
  - `Status = Packaged`
  - `FilePath` is non-null and points at a file in the output directory
  - `FormatVersion = "csv-v1"` (default formatter)
  - `FileSizeBytes > 0`
  - `FileSha256` is a 64-char lowercase hex string
  - `PackagingAttempts = 1`
  - `PackagedById = "QM1"`, `PackagedDate ≈ now`
- File on disk exists; filename pattern: `gpra-fy2026-Quarter1-TRIBAL-HUB-attempt01-<timestamp>.csv`

### Scenario 2: Inspect the packaged file contents

### Steps

1. Open the file in any text editor or:
   ```powershell
   Get-Content $submission.FilePath | Select-Object -First 20
   ```

### Expected Result

- Top of file: `# GPRA Submission File (csv-v1)` with a `# Key,Value` header section listing FiscalYear, FacilityId, FacilityName, ActiveUserPopulation, IndicatorCount, period dates.
- Below the header section: a column-name row `MeasureId,Title,Category,CurrentDenominator,CurrentNumerator,...`
- Below that: one CSV row per indicator with current/baseline counts, percentage-point change, Improved (Y/N), TargetMet (Y/N).

---

## Part B: Record IHS Transmission

> **Note:** IHS national-office submission is performed out-of-band (IHS
> Portal upload, FTP, or whatever channel the IHS-coordination calendar
> item established). The system records that the upload happened, not the
> upload itself.

### Scenario 3: Operator records the file as transmitted

### Steps

1. Operator uploads the file to the IHS portal manually and records the
   tracking number IHS returns.
2. Call:
   ```
   await Submission("fy2026-q1-tribal-hub").RecordTransmissionAsync(
       transmissionDate: <now>,
       trackingId: "IHS-PORTAL-RECEIPT-12345");
   ```

### Expected Result

- `Status` transitions from `Packaged` to `Submitted`.
- `TransmissionDate` and `TransmissionTrackingId` populated.

### Scenario 4: Recording transmission before packaging is rejected

### Steps

1. On a fresh `IGpraSubmissionGrain` (no `PackageAsync` yet), call
   `RecordTransmissionAsync` directly.

### Expected Result

- Throws `InvalidOperationException`: "Cannot record transmission from status Pending; package the submission first."

---

## Part C: Record IHS Response

### Scenario 5: IHS accepts the submission

### Steps

1. After IHS returns acceptance (typically within a day or two), call:
   ```
   await Submission("fy2026-q1-tribal-hub").RecordIhsResponseAsync(
       responseDate: <now>,
       accepted: true,
       responseReceipt: "ACCEPTED. Confirmation 99887. All indicators valid.");
   ```

### Expected Result

- `Status` transitions from `Submitted` to `Accepted`.
- `IhsAccepted = true`, `IhsResponseReceipt` captures verbatim text, `IhsResponseDate` populated.

### Scenario 6: IHS rejects the submission, operator re-packages

### Steps

1. Pretend IHS rejected (validation error). Record:
   ```
   await Submission("fy2026-q1-tribal-hub").RecordIhsResponseAsync(
       responseDate: <now>,
       accepted: false,
       responseReceipt: "REJECTED: indicator GPRA-DM-01 numerator > denominator.");
   ```
2. Operator fixes whatever IHS flagged in the source data, then re-packages:
   ```
   await Submission("fy2026-q1-tribal-hub").PackageAsync(
       reportId: "fy2026-q1-tribal-hub",
       outputDirectory: "C:\\NewVistas-GpraSubmissions",
       packagedById: "QM1",
       packagedByName: "Quality Coordinator");
   ```

### Expected Result

- After response: `Status = Rejected`, `IhsAccepted = false`.
- After re-package: `Status = Packaged`, `PackagingAttempts = 2`. **A new file on disk** with `attempt02` in the filename. **The original `attempt01` file is preserved** (audit trail of the rejected submission). Re-record transmission and acceptance to walk the lifecycle to a successful outcome.

---

## Part D: Auth Gate

### Scenario 7: Non-coordinator cannot package

### Steps

1. Login as `DOCTOR1` (no `CanSubmitGpra` key).
2. Attempt `PackageAsync`.

### Expected Result

- Call rejected with `UnauthorizedAccessException` from the grain-side
  `AuthorizationCallFilter`.
- Submission state unchanged; no file written.
- Audit log records the denied attempt.

---

## Part E -- alt: WpfDelphiUI Reports-tab GPRA Section (CPRS-style frontend)

The WpfDelphiUI Reports tab now has a facility-wide GPRA section in
addition to the per-patient radiology list. Submission is still done via
the Blazor admin page or the API (Parts A--D above); the WPF surface is
**read-only** -- coordinators use it to drill into recently-built reports.

### Scenario: GPRA list appears beneath the Radiology list

### Steps

1. With at least one GPRA report built (Part A), launch `NewVistas.WpfDelphiUI`,
   log in as `DOCTOR1`, select any patient, click the **Reports** tab.

### Expected Result

- The tab splits horizontally with a draggable splitter:
  - **Top**: per-patient Radiology Reports.
  - **Bottom**: GPRA section, further split into:
    - **Left**: GPRA Reports list, columns FY / Period / Facility / Status
      / Indicator-count.
    - **Right**: indicator drilldown for the selected report.
- The Period and Status columns use the int → label converters
  (`ReportingPeriodLabelConverter`, `GpraStatusLabelConverter`).

### Scenario: Drill into a report's indicators

### Steps

1. Click a GPRA report in the bottom-left list.

### Expected Result

- The header above the right-side grid updates to
  "{Facility} -- FY{N} {Period} ({Status}, {N} indicators, AUP={N})".
- The right-side grid populates with that report's `Indicators`:
  Measure / Title / Category / Current % / Baseline % / Δ pp / Target Met.
- The Δ (pp) column is color-coded -- **green** for improved, **red** for not.
- The Category column uses `GpraCategoryLabelConverter` to show readable
  labels (Diabetes, CV, Women's Health, ...).

### Scenario: Empty state -- no GPRA reports

### Steps

1. On a fresh silo with no GPRA reports built yet, open the Reports tab.

### Expected Result

- Radiology list populates as expected (per-patient).
- GPRA list is empty -- no crash, no error strip. (`SafeGetGpraReportsAsync`
  swallows errors so a missing endpoint or empty index doesn't break the
  Reports tab load.)

### Scenario: Cross-patient invariance

### Steps

1. Switch the patient via `File → Select New Patient`.

### Expected Result

- The Radiology list reloads against the new patient.
- The GPRA list is **unchanged** -- GPRA reports are facility-scoped, not
  per-patient. (The WpfDelphiUI Reports view loads `api/gpra/reports` once
  on tab activation.)

---

## Part E: Verification Checklist

- [ ] Quality coordinator can package a completed GPRA report
- [ ] Packaged file exists on disk with the documented filename pattern
- [ ] Packaged file contains the `# header,value` section + column headers + one row per indicator
- [ ] `FormatVersion`, `FileSizeBytes`, and `FileSha256` populated on the submission state
- [ ] Operator can record transmission with optional tracking id
- [ ] Operator can record IHS acceptance
- [ ] Operator can record IHS rejection
- [ ] Re-packaging after rejection produces a NEW file (preserves prior file for audit)
- [ ] `PackagingAttempts` increments on each re-package
- [ ] State-machine guards: cannot record transmission before packaging; cannot record IHS response before transmission
- [ ] Non-coordinator (no `CanSubmitGpra` key) is rejected
- [ ] Audit log records each operator action via `[AuditAction]` on the grain methods
- [ ] WpfDelphiUI Reports tab shows the GPRA section beneath Radiology
- [ ] Selecting a GPRA report in the WpfDelphiUI populates the indicator drilldown
- [ ] Δ (pp) column color-codes green for improved, red for not
- [ ] GPRA list is facility-scoped (does not change when patient changes)

---

## Cross-References

- Grain interface: [`IGpraSubmissionGrain.cs`](../../../../GrainInterfaces/IGpraSubmissionGrain.cs)
- Implementation: [`GpraSubmissionGrain.cs`](../../../../Grains/GpraSubmissionGrain.cs)
- State: [`GpraSubmissionState.cs`](../../../../GrainStates/GpraSubmissionState.cs)
- Formatter: [`IGpraSubmissionFormatter.cs`](../../../../Reporting/IGpraSubmissionFormatter.cs), [`CsvGpraSubmissionFormatter.cs`](../../../../Reporting/CsvGpraSubmissionFormatter.cs)
- Source report: [`IGpraReportGrain.cs`](../../../../GrainInterfaces/IGpraReportGrain.cs)
- Security key: [`SecurityKeys.cs`](../../../../Security/SecurityKeys.cs) `CanSubmitGpra`
- Functional tests:
  - `CsvGpraSubmissionFormatterTests` (12 unit tests — format determinism, escaping, validation)
  - `GpraSubmissionTests` (10 functional tests — packaging, file write, lifecycle, re-package, state-machine guards)
- Architecture: tribal-deployment plan, IHS-coordination calendar item (NDW + GPRA spec acquisition)
- WpfDelphiUI Reports tab: [`ReportsView.xaml`](../../../../../NewVistas.WpfDelphiUI/Views/ReportsView.xaml), [`ReportsViewModel.cs`](../../../../../NewVistas.WpfDelphiUI/ViewModels/ReportsViewModel.cs); enum-int label converters in [`Converters/GpraEnumLabelConverters.cs`](../../../../../NewVistas.WpfDelphiUI/Converters/GpraEnumLabelConverters.cs)
- WpfDelphiUI walkthrough (smoke + IHS chart-tab features): [`WpfDelphiUI/Doctors/01-CPRS-Chart-Walkthrough.md`](../../WpfDelphiUI/Doctors/01-CPRS-Chart-Walkthrough.md)

> **Note:** the default CSV format is a stand-in. To match the authoritative
> IHS GPRA+ submission spec, register a spec-conformant
> `IGpraSubmissionFormatter` in DI (e.g., in `IhsTribalSiteProfile`) before
> `AddCommonSiloServices` is called. The grain, lifecycle, and tests do not
> change.
