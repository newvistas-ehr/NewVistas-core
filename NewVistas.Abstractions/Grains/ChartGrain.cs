// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ChartGrain : Grain, IChartGrain
{
    private readonly IPersistentState<ChartState> _state;

    public ChartGrain(
        [PersistentState("rtChartState", "rtChartStore")] IPersistentState<ChartState> state)
    {
        _state = state;
    }

    private void RecordMovement(ChartMovementAction action, string fromLocation, string toLocation,
        string borrowerId, string borrowerName, string handledBy, string notes = "")
    {
        _state.State.MovementHistory.Add(new ChartMovement
        {
            MovementId = Guid.NewGuid().ToString(),
            MovementDate = DateTime.UtcNow,
            Action = action,
            FromLocation = fromLocation,
            ToLocation = toLocation,
            BorrowerId = borrowerId,
            BorrowerName = borrowerName,
            HandledBy = handledBy,
            Notes = notes
        });
    }

    public async Task InitializeChartAsync(string patientId, string patientName, string chartNumber, string homeLocation)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ChartNumber = chartNumber;
        _state.State.CurrentLocation = homeLocation;
        _state.State.CurrentLocationType = ChartLocationType.FileRoom;
        _state.State.HomeLocation = homeLocation;
        _state.State.IsCheckedOut = false;
        _state.State.IsLost = false;
        _state.State.InitializedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        // Initialize with Volume 1
        _state.State.Volumes.Add(new ChartVolume
        {
            VolumeId = Guid.NewGuid().ToString(),
            VolumeNumber = 1,
            DateRange = $"{DateTime.UtcNow:MM/yyyy} - present",
            IsActive = true,
            CurrentLocation = homeLocation
        });
        RecordMovement(ChartMovementAction.Initialized, string.Empty, homeLocation,
            string.Empty, string.Empty, "System");
        await _state.WriteStateAsync();
    }

    public async Task CheckOutChartAsync(string borrowerId, string borrowerName, string location,
        ChartLocationType locationType, DateTime? expectedReturnDate, string handledBy)
    {
        string prevLocation = _state.State.CurrentLocation;
        _state.State.CurrentBorrowerId = borrowerId;
        _state.State.CurrentBorrowerName = borrowerName;
        _state.State.CurrentLocation = location;
        _state.State.CurrentLocationType = locationType;
        _state.State.IsCheckedOut = true;
        _state.State.CheckOutDate = DateTime.UtcNow;
        _state.State.ExpectedReturnDate = expectedReturnDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        RecordMovement(ChartMovementAction.CheckedOut, prevLocation, location,
            borrowerId, borrowerName, handledBy);
        await _state.WriteStateAsync();
    }

    public async Task CheckInChartAsync(string handledBy)
    {
        string prevLocation = _state.State.CurrentLocation;
        string prevBorrower = _state.State.CurrentBorrowerName;
        _state.State.CurrentLocation = _state.State.HomeLocation;
        _state.State.CurrentLocationType = ChartLocationType.FileRoom;
        _state.State.CurrentBorrowerId = string.Empty;
        _state.State.CurrentBorrowerName = string.Empty;
        _state.State.IsCheckedOut = false;
        _state.State.IsOnRequest = false;
        _state.State.CheckOutDate = null;
        _state.State.ExpectedReturnDate = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        RecordMovement(ChartMovementAction.CheckedIn, prevLocation, _state.State.HomeLocation,
            string.Empty, prevBorrower, handledBy);
        await _state.WriteStateAsync();
    }

    public async Task TransferChartAsync(string newLocation, ChartLocationType newLocationType,
        string newBorrowerId, string newBorrowerName, string handledBy)
    {
        string prevLocation = _state.State.CurrentLocation;
        _state.State.CurrentLocation = newLocation;
        _state.State.CurrentLocationType = newLocationType;
        _state.State.CurrentBorrowerId = newBorrowerId;
        _state.State.CurrentBorrowerName = newBorrowerName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        RecordMovement(ChartMovementAction.Transferred, prevLocation, newLocation,
            newBorrowerId, newBorrowerName, handledBy);
        await _state.WriteStateAsync();
    }

    public async Task SetRequestFlagAsync(bool isOnRequest)
    {
        _state.State.IsOnRequest = isOnRequest;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        if (isOnRequest)
            RecordMovement(ChartMovementAction.Requested,
                _state.State.CurrentLocation, _state.State.CurrentLocation,
                string.Empty, string.Empty, "System");
        await _state.WriteStateAsync();
    }

    public async Task AddVolumeAsync(int volumeNumber, string dateRange)
    {
        // Close the current active volume's date range
        ChartVolume? active = _state.State.Volumes.Find(v => v.IsActive);
        if (active is not null)
            active.IsActive = false;

        _state.State.Volumes.Add(new ChartVolume
        {
            VolumeId = Guid.NewGuid().ToString(),
            VolumeNumber = volumeNumber,
            DateRange = dateRange,
            IsActive = true,
            CurrentLocation = _state.State.CurrentLocation
        });
        RecordMovement(ChartMovementAction.VolumeAdded, string.Empty, string.Empty,
            string.Empty, string.Empty, "System", $"Volume {volumeNumber} added");
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkChartLostAsync(string notes, string handledBy)
    {
        string prevLocation = _state.State.CurrentLocation;
        _state.State.IsLost = true;
        _state.State.CurrentLocationType = ChartLocationType.Lost;
        _state.State.CurrentLocation = "Unknown";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        RecordMovement(ChartMovementAction.Lost, prevLocation, "Unknown",
            string.Empty, string.Empty, handledBy, notes);
        await _state.WriteStateAsync();
    }

    public async Task MarkChartFoundAsync(string location, ChartLocationType locationType, string handledBy)
    {
        _state.State.IsLost = false;
        _state.State.CurrentLocation = location;
        _state.State.CurrentLocationType = locationType;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        RecordMovement(ChartMovementAction.Found, "Unknown", location,
            string.Empty, string.Empty, handledBy);
        await _state.WriteStateAsync();
    }

    public Task<ChartState> GetChartAsync() => Task.FromResult(_state.State);
}
