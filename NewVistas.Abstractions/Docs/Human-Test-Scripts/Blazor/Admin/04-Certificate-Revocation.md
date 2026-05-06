# Certificate Revocation -- Administrator Human Test Script

**Purpose:** Verify that an admin can revoke an issued spoke certificate, that
the in-memory revocation cache picks up the revocation within its TTL, and
that subsequent inbound mTLS connections from the revoked spoke are rejected.

This script tests commit **878a6610** (Revocation Registry).

---

## Prerequisites

- **Login:** `ADMIN1` / Password: `smythVista1`
- **Pre-conditions:**
  1. Two-silo Hub + Spoke environment from [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md) up and running.
  2. Spoke onboarded successfully via [02-Hub-CA-Spoke-Onboarding.md](02-Hub-CA-Spoke-Onboarding.md) (you have a working `spoke.pfx` whose thumbprint you know).
  3. Hub config has revocation enabled with a short cache TTL for testing:
     ```json
     "Federation": {
       "Revocation": {
         "Enabled": true,
         "CacheTtlSeconds": 60
       }
     }
     ```

---

## Part A: Capture Cert Identity

### Scenario 1: Identify Cert Thumbprint

### Steps

1. From PowerShell:
   ```powershell
   $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
     "C:\NewVistas-Federation\Spoke\Certs\spoke.pfx", "")
   $thumbprint = $cert.Thumbprint
   "Thumbprint: $thumbprint"
   "Subject:    $($cert.Subject)"
   "Serial:     $($cert.SerialNumber)"
   ```
2. Note the thumbprint -- you will pass it to the revoke endpoint.

### Expected Result

- Thumbprint is a 40-char uppercase hex string.
- Subject = `CN=SPOKE-TEST-1, O=NewVistas Test, C=US`.

---

## Part B: Confirm Spoke Can Talk to Hub Pre-Revocation

### Scenario 2: Baseline Inbound Call Succeeds

### Steps

1. Use the spoke's cert to make any authenticated inbound call. Easiest is to trigger a federation event via clinical activity:
   - Login as `DOCTOR1` on the Spoke's BlazorWeb (`https://localhost:8137`), select a patient, place a lab order.
   - This appends a clinical event, which the Spoke's outbox drains and POSTs to the Hub via mTLS.
2. Watch the Hub WebServer log.

### Expected Result

- Hub log shows a successful inbound POST to `/api/federation/inbound/...` with client cert subject `CN=SPOKE-TEST-1`.
- No `RevokedCertificate` log entries.

---

## Part C: Revoke the Cert

### Scenario 3: Admin Revokes Spoke Cert

### Steps

1. As `ADMIN1`, get a JWT (per [02-Hub-CA-Spoke-Onboarding.md](02-Hub-CA-Spoke-Onboarding.md) Scenario 1).
2. Revoke:
   ```powershell
   $body = @{
     thumbprint = $thumbprint
     reason = "KeyCompromise"
     notes = "Test revocation -- Admin/04 Scenario 3"
   } | ConvertTo-Json
   Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/federation/admin/revoke `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body $body -ContentType "application/json"
   ```
3. Confirm via the listing endpoint:
   ```powershell
   Invoke-RestMethod -Method Get `
     -Uri https://localhost:7127/api/federation/admin/revocations `
     -Headers @{ Authorization = "Bearer $jwt" }
   ```

### Expected Result

- POST returns 200 (or 201) with the revocation record echoed back.
- GET returns a list containing the just-revoked cert with normalized thumbprint, reason `KeyCompromise`, recent `revokedUtc`.
- Cross-ref: `RevocationRegistryTests.Revoke_AddsToList_AndIsRevokedReturnsTrue`, `RevocationRegistryTests.Revoke_NormalizesThumbprintFormat`.

### Scenario 4: Revocation Visible on Federation Dashboard

### Steps

1. Browse to `https://localhost:7137/admin/federation` as `ADMIN1`.
2. Click **Refresh** in the Revoked certs panel (or wait 30s).

### Expected Result

- New row appears with the just-revoked thumbprint, cluster ID `SPOKE-TEST-1`, reason `KeyCompromise`, and the recent timestamp.

### Scenario 5: Idempotent Revocation

### Steps

1. Re-run the same POST from Scenario 3.

### Expected Result

- HTTP 200/201 (still success); registry retains a single record with the **original** `revokedUtc` timestamp -- the duplicate does not overwrite it.
- Cross-ref: `RevocationRegistryTests.Revoke_IsIdempotent_PreservesOriginalRecord`.

---

