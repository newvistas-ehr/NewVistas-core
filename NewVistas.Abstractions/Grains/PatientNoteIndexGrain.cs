// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient index of all TIU document grain keys.
/// Maintains entries sorted by ReferenceDate descending (most recent first).
/// Supports document type and status filtering without activating individual
/// TIU document grains.
/// </summary>
public class PatientNoteIndexGrain : Grain, IPatientNoteIndexGrain
{
    private readonly IPersistentState<PatientNoteIndexState> _state;

    public PatientNoteIndexGrain(
        [PersistentState("patientNoteIndexState", "patientNoteIndexStore")]
        IPersistentState<PatientNoteIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddOrUpdateNoteAsync(NoteIndexEntry entry)
    {
        // Remove existing entry with same key if present (status may have changed)
        _state.State.Entries.RemoveAll(e => e.DocumentGrainKey == entry.DocumentGrainKey);

        // Insert in sorted position (descending by ReferenceDate)
        int insertIndex = _state.State.Entries.FindIndex(e => e.ReferenceDate <= entry.ReferenceDate);
        if (insertIndex < 0)
            _state.State.Entries.Add(entry);
        else
            _state.State.Entries.Insert(insertIndex, entry);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveNoteAsync(string documentGrainKey)
    {
        int removed = _state.State.Entries.RemoveAll(e => e.DocumentGrainKey == documentGrainKey);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<NoteIndexEntry>> GetEntriesAsync(string? documentType, int maxCount)
    {
        List<NoteIndexEntry> result = _state.State.Entries
            .Where(e => !e.IsAddendum)
            .Where(e => e.Status != "RETRACTED")
            .Where(e => documentType == null ||
                e.DocumentType.Equals(documentType, StringComparison.OrdinalIgnoreCase))
            .Take(maxCount)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<NoteIndexEntry>> GetAllEntriesAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<NoteIndexEntry>> GetEntriesByDateRangeAsync(DateTime from, DateTime to)
    {
        List<NoteIndexEntry> result = _state.State.Entries
            .Where(e => !e.IsAddendum)
            .Where(e => e.Status != "RETRACTED")
            .Where(e => e.ReferenceDate >= from && e.ReferenceDate <= to)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<NoteIndexEntry>> GetEntriesByStatusAsync(string status)
    {
        List<NoteIndexEntry> result = _state.State.Entries
            .Where(e => !e.IsAddendum)
            .Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<int> GetCountAsync()
    {
        int count = _state.State.Entries
            .Count(e => !e.IsAddendum && e.Status != "RETRACTED");
        return Task.FromResult(count);
    }
}
