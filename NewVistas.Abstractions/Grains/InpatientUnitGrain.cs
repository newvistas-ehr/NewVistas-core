// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Inpatient unit — the single writer/owner of its rooms, beds, occupancy,
/// reservations, EVS turnover, and nursing bed assignments (Files #42/#210/#405.4).
///
/// Consistency model: every mutation is atomic within the unit (Orleans
/// single-threading), then pushes a compact rollup to the institution's
/// IBedCapacityGrain. Reservation expiry is enforced by a lazy sweep at the top
/// of every operation and on activation — no timers, deterministic, and the
/// next capacity push always carries corrected counts.
/// </summary>
public class InpatientUnitGrain : Grain, IInpatientUnitGrain
{
    private readonly IPersistentState<InpatientUnitState> _state;

    public InpatientUnitGrain(
        [PersistentState("inpatientUnit", "inpatientUnitStore")]
        IPersistentState<InpatientUnitState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Parse "UNIT:{institutionId}:{unitId}" from the grain key.
        if (string.IsNullOrEmpty(_state.State.UnitId))
        {
            string rawKey = this.GetPrimaryKeyString();
            if (rawKey.StartsWith("UNIT:"))
            {
                string remainder = rawKey["UNIT:".Length..];
                int split = remainder.IndexOf(':');
                if (split > 0)
                {
                    _state.State.InstitutionId = remainder[..split];
                    _state.State.UnitId = remainder[(split + 1)..];
                }
            }
        }

        // Self-heal: a configured unit re-pushes its rollup so a missed push
        // (crash between write and push) is corrected on next activation.
        if (!string.IsNullOrEmpty(_state.State.Name))
        {
            SweepExpiredReservations();
            await PushCapacityAsync();
        }

        await base.OnActivateAsync(cancellationToken);
    }

    // ─── Reads ───────────────────────────────────────────────────────────

    public async Task<InpatientUnitState> GetAsync()
    {
        if (await SweepAndSaveAsync())
            await PushCapacityAsync();
        return _state.State;
    }

    public async Task<List<UnitCensusEntry>> GetCensusAsync()
    {
        if (await SweepAndSaveAsync())
            await PushCapacityAsync();

        var census = _state.State.Beds
            .Where(b => b.State == BedLifecycleState.Occupied && b.PatientId != null)
            .Select(b => new UnitCensusEntry
            {
                PatientId = b.PatientId!,
                PatientName = b.PatientName ?? string.Empty,
                BedId = b.BedId,
                RoomId = string.IsNullOrEmpty(b.RoomId) ? null : b.RoomId,
                MovementId = b.MovementId ?? string.Empty,
                AdmitDate = b.OccupiedSince ?? DateTime.MinValue,
                TreatingSpecialty = b.TreatingSpecialty,
                AttendingPhysicianName = b.AttendingPhysicianName,
                AttendingNurseName = b.AttendingNurseName,
                AcuityLevel = b.AcuityLevel
            })
            .Concat(_state.State.Boarders.Select(x => new UnitCensusEntry
            {
                PatientId = x.PatientId,
                PatientName = x.PatientName,
                BedId = null,
                RoomId = null,
                MovementId = x.MovementId,
                AdmitDate = x.AdmitDate,
                TreatingSpecialty = x.TreatingSpecialty,
                AttendingPhysicianName = x.AttendingPhysicianName
            }))
            .OrderBy(e => e.BedId ?? "~") // boarders sort last
            .ToList();

        return census;
    }

    public async Task<UnitCapacitySummary> GetCapacitySummaryAsync()
    {
        if (await SweepAndSaveAsync())
            await PushCapacityAsync();
        return BuildSummary();
    }

    // ─── Structure ───────────────────────────────────────────────────────

    public async Task ConfigureUnitAsync(string name, string? unitType, string? defaultTreatingSpecialty)
    {
        _state.State.Name = name;
        _state.State.UnitType = unitType;
        _state.State.DefaultTreatingSpecialty = defaultTreatingSpecialty;
        _state.State.IsActive = true;
        await SaveAndPushAsync();
    }

