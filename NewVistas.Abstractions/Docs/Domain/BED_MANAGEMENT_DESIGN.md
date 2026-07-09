# Bed & Room Management — Design (ADR-003 companion)

**Status: implemented (2026-07-03).** Architecture decisions and rationale live in
[ADR-003](../Architect-decisions/ADR-003-Institution-Location-Hierarchy.md); this doc
is the working reference: keys, shapes, lifecycle, status machines, surfaces, seeds.

## Key invariants (the contract between layers)

- `{institutionId}` in every location key **is** `InstitutionState.InstitutionId`
  (the segment after `INST:`) — "500", "LAHEY-BURLINGTON". Legacy spellings
  ("MAIN", "INST-500") resolve via `IInstitutionIndexGrain.ResolveLegacyFacilityIdAsync`.
- Grain keys: `INST:{institutionId}` · `INSTITUTION-INDEX` ·
  `UNIT:{institutionId}:{unitId}` · `BED-CAPACITY:{institutionId}` ·
  `SYSTEM-CAPACITY` · `XFER-{guid}` · `TRANSFER-CENTER:{institutionId}`.
- Stores: institutionStore, institutionIndexStore, inpatientUnitStore,
  bedCapacityStore, transferRequestStore, transferCenterStore.
- **Bed truth lives ONLY on the unit grain.** Census (`GetCensusAsync`) and capacity
  (`UnitCapacitySummary` pushes) are projections. Nothing else may store occupancy.
- A patient appears on a unit census either **in a bed** or as a **boarder**
  (admitted, `bedId null`) — never both, never twice.

## Bed lifecycle

```
                 reserve                admit/assign
   ┌─────────── Available ─────────────▶ Occupied
   │            ▲   │  ▲    ─────────▶     │ release (discharge/transfer-out)
   │   unblock  │   │  └── mark-clean      ▼
Blocked ◀──block┘   │mark-dirty          Dirty ──start-cleaning──▶ Cleaning
   │                └───────────────────◀──┴──────── mark-clean ────┘
   └▶ (block also legal from Dirty/Cleaning)
OutOfService ◀── set-out-of-service (from any non-Occupied/-Reserved)
     └── return-to-service ──▶ Dirty      (physical work ⇒ EVS pass required)
```

- Rejections (throw `InvalidOperationException`): occupy an Occupied bed; occupy a
  bed Reserved for someone else without `OverrideReservation`; Block/OutOfService an
  Occupied or Reserved bed; MarkClean from anything but Dirty/Cleaning; Reserve a
  non-Available bed; RemoveBed while Occupied/Reserved; DeactivateUnit with any
  occupant/boarder/reservation.
- Reserved-for-THIS-patient auto-clears on their admission. Reservation expiry:
  lazy sweep, `ReservationExpiresAt` → Available.
- Keys: placement ops `DG ADMIT`; structure + block/OOS `DG BED CONTROL`; EVS flips
  `DG BED CONTROL` **or** `ORELSE` (nurses turn beds at small sites).
  `SetChargeNurseAsync`/`AssignBedNurseAsync`/acuity are ungated (parity with the
  retired NursingUnitGrain); the nurse assignment fires the ADR-002 UnitCoverage
  relationship with sourceRef `BED:{unitId}:{bedId}` when the bed is occupied.

## Transfer request status machine

```
REQUESTED ──AcceptAsync(unit,bed)──▶ ACCEPTED ──CompleteAsync──▶ COMPLETED
   │  └──DeclineAsync(reason)──▶ DECLINED          │
   └────────────CancelAsync──────────┴──▶ CANCELLED (releases reservation)
```

- Reserve-FIRST on accept (workflow reserves via the unit grain, then flips status).
- Complete sequence (all on the one ICN-keyed workflow grain):
  `RecordAdmissionAsync` at receiver (occupies reserved bed; ADR-002 attending hook;
  restores `CurrentAdmission`) → `RecordDischargeAsync(sendingAdmissionId,
  disposition: TRANSFER)` at sender → MPI correlation + TREATING-FAC upsert →
  `xfer.CompleteAsync` → both TRANSFER-CENTER queues refreshed.
