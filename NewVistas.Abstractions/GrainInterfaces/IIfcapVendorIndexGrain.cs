// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Global singleton index of all IFCAP procurement vendors.
/// Grain key: "IFCAP-VENDOR-IDX"
/// </summary>
public interface IIfcapVendorIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a vendor entry in the index.</summary>
    Task AddOrUpdateAsync(IfcapVendorIndexEntry entry);

    /// <summary>Returns all vendors.</summary>
    Task<List<IfcapVendorIndexEntry>> GetAllAsync();

    /// <summary>Returns only active vendors.</summary>
    Task<List<IfcapVendorIndexEntry>> GetActiveAsync();

    /// <summary>
    /// Returns vendors whose name or vendor number contains
    /// <paramref name="text"/> (case-insensitive).
    /// </summary>
    Task<List<IfcapVendorIndexEntry>> SearchAsync(string text);
}
