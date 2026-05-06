// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index grain for interaction screenings.
/// Provides efficient querying without activating individual screening grains.
///
/// Key format: IXSCREEN-IDX:{patientId}
/// Store: interactionScreeningIndexStore
/// </summary>
public interface IInteractionScreeningIndexGrain : IGrainWithStringKey
{
    /// <summary>Gets all screening entries for this patient.</summary>
    Task<List<GrainStates.InteractionScreeningIndexEntry>> GetAllAsync();

    /// <summary>Gets screenings that are currently blocking a fill.</summary>
    Task<List<GrainStates.InteractionScreeningIndexEntry>> GetBlockedAsync();

    /// <summary>Gets the screening for a specific prescription.</summary>
    Task<GrainStates.InteractionScreeningIndexEntry?> GetByPrescriptionAsync(string prescriptionId);

    /// <summary>Adds a new entry to the index.</summary>
    Task AddEntryAsync(GrainStates.InteractionScreeningIndexEntry entry);

    /// <summary>Updates the status of an existing entry.</summary>
    Task UpdateEntryAsync(
        string screeningId,
        GrainStates.InteractionScreeningStatus status,
        int blockingCount,
        int totalInteractionCount);
}
