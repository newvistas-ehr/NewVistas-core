// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton protocol index. Self-seeds with demo protocols.
/// Grain key: "RAD-PROTOCOL-INDEX"
/// </summary>
public interface IRadProtocolIndexGrain : IGrainWithStringKey
{
    Task<List<RadProtocolIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(RadProtocolIndexEntry entry);
    Task<List<RadProtocolIndexEntry>> SearchAsync(string query);
    Task<List<RadProtocolIndexEntry>> GetByImagingTypeAsync(string imagingType);
}
