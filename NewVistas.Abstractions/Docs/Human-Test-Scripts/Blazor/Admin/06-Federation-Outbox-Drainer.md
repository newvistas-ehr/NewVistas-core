# Federation Outbox Drainer (HTTP) -- Administrator Human Test Script

**Purpose:** Verify the SQL-backed federation outbox: clinical events appended
on the Spoke are durably persisted to a SQL row, picked up by the
`OutboxDrainerService`, transmitted to the Hub via `HttpFederationTransport`,
and marked Sent. Cover happy path, transient transport failures with retry,
and ordering preservation.

This script tests commits **c863e6b4** (SQL outbox), **73af2ca1** (replication
infrastructure), and **fd5e7f92** (inbound applier).

---

## Prerequisites

- **Login (Hub admin):** `ADMIN1` / Password: `smythVista1`
- **Login (Spoke clinician):** `DOCTOR1` / Password: `smythVista1`
- **Pre-conditions:**
  1. Two-silo Hub + Spoke environment from [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md) running.
  2. Spoke configured with `Transport:Type = "Http"` and `HubUrl = "https://localhost:7127/api/federation/inbound"`.
  3. Spoke onboarded with valid cert (per [02-Hub-CA-Spoke-Onboarding.md](02-Hub-CA-Spoke-Onboarding.md)).
  4. Spoke `appsettings`:
     ```json
     "Federation": {
       "Outbox": {
         "Enabled": true,
         "CheckIntervalSeconds": 15,
         "MaxRetries": 5,
         "RetryDelaySeconds": 30
       }
     }
     ```

---

## Part A: Baseline Outbox State

### Scenario 1: Reset Outbox

### Steps

1. Truncate Spoke outbox:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -Q "TRUNCATE TABLE FederationOutbox"
   ```
2. Verify the Federation Dashboard on the Spoke (`https://localhost:8137/admin/federation`) shows Pending = 0, Sent = 0.

### Expected Result

- Outbox table is empty.
- Dashboard reflects zero counts after manual Refresh.

---

## Part B: Happy Path (Single Event)

### Scenario 2: Single Clinical Event Drains Successfully

### Steps

1. On Spoke BlazorWeb, login as `DOCTOR1`, select `P1`, document a vital sign (per [Blazor/Nurses/02-Vital-Signs-Recording.md](../Nurses/02-Vital-Signs-Recording.md)).
2. Inspect Spoke outbox immediately:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -Q "SELECT TOP 5 Id, AggregateId, Status, Attempts, NextAttemptUtc, CreatedUtc FROM FederationOutbox ORDER BY Id DESC"
   ```
3. Wait `CheckIntervalSeconds + 5` (~20 seconds).
4. Re-inspect.

### Expected Result

- Initially: 1 row, Status = `Pending`, Attempts = 0, AggregateId = patient ID.
- After drainer cycle: row Status = `Sent`, Attempts = 1, `SentUtc` populated.
- Cross-ref: `OutboxSinkAndDrainerTests.Sink_FreshPublish_InsertsRow`.

### Scenario 3: Hub Receives & Applies Event

### Steps

1. After Scenario 2, login as `DOCTOR1` on the **Hub** BlazorWeb (`https://localhost:7137`), select P1.
2. View vitals.
3. Inspect Hub log for the inbound POST.

### Expected Result

- Vital sign recorded on Spoke is visible on Hub (same value, same timestamp).
- Hub log shows POST to `/api/federation/inbound` with HTTP 200 OK response.
- Hub log line confirms applier ran, version advanced.
- Cross-ref: `FederationInboundApplierTests.ApplyBatch_FreshEnvelopes_AllApplied_VersionAdvances`.

---

## Part C: Transport Failure & Retry

### Scenario 4: Hub Down -- Spoke Retries

### Steps

