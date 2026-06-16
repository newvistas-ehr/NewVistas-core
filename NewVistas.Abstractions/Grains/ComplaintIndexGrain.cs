// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class ComplaintIndexState
{
    [Id(0)] public List<ComplaintIndexEntry> Complaints { get; set; } = new();
}

public class ComplaintIndexGrain : Grain, IComplaintIndexGrain
{
    private readonly IPersistentState<ComplaintIndexState> _state;

    public ComplaintIndexGrain(
        [PersistentState("paComplaintIndexState", "paComplaintIndexStore")] IPersistentState<ComplaintIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertComplaintAsync(ComplaintIndexEntry entry)
    {
        ComplaintIndexEntry? existing = _state.State.Complaints.Find(c => c.ComplaintId == entry.ComplaintId);
        if (existing is not null)
            _state.State.Complaints.Remove(existing);
        _state.State.Complaints.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<ComplaintIndexEntry>> GetAllComplaintsAsync()
    {
        List<ComplaintIndexEntry> result = _state.State.Complaints
            .OrderByDescending(c => c.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ComplaintIndexEntry>> GetComplaintsByStatusAsync(ComplaintStatus status)
    {
        List<ComplaintIndexEntry> result = _state.State.Complaints
            .Where(c => c.Status == status)
            .OrderByDescending(c => c.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ComplaintIndexEntry>> GetComplaintsByPatientAsync(string patientId, int maxResults = 50)
    {
        List<ComplaintIndexEntry> result = _state.State.Complaints
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.ReceivedDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ComplaintIndexEntry>> GetComplaintsByTypeAsync(ComplaintType complaintType)
    {
        List<ComplaintIndexEntry> result = _state.State.Complaints
            .Where(c => c.ComplaintType == complaintType)
            .OrderByDescending(c => c.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<ComplaintIndexEntry>> GetOverdueComplaintsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<ComplaintIndexEntry> result = _state.State.Complaints
            .Where(c => c.ResponseDue < now
                     && c.Status is ComplaintStatus.Received or ComplaintStatus.Acknowledged
                        or ComplaintStatus.UnderInvestigation or ComplaintStatus.ResponseDrafted
                        or ComplaintStatus.Escalated)
            .OrderBy(c => c.ResponseDue)
            .ToList();
        return Task.FromResult(result);
    }
}
