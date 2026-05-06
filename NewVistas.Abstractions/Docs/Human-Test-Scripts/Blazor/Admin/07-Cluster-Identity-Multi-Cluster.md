# Cluster Identity & Multi-Cluster Attribution -- Administrator Human Test Script

**Purpose:** Verify that every clinical event envelope appended on a silo is
stamped with that silo's `IClusterIdentity`, that the Hub-side audit trail
preserves the originating cluster ID across replication, and that the
inbound auth handler enforces `RequireMatchingClusterId` (an event whose
envelope `sourceClusterId` does not match the connecting client cert's CN
is rejected).

This script tests commits **2ef2edc4** (Cluster identity), **48295ce1** (Site
profiles), and the inbound enforcement layer in commit **90d32551**.

---

## Prerequisites

- **Login (Hub admin):** `ADMIN1` / Password: `smythVista1`
- **Login (Privacy officer for audit):** `ADMIN4` / Password: `smythVista1`
- **Pre-conditions:**
  1. Two-silo Hub + Spoke environment from [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md) running.
  2. Hub `appsettings`:
     ```json
     "ClusterIdentity":  { "ClusterId": "HUB-PRIMARY" },
     "Federation": {
       "Inbound": {
         "AllowedClusterIds": [ "SPOKE-TEST-1", "SPOKE-TEST-2" ],
         "RequireMatchingClusterId": true
       }
     }
     ```
  3. Spoke `appsettings`: `"ClusterIdentity": { "ClusterId": "SPOKE-TEST-1" }`.
  4. Both onboarded successfully via [02-Hub-CA-Spoke-Onboarding.md](02-Hub-CA-Spoke-Onboarding.md).

---

## Part A: Cluster Identity Stamping

### Scenario 1: Spoke Stamps Locally-Appended Events with SPOKE-TEST-1

### Steps

