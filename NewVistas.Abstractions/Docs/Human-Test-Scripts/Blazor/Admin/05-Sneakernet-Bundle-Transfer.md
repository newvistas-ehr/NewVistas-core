# Sneakernet Bundle Transfer -- Administrator Human Test Script

**Purpose:** Verify offline ("sneakernet") federation: a clinical event is
appended on the Spoke, exported to a JSON bundle on disk, manually copied to
the Hub's inbound directory (simulating USB transfer or store-and-forward),
and applied to the Hub's per-patient event stream by the
`FileBundleInboundService`.

This script tests commit **3b747a17** (Sneakernet Federation).

---

## Prerequisites

- **Login:** `ADMIN1` (for Hub admin tasks); `DOCTOR1` on the Spoke for clinical activity.
- **Pre-conditions:**
  1. Two-silo Hub + Spoke environment from [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md) running.
  2. Both silos configured with `Federation:FileBundle` directories (see config in script 00).
  3. Spoke configured with `Transport:Type = "FileBundle"` (override the Http transport for this test):
     ```json
     "Federation": {
       "Transport": {
         "Type": "FileBundle"
       }
     }
     ```
     Restart Spoke to apply.
  4. Hub `Federation:FileBundle:ScanIntervalSeconds` set to `15` (so test runs quickly).

---

## Part A: Generate Activity on Spoke

### Scenario 1: Append Clinical Events on Spoke

### Steps

1. On the Spoke BlazorWeb (`https://localhost:8137`), login as `DOCTOR1`.
2. Select patient `P1` (or any seeded patient).
3. Place 2 clinical actions:
   - Add a problem (per [Blazor/Doctors/05-Problem-List.md](../Doctors/05-Problem-List.md))
   - Document an allergy (per [Blazor/Doctors/14-Allergy-Documentation.md](../Doctors/14-Allergy-Documentation.md))
4. Watch the Spoke silo log.

### Expected Result

- Two clinical events appended to the patient's event stream.
- Spoke log shows `ClinicalEventReplicationSink` invoked for each event.
- Spoke SQL outbox table now has 2 pending rows:
  ```powershell
  sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -Q "SELECT COUNT(*) FROM FederationOutbox WHERE Status = 'Pending'"
  ```

---

## Part B: Export Bundle to Disk

### Scenario 2: Outbox Drainer Writes Bundle File

### Steps

1. Wait `CheckIntervalSeconds + ScanIntervalSeconds + 5` (~30 seconds for default test config).
2. Inspect the Spoke's outbound directory:
   ```powershell
   Get-ChildItem C:\NewVistas-Federation\Spoke\Outbound | Format-Table Name, Length, LastWriteTime
   ```
3. Open one of the JSON files in a text editor (or `Get-Content -Raw <file> | ConvertFrom-Json`).

### Expected Result

- One or more `.json` (or `.bundle.json`) files appear in `C:\NewVistas-Federation\Spoke\Outbound\`.
- File contents are valid JSON containing an array of `EventEnvelope` objects with fields:
  - `eventId`, `patientId`, `domain` (Allergy, Problem, etc.), `payload`, `version`, `previousHash`, `hash`, `sourceClusterId = "SPOKE-TEST-1"`, `appendedUtc`.
- Spoke SQL outbox rows for these events transitioned from `Pending` to `Sent` (the FileBundle transport considers a successful file write as "sent").
- Cross-ref: `FileBundleTransportTests.Send_WritesBundleFileToOutboundDirectory`, `FileBundleTransportTests.Send_TwoBatches_ProduceDistinctFilenames`.

### Scenario 3: Empty Batch Does Not Create File

### Steps

1. Truncate any pending outbox rows on the Spoke (none should exist after Scenario 2 -- but verify):
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -Q "SELECT COUNT(*) FROM FederationOutbox WHERE Status = 'Pending'"
   ```
2. Note the count of files in `Outbound\`.
3. Wait one drainer interval.
4. Re-count the files.

### Expected Result

- File count unchanged (no zero-event bundle written).
- Cross-ref: `FileBundleTransportTests.Send_EmptyBatch_DoesNotWriteFile`.

---

## Part C: Sneakernet Transfer (Manual Copy)

### Scenario 4: Copy Bundle to Hub Inbound

### Steps

1. Simulate offline transfer (USB drive):
   ```powershell
   $bundles = Get-ChildItem C:\NewVistas-Federation\Spoke\Outbound\*.json
   foreach ($b in $bundles) {
     Copy-Item $b.FullName C:\NewVistas-Federation\Transfer\
     Move-Item $b.FullName "$($b.FullName).archived"  # Spoke archives its own copy
   }
   "Bundles staged on transfer media:"
   Get-ChildItem C:\NewVistas-Federation\Transfer
   ```
2. Now move from "USB" to Hub inbound:
   ```powershell
   Move-Item C:\NewVistas-Federation\Transfer\*.json C:\NewVistas-Federation\Hub\Inbound\
   ```

### Expected Result

- Bundle files now sit in `C:\NewVistas-Federation\Hub\Inbound\`.
- Spoke's outbound directory no longer contains active `.json` (only `.archived`).

---

## Part D: Hub Inbound Apply

### Scenario 5: Hub Picks Up & Applies Bundles

### Steps

1. Wait one Hub `ScanIntervalSeconds` (~15s).
2. Watch Hub silo log.
3. Inspect directories:
   ```powershell
   "Inbound:"
   Get-ChildItem C:\NewVistas-Federation\Hub\Inbound
   "Processed:"
   Get-ChildItem C:\NewVistas-Federation\Hub\Processed
   ```
4. Verify the Hub now holds the events for patient P1 -- login as `DOCTOR1` on the **Hub** BlazorWeb (`https://localhost:7137`), select P1, and view problem list / allergies.

