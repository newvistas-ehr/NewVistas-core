// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// DSS Unit Grain — represents a Decision Support System (DSS) unit/department
/// used for workload tracking in Event Capture.
/// Based on VistA File #724 (DSS UNIT).
/// MUMPS routines: ECPEDSS.m, ECPEWL.m.
///
/// Grain key format: "EC-DSS-UNIT:{dssUnitId}"
/// </summary>
public interface IDssUnitGrain : IGrainWithStringKey
{
    /// <summary>Returns the full DSS unit state.</summary>
    Task<GrainStates.DssUnitState> GetUnitAsync();

    /// <summary>
    /// Creates or updates a DSS unit definition.
    /// </summary>
    Task UpsertAsync(
        string unitName,
        string unitCode,
        string? divisionId,
        string? divisionName,
        string? primaryStopCode,
        string? creditStopCode,
        string? treatmentCode,
        string? description,
        bool isActive);

    /// <summary>
    /// Deactivates this DSS unit (no longer available for new encounters).
    /// </summary>
    Task DeactivateAsync();

    /// <summary>
    /// Reactivates a previously deactivated DSS unit.
    /// </summary>
    Task ReactivateAsync();
}