## Part D: Cache Refresh & Inbound Rejection

### Scenario 6: Wait for Cache Refresh

### Steps

1. Note the current time.
2. Wait at least `Federation:Revocation:CacheTtlSeconds + 5` seconds (e.g., 65 seconds for the 60s test config).
3. Watch the Hub log for `RevocationRefreshService` log entries.

### Expected Result

- Hub log emits a refresh log line ("Loaded N revocations into cache") within the TTL window.
- Cross-ref: `RevocationRegistryTests.Cache_RefreshFromGrain_PicksUpNewRevocations`.

### Scenario 7: Spoke Now Rejected on Inbound mTLS

### Steps

1. Repeat Scenario 2 -- have the Spoke trigger another outbound clinical event toward the Hub.

### Expected Result

- Hub log shows the inbound TLS handshake **rejected** with reason `RevokedCertificate` (or HTTP 403 / `CertificateRevoked` from the auth handler).
- Spoke outbox row stays Pending (or marks as failed, depending on transport behavior); this is expected -- a revoked cert cannot send.
- Federation Dashboard's Outbox panel on the Spoke shows pending rows accumulating.

### Scenario 8: Unknown Thumbprint -- Allowed

### Steps

1. Query the revoked-state of a fake thumbprint via the grain:
   ```powershell
   Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/federation/admin/revocations/AABBCCDDEEFF00112233445566778899AABBCCDD/check" `
     -Headers @{ Authorization = "Bearer $jwt" }
   ```
   (If the API doesn't expose this, skip -- the unit test below is authoritative.)

### Expected Result

- Returns `{ "thumbprint": "AABB...DD", "isRevoked": false }`.
- Cross-ref: `RevocationRegistryTests.IsRevokedAsync_UnknownThumbprint_ReturnsFalse`.

---

## Part E: Restoration (for Subsequent Tests)

### Scenario 9: Reissue Cert to Restore Spoke

The Hub-CA does **not** support unrevocation by design (you must reissue). To restore the Spoke for subsequent tests:

### Steps

1. On the Hub, issue a fresh provisioning token for `SPOKE-TEST-1` (per [02-Hub-CA-Spoke-Onboarding.md](02-Hub-CA-Spoke-Onboarding.md) Scenario 2).
2. On the Spoke, generate a new keypair + CSR and exchange the token for a new cert (Scenarios 4-7 of script 02).
3. Replace `spoke.pfx` and restart the Spoke silo.
4. Repeat Scenario 2 above to confirm the **new** cert is accepted.

### Expected Result

- Spoke outbox drains successfully again.
- Hub log shows successful inbound from `CN=SPOKE-TEST-1` with the **new** thumbprint.
- The old (revoked) thumbprint remains in the revocation list -- correct behavior.

---

## Part F: Verification Checklist

- [ ] Cert thumbprint identified
- [ ] Spoke can talk to Hub before revocation (baseline)
- [ ] POST `/api/federation/admin/revoke` returns 200 with record
- [ ] GET `/api/federation/admin/revocations` lists the record
- [ ] Federation Dashboard "Revoked certs" panel shows the new row
- [ ] Re-revoking the same thumbprint is idempotent (original timestamp preserved)
- [ ] Within `CacheTtlSeconds`, the cache picks up the new revocation
- [ ] Hub rejects the revoked cert on next inbound mTLS attempt
- [ ] `IsRevoked` returns `false` for unknown thumbprints
- [ ] Reissued cert restores spoke connectivity (does not require unrevocation)
- [ ] Original (revoked) thumbprint remains permanently on the list

---

## Cross-References

- Grain: [RevocationRegistryGrain.cs](../../../../NewVistas.Abstractions/Grains/RevocationRegistryGrain.cs)
- Cache: [InMemoryRevocationCache.cs](../../../../../NewVistas.WebServer/Infrastructure/Federation/InMemoryRevocationCache.cs)
- Refresh service: [RevocationRefreshService.cs](../../../../../NewVistas.WebServer/Infrastructure/Federation/RevocationRefreshService.cs)
- Functional tests:
  - `RevocationRegistryTests.Revoke_AddsToList_AndIsRevokedReturnsTrue`
  - `RevocationRegistryTests.Revoke_IsIdempotent_PreservesOriginalRecord`
  - `RevocationRegistryTests.IsRevokedAsync_UnknownThumbprint_ReturnsFalse`
  - `RevocationRegistryTests.Revoke_NormalizesThumbprintFormat`
  - `RevocationRegistryTests.Cache_RefreshFromGrain_PicksUpNewRevocations`