    public async Task AddOrUpdateRoomAsync(InpatientRoom room)
    {
        if (string.IsNullOrWhiteSpace(room.RoomId))
            throw new InvalidOperationException("RoomId is required.");

        _state.State.Rooms.RemoveAll(r => r.RoomId == room.RoomId);
        _state.State.Rooms.Add(room);
        _state.State.Rooms.Sort((a, b) => string.Compare(a.RoomId, b.RoomId, StringComparison.OrdinalIgnoreCase));
        await SaveAndPushAsync();
    }

    public async Task AddBedAsync(string bedId, string? roomId, BedType bedType)
    {
        if (string.IsNullOrWhiteSpace(bedId))
            throw new InvalidOperationException("BedId is required.");
        if (_state.State.Beds.Any(b => b.BedId == bedId))
            throw new InvalidOperationException($"Bed '{bedId}' already exists on unit {_state.State.UnitId}.");
        // A room reference must be real when given; a blank roomId is the no-rooms mode.
        if (!string.IsNullOrWhiteSpace(roomId) && _state.State.Rooms.All(r => r.RoomId != roomId))
            throw new InvalidOperationException($"Room '{roomId}' does not exist on unit {_state.State.UnitId}.");

        _state.State.Beds.Add(new InpatientBed
        {
            BedId = bedId,
            RoomId = roomId ?? string.Empty,
            BedType = bedType,
            State = BedLifecycleState.Available,
            LastModifiedDate = DateTime.UtcNow
        });
        _state.State.Beds.Sort((a, b) => string.Compare(a.BedId, b.BedId, StringComparison.OrdinalIgnoreCase));
        await SaveAndPushAsync();
    }

    public async Task RemoveBedAsync(string bedId)
    {
        InpatientBed bed = RequireBed(bedId);
        if (bed.State is BedLifecycleState.Occupied or BedLifecycleState.Reserved)
            throw new InvalidOperationException($"Bed '{bedId}' is {bed.State} — release it before removing.");

        _state.State.Beds.Remove(bed);
        await SaveAndPushAsync();
    }

    public async Task DeactivateUnitAsync()
    {
        SweepExpiredReservations();
        if (_state.State.Beds.Any(b => b.State is BedLifecycleState.Occupied or BedLifecycleState.Reserved)
            || _state.State.Boarders.Count > 0)
            throw new InvalidOperationException(
                $"Unit {_state.State.UnitId} has occupants, boarders, or reservations — it cannot be deactivated.");

        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await Capacity().RemoveUnitAsync(_state.State.UnitId);
    }

    public async Task SetChargeNurseAsync(string nurseId, string nurseName)
    {
        _state.State.ChargeNurseId = nurseId;
        _state.State.ChargeNurseName = nurseName;
        await SaveAndPushAsync();
    }

    // ─── Placement ───────────────────────────────────────────────────────

    public async Task AdmitPatientAsync(UnitAdmissionRequest request)
    {
        SweepExpiredReservations();

        if (string.IsNullOrWhiteSpace(request.PatientId) || string.IsNullOrWhiteSpace(request.MovementId))
            throw new InvalidOperationException("PatientId and MovementId are required.");

        // Idempotent by MovementId: a retry of the same placement is a no-op success.
        if (_state.State.Beds.Any(b => b.MovementId == request.MovementId && b.PatientId == request.PatientId)
            || _state.State.Boarders.Any(x => x.MovementId == request.MovementId))
            return;

        if (!_state.State.IsActive)
            throw new InvalidOperationException($"Unit {_state.State.UnitId} is inactive.");

        // One census entry per patient per unit.
        if (_state.State.Beds.Any(b => b.State == BedLifecycleState.Occupied && b.PatientId == request.PatientId)
            || _state.State.Boarders.Any(x => x.PatientId == request.PatientId))
            throw new InvalidOperationException(
                $"Patient {request.PatientId} is already on unit {_state.State.UnitId}.");

        string? specialty = request.TreatingSpecialty ?? _state.State.DefaultTreatingSpecialty;

        if (string.IsNullOrWhiteSpace(request.BedId))
        {
            // Boarder — admitted to the unit, no bed (ED boarding / bedless small site).
            _state.State.Boarders.Add(new UnitBoarder
            {
                PatientId = request.PatientId,
                PatientName = request.PatientName,
                MovementId = request.MovementId,
                AdmitDate = request.AdmitDate,
                TreatingSpecialty = specialty,
                AttendingPhysicianName = request.AttendingPhysicianName
            });
        }
        else
        {
            InpatientBed bed = RequireBed(request.BedId);
            OccupyBed(bed, request, specialty);

            // ADR-002 Phase 4b: an attending-nurse assignment at admit authorizes the
            // nurse on the patient's chart (UnitCoverage) — see AssignBedNurseAsync.
            if (!string.IsNullOrWhiteSpace(request.AttendingNurseId))
                await EstablishNurseCoverageAsync(request.AttendingNurseId, request.PatientId, bed.BedId);
        }

        await SaveAndPushAsync();
    }

