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
/// Nursing unit census grain — VistA File #210 (NURSING UNIT).
/// Tracks bed assignments, occupancy, and charge nurse for one inpatient unit.
/// </summary>
public class NursingUnitGrain : Grain, INursingUnitGrain
{
    private readonly IPersistentState<NursingUnitState> _state;

    public NursingUnitGrain(
        [PersistentState("nursingUnitState", "nursingUnitStore")]
        IPersistentState<NursingUnitState> state)
    {
        _state = state;
    }

    public Task<NursingUnitState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task InitializeAsync(string unitName, string? unitType, int totalBeds)
    {
        if (!string.IsNullOrEmpty(_state.State.UnitId))
            return; // idempotent

        string rawKey = this.GetPrimaryKeyString();
        _state.State.UnitId    = rawKey.StartsWith("NURS-UNIT:")
            ? rawKey["NURS-UNIT:".Length..]
            : rawKey;
        _state.State.UnitName  = unitName;
        _state.State.UnitType  = unitType;
        _state.State.TotalBeds = totalBeds;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetChargeNurseAsync(string nurseId, string nurseName)
    {
        _state.State.ChargeNurseId   = nurseId;
        _state.State.ChargeNurseName = nurseName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignPatientAsync(
        string bed,
        string patientId,
        string patientName,
        DateTime admissionDateTime,
        string? attendingNurseId,
        string? attendingNurseName)
    {
        NursingBedAssignment? existing =
            _state.State.BedAssignments.FirstOrDefault(b => b.Bed == bed);

        if (existing is not null)
        {
            existing.PatientId          = patientId;
            existing.PatientName        = patientName;
            existing.AdmissionDateTime  = admissionDateTime;
            existing.IsOccupied         = true;
            existing.AttendingNurseId   = attendingNurseId;
            existing.AttendingNurseName = attendingNurseName;
        }
        else
        {
            _state.State.BedAssignments.Add(new NursingBedAssignment
            {
                Bed                 = bed,
                PatientId           = patientId,
                PatientName         = patientName,
                AdmissionDateTime   = admissionDateTime,
                IsOccupied          = true,
                AttendingNurseId    = attendingNurseId,
                AttendingNurseName  = attendingNurseName
            });
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargeFromBedAsync(string bed)
    {
        NursingBedAssignment? assignment =
            _state.State.BedAssignments.FirstOrDefault(b => b.Bed == bed);

        if (assignment is null) return;

        assignment.PatientId          = null;
        assignment.PatientName        = null;
        assignment.AdmissionDateTime  = null;
        assignment.AcuityLevel        = null;
        assignment.IsOccupied         = false;
        assignment.AttendingNurseId   = null;
        assignment.AttendingNurseName = null;

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateBedAcuityAsync(string bed, AcuityLevel level)
    {
        NursingBedAssignment? assignment =
            _state.State.BedAssignments.FirstOrDefault(b => b.Bed == bed);

        if (assignment is null) return;

        assignment.AcuityLevel = level;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<NursingUnitEntry> GetUnitEntryAsync()
    {
        int occupied = _state.State.BedAssignments.Count(b => b.IsOccupied);
        return Task.FromResult(new NursingUnitEntry
        {
            UnitId       = _state.State.UnitId,
            UnitName     = _state.State.UnitName,
            UnitType     = _state.State.UnitType,
            TotalBeds    = _state.State.TotalBeds,
            OccupiedBeds = occupied
        });
    }
}
