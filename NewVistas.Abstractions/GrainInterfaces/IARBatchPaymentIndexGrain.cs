// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of all AR batch payment sessions for listing and audit.
/// Grain key: "AR-BATCH-IDX"
/// </summary>
public interface IARBatchPaymentIndexGrain : IGrainWithStringKey
{
    /// <summary>Upserts a batch payment summary in the index.</summary>
    Task AddOrUpdateAsync(ARBatchPaymentIndexEntry entry);

    /// <summary>Returns all batch payment summaries.</summary>
    Task<List<ARBatchPaymentIndexEntry>> GetAllAsync();

    /// <summary>Returns batch payment summaries that have not yet been posted.</summary>
    Task<List<ARBatchPaymentIndexEntry>> GetUnpostedAsync();
}