    public async Task AssignBedAsync(string patientId, string bedId, bool overrideReservation)
    {
        SweepExpiredReservations();

        UnitBoarder boarder = _state.State.Boarders.FirstOrDefault(x => x.PatientId == patientId)
            ?? throw new InvalidOperationException(
                $"Patient {patientId} is not a boarder on unit {_state.State.UnitId}.");

        InpatientBed bed = RequireBed(bedId);
        OccupyBed(bed, new UnitAdmissionRequest
        {
            PatientId = boarder.PatientId,
            PatientName = boarder.PatientName,
            MovementId = boarder.MovementId,
            AdmitDate = boarder.AdmitDate,
            AttendingPhysicianName = boarder.AttendingPhysicianName,
            OverrideReservation = overrideReservation
        }, boarder.TreatingSpecialty);
        // Bed truth is the unit census; the original ADT movement is history and is
        // not rewritten when a boarder is later placed in a bed.
        bed.OccupiedSince = boarder.AdmitDate;

        _state.State.Boarders.Remove(boarder);
        await SaveAndPushAsync();
    }

    public async Task ReserveBedAsync(string bedId, string patientId, string patientName, DateTime? expiresAt)
    {
        SweepExpiredReservations();
        InpatientBed bed = RequireBed(bedId);

        // Idempotent re-reserve for the same patient refreshes the expiry.
        if (bed.State == BedLifecycleState.Reserved && bed.ReservedForPatientId == patientId)
        {
            bed.ReservationExpiresAt = expiresAt;
            bed.LastModifiedDate = DateTime.UtcNow;
            await SaveAndPushAsync();
            return;
        }

        Transition(bed, BedLifecycleState.Reserved, expected: BedLifecycleState.Available);
        bed.ReservedForPatientId = patientId;
        bed.ReservedForPatientName = patientName;
        bed.ReservationExpiresAt = expiresAt;
        await SaveAndPushAsync();
    }

