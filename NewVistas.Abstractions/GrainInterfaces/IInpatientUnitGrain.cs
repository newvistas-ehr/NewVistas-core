// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Inpatient unit — the SINGLE writer/owner of its rooms, beds, occupancy,
/// reservations, EVS turnover, and nursing bed assignments. Merges VistA
/// WARD LOCATION (#42), NURSING UNIT (#210), and DGPM Bed Control (#405.4).
///
/// Grain key: "UNIT:{institutionId}:{unitId}". Store: inpatientUnitStore.
///
/// Every bed mutation is atomic within the unit (Orleans single-threading), and
/// each mutation pushes a compact UnitCapacitySummary to the institution's
/// IBedCapacityGrain — one direction, eventual, self-healing on activation.
/// Census and capacity are PROJECTIONS of this state; nothing else stores them.
/// </summary>
public interface IInpatientUnitGrain : IGrainWithStringKey
{
    Task<InpatientUnitState> GetAsync();

    /// <summary>Live census projection (beds + boarders). Sweeps expired reservations first.</summary>
    Task<List<UnitCensusEntry>> GetCensusAsync();

    Task<UnitCapacitySummary> GetCapacitySummaryAsync();

    // ─── Structure (DG BED CONTROL) ──────────────────────────────────────

    /// <summary>Create or update the unit profile. Idempotent; institutionId/unitId parsed from the grain key.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task ConfigureUnitAsync(string name, string? unitType, string? defaultTreatingSpecialty);

    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task AddOrUpdateRoomAsync(InpatientRoom room);

    /// <summary>Rejects duplicate bedId; roomId must exist when the unit models rooms.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task AddBedAsync(string bedId, string? roomId, BedType bedType);

    /// <summary>Rejects while the bed is Occupied or Reserved.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task RemoveBedAsync(string bedId);

    /// <summary>Rejects while any occupant, boarder, or reservation exists; removes the unit from the capacity directory.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task DeactivateUnitAsync();

    /// <summary>Charge nurse for the shift (File #210).</summary>
    Task SetChargeNurseAsync(string nurseId, string nurseName);

    // ─── Patient placement (DG ADMIT or DG BED CONTROL) ─────────────────
    // ADT clerks admit with DG ADMIT; a transfer coordinator/bed-control user
    // (DG BED CONTROL) must equally be able to reserve/occupy/release — the
    // Transfer Center's accept and completion flow through these ops with the
    // ORIGINAL caller's context propagated across grain boundaries.

    /// <summary>
    /// Place a patient on the unit: into a bed (BedId set) or as a boarder (BedId null).
    /// Idempotent by MovementId. Rejects Occupied beds and beds Reserved for someone
    /// else unless OverrideReservation; a bed Reserved for THIS patient auto-clears.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.DG_ADMIT, SecurityKeys.DG_BED_CONTROL)]
    Task AdmitPatientAsync(UnitAdmissionRequest request);

    /// <summary>Move an existing boarder into a bed (ED-boarding resolution).</summary>
    [RequiresSecurityKey(SecurityKeys.DG_ADMIT, SecurityKeys.DG_BED_CONTROL)]
    Task AssignBedAsync(string patientId, string bedId, bool overrideReservation);

    /// <summary>Hold an Available bed for a pending admission or incoming transfer.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_ADMIT, SecurityKeys.DG_BED_CONTROL)]
    Task ReserveBedAsync(string bedId, string patientId, string patientName, DateTime? expiresAt);

    [RequiresSecurityKey(SecurityKeys.DG_ADMIT, SecurityKeys.DG_BED_CONTROL)]
    Task ClearReservationAsync(string bedId);

    /// <summary>
    /// Remove the patient from the unit on discharge or transfer-out: their bed goes
    /// to Dirty, or the boarder entry is removed. Idempotent — returns the vacated
    /// bedId, or null when the patient wasn't on the unit (no-op).
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.DG_ADMIT, SecurityKeys.DG_BED_CONTROL)]
    Task<string?> ReleasePatientAsync(string patientId, string movementId);

    /// <summary>Intra-unit bed swap, atomic: target must be placeable; the old bed goes to Dirty.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_ADMIT, SecurityKeys.DG_BED_CONTROL)]
    Task MoveOccupantAsync(string patientId, string toBedId, string movementId, bool overrideReservation);

    // ─── Nursing (ungated — parity with the retired NursingUnitGrain) ────

    /// <summary>
    /// Assign the bed's attending nurse. When the bed is occupied this establishes the
    /// nurse's UnitCoverage treatment relationship on the patient's chart (ADR-002
    /// Phase 4b — the covering nurse who ends up in your room is authorized by the
    /// bed assignment itself, frictionlessly; never break-the-glass for the team).
    /// </summary>
    Task AssignBedNurseAsync(string bedId, string? nurseId, string? nurseName);

    Task UpdateBedAcuityAsync(string bedId, AcuityLevel level);

    // ─── EVS turnover + bed condition ────────────────────────────────────

    /// <summary>Dirty → Cleaning.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL, SecurityKeys.ORELSE)]
    Task StartCleaningAsync(string bedId, string? byUserName);

    /// <summary>Dirty|Cleaning → Available (skip-start allowed so tiny sites turn a bed in one click).</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL, SecurityKeys.ORELSE)]
    Task MarkBedCleanAsync(string bedId, string? byUserName);

    /// <summary>Available → Dirty (spill, contamination).</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL, SecurityKeys.ORELSE)]
    Task MarkBedDirtyAsync(string bedId);

    /// <summary>Administrative hold; reason required. Rejected while Occupied/Reserved.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task BlockBedAsync(string bedId, string reason);

    /// <summary>Blocked → Available (administrative hold, not hygiene).</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task UnblockBedAsync(string bedId);

    /// <summary>Maintenance/decommission; reason required. Rejected while Occupied/Reserved.</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task SetOutOfServiceAsync(string bedId, string reason);

    /// <summary>OutOfService → Dirty — physical work happened, so the bed must be cleaned before it is placeable (honest capacity).</summary>
    [RequiresSecurityKey(SecurityKeys.DG_BED_CONTROL)]
    Task ReturnToServiceAsync(string bedId);

    Task SetBedIsolationAsync(string bedId, BedIsolationType isolation);
}
