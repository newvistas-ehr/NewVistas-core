// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Global singleton index of all IFCAP control points.
/// Grain key: "IFCAP-CP-IDX"
/// </summary>
public interface IControlPointIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a control point entry in the index.</summary>
    Task AddOrUpdateAsync(ControlPointIndexEntry entry);

    /// <summary>Returns all control points across all fiscal years.</summary>
    Task<List<ControlPointIndexEntry>> GetAllAsync();

    /// <summary>Returns control points for a specific fiscal year.</summary>
    Task<List<ControlPointIndexEntry>> GetByFiscalYearAsync(int fiscalYear);

    /// <summary>Returns only active control points.</summary>
    Task<List<ControlPointIndexEntry>> GetActiveAsync();
}