    public async Task ClearReservationAsync(string bedId)
    {
        InpatientBed bed = RequireBed(bedId);
        if (bed.State != BedLifecycleState.Reserved)
            return; // idempotent — expired sweep or double-clear

        bed.State = BedLifecycleState.Available;
        ClearReservationFields(bed);
        bed.LastModifiedDate = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    public async Task<string?> ReleasePatientAsync(string patientId, string movementId)
    {
        InpatientBed? bed = _state.State.Beds.FirstOrDefault(
            b => b.State == BedLifecycleState.Occupied && b.PatientId == patientId);
        if (bed is not null)
        {
            VacateBed(bed);
            await SaveAndPushAsync();
            return bed.BedId;
        }

        UnitBoarder? boarder = _state.State.Boarders.FirstOrDefault(x => x.PatientId == patientId);
        if (boarder is not null)
        {
            _state.State.Boarders.Remove(boarder);
            await SaveAndPushAsync();
        }
        // Not on the unit at all → idempotent no-op (movement history still records the discharge).
        return null;
    }

    public async Task MoveOccupantAsync(string patientId, string toBedId, string movementId, bool overrideReservation)
    {
        SweepExpiredReservations();

        InpatientBed from = _state.State.Beds.FirstOrDefault(
                b => b.State == BedLifecycleState.Occupied && b.PatientId == patientId)
            ?? throw new InvalidOperationException(
                $"Patient {patientId} does not occupy a bed on unit {_state.State.UnitId}.");
        if (from.BedId == toBedId)
            return; // no-op

        InpatientBed to = RequireBed(toBedId);
        OccupyBed(to, new UnitAdmissionRequest
        {
            PatientId = from.PatientId!,
            PatientName = from.PatientName ?? string.Empty,
            MovementId = movementId,
            AdmitDate = from.OccupiedSince ?? DateTime.UtcNow,
            ExpectedDischargeDate = from.ExpectedDischargeDate,
            AttendingPhysicianId = from.AttendingPhysicianId,
            AttendingPhysicianName = from.AttendingPhysicianName,
            OverrideReservation = overrideReservation
        }, from.TreatingSpecialty);
        // Carry the nursing assignment with the patient.
        to.AttendingNurseId = from.AttendingNurseId;
        to.AttendingNurseName = from.AttendingNurseName;
        to.AcuityLevel = from.AcuityLevel;
        to.OccupiedSince = from.OccupiedSince;

        VacateBed(from);
        await SaveAndPushAsync();
    }

    // ─── Nursing ─────────────────────────────────────────────────────────

    public async Task AssignBedNurseAsync(string bedId, string? nurseId, string? nurseName)
    {
        InpatientBed bed = RequireBed(bedId);
        bed.AttendingNurseId = nurseId;
        bed.AttendingNurseName = nurseName;
        bed.LastModifiedDate = DateTime.UtcNow;

        // ADR-002 Phase 4b: the bed's attending nurse gains a treatment relationship to
        // THIS patient — the "covering nurse who ends up in your room" case. Being
        // assigned to the bed authorizes her frictionlessly on a sensitive/employee-
        // patient chart (no break-the-glass), rather than the unworkable alternative of
        // a pre-published who-will-know roster. Access is still audited; a nurse never
        // assigned to the bed still hits break-the-glass. The source is qualified by
        // unit ("301A" exists on every floor) so cross-unit bed names can't collide.
        if (!string.IsNullOrWhiteSpace(nurseId) && bed.State == BedLifecycleState.Occupied && bed.PatientId != null)
            await EstablishNurseCoverageAsync(nurseId, bed.PatientId, bed.BedId);

        await SaveAndPushAsync();
    }

    public async Task UpdateBedAcuityAsync(string bedId, AcuityLevel level)
    {
        InpatientBed bed = RequireBed(bedId);
        bed.AcuityLevel = level;
        bed.LastModifiedDate = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    // ─── EVS + bed condition ─────────────────────────────────────────────

    public async Task StartCleaningAsync(string bedId, string? byUserName)
    {
        InpatientBed bed = RequireBed(bedId);
        Transition(bed, BedLifecycleState.Cleaning, expected: BedLifecycleState.Dirty);
        bed.CleaningStartedAt = DateTime.UtcNow;
        bed.CleaningByUserName = byUserName;
        await SaveAndPushAsync();
    }

    public async Task MarkBedCleanAsync(string bedId, string? byUserName)
    {
        InpatientBed bed = RequireBed(bedId);
        if (bed.State is not (BedLifecycleState.Dirty or BedLifecycleState.Cleaning))
            throw new InvalidOperationException(
                $"Bed '{bedId}' is {bed.State} — only a Dirty or Cleaning bed can be marked clean.");

        bed.State = BedLifecycleState.Available;
        bed.LastCleanedAt = DateTime.UtcNow;
        bed.CleaningByUserName = byUserName ?? bed.CleaningByUserName;
        bed.DirtySince = null;
        bed.CleaningStartedAt = null;
        bed.LastModifiedDate = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    public async Task MarkBedDirtyAsync(string bedId)
    {
        InpatientBed bed = RequireBed(bedId);
        Transition(bed, BedLifecycleState.Dirty, expected: BedLifecycleState.Available);
        bed.DirtySince = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    public async Task BlockBedAsync(string bedId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A block reason is required.");

        InpatientBed bed = RequireBed(bedId);
        if (bed.State is BedLifecycleState.Occupied or BedLifecycleState.Reserved)
            throw new InvalidOperationException(
                $"Bed '{bedId}' is {bed.State} — release/clear it before blocking.");

        bed.State = BedLifecycleState.Blocked;
        bed.BlockReason = reason;
        bed.LastModifiedDate = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    public async Task UnblockBedAsync(string bedId)
    {
        InpatientBed bed = RequireBed(bedId);
        Transition(bed, BedLifecycleState.Available, expected: BedLifecycleState.Blocked);
        bed.BlockReason = null;
        await SaveAndPushAsync();
    }

    public async Task SetOutOfServiceAsync(string bedId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("An out-of-service reason is required.");

        InpatientBed bed = RequireBed(bedId);
        if (bed.State is BedLifecycleState.Occupied or BedLifecycleState.Reserved)
            throw new InvalidOperationException(
                $"Bed '{bedId}' is {bed.State} — release/clear it before taking it out of service.");

        bed.State = BedLifecycleState.OutOfService;
        bed.BlockReason = reason;
        bed.LastModifiedDate = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    public async Task ReturnToServiceAsync(string bedId)
    {
        InpatientBed bed = RequireBed(bedId);
        // Physical work happened → the bed must be cleaned before it is placeable.
        Transition(bed, BedLifecycleState.Dirty, expected: BedLifecycleState.OutOfService);
        bed.BlockReason = null;
        bed.DirtySince = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    public async Task SetBedIsolationAsync(string bedId, BedIsolationType isolation)
    {
        InpatientBed bed = RequireBed(bedId);
        bed.Isolation = isolation;
        bed.LastModifiedDate = DateTime.UtcNow;
        await SaveAndPushAsync();
    }

    // ─── Internals ───────────────────────────────────────────────────────

    private IBedCapacityGrain Capacity()
        => GrainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{_state.State.InstitutionId}");

    private InpatientBed RequireBed(string bedId)
        => _state.State.Beds.FirstOrDefault(b => b.BedId == bedId)
           ?? throw new InvalidOperationException($"Bed '{bedId}' does not exist on unit {_state.State.UnitId}.");

    /// <summary>The single transition guard — every lifecycle change funnels through here.</summary>
    private void Transition(InpatientBed bed, BedLifecycleState to, BedLifecycleState expected)
    {
        if (bed.State != expected)
            throw new InvalidOperationException(
                $"Bed '{bed.BedId}' is {bed.State}; {expected} is required to go to {to}.");
        bed.State = to;
        bed.LastModifiedDate = DateTime.UtcNow;
    }

    /// <summary>Occupy a bed for a placement request — the one place occupancy rules live.</summary>
    private void OccupyBed(InpatientBed bed, UnitAdmissionRequest request, string? specialty)
    {
        switch (bed.State)
        {
            case BedLifecycleState.Available:
                break;
            case BedLifecycleState.Reserved when bed.ReservedForPatientId == request.PatientId:
                break; // the reserved patient arrives — reservation auto-clears below
            case BedLifecycleState.Reserved when request.OverrideReservation:
                break; // bed-control override (VistA-style)
            case BedLifecycleState.Reserved:
                throw new InvalidOperationException(
                    $"Bed '{bed.BedId}' is reserved for {bed.ReservedForPatientName ?? bed.ReservedForPatientId}.");
            case BedLifecycleState.Occupied:
                throw new InvalidOperationException(
                    $"Bed '{bed.BedId}' is occupied by {bed.PatientName ?? bed.PatientId}.");
            default:
                throw new InvalidOperationException(
                    $"Bed '{bed.BedId}' is {bed.State} — not placeable.");
        }

        ClearReservationFields(bed);
        bed.State = BedLifecycleState.Occupied;
        bed.PatientId = request.PatientId;
        bed.PatientName = request.PatientName;
        bed.MovementId = request.MovementId;
        bed.OccupiedSince = request.AdmitDate;
        bed.ExpectedDischargeDate = request.ExpectedDischargeDate;
        bed.TreatingSpecialty = specialty;
        bed.AttendingPhysicianId = request.AttendingPhysicianId;
        bed.AttendingPhysicianName = request.AttendingPhysicianName;
        bed.AttendingNurseId = request.AttendingNurseId;
        bed.AttendingNurseName = request.AttendingNurseName;
        bed.LastModifiedDate = DateTime.UtcNow;
    }

    /// <summary>Vacate an occupied bed — it goes to Dirty (EVS turnover), never straight to Available.</summary>
    private static void VacateBed(InpatientBed bed)
    {
        bed.State = BedLifecycleState.Dirty;
        bed.DirtySince = DateTime.UtcNow;
        bed.PatientId = null;
        bed.PatientName = null;
        bed.MovementId = null;
        bed.OccupiedSince = null;
        bed.ExpectedDischargeDate = null;
        bed.TreatingSpecialty = null;
        bed.AttendingPhysicianId = null;
        bed.AttendingPhysicianName = null;
        bed.AttendingNurseId = null;
        bed.AttendingNurseName = null;
        bed.AcuityLevel = null;
        bed.LastModifiedDate = DateTime.UtcNow;
    }

    private static void ClearReservationFields(InpatientBed bed)
    {
        bed.ReservedForPatientId = null;
        bed.ReservedForPatientName = null;
        bed.ReservationExpiresAt = null;
    }

    /// <summary>Lazy reservation-expiry sweep. Returns true when anything changed.</summary>
    private bool SweepExpiredReservations()
    {
        bool changed = false;
        DateTime now = DateTime.UtcNow;
        foreach (InpatientBed bed in _state.State.Beds)
        {
            if (bed.State == BedLifecycleState.Reserved
                && bed.ReservationExpiresAt is { } expiry && expiry <= now)
            {
                bed.State = BedLifecycleState.Available;
                ClearReservationFields(bed);
                bed.LastModifiedDate = now;
                changed = true;
            }
        }
        return changed;
    }

    private async Task<bool> SweepAndSaveAsync()
    {
        if (!SweepExpiredReservations())
            return false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return true;
    }

    private async Task SaveAndPushAsync()
    {
        SweepExpiredReservations();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await PushCapacityAsync();
    }

    private async Task PushCapacityAsync()
        => await Capacity().UpsertUnitAsync(BuildSummary());

    private UnitCapacitySummary BuildSummary()
    {
        List<InpatientBed> beds = _state.State.Beds;
        return new UnitCapacitySummary
        {
            UnitId = _state.State.UnitId,
            InstitutionId = _state.State.InstitutionId,
            Name = _state.State.Name,
            UnitType = _state.State.UnitType,
            IsActive = _state.State.IsActive,
            TotalBeds = beds.Count,
            Available = beds.Count(b => b.State == BedLifecycleState.Available),
            Reserved = beds.Count(b => b.State == BedLifecycleState.Reserved),
            Occupied = beds.Count(b => b.State == BedLifecycleState.Occupied),
            Dirty = beds.Count(b => b.State == BedLifecycleState.Dirty),
            Cleaning = beds.Count(b => b.State == BedLifecycleState.Cleaning),
            Blocked = beds.Count(b => b.State == BedLifecycleState.Blocked),
            OutOfService = beds.Count(b => b.State == BedLifecycleState.OutOfService),
            Boarders = _state.State.Boarders.Count,
            DirtyBeds = beds
                .Where(b => b.State is BedLifecycleState.Dirty or BedLifecycleState.Cleaning)
                .Select(b => new DirtyBedEntry
                {
                    BedId = b.BedId,
                    RoomId = b.RoomId,
                    State = b.State,
                    DirtySince = b.DirtySince,
                    Isolation = b.Isolation
                })
                .ToList(),
            AvailableByType = beds
                .Where(b => b.State == BedLifecycleState.Available)
                .GroupBy(b => b.BedType.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            LastUpdated = DateTime.UtcNow
        };
    }

    private Task EstablishNurseCoverageAsync(string nurseId, string patientId, string bedId)
        => GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}")
            .EstablishRelationshipAsync(nurseId, TreatmentRelationshipReason.UnitCoverage,
                $"BED:{_state.State.UnitId}:{bedId}", null);
}
