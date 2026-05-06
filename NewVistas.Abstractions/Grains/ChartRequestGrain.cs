// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ChartRequestGrain : Grain, IChartRequestGrain
{
    private readonly IPersistentState<ChartRequestState> _state;

    public ChartRequestGrain(
        [PersistentState("rtRequestState", "rtRequestStore")] IPersistentState<ChartRequestState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.RequestId))
            _state.State.RequestId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateRequestAsync(
        string patientId,
        string patientName,
        string requestedById,
        string requestedByName,
        DateTime neededBy,
        ChartRequestPriority priority,
        string requestedForLocation,
        ChartRequestType requestType,
        string notes)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.RequestedById = requestedById;
        _state.State.RequestedByName = requestedByName;
        _state.State.RequestDate = DateTime.UtcNow;
        _state.State.NeededBy = neededBy;
        _state.State.Priority = priority;
        _state.State.RequestedForLocation = requestedForLocation;
        _state.State.RequestType = requestType;
        _state.State.Notes = notes;
        _state.State.Status = ChartRequestStatus.Pending;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FulfillRequestAsync(string fulfilledBy)
    {
        _state.State.Status = ChartRequestStatus.Pulled;
        _state.State.FulfilledBy = fulfilledBy;
        _state.State.FulfilledDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkInTransitAsync(string handledBy)
    {
        _state.State.Status = ChartRequestStatus.InTransit;
        _state.State.FulfilledBy = handledBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDeliveredAsync(string handledBy)
    {
        _state.State.Status = ChartRequestStatus.Delivered;
        _state.State.FulfilledBy = handledBy;
        _state.State.FulfilledDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkNotFoundAsync()
    {
        _state.State.Status = ChartRequestStatus.NotFound;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelRequestAsync(string cancellationReason)
    {
        _state.State.Status = ChartRequestStatus.Cancelled;
        _state.State.CancellationReason = cancellationReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<ChartRequestState> GetRequestAsync() => Task.FromResult(_state.State);
}
