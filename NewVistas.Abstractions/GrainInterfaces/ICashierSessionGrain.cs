// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Cashier daily session grain tracking all receipts, running totals, reconciliation,
/// and turn-in to fiscal (VistA File #36 AGENT CASHIER session record).
/// Grain key: "CASHIER-SESSION:{sessionId}".
/// Managed by RCDPS.m MUMPS routine.
/// </summary>
public interface ICashierSessionGrain : IGrainWithStringKey
{
    /// <summary>Returns the current session state.</summary>
    Task<CashierSessionState> GetAsync();

    /// <summary>
    /// Opens a new cashier session for the specified station and cashier.
    /// Sets Status = Open, ExpectedBalance = OpeningBalance.
    /// Should only be called once per grain.
    /// </summary>
    Task OpenAsync(
        string stationId,
        string stationName,
        string cashierId,
        string cashierName,
        DateTime sessionDate,
        decimal openingBalance);

    /// <summary>
    /// Records a receipt in this session, updating running totals by payment method
    /// and recalculating ExpectedBalance. Called by ICashierReceiptGrain.IssueAsync.
    /// </summary>
    Task RecordReceiptAsync(string receiptId, decimal amount, string paymentMethod, DateTime receiptDate);

    /// <summary>
    /// Closes the session after the cashier counts the drawer.
    /// Sets Status = Closed, ActualBalance, and Discrepancy = ExpectedBalance − ActualBalance.
    /// </summary>
    Task CloseAsync(decimal actualBalance, string? notes);

    /// <summary>
    /// Records the turn-in of collected funds to the fiscal office.
    /// Sets Status = TurnedIn.
    /// </summary>
    Task TurnInAsync(decimal turnedInAmount, string turnedInToUserId, string? turnedInReceiptNumber);
}
