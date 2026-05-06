// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all fee basis batch payments.
/// Grain key: "FEE-BATCH-IDX"
/// </summary>
public interface IFeeBatchPaymentIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new entry or updates an existing one (matched by BatchId).</summary>
    Task AddOrUpdateAsync(FeeBatchPaymentIndexEntry entry);

    /// <summary>Returns all batch payment entries.</summary>
    Task<List<FeeBatchPaymentIndexEntry>> GetAllAsync();

    /// <summary>Returns only batches that have not yet been posted.</summary>
    Task<List<FeeBatchPaymentIndexEntry>> GetUnpostedAsync();
}
