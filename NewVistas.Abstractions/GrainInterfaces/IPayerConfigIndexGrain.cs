// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of all configured payers for eligibility verification.
/// Self-seeds with demo payer configurations on activation.
/// Grain key: "PAYER-CFG-INDEX"
/// </summary>
public interface IPayerConfigIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all payer configuration entries.</summary>
    Task<List<PayerConfigIndexEntry>> GetAllAsync();

    /// <summary>Searches payers by name (case-insensitive partial match).</summary>
    Task<List<PayerConfigIndexEntry>> SearchAsync(string query);

    /// <summary>Returns only payers that support real-time 270/271 verification.</summary>
    Task<List<PayerConfigIndexEntry>> GetRealTimePayersAsync();

    /// <summary>Adds or updates a payer entry in the index.</summary>
    Task AddOrUpdateAsync(PayerConfigIndexEntry entry);

    /// <summary>Removes a payer entry from the index.</summary>
    Task RemoveAsync(string payerId);
}
