// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class DBQIndexState
{
    [Id(0)] public List<DBQIndexEntry> DBQs { get; set; } = new();
}

public class DBQIndexGrain : Grain, IDBQIndexGrain
{
    private readonly IPersistentState<DBQIndexState> _state;

    public DBQIndexGrain(
        [PersistentState("cpDbqIndexState", "cpDbqIndexStore")] IPersistentState<DBQIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertDBQAsync(DBQIndexEntry entry)
    {
        DBQIndexEntry? existing = _state.State.DBQs.Find(d => d.DbqId == entry.DbqId);
        if (existing is not null)
            _state.State.DBQs.Remove(existing);
        _state.State.DBQs.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<DBQIndexEntry>> GetAllDBQsAsync()
    {
        List<DBQIndexEntry> result = _state.State.DBQs
            .OrderByDescending(d => d.CompletedDate ?? DateTime.MinValue)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<DBQIndexEntry>> GetDBQsForExamAsync(string examId)
    {
        List<DBQIndexEntry> result = _state.State.DBQs
            .Where(d => d.ExamId == examId)
            .OrderByDescending(d => d.CompletedDate ?? DateTime.MinValue)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<DBQIndexEntry>> GetCompletedDBQsAsync()
    {
        List<DBQIndexEntry> result = _state.State.DBQs
            .Where(d => d.Status is DBQStatus.Completed or DBQStatus.Signed)
            .OrderByDescending(d => d.CompletedDate)
            .ToList();
        return Task.FromResult(result);
    }
}
