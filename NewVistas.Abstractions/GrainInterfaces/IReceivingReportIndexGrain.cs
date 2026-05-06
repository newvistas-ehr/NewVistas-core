// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-PO index of receiving reports.
/// Grain key: "IFCAP-RR-IDX:{purchaseOrderId}"
/// </summary>
public interface IReceivingReportIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a receiving report entry in the index.</summary>
    Task AddOrUpdateAsync(ReceivingReportIndexEntry entry);

    /// <summary>Returns all receiving reports for this purchase order.</summary>
    Task<List<ReceivingReportIndexEntry>> GetAllAsync();
}
