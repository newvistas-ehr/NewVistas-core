// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all EDI transmission batches.
/// Grain key: "EDI-TX-IDX"
/// </summary>
public interface IEdiTransmissionIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new entry or updates an existing one (matched by TransmissionId).</summary>
    Task AddOrUpdateAsync(EdiTransmissionIndexEntry entry);

    /// <summary>Returns all transmission batch entries.</summary>
    Task<List<EdiTransmissionIndexEntry>> GetAllAsync();

    /// <summary>Returns only batches with Status == "Open".</summary>
    Task<List<EdiTransmissionIndexEntry>> GetOpenAsync();
}
