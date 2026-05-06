// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of collection/dunning letters.
/// Grain key: "AR-LETTER-IDX:{patientId}"
/// </summary>
public interface ICollectionLetterIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all collection letter entries for this patient.</summary>
    Task<List<CollectionLetterIndexEntry>> GetAllAsync();

    /// <summary>Adds or updates a collection letter entry in the index.</summary>
    Task AddOrUpdateAsync(CollectionLetterIndexEntry entry);

    /// <summary>Returns the current dunning sequence number (highest sequence + 1).</summary>
    Task<int> GetNextDunningSequenceAsync();

    /// <summary>Returns letters filtered by status.</summary>
    Task<List<CollectionLetterIndexEntry>> GetByStatusAsync(CollectionLetterStatus status);

    /// <summary>Returns the most recent letter of a given type.</summary>
    Task<CollectionLetterIndexEntry?> GetLatestByTypeAsync(CollectionLetterType letterType);
}