1. **Stop only the Hub WebServer** process (leave Hub silo and Hub BlazorWeb running). The Spoke can no longer reach `https://localhost:7127`.
2. On the Spoke BlazorWeb (still up), append another clinical event (e.g., add a new problem to P1).
3. Watch Spoke outbox:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -Q "SELECT TOP 5 Id, Status, Attempts, NextAttemptUtc, LastError FROM FederationOutbox WHERE Status = 'Pending' ORDER BY Id DESC"
   ```
4. Wait through 2 retry cycles (~75 seconds).

### Expected Result

- Row Status remains `Pending`, Attempts increments after each cycle.
- `LastError` column captures the connection-refused / network error message.
- `NextAttemptUtc` advances by `RetryDelaySeconds` (or backed-off interval).
- Cross-ref: `HttpFederationTransportTests.Send_2xxOkWithZeroErrors_ReturnsOk` (positive case) and the timeout/network-error scenarios.

### Scenario 5: Hub Comes Back -- Drains Successfully

### Steps

1. Restart the Hub WebServer.
2. Wait one drainer interval.
3. Re-inspect the Spoke outbox.

### Expected Result

- Pending row transitions to `Sent` on the next attempt; Attempts increments to its final count; `SentUtc` populated.
- Hub patient P1 shows the new problem.

### Scenario 6: Permanent Failure -- Max Retries Reached

### Steps

1. Stop the Hub WebServer **and** edit Spoke config: `Transport:HubUrl = "https://localhost:7777/api/federation/inbound"` (a port nothing is listening on). Restart Spoke.
2. Append a clinical event on the Spoke.
3. Wait `MaxRetries × CheckIntervalSeconds` (default: 5 × 15 = ~75 seconds, plus retry backoff).

### Expected Result

- After max retries, the row transitions to `Failed` (or `Dead`) status -- no further attempts made.
- Federation Dashboard's "Max attempts on a pending row" stat reaches `MaxRetries`.
- Operator action is now required (admin-triggered re-queue, not in this test).
- Restore Spoke config to the correct HubUrl and restart for subsequent scripts.

---

## Part D: Ordering & Idempotency

### Scenario 7: In-Order Delivery Per Patient

### Steps

1. Reset outbox (Scenario 1).
2. On Spoke, append 5 events for P1 in rapid succession (e.g., place 5 lab orders).
3. Wait one drainer interval.
4. Inspect Hub patient P1.

### Expected Result

- All 5 events arrive at the Hub in the same order they were appended on the Spoke.
- Per-patient event stream `Version` numbers are sequential.
- No interleaving issues even under rapid append.

### Scenario 8: Replay-Safe (Spoke Resends Already-Sent)

### Steps

1. Manually mark a Sent row as Pending again to simulate a duplicate send:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -Q "UPDATE TOP (1) FederationOutbox SET Status='Pending', Attempts=0 WHERE Status='Sent'"
   ```
2. Wait one drainer interval.
3. Verify on the Hub that the patient does **not** have a duplicate event.

### Expected Result

- Hub responds with HTTP 200 (or 200 + applied=0/skipped=1) and Spoke marks the row Sent again.
- No clinical-record duplication.

---

## Part E: Volume / Stress (Optional)

### Scenario 9: Burst of 100 Events

### Steps

1. Use any clinical workflow to append 100 events on the Spoke. Easiest: run the demo data loader for a fresh patient set.
2. Watch the Federation Dashboard outbox panel.
3. Time how long until `Pending` returns to `0`.

### Expected Result

- All 100 events drain within `100 / batchSize × CheckIntervalSeconds` seconds (drainer batches per cycle).
- No row stuck in Pending.
- No HTTP 5xx responses from Hub.
- Hub silo CPU does not pin to 100%.

---

## Part F: Verification Checklist

- [ ] Empty outbox after truncation
- [ ] Single event inserts row with Status=Pending
- [ ] Drainer transitions row to Sent within one interval
- [ ] Event visible on Hub patient record
- [ ] Hub down: Spoke retries, Attempts increments, LastError captures network error
- [ ] Hub recovers: Pending row drains successfully on next attempt
- [ ] Max retries reached: row marked Failed, no infinite retry loop
- [ ] In-order delivery preserved per patient (Version numbers sequential)
- [ ] Replayed Sent rows do not produce duplicates on the Hub
- [ ] 100-event burst drains within reasonable time without stuck rows
- [ ] Federation Dashboard outbox panel reflects all of the above accurately

---

## Cross-References

- SQL outbox repo: [SqlOutboxRepository.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/SqlOutboxRepository.cs)
- Sink: [SqlOutboxClinicalEventReplicationSink.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/SqlOutboxClinicalEventReplicationSink.cs)
- Drainer: [OutboxDrainerService.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/OutboxDrainerService.cs)
- HTTP transport: [HttpFederationTransport.cs](../../../../../NewVistas.SiloHost/Infrastructure/Federation/HttpFederationTransport.cs)
- Schema: [FederationOutboxSchema.sql](../../../../../NewVistas.SiloHost/Sql/FederationOutboxSchema.sql)
- Functional / unit tests:
  - `OutboxSinkAndDrainerTests.Sink_FreshPublish_InsertsRow`
  - `HttpFederationTransportTests` (full suite covering 2xx, timeouts, network errors)
  - `FederationInboundApplierTests.ApplyBatch_FreshEnvelopes_AllApplied_VersionAdvances`
  - `ClinicalEventReplicationSinkTests.Append_FreshEvent_InvokesSinkOnce_WithSealedEnvelope`
