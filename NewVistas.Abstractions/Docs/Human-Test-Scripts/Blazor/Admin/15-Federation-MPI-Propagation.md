# Cross-Cluster MPI Propagation -- Federation Architect Human Test Script

**Purpose:** Validate that the cross-cluster MPI propagation seam fires
correctly when patients are registered or merged. In a multi-facility
deployment under one tribal authority (or any federated topology), peer
clusters need to keep their MPI search and correlation grains in sync so:

- A patient registered at facility A can be found by name/SSN/DOB by a
  clinician at facility B (without requiring a separate cross-cluster query
  on every search)
- A patient merge performed at facility A propagates the source-ICN→target-
  ICN alias to facility B, so future lookups of the source ICN at B
  redirect to the target

This script covers two delivered iterations:
- **Iteration 1** delivered the architectural seam (`IMpiFederationAnnouncer`)
  with spy-based validation of announce-call wiring.
- **Iteration 2** ships the real outbox-backed implementation:
  `OutboxMpiFederationAnnouncer` publishes `MpiPatientRegisteredV1` /
  `MpiPatientMergedV1` envelopes through the existing federation outbox
  + transport, and `FederationInboundApplier` dispatches Domain="MPI"
  envelopes on the receiving cluster to `DefaultMpiInboundHandler`,
  which updates local `IMpiSearchGrain` and `IMpiCorrelationGrain`.
  End-to-end MPI propagation works against the same federation transport
  (HTTP/mTLS or sneakernet) that clinical events already use.

---

## Prerequisites

- **Login:** `ADMIN1` / Password: `smythVista1`
- **Site profile:** any. Single-cluster deployments use the default
  `NoOpMpiFederationAnnouncer` and incur zero overhead. Federated
  deployments override with an outbox-backed implementation.

---

## Part A: Verify the Announce Hook Fires on Registration

### Scenario 1: Spy announcer captures a registration

This scenario is exercised by the functional test
`MpiFederationAnnouncerTests.Register_FiresAnnouncePatientRegistered`. To
verify manually in a development silo:

### Steps

1. Replace the registered `IMpiFederationAnnouncer` with a logging
   implementation (or set a breakpoint on `NoOpMpiFederationAnnouncer.AnnouncePatientRegisteredAsync`).
2. Register a patient via the standard registration flow (REST endpoint or
   `IPatientRegistrationGrain.RegisterPatientAsync` directly).

### Expected Result

- `AnnouncePatientRegisteredAsync` is invoked exactly once per successful
  registration.
- The supplied `MpiSearchEntry` carries: ICN, patient name, SSN,
  date of birth, sex, `IsDeceased = false`, `FacilityCount = 1`.
- The `originatingFacilityId` argument is the local cluster id
  (from `IClusterIdentity.LocalClusterId`).

### Scenario 2: Announcer failure does not fail registration

### Steps

1. Configure the test announcer to throw on the next registered call.
2. Register a patient.

### Expected Result

- The registration completes normally; the patient grain is created and
  the local MPI grains are populated as usual.
- The exception from the announcer is swallowed (logged as operational
  failure, not propagated). Local clinical-state correctness is not
  conditional on federation-transport availability.

---

## Part B: Verify the Announce Hook Fires on Merge

### Scenario 3: Spy announcer captures a merge

### Steps

1. Enable the `PATIENT_MERGE` site feature.
2. Register two patients (target + source).
3. Execute a merge via `IPatientMergeGrain.ExecuteMergeAsync` (or via
   `IPatientWorkflowGrain.MergePatientAsync` after auth).

### Expected Result

- After local merge phases complete (clinical-data move, MPI propagation
  on this cluster), `AnnouncePatientMergedAsync` is invoked exactly once.
- Arguments: source ICN, target ICN, originating facility id.

### Scenario 4: Announcer failure does not fail merge

### Steps

1. Configure the spy/test announcer to throw on the next merged call.
2. Execute a merge.

### Expected Result

- The local merge succeeds (`PatientMergeResult.Success = true`).
- Clinical-data movement, MPI alias on this cluster, and audit trail are
  all consistent.
- The federation announce failure is logged but does not propagate.

### Scenario 5: Pre-ICN legacy patients do not generate announcements

### Steps

1. Create two pre-ICN-issuance legacy patients (no ICN on `PatientState`).
2. Merge them via `IPatientMergeGrain.ExecuteMergeAsync`.

### Expected Result

- Merge succeeds.
- `AnnouncePatientMergedAsync` is **not** called (no ICN to alias).

---

## Part C: The Outbox-Backed Implementation (now live)

The `IhsTribalSiteProfile` registers `OutboxMpiFederationAnnouncer` (overriding
the no-op default). Other federated profiles can opt in the same way —
**before** `AddCommonSiloServices`:

```csharp
siloBuilder.Services.AddSingleton<IMpiFederationAnnouncer, OutboxMpiFederationAnnouncer>();
```

What it does on send:

1. Wrap each announcement in an `EventEnvelope` whose `Payload` is
   `MpiPatientRegisteredV1` or `MpiPatientMergedV1`.
