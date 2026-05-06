// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ROIRequestIndexState
{
    [Id(0)] public List<ROIRequestIndexEntry> Requests { get; set; } = new();
}

public class ROIRequestIndexGrain : Grain, IROIRequestIndexGrain
{
    private readonly IPersistentState<ROIRequestIndexState> _state;

    public ROIRequestIndexGrain(
        [PersistentState("roiRequestIndexState", "roiRequestIndexStore")] IPersistentState<ROIRequestIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertRequestAsync(ROIRequestIndexEntry entry)
    {
        ROIRequestIndexEntry? existing = _state.State.Requests.Find(r => r.RequestId == entry.RequestId);
        if (existing is not null)
            _state.State.Requests.Remove(existing);
        _state.State.Requests.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ROIRequestIndexEntry>> GetAllRequestsAsync()
    {
        List<ROIRequestIndexEntry> result = _state.State.Requests
            .OrderByDescending(r => r.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ROIRequestIndexEntry>> GetRequestsByStatusAsync(ROIRequestStatus status)
    {
        List<ROIRequestIndexEntry> result = _state.State.Requests
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ROIRequestIndexEntry>> GetRequestsByPatientAsync(string patientId)
    {
        List<ROIRequestIndexEntry> result = _state.State.Requests
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ROIRequestIndexEntry>> GetOverdueRequestsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<ROIRequestIndexEntry> result = _state.State.Requests
            .Where(r => r.DueDate < now
                     && r.Status is ROIRequestStatus.Received or ROIRequestStatus.Acknowledged
                        or ROIRequestStatus.InProcess or ROIRequestStatus.PendingAuthorization)
            .OrderBy(r => r.DueDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ROIRequestIndexEntry>> GetRequestsByRequesterTypeAsync(RequesterType requesterType)
    {
        List<ROIRequestIndexEntry> result = _state.State.Requests
            .Where(r => r.RequesterType == requesterType)
            .OrderByDescending(r => r.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }
}