1. On Spoke (`https://localhost:8137`), login as `DOCTOR1`, select P1, document a vital sign.
2. Inspect the patient's clinical event stream via the API:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:8127/api/auth/login `
     -Body (@{ username = "DOCTOR1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $events = Invoke-RestMethod -Method Get `
     -Uri "https://localhost:8127/api/patient/P1/clinical-events?max=10" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   $events | Select-Object eventId, domain, version, sourceClusterId, appendedUtc | Format-Table
   ```

### Expected Result

- The newly-appended vital event has `sourceClusterId = "SPOKE-TEST-1"`.
- All other Spoke-appended events carry the same `sourceClusterId`.
- Cross-ref: `ClinicalEventReplicationSinkTests.Append_FreshEvent_InvokesSinkOnce_WithSealedEnvelope` (verifies envelope is sealed with cluster identity before sink invocation).

### Scenario 2: Hub Stamps Locally-Appended Events with HUB-PRIMARY

### Steps

1. On Hub (`https://localhost:7137`), login as `DOCTOR1`, select a different patient `P2` (one that has no Spoke activity).
2. Document a vital sign.
3. Repeat the API inspection from Scenario 1 on the Hub (`https://localhost:7127/api/patient/P2/clinical-events`).

### Expected Result

- The newly-appended vital event has `sourceClusterId = "HUB-PRIMARY"`.

---

## Part B: Cluster Identity Preservation Across Replication

### Scenario 3: Replicated Spoke Events Retain SPOKE-TEST-1 on Hub

### Steps

1. After Scenario 1 (Spoke event for P1), wait for the federation outbox to drain (~15s). Confirm via the Hub Federation Dashboard outbox panel.
2. On the **Hub**, query the same patient P1:
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "DOCTOR1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $events = Invoke-RestMethod -Method Get `
     -Uri "https://localhost:7127/api/patient/P1/clinical-events?max=10" `
     -Headers @{ Authorization = "Bearer $($login.token)" }
   $events | Select-Object eventId, domain, version, sourceClusterId, appendedUtc | Format-Table
   ```

### Expected Result

- The replicated event on the Hub has `sourceClusterId = "SPOKE-TEST-1"` (the **originating** cluster) -- **not** `HUB-PRIMARY`.
- This is the forensic-attribution guarantee: even after data lives on the Hub, the originating cluster is preserved.

### Scenario 4: Mixed-Origin Stream Visible on Hub

### Steps

1. On Hub, append a new event for P1 (e.g., add an allergy).
2. On Spoke, append a new event for P1 (e.g., add a problem).
3. Wait for replication.
4. Re-query the Hub's clinical event stream for P1.

### Expected Result

- Stream now contains events with **both** `sourceClusterId = "HUB-PRIMARY"` and `sourceClusterId = "SPOKE-TEST-1"`.
- Sequence is deterministic per silo's append order; cross-silo ordering reflects arrival time at Hub.

---

## Part C: Inbound Cluster ID Enforcement

### Scenario 5: Spoke Sends Mismatched Envelope -- Rejected

> **Setup:** This scenario requires constructing a malformed batch (envelope claims `sourceClusterId = "SPOKE-EVIL"` but the client cert is `CN=SPOKE-TEST-1`). The cleanest way is via a small PowerShell script that POSTs a hand-crafted payload using the spoke's PFX as a client cert. If you do not have time to build the harness, the unit test below is authoritative.

### Steps

1. Hand-craft a batch:
   ```powershell
   $batch = @(@{
     eventId = [Guid]::NewGuid().ToString()
     patientId = "P1"
     domain = "Allergy"
     payload = @{ description = "Test injection" }
     version = 99999
     previousHash = "0".PadLeft(64, '0')
     hash = "abc"
     sourceClusterId = "SPOKE-EVIL"   # MISMATCH -- cert says SPOKE-TEST-1
     appendedUtc = (Get-Date).ToUniversalTime().ToString("o")
   }) | ConvertTo-Json -Depth 5
   ```
2. POST with the spoke's mTLS cert:
   ```powershell
   # Use a small helper or curl with --cert / --key
   curl.exe --cert C:\NewVistas-Federation\Spoke\Certs\spoke.pfx: `
     --cert-type P12 `
     --cacert C:\NewVistas-Federation\Hub\Certs\hub-ca.crt `
     -X POST https://localhost:7127/api/federation/inbound `
     -H "Content-Type: application/json" `
     --data $batch
   ```

### Expected Result

- HTTP **400 Bad Request** (or **403 Forbidden**) with body indicating the envelope's `sourceClusterId` does not match the authenticated client.
- Hub log emits a security warning naming both the claimed and authenticated cluster IDs.
- No events appended to the Hub for that patient.

### Scenario 6: Disable Enforcement, Mismatch Now Allowed (Audit Only)

### Steps

1. Edit Hub config: `RequireMatchingClusterId = false`. Restart Hub WebServer.
2. Repeat the POST from Scenario 5.

### Expected Result

- HTTP 200 -- event is **applied** despite the CN/envelope mismatch.
- Hub log emits an **audit warning** naming the mismatch (so it's still recorded), but the request is not rejected.
- Restore `RequireMatchingClusterId = true` after this test.

### Scenario 7: Cluster ID Not in AllowedClusterIds -- Rejected

### Steps

1. On the Hub, edit `AllowedClusterIds` to remove `SPOKE-TEST-1` (e.g., `[ "SPOKE-TEST-2" ]` only). Restart the Hub WebServer.
2. From the Spoke, append a clinical event so the outbox tries to send.
3. Inspect Hub log.

### Expected Result

- Inbound POST rejected with HTTP 403 -- "Cluster SPOKE-TEST-1 not in allow-list".
- Spoke outbox row stays Pending and retries (per [06-Federation-Outbox-Drainer.md](06-Federation-Outbox-Drainer.md) Scenario 4).
- Restore `AllowedClusterIds` after this test.

---

## Part D: Site Profile Resolution

### Scenario 8: Verify Profile Selection at Startup

### Steps

1. Start the Spoke with no `--profile` arg in Development mode:
   ```powershell
   dotnet run --project NewVistas.SiloHost
   ```
2. Read silo log for the resolved profile.

### Expected Result

- Log line: "Resolved site profile: LocalhostDev" (or similar).
- Cross-ref: `SiteProfileResolverTests.Resolve_DefaultsToLocalhostDev_InDevelopment`.

### Scenario 9: Override via Environment Variable

### Steps

1. Stop the Spoke and restart with:
   ```powershell
   $env:NEWVISTAS_PROFILE = "RemoteOnline"
   dotnet run --project NewVistas.SiloHost
   ```

### Expected Result

- Log line: "Resolved site profile: RemoteOnline".
- Cross-ref: `SiteProfileResolverTests.Resolve_EnvironmentVariable_PicksRemoteOnline`.
- Federation outbox + HTTP transport are wired up (visible in startup log).
- Clear the env var: `Remove-Item Env:NEWVISTAS_PROFILE`.

### Scenario 10: --profile CLI Wins Over Env Var

### Steps

1. With `NEWVISTAS_PROFILE=RemoteOnline` set:
   ```powershell
   $env:NEWVISTAS_PROFILE = "RemoteOnline"
   dotnet run --project NewVistas.SiloHost -- --profile LocalhostDev
   ```

### Expected Result

- Log line: "Resolved site profile: LocalhostDev" -- CLI arg wins.
- Cross-ref: `SiteProfileResolverTests.Resolve_ProfileArg_BeatsEnvironmentVariable`.

---

## Part E: Verification Checklist

- [ ] Spoke-appended event carries `sourceClusterId = "SPOKE-TEST-1"`
- [ ] Hub-appended event carries `sourceClusterId = "HUB-PRIMARY"`
- [ ] Replicated Spoke event preserves `sourceClusterId = "SPOKE-TEST-1"` on Hub
- [ ] Mixed-origin stream visible after cross-silo activity
- [ ] Mismatched envelope rejected when `RequireMatchingClusterId = true`
- [ ] Mismatched envelope applied (with audit warning) when `RequireMatchingClusterId = false`
- [ ] Cluster not in `AllowedClusterIds` is rejected by Hub
- [ ] Default profile in Development mode = `LocalhostDev`
- [ ] `NEWVISTAS_PROFILE` env var selects profile
- [ ] `--profile` CLI arg overrides env var

---

## Cross-References

- Cluster identity interface: [IClusterIdentity.cs](../../../../NewVistas.Abstractions/Federation/IClusterIdentity.cs)
- Static implementation: [StaticClusterIdentity.cs](../../../../NewVistas.Abstractions/Federation/StaticClusterIdentity.cs)
- Inbound options: [InboundAuthOptions.cs](../../../../../NewVistas.WebServer/Infrastructure/Federation/InboundAuthOptions.cs)
- Site profile resolver: [SiteProfileResolver.cs](../../../../../NewVistas.SiloHost/Infrastructure/Profiles/SiteProfileResolver.cs)
- Profiles: [LocalhostDevProfile.cs](../../../../../NewVistas.SiloHost/Infrastructure/Profiles/LocalhostDevProfile.cs), [AzureCloudProfile.cs](../../../../../NewVistas.SiloHost/Infrastructure/Profiles/AzureCloudProfile.cs), [RemoteOnlineProfile.cs](../../../../../NewVistas.SiloHost/Infrastructure/Profiles/RemoteOnlineProfile.cs), [RemoteOfflineProfile.cs](../../../../../NewVistas.SiloHost/Infrastructure/Profiles/RemoteOfflineProfile.cs)
- Functional / unit tests:
  - `SiteProfileResolverTests.Resolve_DefaultsToLocalhostDev_InDevelopment`
  - `SiteProfileResolverTests.Resolve_DefaultsToAzureCloud_OutsideDevelopment`
  - `SiteProfileResolverTests.Resolve_LegacySqlExpressFlag_PicksDemoProfile`
  - `SiteProfileResolverTests.Resolve_ProfileArgWinsOverSqlExpressFlag`
  - `SiteProfileResolverTests.Resolve_EnvironmentVariable_PicksRemoteOnline`
  - `SiteProfileResolverTests.Resolve_ProfileArg_BeatsEnvironmentVariable`
  - `ClinicalEventReplicationSinkTests.Append_FreshEvent_InvokesSinkOnce_WithSealedEnvelope`