### Expected Result

- Hub log shows `FileBundleInboundService` reading each bundle, calling `IFederationInboundApplier`, and reporting success.
- Inbound directory is **empty**; processed directory contains the bundles.
- Hub patient P1 now displays the problem and allergy added on the Spoke.
- Cross-ref: `FileBundleInboundServiceTests.Process_ValidBundle_AppliesAndMovesToProcessed`.
- Cross-ref: `FederationInboundApplierTests.ApplyBatch_FreshEnvelopes_AllApplied_VersionAdvances`.

### Scenario 6: Idempotent Reapplication

### Steps

1. Copy the same processed bundle back to the Hub's inbound:
   ```powershell
   Copy-Item C:\NewVistas-Federation\Hub\Processed\*.json C:\NewVistas-Federation\Hub\Inbound\
   ```
2. Wait one scan interval.
3. View patient P1 again on the Hub.

### Expected Result

- Bundle moves to `Processed\` again.
- **No duplicate problems or allergies** appear on patient P1 (envelope hashes already exist; deduplication kicks in).
- Hub log notes "applied: 0, skipped (already present): 2" or equivalent.

### Scenario 7: Malformed Bundle Quarantined

### Steps

1. Create a junk file:
   ```powershell
   "this is not valid json" | Set-Content C:\NewVistas-Federation\Hub\Inbound\bad-bundle.json
   ```
2. Wait one scan interval.

### Expected Result

- Bundle is moved to a `Failed` (or `Quarantine`) subdirectory under inbound or processed.
- Hub log emits an error noting the parse failure.
- Other valid bundles in the same scan continue to be processed.
- Cross-ref: `FileBundleInboundServiceTests.Process_MalformedJson_MovesToFailedSubdirectory`.

### Scenario 8: Application Failure Leaves Bundle for Retry

### Steps

1. Hand-craft a bundle with valid JSON shape but a deliberately-broken envelope (e.g., bad hash chain):
   ```powershell
   '[{"eventId":"00000000-0000-0000-0000-000000000001","patientId":"P1","domain":"Allergy","payload":{},"version":99999,"previousHash":"deadbeef","hash":"badhash","sourceClusterId":"SPOKE-TEST-1","appendedUtc":"2026-04-27T12:00:00Z"}]' |
     Set-Content C:\NewVistas-Federation\Hub\Inbound\broken-chain.json
   ```
2. Wait one scan interval.

### Expected Result

- Applier reports errors; bundle remains in **inbound** for retry (does **not** silently move to processed).
- Cross-ref: `FileBundleInboundServiceTests.Process_ApplierReportsErrors_BundleStaysInInbound`.

### Scenario 9: Multiple Bundles Processed in Sorted Order

### Steps

1. Drop several timestamped bundles into inbound:
   ```powershell
   for ($i = 1; $i -le 3; $i++) {
     Copy-Item C:\NewVistas-Federation\Hub\Processed\*.json `
       "C:\NewVistas-Federation\Hub\Inbound\bundle-2026-04-27T12-00-0$i.json"
     Start-Sleep -Milliseconds 100
   }
   ```
2. Wait one scan interval.
3. Watch Hub log for the order of "Processing bundle: bundle-..." log lines.

### Expected Result

- Bundles processed in **filename-sorted** (and therefore time-sorted) order.
- Cross-ref: `FileBundleInboundServiceTests.Process_MultipleBundles_ProcessedInSortedOrder`.

---

## Part E: Verification Checklist

- [ ] Spoke clinical activity emits events to outbox
- [ ] FileBundle transport writes JSON bundle(s) to Spoke `Outbound\`
- [ ] Bundle JSON contains valid `EventEnvelope` objects with `sourceClusterId = "SPOKE-TEST-1"`
- [ ] Empty batch does **not** create a zero-event bundle file
- [ ] Manual copy to Hub `Inbound\` simulates USB transfer
- [ ] Hub `FileBundleInboundService` picks up bundles within scan interval
- [ ] Hub applies events; bundles move to `Processed\`
- [ ] Patient state on Hub matches patient state on Spoke after transfer
- [ ] Re-applying the same bundle is idempotent (no duplicates)
- [ ] Malformed JSON bundles are quarantined (not lost, not silently dropped)
- [ ] Apply failures leave bundle in inbound for retry
- [ ] Multiple bundles processed in sorted filename order

---

## Cross-References

- Inbound service: [FileBundleInboundService.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/FileBundleInboundService.cs)
- Outbound transport: [FileBundleFederationTransport.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/FileBundleFederationTransport.cs)
- Inbound applier: [FederationInboundApplier.cs](../../../../NewVistas.Abstractions/Federation/FederationInboundApplier.cs)
- Functional tests:
  - `FileBundleTransportTests.Send_WritesBundleFileToOutboundDirectory`
  - `FileBundleTransportTests.Send_EmptyBatch_DoesNotWriteFile`
  - `FileBundleTransportTests.Send_DirectoryDoesNotExist_CreatedAutomatically`
  - `FileBundleTransportTests.Send_TwoBatches_ProduceDistinctFilenames`
  - `FileBundleInboundServiceTests.Process_ValidBundle_AppliesAndMovesToProcessed`
  - `FileBundleInboundServiceTests.Process_ApplierReportsErrors_BundleStaysInInbound`
  - `FileBundleInboundServiceTests.Process_MalformedJson_MovesToFailedSubdirectory`
  - `FileBundleInboundServiceTests.Process_MultipleBundles_ProcessedInSortedOrder`
  - `FederationInboundApplierTests.ApplyBatch_FreshEnvelopes_AllApplied_VersionAdvances`
