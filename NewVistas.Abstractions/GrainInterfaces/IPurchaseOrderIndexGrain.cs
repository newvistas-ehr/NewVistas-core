// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Global singleton index of all IFCAP purchase orders.
/// Grain key: "IFCAP-PO-IDX"
/// </summary>
public interface IPurchaseOrderIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a purchase order entry in the index.</summary>
    Task AddOrUpdateAsync(PurchaseOrderIndexEntry entry);

    /// <summary>Returns all purchase orders.</summary>
    Task<List<PurchaseOrderIndexEntry>> GetAllAsync();

    /// <summary>Returns purchase orders with status Open or PartiallyReceived.</summary>
    Task<List<PurchaseOrderIndexEntry>> GetOpenAsync();

    /// <summary>Returns purchase orders for a specific control point.</summary>
    Task<List<PurchaseOrderIndexEntry>> GetByControlPointAsync(string controlPointId);
}