2. Stamp `EventEnvelope.SourceClusterId` from
   `IClusterIdentity.LocalClusterId` directly (MPI events bypass the
   per-patient clinical event stream that normally seals this).
3. Set `EventEnvelope.Domain = "MPI"` (drives inbound dispatch).
4. Publish through the configured `IClinicalEventReplicationSink` — the
   same SQL outbox + drainer + HTTP/mTLS or sneakernet transport that
   clinical events use. No parallel federation pipeline.

What `FederationInboundApplier` does on receive:

1. Inspect `envelope.Domain`. If `"MPI"`, route to `IMpiInboundHandler.ApplyAsync`
   instead of the per-patient clinical event stream.
2. `DefaultMpiInboundHandler` switches on `envelope.Payload`:
   - `MpiPatientRegisteredV1` → `IMpiSearchGrain.AddOrUpdatePatientAsync`
     (preserves any existing `MergedIntoIcn` alias on the receiving cluster
     so a peer's late-arriving registration can't reset a local merge).
   - `MpiPatientMergedV1` → `IMpiCorrelationGrain.MarkAsMergedAsync` on the
     source ICN (idempotent for same target; refuses re-route to a different
     target, surfacing as a logged warning), then refreshes the source's
     MPI search entry to surface the alias.

### Cross-cluster patient merge — full lifecycle (end-to-end)

1. Clerk at facility A registers patient `9100000007V045712` →
   `OutboxMpiFederationAnnouncer.AnnouncePatientRegisteredAsync` fires →
   envelope drains to facility B → B's `IMpiSearchGrain` now indexes the
   patient. Searching for the patient at B by name/SSN/DOB returns the hit.
2. Same patient (turns out to be) registered separately at facility B as
   `9110000003V045712`. B announces this back to A.
3. A clerk discovers the duplicate and merges B's record into A's:
   `IPatientMergeGrain.ExecuteMergeAsync(target=9100…, source=9110…)`. Phase
   4b updates A's local MPI; Phase 4c calls
   `OutboxMpiFederationAnnouncer.AnnouncePatientMergedAsync(source, target,
   "FACILITY-A")`.
4. The envelope drains to B (and any other peers). B's
   `DefaultMpiInboundHandler` calls `MarkAsMergedAsync` on its source MPI
   correlation grain and stamps `MergedIntoIcn` on its search index entry.
5. From this moment on, a search at B that returns the source ICN flags
   it as an alias and shows the target ICN to follow.

---

## Part D: Verification Checklist

- [ ] `IMpiFederationAnnouncer` is registered as a DI singleton on every silo
- [ ] Default registration is `NoOpMpiFederationAnnouncer` (via `CommonSiloConfig.AddCommonSiloServices`)
- [ ] `IPatientRegistrationGrain.RegisterPatientAsync` calls `AnnouncePatientRegisteredAsync` after MPI setup
- [ ] `PatientMergeGrain.ExecuteMergeAsync` calls `AnnouncePatientMergedAsync` after Phase 4b MPI propagation
- [ ] Registration-announce errors do not fail the registration
- [ ] Merge-announce errors do not fail the merge
- [ ] Legacy (pre-ICN) merges do not fire merge announcements
- [ ] Functional test fixture `MpiFederationAnnouncerTests` passes (6 scenarios)
- [ ] Document `OutboxMpiFederationAnnouncer` as the next-iteration implementation hook

---

## Cross-References

- Seam: [`IMpiFederationAnnouncer.cs`](../../../../Federation/IMpiFederationAnnouncer.cs)
- Default: [`NoOpMpiFederationAnnouncer.cs`](../../../../Federation/NoOpMpiFederationAnnouncer.cs)
- Registration call site: [`PatientRegistrationGrain.cs`](../../../../Grains/PatientRegistrationGrain.cs) — end of `RegisterPatientAsync`
- Merge call site: [`PatientMergeGrain.cs`](../../../../Grains/PatientMergeGrain.cs) — Phase 4c
- Functional tests: `MpiFederationAnnouncerTests` (6 scenarios — register, hint propagation, register-failure-tolerance, merge, merge-failure-tolerance, legacy-no-announce)
- Sister federation infrastructure: [`Admin/06-Federation-Outbox-Drainer.md`](06-Federation-Outbox-Drainer.md), [`Admin/07-Cluster-Identity-Multi-Cluster.md`](07-Cluster-Identity-Multi-Cluster.md)
- Local merge reference: [`Admin/10-Patient-Merge.md`](10-Patient-Merge.md)
- Architecture: [`ADR-001 — Patient Identity Strategy`](../../../Architect-decisions/ADR-001-Patient-Identity-Strategy.md), tribal-deployment plan (multi-facility federation hardening, cross-facility patient merge)

> **Note:** this round delivers the **announce seam + spy validation**. The
> outbox-backed federation transport for MPI events is the next iteration's
> work — likely combined with the broader federated-GPRA-aggregation and
> cross-cluster patient lookup features. The seam shape (announcer interface +
> per-deployment policy registered via DI) deliberately matches
> `IRegistrationEligibilityPolicy`, `IGpraSubmissionFormatter`, and
> `INdwExportFormatter` so the architectural pattern is consistent across all
> per-deployment policies.
