// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
