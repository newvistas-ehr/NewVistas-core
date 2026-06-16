// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages the lifecycle of an IFCAP Purchase Order issued to a vendor.
/// Grain key: "IFCAP-PO:{poId}"
/// </summary>
public interface IPurchaseOrderGrain : IGrainWithStringKey
{
    /// <summary>Returns the current purchase order state.</summary>
    Task<PurchaseOrderState> GetAsync();

    /// <summary>Creates the purchase order and sets Status = Open.</summary>
    Task CreateAsync(
        string requestId,
        string controlPointId,
        string vendorId,
        string vendorName,
        string? vendorAddress,
        DateTime requiredDeliveryDate,
        List<PoLineItem> lineItems,
        string issuedByUserId,
        string issuedByUserName);

    /// <summary>
    /// Records a receiving report against this PO.
    /// Updates QuantityReceived on matching line items.
    /// If all lines are fully received, Status = Received; otherwise PartiallyReceived.
    /// Adds the receiving report ID to ReceivingReportIds.
    /// </summary>
    Task RecordReceiptAsync(
        string receivingReportId,
        List<(int LineNumber, decimal QuantityAccepted)> lineReceipts);

    /// <summary>Cancels the purchase order. Sets Status = Cancelled.</summary>
    Task CancelAsync(string reason, string cancelledByUserId);

    /// <summary>Closes/finalizes the purchase order. Sets Status = Closed.</summary>
    Task CloseAsync(string closedByUserId);
}
