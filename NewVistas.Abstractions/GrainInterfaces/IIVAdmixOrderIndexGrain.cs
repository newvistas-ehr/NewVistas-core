// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient IV admixture order index grain.
/// Stores lightweight summary entries for each order belonging to this patient.
/// Grain key pattern: "IVAD-ORDER-IDX:{patientId}"
/// </summary>
public interface IIVAdmixOrderIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all IV admixture order summaries for this patient (newest first).</summary>
    Task<List<IVAdmixOrderIndexEntry>> GetAllOrdersAsync();

    /// <summary>Returns orders with Pending or Verified status (awaiting compounding).</summary>
    Task<List<IVAdmixOrderIndexEntry>> GetPendingOrdersAsync();

    /// <summary>Returns orders currently being compounded (Compounding or Ready).</summary>
    Task<List<IVAdmixOrderIndexEntry>> GetActiveOrdersAsync();

    /// <summary>Returns all orders for this patient filtered by status.</summary>
    Task<List<IVAdmixOrderIndexEntry>> GetOrdersByStatusAsync(IVAdmixOrderStatus status);

    /// <summary>Adds or updates an order summary entry in the index.</summary>
    Task UpsertOrderAsync(IVAdmixOrderIndexEntry entry);

    /// <summary>Removes an order summary entry by order ID.</summary>
    Task RemoveOrderAsync(string orderId);
}
