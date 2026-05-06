// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-pregnancy prenatal visit index grain.
/// Key: "OB-VISIT-IDX:{pregnancyId}"
///
/// Maintains a lightweight list of visit summaries for a pregnancy,
/// allowing the flowsheet / visit history to render without activating
/// every individual visit grain.
/// </summary>
public interface IPrenatalVisitIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all visit index entries (newest first).</summary>
    Task<List<GrainStates.PrenatalVisitIndexEntry>> GetAllAsync();

    /// <summary>Adds a new visit summary entry.</summary>
    Task AddEntryAsync(GrainStates.PrenatalVisitIndexEntry entry);

    /// <summary>Returns visit count for the pregnancy.</summary>
    Task<int> GetVisitCountAsync();
}