- Lost reservation at completion ⇒ stays ACCEPTED; `ReassignTransferBedAsync`
  re-reserves (new bed first, old reservation cleared after), then retry complete.
- Idempotent: re-create, re-accept same bed, re-complete, re-decline, re-cancel are
  no-ops; terminal states reject other transitions.
- All transfer workflow methods: `[RequiresSecurityKey(DG_BED_CONTROL)]` + audited.

## EXT-REF vs XFER

| | ExternalReferral (`EXT-REF:{guid}`) | TransferRequest (`XFER-{guid}`) |
|---|---|---|
| Far end | OUTSIDE organization (community care, CHS) | Another institution in THIS deployment |
| Bed authority | None — tracking only | Receiving side reserves/occupies a real bed |
| Patient record | May not exist at far end | Same ICN-keyed chart everywhere |
| Produces | Authorization/claim trail | File #405 discharge+admission pair |

## Surfaces

- **REST**: `api/beds` (institution capacity, unit board/census, EVS queue,
  available-bed query, unit/room/bed setup, per-bed lifecycle actions),
  `api/transfers` (create/get, center incoming/outgoing, accept/reassign-bed/
  decline/cancel/complete), `api/institutions` (directory, CRUD, system-capacity,
  placement-targets), `api/adt` (structured UnitId/BedId/InstitutionId DTOs;
  `GET api/adt/wards` kept its route, payload is now `UnitCapacitySummary`),
  `api/nursing` unit routes (nurse/acuity only — placement removed on purpose).
- **Blazor**: `/beds` Bed Board (unit cards → room/bed grid, lifecycle colors, EVS +
  block actions, EVS-queue tab, institution picker); `/transfer-center` (incoming/
  outgoing queues, new-request form fed by placement-target search, accept-with-bed-
  picker, complete-arrival; self-hides on single-institution sites);
  `/admin/institutions`; Adt/Nursing/RegistrationEnhanced rewired to the unit model.
- **WPF/CharUI**: mechanically rewired (capacity + unit grains, structured admit).

## Demo seeds (order matters; all idempotent, XUPROG context)

1. `InstitutionSeed` — 500 (aliases MAIN/INST-500) + historical LSH/SRMC/REHAB-NSG +
   BILH trio (LAHEY-BURLINGTON w/ ICU-1+MED-4A, LAWRENCE-GENERAL w/ TELE-2+MED-3B,
   4-bed BILH-CLINIC-ANDOVER) + lifecycle variety.
2. `InpatientUnitSeed` — institution 500's units: MED-3A, MED-4B, SURG-2C, ICU-1,
   TELE-4B, PSYCH-5A (bed-only), OBS-1 + variety (dirty/cleaning/blocked/OOS/isolation).
3. `ExtremeLeeSickSeed` — P9001's outside-facility stays are unit **boarders** at
   LSH/SRMC/REHAB-NSG (released at discharge; no dirty-bed residue).
4. `InterfacilityTransferSeed` — **P9008 "TRANSFERRE,TERRY"**: admitted
   LAHEY-BURLINGTON ICU-1/ICU-2 (NSTEMI), in-flight **REQUESTED** transfer to
   LAWRENCE-GENERAL (TELEMETRY, URGENT) — Lawrence's Transfer Center has one
   actionable incoming request on first login.

## Known limits / future work
- Per-facility RBAC + per-institution feature flags (ADR-003 §5).
- Cross-cluster transfers (RemoteClusterId reserved; federation seam).
- Boarder→bed placement does not write an additional #405 movement (bed truth is the
  unit census; add a VistA-style movement later if wanted).
- Room-level constraints (gender policy, room isolation) are carried on the room but
  not yet ENFORCED at assignment time.
- EVS queue has no personnel assignment / work-order tracking.
