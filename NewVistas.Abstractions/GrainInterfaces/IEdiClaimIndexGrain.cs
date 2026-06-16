// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index grain for EDI claims.
/// Grain key: "EDI-CLAIM-IDX:{patientId}"
/// </summary>
public interface IEdiClaimIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new entry or updates an existing one (matched by ClaimId).</summary>
    Task AddOrUpdateAsync(EdiClaimIndexEntry entry);

    /// <summary>Returns all EDI claim entries for this patient.</summary>
    Task<List<EdiClaimIndexEntry>> GetAllAsync();
}
