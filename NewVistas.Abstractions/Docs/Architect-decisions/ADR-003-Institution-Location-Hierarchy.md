# ADR-003 — Institution & Location Hierarchy (Bed & Room Management)

**Status:** Accepted — implemented (2026-07-03)

## Context

NewVistas must run at both extremes: a clinic with four beds, and a multi-hospital
health system (the motivating example: BILH — "Lahey Burlington might want to put a
patient in Lawrence General"). Before this ADR:

- **Facility was a string.** "500", "MAIN", and "INST-500" all floated through
  patient, staff, ADT, and bed records with no File #4 INSTITUTION analog and no way
  to say "these two hospitals belong to one health system."
- **Bed/occupancy truth lived in FOUR disconnected places**: a per-bed `IBedGrain`,
  an `IBedBoardGrain` index synced only by explicit controller calls, the
  `NursingUnitGrain`'s own bed-assignment list, and the `WardCensusGrain` written by
  the ADT workflow. ADT's `RoomBed` was free text validated against nothing.
- EVS was one opaque `CLEANING` status; capacity numbers counted dirty and blocked
  beds as free.

Decision owner's constraints: full EVS lifecycle; cross-facility placement as a
request→accept workflow; **replace the old machinery outright** ("no one is using
this system") provided small sites don't get harder.

## Decision

### 1. Location hierarchy

```
HealthSystem (plain grouping fields)          e.g. BILH
  └─ Institution  INST:{institutionId}        File #4 — first-class grain + INSTITUTION-INDEX
       └─ Unit    UNIT:{institutionId}:{unitId}   merges File #42 (ward) + File #210 (nursing unit)
            └─ Room (optional grouping inside the unit state)
                 └─ Bed (File #405.4 + folded #210 bed assignment)
```

- **Institution** is a grain (`InstitutionState`): name, type, station number,
  health-system fields, address, capabilities (ICU/TELEMETRY/PEDS/...), an
  `AcceptsInboundTransfers` switch, and **`LegacyFacilityAliases`** — the absorption
  path for the three historical spellings ("500" canonical; "MAIN", "INST-500"
  aliases). `IInstitutionIndexGrain.ResolveLegacyFacilityIdAsync` maps old strings to
  canonical ids so nothing already written breaks.
- **A health system is fields, not a grain.** It has no behavior in v1 — no state
  transitions, no invariants; the index groups by it. Promote it if it ever needs
  per-system configuration.
- The name `IFacilityGrain` was already taken by Engineering (File #6914, physical
  spaces/equipment) — hence "Institution" (which is also the correct VistA term).

### 2. The unit grain is the single writer for bed truth

A unit is bounded (8–40 beds), so ONE grain (`IInpatientUnitGrain`) owns its rooms,
beds, boarders, and nursing assignments. Every mutation — occupy, release, reserve,
EVS transition, block — is atomic within the unit by Orleans single-threading.
**Census and capacity are projections of this state and are never stored elsewhere.**
This kills all four divergent lists by construction, not by discipline.

- **Bed lifecycle** (`BedLifecycleState`): Available ↔ Reserved → Occupied → Dirty →
  Cleaning → Available; Blocked(reason); OutOfService (returns to service via Dirty —
  physical work means an EVS pass). "Placeable" is derived: **only Available counts.**
  Illegal transitions (Occupied→Blocked, Reserve a non-Available bed, ...) throw.
- **Reservation expiry** lives on the bed (`ReservationExpiresAt`) and is enforced by
  a lazy sweep at the top of every unit operation and on activation — deterministic,
  no timers.
- **Boarders**: `institutionId` + `unitId` are required on admission; `bedId` is
  optional. A bedless admission is a unit *boarder* — ED boarding and
  don't-track-beds small sites — and the census stays honest either way. Free-text
  location is gone; it was the root disease.
- **Rollups, not sync**: after every mutation the unit pushes a compact
  `UnitCapacitySummary` (counts by state, per-type availability, dirty-bed detail) to
  the per-institution `BED-CAPACITY:{institutionId}` grain, and re-pushes on
  activation so a missed push self-heals. The capacity grain IS the unit directory
  (replacing WARD-LOCATION-INDEX and NURS-UNIT-IDX) and serves the institution-wide
  EVS queue in one read. System-wide, a `[StatelessWorker]` `SYSTEM-CAPACITY` grain
  fans out over institutions via the index — no persisted global aggregate (escape
  hatch documented for >100 institutions).
- ADT (File #405) movement history is untouched as history; movements gained a
  structured `InstitutionId` and their `RoomBed` now records the real bed id. The
  workflow occupies the bed BEFORE writing the movement (clean failure) and
  compensates by releasing if the movement write fails; placements are idempotent by
  movement id.
- The **ADR-002 hooks survived verbatim**: the attending physician gains an
  `Admission` treatment relationship on admission/transfer, and the bed's attending
  nurse gains `UnitCoverage` (sourceRef now unit-qualified: `BED:{unitId}:{bedId}` —
  "301A" exists on every floor). Admission also finally sets
  `PatientState.CurrentAdmission` (previously dead — `IsAdmitted` never worked).

### 3. Inter-facility transfer = request → accept → complete

`ITransferRequestGrain` (`XFER-{guid}`) clones the consult pattern (idempotent
create, status strings, actor-stamped timeline, clinical-event outbox):

```
REQUESTED ──accept(reserve bed FIRST, then flip)──▶ ACCEPTED ──complete──▶ COMPLETED
    │                                                  │
    └────────▶ DECLINED (receiver)                     └──▶ CANCELLED (sender; releases reservation)
```

- **The receiving facility controls its own beds**: accept names a specific unit+bed,
  reserved via the unit grain BEFORE the status flips (no orphan reservations on a
  race). A lost reservation (bed went out of service) leaves the request ACCEPTED
  with a `ReassignTransferBedAsync` re-reserve path.
- **Completion is not a saga.** Patients are ICN-keyed (ADR-001), so sender-discharge
  and receiver-admission are direct partial-class calls on the ONE workflow grain:
  admission at the receiver FIRST (occupies the reserved bed, fires the ADR-002
  attending hook), then discharge at the sender (disposition TRANSFER, old bed →
  Dirty). Admission-first means a failed admission leaves the patient safely admitted
  at the sender; the brief dual-census window self-heals because release is idempotent.
- Completion also writes the **previously missing MPI side-effects**: the receiving
  institution is added to `MPI:{icn}` correlations (File #985) and the patient's
  `TREATING-FAC` list (File #391.91) — plain `RecordAdmissionAsync` never did this.
- Per-institution `TRANSFER-CENTER:{id}` queue grains (incoming/outgoing) are written
  by the workflow layer on every transition.
- **EXT-REF vs XFER**: `ExternalReferralGrain` remains the model for care outside the
  organization (community care — no bed authority at the far end); XFER is placement
  inside the deployment where we control both ends' beds.

### 4. Access, flags, small sites

- New key **`DG BED CONTROL`** (VistA Bed Control menu): unit/room/bed structure,
  block/out-of-service, EVS turnover, and all transfer-center actions. Demo roles:
  Nurse, RegistrationClerk, Administrator. Nurses' existing `ORELSE` also satisfies
  the EVS flips so small sites can turn beds without extra grants. Placement stays
  under `DG ADMIT`.
- One flag: **`BED_MANAGEMENT`** (Modern, on by default) gating the Bed Board /
  Transfer Center UI and transfer workflow. Structured unit/bed placement inside ADT
  is core VistA and is not gated. There is deliberately NO second transfer flag: the
  Transfer Center self-hides when the institution index has ≤1 active institution —
  a better small-site story than a flag someone must remember to turn off.
- **Small-site collapse** is strictly less ceremony than before: one institution
  (seeded), one `ConfigureUnitAsync` + N × `AddBedAsync`, no rooms (beds render
  flat), boarder mode if they skip beds entirely, and `MarkBedCleanAsync` straight
  from Dirty (skip Start) turns a bed in one click.

### 5. User facility context (v1) — and what is deferred

`UserSecurityContext` gains `HomeInstitutionId` (from the staff record's
InstitutionId, legacy-resolved) and a mutable `ActingInstitutionId` behind a page
header picker. Receiving-side control on transfers is a workflow guard
(`actingInstitutionId == ReceivingInstitutionId`). **Honest limits, deferred as
future work:** nothing prevents a user from *claiming* another institution — real
per-facility RBAC (keys scoped to an institution, picker restricted to institutions
where the user holds a position, an `InstitutionId` RequestContext key checked in the
call filter) is the upgrade path. Per-institution feature flags (SiteParameters is a
`SITE:DEFAULT` singleton) are likewise deferred; institutions carry their own
operational fields instead.

### 6. Cross-cluster hook

The realistic BILH deployment is ONE cluster hosting many institutions.
`TransferRequestState.RemoteClusterId` is reserved (always null in v1); a future
cross-cluster transfer replicates the XFER grain via the existing `EventEnvelope`
federation seam (ADR-001 lab machinery).

## Consequences

- Deleted outright (15 files): BedManagementGrain/IBedManagementGrain/BedManagementState,
  WardCensusGrain, WardLocationIndexGrain, NursingUnitGrain(+Index) and their
  interfaces/states; stores `bedStore`, `bedBoardStore`, `wardCensusStore`,
  `wardLocationStore`, `nursingUnitStore`, `nursingUnitIndexStore`. Every consumer
  (controllers, Blazor pages, WPF, CharUI, seeds, tests) rewired to the unit model.
- `RecordAdmissionAsync`/`RecordTransferAsync` changed **semantics at the same
  arity** (wardName slot became unitId, roomBed became bedId) — call sites were fixed
  by hand, not by the compiler. Anything external calling the old REST DTO shape must
  move to `UnitId`/`BedId`/`InstitutionId`.
- Admissions now require a configured, active unit; historical/outside-facility
  seeds carry their stays as boarders at minimal bed-less units (LSH, SRMC, REHAB-NSG
  institutions).

## Non-goals
- No auto-merging of institutions; no institution-level policy engine.
- Not touching ICN/MPI (ADR-001) or Person (ADR-002) — this ADR consumes both.
- Charge-nurse/unit-WIDE coverage rosters and discharge-driven relationship expiry
  remain ADR-002 follow-ons, not bed-management concerns.

## Risks
| Risk | Mitigation |
|---|---|
| Unit grain as hot spot | A unit is one ward's traffic — trivially low write rates; capacity pushes are one extra call. |
| Rollup drift | One-directional pushes + re-push on activation; census/board reads come straight from unit truth. |
| Same-arity signature change | All call sites audited by hand (grep) in the same change set; REST DTOs renamed so external callers break loudly, not silently. |
| Acting-institution spoofing | Documented v1 limit; per-facility RBAC is the named upgrade path. |
