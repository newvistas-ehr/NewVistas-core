# Spoke Certificate Renewal -- Administrator Human Test Script

**Purpose:** Verify the Spoke's `CertificateRenewalService` proactively renews
its federation cert as it approaches expiry, performs an atomic file swap,
backs up the prior cert, and refuses to install a cert whose CN does not
match the spoke's cluster ID.

This script tests commit **03ad1887** (Certificate Renewal Service).

---

## Prerequisites

- **Pre-conditions:**
  1. [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md) and [02-Hub-CA-Spoke-Onboarding.md](02-Hub-CA-Spoke-Onboarding.md) completed -- Spoke holds a valid cert at `C:\NewVistas-Federation\Spoke\Certs\spoke.pfx`.
  2. Spoke `appsettings.Development.json` includes:
     ```json
     "Federation": {
       "Renewal": {
         "Enabled": true,
         "CheckIntervalHours": 1,
         "RenewBeforeDaysExpire": 350,
         "HubUrl": "https://localhost:7127",
         "CertificatePath": "C:\\NewVistas-Federation\\Spoke\\Certs\\spoke.pfx",
         "BackupDirectory": "C:\\NewVistas-Federation\\Spoke\\Certs\\Backup"
       }
     }
     ```
     `RenewBeforeDaysExpire = 350` is intentionally aggressive so a fresh 365-day cert renews on first check.
  3. Hub silo running with `Federation:HubCa:Enabled = true`.
  4. Backup directory exists or will be created automatically: `C:\NewVistas-Federation\Spoke\Certs\Backup`

---

## Part A: Baseline Observation

### Scenario 1: Capture Pre-Renewal Cert State

### Steps

1. Record the current cert thumbprint and validity:
   ```powershell
   $before = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
     "C:\NewVistas-Federation\Spoke\Certs\spoke.pfx", "")
   "Thumbprint: $($before.Thumbprint)"
   "NotBefore:  $($before.NotBefore)"
   "NotAfter:   $($before.NotAfter)"
   "Days remaining: $([int]($before.NotAfter - (Get-Date)).TotalDays)"
   ```
2. List the backup directory:
   ```powershell
   Get-ChildItem C:\NewVistas-Federation\Spoke\Certs\Backup -ErrorAction SilentlyContinue
   ```

### Expected Result

- Thumbprint captured (40-char hex).
- NotAfter is ~365 days out.
- Days remaining `> 350` means renewal will trigger on the next check.
- Backup directory is empty (first-run condition).

---

## Part B: Triggering Renewal

### Scenario 2: Spoke Starts and Renews on First Check

### Steps

1. Restart the Spoke silo so the renewal service starts fresh:
   ```powershell
   # In the Spoke SiloHost terminal: Ctrl+C, then:
   dotnet run --project NewVistas.SiloHost -- --use-sqlexpress --profile RemoteOnline
   ```
2. Watch the Spoke silo log for the renewal service:
   - On startup, `CertificateRenewalService` should log "Starting" and immediately perform a check (because `CheckIntervalHours = 1` does not delay the first run).
3. Within ~30 seconds, look for one of:
   - `Certificate renewal not needed; days remaining = 365` (if cert is fresh enough)
   - `Renewing certificate; days remaining = 365 < threshold 350` followed by success
4. If "not needed", **temporarily** lower `RenewBeforeDaysExpire` to `400` in `appsettings.Development.json` (so any cert under 400 days remaining renews) and restart the Spoke.

### Expected Result

- Spoke log emits a renewal cycle: request submitted to Hub at `https://localhost:7127`, response received, file swap performed.
- Hub log shows a CSR signed for `SPOKE-TEST-1` (this counts as a renewal CSR).
- No exceptions thrown.

### Scenario 3: Atomic File Swap & Backup

### Steps

1. After the renewal in Scenario 2 completes:
   ```powershell
   $after = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
     "C:\NewVistas-Federation\Spoke\Certs\spoke.pfx", "")
   "New thumbprint:   $($after.Thumbprint)"
   "Old thumbprint:   $($before.Thumbprint)"
   "Same?             $($after.Thumbprint -eq $before.Thumbprint)"
   "New NotAfter:     $($after.NotAfter)"
   ```
2. Inspect the backup directory:
   ```powershell
   Get-ChildItem C:\NewVistas-Federation\Spoke\Certs\Backup | Sort-Object LastWriteTime -Descending
   ```

### Expected Result

- New thumbprint **differs** from the pre-renewal thumbprint (a fresh keypair was issued).
- New `NotAfter` is ~365 days from now (further in the future than the original).
- Backup directory contains a file named like `spoke.pfx.backup-2026-04-27T17-30-12Z` (timestamped) with a size matching the original.
- The live cert and the backup file are **different** files (atomic swap completed).
- Cross-ref: `CertificateRenewalServiceTests.RenewalDue_SwapsFileAndBacksUpPrevious`, `CertificateRenewalServiceTests.AtomicSwap_RenamesNewOverLive_AndBacksUpPrevious`.

### Scenario 4: Idempotent Subsequent Check

