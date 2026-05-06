// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class QMReviewIndexState
{
    [Id(0)] public List<QMReviewIndexEntry> Reviews { get; set; } = new();
}

public class QMReviewIndexGrain : Grain, IQMReviewIndexGrain
{
    private readonly IPersistentState<QMReviewIndexState> _state;

    public QMReviewIndexGrain(
        [PersistentState("qmReviewIndexState", "qmReviewIndexStore")] IPersistentState<QMReviewIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertReviewAsync(QMReviewIndexEntry entry)
    {
        QMReviewIndexEntry? existing = _state.State.Reviews.Find(r => r.ReviewId == entry.ReviewId);
        if (existing is not null)
            _state.State.Reviews.Remove(existing);
        _state.State.Reviews.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<QMReviewIndexEntry>> GetAllReviewsAsync()
    {
        List<QMReviewIndexEntry> result = _state.State.Reviews
            .OrderByDescending(r => r.DueDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<QMReviewIndexEntry>> GetReviewsForIncidentAsync(string incidentId)
    {
        List<QMReviewIndexEntry> result = _state.State.Reviews
            .Where(r => r.IncidentId == incidentId)
            .OrderByDescending(r => r.DueDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<QMReviewIndexEntry>> GetPendingReviewsAsync()
    {
        List<QMReviewIndexEntry> result = _state.State.Reviews
            .Where(r => r.Status is QMReviewStatus.Pending or QMReviewStatus.InProgress)
            .OrderBy(r => r.DueDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<QMReviewIndexEntry>> GetOverdueReviewsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<QMReviewIndexEntry> result = _state.State.Reviews
            .Where(r => r.DueDate < now
                     && r.Status is QMReviewStatus.Pending or QMReviewStatus.InProgress)
            .OrderBy(r => r.DueDate)
            .ToList();
        return Task.FromResult(result);
    }
}
