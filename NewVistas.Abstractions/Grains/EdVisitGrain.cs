// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// ED Visit Grain — manages a single Emergency Department visit.
/// Store: edVisitStore
/// </summary>
public class EdVisitGrain : Grain, IEdVisitGrain
{
    private readonly IPersistentState<EdVisitState> _state;
    private readonly ILogger<EdVisitGrain> _logger;

    public EdVisitGrain(
        [PersistentState("edVisitState", "edVisitStore")] IPersistentState<EdVisitState> state,
        ILogger<EdVisitGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_state.State.VisitId))
        {
            _state.State.VisitId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(ct);
    }

    public Task<EdVisitState> GetVisitAsync() => Task.FromResult(_state.State);

    public async Task RegisterArrivalAsync(
        string patientId, string patientName, string chiefComplaint,
        string arrivalMode, DateTime arrivalDateTime)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ChiefComplaint = chiefComplaint;
        _state.State.ArrivalMode = arrivalMode;
        _state.State.ArrivalDateTime = arrivalDateTime;
        _state.State.Status = "WAITING";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task TriageAsync(int acuityLevel, string nurseId, string nurseName)
    {
        _state.State.AcuityLevel = acuityLevel;
        _state.State.TriageNurseId = nurseId;
        _state.State.TriageNurseName = nurseName;
        _state.State.TriageDateTime = DateTime.UtcNow;
        _state.State.Status = "TRIAGED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignBedAsync(string bedName)
    {
        _state.State.AssignedBed = bedName;
        _state.State.BedAssignedDateTime = DateTime.UtcNow;
        if (_state.State.Status is "WAITING" or "TRIAGED")
            _state.State.Status = "IN_TREATMENT";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignPhysicianAsync(string physicianId, string physicianName)
    {
        _state.State.AttendingPhysicianId = physicianId;
        _state.State.AttendingPhysicianName = physicianName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignNurseAsync(string nurseId, string nurseName)
    {
        _state.State.AssignedNurseId = nurseId;
        _state.State.AssignedNurseName = nurseName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordPhysicianSeenAsync()
    {
        _state.State.PhysicianSeenDateTime = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateBoardCommentAsync(string comment)
    {
        _state.State.BoardComment = comment;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdatePendingResultsAsync(string pendingResults)
    {
        _state.State.PendingResults = pendingResults;
        _state.State.Status = "AWAITING_RESULTS";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DischargeAsync(string diagnosis)
    {
        _state.State.Disposition = "DISCHARGE";
        _state.State.DischargeDiagnosis = diagnosis;
        _state.State.DispositionDateTime = DateTime.UtcNow;
        _state.State.Status = "DISCHARGED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AdmitAsync(string destinationWard)
    {
        _state.State.Disposition = "ADMIT";
        _state.State.AdmitDestination = destinationWard;
        _state.State.DispositionDateTime = DateTime.UtcNow;
        _state.State.Status = "ADMITTED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task TransferAsync(string destinationFacility)
    {
        _state.State.Disposition = "TRANSFER";
        _state.State.AdmitDestination = destinationFacility;
        _state.State.DispositionDateTime = DateTime.UtcNow;
        _state.State.Status = "TRANSFERRED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordLwbsAsync()
    {
        _state.State.Disposition = "LWBS";
        _state.State.DispositionDateTime = DateTime.UtcNow;
        _state.State.Status = "LWBS";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAmaAsync()
    {
        _state.State.Disposition = "AMA";
        _state.State.DispositionDateTime = DateTime.UtcNow;
        _state.State.Status = "AMA";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// ED Tracking Board Grain — singleton per facility for live ED board.
/// Store: edBoardStore
/// </summary>
public class EdBoardGrain : Grain, IEdBoardGrain
{
    private readonly IPersistentState<EdBoardState> _state;
    private readonly ILogger<EdBoardGrain> _logger;

    public EdBoardGrain(
        [PersistentState("edBoardState", "edBoardStore")] IPersistentState<EdBoardState> state,
        ILogger<EdBoardGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public Task<EdBoardState> GetBoardAsync() => Task.FromResult(_state.State);

    public Task<List<EdBoardEntry>> GetActiveVisitsAsync() =>
        Task.FromResult(_state.State.ActiveVisits
            .OrderBy(v => v.AcuityLevel)
            .ThenBy(v => v.ArrivalDateTime)
            .ToList());

    public Task<List<EdBoardEntry>> GetWaitingPatientsAsync() =>
        Task.FromResult(_state.State.ActiveVisits
            .Where(v => v.Status == "WAITING")
            .OrderBy(v => v.AcuityLevel)
            .ThenBy(v => v.ArrivalDateTime)
            .ToList());

    public async Task AddOrUpdateVisitAsync(EdBoardEntry entry)
    {
        int idx = _state.State.ActiveVisits.FindIndex(v => v.VisitId == entry.VisitId);
        if (idx >= 0)
            _state.State.ActiveVisits[idx] = entry;
        else
            _state.State.ActiveVisits.Add(entry);

        _state.State.OccupiedBeds = _state.State.ActiveVisits.Count(v => v.AssignedBed != null);
        await _state.WriteStateAsync();
    }

    public async Task RemoveVisitAsync(string visitId)
    {
        _state.State.ActiveVisits.RemoveAll(v => v.VisitId == visitId);
        _state.State.OccupiedBeds = _state.State.ActiveVisits.Count(v => v.AssignedBed != null);
        await _state.WriteStateAsync();
    }

    public Task<int> GetWaitingCountAsync() =>
        Task.FromResult(_state.State.ActiveVisits.Count(v => v.Status == "WAITING"));

    public Task<int> GetOccupiedBedCountAsync() =>
        Task.FromResult(_state.State.OccupiedBeds);
}
