// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class OutbreakGrain : Grain, IOutbreakGrain
{
    private readonly IPersistentState<OutbreakState> _state;

    public OutbreakGrain(
        [PersistentState("outbreakState", "haiOutbreakStore")] IPersistentState<OutbreakState> state)
    {
        _state = state;
    }

    public Task<OutbreakState> GetOutbreakAsync() =>
        Task.FromResult(_state.State);

    public async Task CreateOutbreakAsync(
        string outbreakId,
        string name,
        string description,
        HAIType haiType,
        DateTime? startDate,
        string locationId,
        string locationName,
        string pathogen)
    {
        _state.State.OutbreakId = outbreakId;
        _state.State.Name = name;
        _state.State.Description = description ?? string.Empty;
        _state.State.HAIType = haiType;
        _state.State.Status = OutbreakStatus.Active;
        _state.State.StartDate = startDate;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Pathogen = pathogen ?? string.Empty;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(OutbreakStatus status, DateTime? controlDate, DateTime? closeDate)
    {
        _state.State.Status = status;
        if (controlDate.HasValue)
            _state.State.ControlDate = controlDate;
        if (closeDate.HasValue)
            _state.State.CloseDate = closeDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCaseAsync(string caseId)
    {
        if (!_state.State.LinkedCaseIds.Contains(caseId))
        {
            _state.State.LinkedCaseIds.Add(caseId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveCaseAsync(string caseId)
    {
        if (_state.State.LinkedCaseIds.Remove(caseId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task NotifyPublicHealthAsync(DateTime notificationDate)
    {
        _state.State.NotifiedPublicHealth = true;
        _state.State.NotificationDate = notificationDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
