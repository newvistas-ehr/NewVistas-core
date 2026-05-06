// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all TIU document grain keys.
/// Stores keys sorted by ReferenceDate descending for efficient filtering and range queries.
/// Grain Key: patient ID (same key as IPatientGrain).
///
/// Notes have mutable status (UNSIGNED → UNCOSIGNED → COMPLETED, AMENDED, RETRACTED),
/// so this index supports AddOrUpdate to keep status metadata current without activating
/// individual TIU document grains.
///
/// Supports filtering by document type and status, and date range queries,
/// eliminating the N+1 fan-out problem for note listings.
/// Based on VistA TIU DOCUMENT file (#8925) and TIUSRVL.m (list notes).
/// </summary>
public interface IPatientNoteIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds or updates a note entry in the index.
    /// If an entry with the same DocumentGrainKey exists, it is replaced.
    /// Entries are maintained sorted by ReferenceDate descending.
    /// </summary>
    Task AddOrUpdateNoteAsync(GrainStates.NoteIndexEntry entry);

    /// <summary>
    /// Removes a note entry from the index.
    /// </summary>
    Task RemoveNoteAsync(string documentGrainKey);

    /// <summary>
    /// Gets all top-level note entries (excluding addenda), sorted by ReferenceDate descending.
    /// Optionally filtered by document type.
    /// </summary>
    Task<List<GrainStates.NoteIndexEntry>> GetEntriesAsync(string? documentType, int maxCount);

    /// <summary>
    /// Gets all entries (including addenda), sorted by ReferenceDate descending.
    /// </summary>
    Task<List<GrainStates.NoteIndexEntry>> GetAllEntriesAsync();

    /// <summary>
    /// Gets note entries for a specific date range, sorted by ReferenceDate descending.
    /// Excludes addenda and retracted notes.
    /// </summary>
    Task<List<GrainStates.NoteIndexEntry>> GetEntriesByDateRangeAsync(DateTime from, DateTime to);

    /// <summary>
    /// Gets note entries matching a status filter (e.g., UNSIGNED, UNCOSIGNED, COMPLETED).
    /// Excludes addenda.
    /// </summary>
    Task<List<GrainStates.NoteIndexEntry>> GetEntriesByStatusAsync(string status);

    /// <summary>
    /// Gets the total count of top-level notes (excluding addenda and retracted).
    /// </summary>
    Task<int> GetCountAsync();
}
