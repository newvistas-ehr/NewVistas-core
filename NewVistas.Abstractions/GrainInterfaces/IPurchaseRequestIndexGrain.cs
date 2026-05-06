// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-control-point index of purchase requests (VA Form 2237).
/// Grain key: "IFCAP-PR-IDX:{controlPointId}"
/// </summary>
public interface IPurchaseRequestIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a purchase request entry in the index.</summary>
    Task AddOrUpdateAsync(PurchaseRequestIndexEntry entry);

    /// <summary>Returns all purchase requests for this control point.</summary>
    Task<List<PurchaseRequestIndexEntry>> GetAllAsync();

    /// <summary>
    /// Returns requests in Draft or Submitted status (awaiting action).
    /// </summary>
    Task<List<PurchaseRequestIndexEntry>> GetPendingAsync();
}
