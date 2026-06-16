// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all cashier sessions across all stations and dates.
/// Grain key: "CASHIER-SESSION-IDX".
/// </summary>
public interface ICashierSessionIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new session entry or updates an existing one (matched by SessionId).</summary>
    Task AddOrUpdateAsync(CashierSessionIndexEntry entry);

    /// <summary>Returns all session entries regardless of status.</summary>
    Task<List<CashierSessionIndexEntry>> GetAllAsync();

    /// <summary>Returns only sessions with Status == "Open".</summary>
    Task<List<CashierSessionIndexEntry>> GetOpenSessionsAsync();

    /// <summary>Returns sessions whose SessionDate falls on the specified calendar date.</summary>
    Task<List<CashierSessionIndexEntry>> GetByDateAsync(DateTime date);
}
