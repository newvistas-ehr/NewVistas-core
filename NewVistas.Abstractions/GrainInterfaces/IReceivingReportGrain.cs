// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages the lifecycle of an IFCAP Receiving Report (proof of delivery).
/// Grain key: "IFCAP-RR:{rrId}"
/// </summary>
public interface IReceivingReportGrain : IGrainWithStringKey
{
    /// <summary>Returns the current receiving report state.</summary>
    Task<ReceivingReportState> GetAsync();

    /// <summary>Creates the receiving report in Draft status.</summary>
    Task CreateAsync(
        string purchaseOrderId,
        string controlPointId,
        string receivedByUserId,
        string receivedByUserName,
        List<RrLineItem> lineItems,
        string? notes);

    /// <summary>Accepts the receiving report. Sets Status = Accepted.</summary>
    Task AcceptAsync(string acceptedByUserId);

    /// <summary>Rejects the receiving report. Sets Status = Rejected.</summary>
    Task RejectAsync(string reason, string rejectedByUserId);
}