### Steps

1. Edit Spoke `appsettings.Development.json`: set `RenewBeforeDaysExpire` back to `30` (normal) and restart Spoke.
2. Wait one check interval (or shorten to a minute for testing).

### Expected Result

- Spoke log emits `Certificate renewal not needed; days remaining = 365`.
- Live cert and backup directory are **unchanged** (same thumbprints, same files).
- Cross-ref: `CertificateRenewalServiceTests.NoRenewalNeeded_LeavesFileUntouched`.

---

## Part C: Failure Modes

### Scenario 5: Hub Unreachable -- No Damage

### Steps

1. Force a renewal: edit `RenewBeforeDaysExpire` to `400` in Spoke config.
2. **Stop the Hub silo** (Ctrl+C its three processes).
3. Restart the Spoke silo.

### Expected Result

- Spoke log: "Renewal attempt failed: connection refused" (or similar) -- **logged**, not thrown to crash the silo.
- Live `spoke.pfx` is **unchanged** (same thumbprint as before).
- Backup directory is **unchanged** (no spurious backup files).
- Cross-ref: `CertificateRenewalServiceTests.RenewalDue_CaFails_LeavesFileUntouched`.

### Scenario 6: Hub Returns Wrong CN -- Refused

### Steps

1. **For testing only**, intentionally misconfigure: change Spoke's `ClusterIdentity:ClusterId` from `SPOKE-TEST-1` to `SPOKE-TEST-2` in `appsettings.Development.json` but **leave** `Federation:HubCa:AllowedClusterIds` on the Hub still listing both. The Spoke will request a CSR with CN=SPOKE-TEST-2 but its previous cert (Subject=SPOKE-TEST-1) still loads.
2. Force a renewal (same as Scenario 5 but with Hub running again).

> Alternative if the above is too fiddly: hand-craft a CSR with CN=SPOKE-WRONG, sign it on the Hub, and have the spoke's renewal code attempt to install it.

### Expected Result

- Spoke detects CN mismatch (returned cert CN ≠ spoke's clusterId).
- Spoke log: "Refusing to install renewed certificate: CN mismatch (got 'SPOKE-WRONG', expected 'SPOKE-TEST-1')".
- Live `spoke.pfx` is **unchanged**.
- Cross-ref: `CertificateRenewalServiceTests.RenewalDue_HubReturnsWrongCn_RefusesToInstall`.
- Restore Spoke's config to `SPOKE-TEST-1` after this test.

### Scenario 7: No Cert File Yet (Bootstrap Edge Case)

### Steps

1. Move the live cert temporarily:
   ```powershell
   Move-Item C:\NewVistas-Federation\Spoke\Certs\spoke.pfx `
             C:\NewVistas-Federation\Spoke\Certs\spoke.pfx.parked
   ```
2. Restart the Spoke silo.

### Expected Result

- Spoke log: "No certificate file found at C:\\NewVistas-Federation\\Spoke\\Certs\\spoke.pfx; renewal service idle until bootstrap".
- Silo continues to start (renewal service does **not** crash other startup work).
- Cross-ref: `CertificateRenewalServiceTests.NoCertFile_LogsAndExitsCleanly`.
- Restore the parked file: `Move-Item ... .parked ... .pfx` and restart.

---

## Part D: Verification Checklist

- [ ] Pre-renewal cert thumbprint captured
- [ ] Renewal triggers on first check when `RenewBeforeDaysExpire > daysRemaining`
- [ ] Live `spoke.pfx` is replaced (new thumbprint, new NotAfter)
- [ ] Previous cert is preserved in `Backup\` with timestamped filename
- [ ] Renewal does **not** repeat on subsequent checks when not needed
- [ ] Hub-down: renewal logs failure, leaves cert and backups untouched
- [ ] CN mismatch: renewal logs refusal, leaves cert untouched
- [ ] Missing cert file: service logs and stays idle (no crash)
- [ ] No exceptions in spoke silo log during any of the above scenarios
- [ ] Hub silo log shows CSR signed events for each successful renewal

---

## Cross-References

- Service: [CertificateRenewalService.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/CertificateRenewalService.cs)
- CA client: [CertificateAuthorityClient.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/CertificateAuthorityClient.cs)
- Cert bundle: [CertificateBundle.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/CertificateBundle.cs)
- Options: [RenewalOptions.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/RenewalOptions.cs)
- Functional tests:
  - `CertificateRenewalServiceTests.NoRenewalNeeded_LeavesFileUntouched`
  - `CertificateRenewalServiceTests.RenewalDue_SwapsFileAndBacksUpPrevious`
  - `CertificateRenewalServiceTests.RenewalDue_CaFails_LeavesFileUntouched`
  - `CertificateRenewalServiceTests.RenewalDue_HubReturnsWrongCn_RefusesToInstall`
  - `CertificateRenewalServiceTests.NoCertFile_LogsAndExitsCleanly`
  - `CertificateRenewalServiceTests.AtomicSwap_RenamesNewOverLive_AndBacksUpPrevious`
