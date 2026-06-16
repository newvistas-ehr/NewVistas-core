// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Bed Grain — manages a single hospital bed's state and assignments.
/// </summary>
public class BedGrain : Grain, IBedGrain
{
    private readonly IPersistentState<BedState> _state;

    public BedGrain(
        [PersistentState("bed", "bedStore")] IPersistentState<BedState> state)
    {
        _state = state;
    }

    public Task<BedState> GetBedAsync() => Task.FromResult(_state.State);

    public async Task SetupBedAsync(string wardId, string wardName, string roomNumber,
        string bedPosition, string bedType, string facilityId)
    {
        string key = this.GetPrimaryKeyString();
        _state.State.BedId = key.Replace($"BED:{facilityId}:", "");
        _state.State.WardId = wardId;
        _state.State.WardName = wardName;
        _state.State.RoomNumber = roomNumber;
        _state.State.BedPosition = bedPosition;
        _state.State.BedType = bedType;
        _state.State.FacilityId = facilityId;
        _state.State.Status = "AVAILABLE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignPatientAsync(string patientId, string patientName, DateTime? expectedDischarge)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.OccupiedSince = DateTime.UtcNow;
        _state.State.ExpectedDischargeDate = expectedDischarge;
        _state.State.ReservedForPatientId = null;
        _state.State.ReservedForPatientName = null;
        _state.State.Status = "OCCUPIED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargePatientAsync()
    {
        _state.State.PatientId = null;
        _state.State.PatientName = null;
        _state.State.OccupiedSince = null;
        _state.State.ExpectedDischargeDate = null;
        _state.State.Status = "CLEANING";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReserveForPatientAsync(string patientId, string patientName)
    {
        _state.State.ReservedForPatientId = patientId;
        _state.State.ReservedForPatientName = patientName;
        _state.State.Status = "RESERVED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ClearReservationAsync()
    {
        _state.State.ReservedForPatientId = null;
        _state.State.ReservedForPatientName = null;
        _state.State.Status = "AVAILABLE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task BlockBedAsync(string reason)
    {
        _state.State.Status = "BLOCKED";
        _state.State.BlockReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UnblockBedAsync()
    {
        _state.State.Status = "AVAILABLE";
        _state.State.BlockReason = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetCleaningAsync()
    {
        _state.State.Status = "CLEANING";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetAvailableAsync()
    {
        _state.State.Status = "AVAILABLE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetIsolationAsync(string? isolationType)
    {
        _state.State.IsolationType = isolationType;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetOutOfServiceAsync(string reason)
    {
        _state.State.Status = "OUT_OF_SERVICE";
        _state.State.BlockReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Bed Board Grain — facility-wide bed index with real-time counts.
/// Self-seeds demo beds on first activation.
/// </summary>
public class BedBoardGrain : Grain, IBedBoardGrain
{
    private readonly IPersistentState<BedBoardState> _state;

    public BedBoardGrain(
        [PersistentState("bedBoard", "bedBoardStore")] IPersistentState<BedBoardState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.Beds.Count == 0)
        {
            await SeedDemoBedsAsync();
        }
        await base.OnActivateAsync(cancellationToken);
    }

    public Task<List<BedSummaryEntry>> GetAllBedsAsync()
        => Task.FromResult(_state.State.Beds);

    public Task<List<BedSummaryEntry>> GetBedsByWardAsync(string wardId)
        => Task.FromResult(_state.State.Beds.Where(b => b.WardId == wardId).ToList());

    public Task<List<BedSummaryEntry>> GetAvailableBedsAsync()
        => Task.FromResult(_state.State.Beds.Where(b => b.Status == "AVAILABLE").ToList());

    public Task<List<BedSummaryEntry>> GetAvailableBedsByTypeAsync(string bedType)
        => Task.FromResult(_state.State.Beds.Where(b => b.Status == "AVAILABLE" && b.BedType == bedType).ToList());

    public async Task AddOrUpdateBedAsync(BedSummaryEntry entry)
    {
        int idx = _state.State.Beds.FindIndex(b => b.BedId == entry.BedId);
        if (idx >= 0)
            _state.State.Beds[idx] = entry;
        else
            _state.State.Beds.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveBedAsync(string bedId)
    {
        _state.State.Beds.RemoveAll(b => b.BedId == bedId);
        await _state.WriteStateAsync();
    }

    public Task<int> GetTotalBedCountAsync()
        => Task.FromResult(_state.State.Beds.Count);

    public Task<int> GetAvailableBedCountAsync()
        => Task.FromResult(_state.State.Beds.Count(b => b.Status == "AVAILABLE"));

    public Task<int> GetOccupiedBedCountAsync()
        => Task.FromResult(_state.State.Beds.Count(b => b.Status == "OCCUPIED"));

    private async Task SeedDemoBedsAsync()
    {
        var wards = new[]
        {
            ("WARD-MED-3A", "Medicine 3A", "REGULAR"),
            ("WARD-SURG-2C", "Surgery 2C", "REGULAR"),
            ("WARD-ICU-1", "ICU", "ICU"),
            ("WARD-TELE-4B", "Telemetry 4B", "TELEMETRY"),
        };

        foreach (var (wardId, wardName, bedType) in wards)
        {
            int bedCount = bedType == "ICU" ? 8 : 12;
            for (int room = 1; room <= bedCount / 2; room++)
            {
                foreach (string pos in new[] { "A", "B" })
                {
                    string bedId = $"{wardId}-{room:D2}{pos}";
                    _state.State.Beds.Add(new BedSummaryEntry
                    {
                        BedId = bedId,
                        WardId = wardId,
                        WardName = wardName,
                        RoomNumber = room.ToString("D2"),
                        BedPosition = pos,
                        Status = "AVAILABLE",
                        BedType = bedType
                    });
                }
            }
        }

        await _state.WriteStateAsync();
    }
}
