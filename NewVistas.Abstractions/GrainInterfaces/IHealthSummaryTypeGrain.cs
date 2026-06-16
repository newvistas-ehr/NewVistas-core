// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Health Summary Type Grain — manages a single configurable report template.
/// VistA HEALTH SUMMARY TYPE file (#142).
/// MUMPS routines: GMTS.m (GMTS BUILD/PRINT), GMTSS.m (HS SETUP), GMTSUP.m (UTILITY).
/// </summary>
/// <remarks>Grain key: "HS-TYPE:{typeId}"</remarks>
public interface IHealthSummaryTypeGrain : IGrainWithStringKey
{
    /// <summary>Get the full health summary type definition.</summary>
    Task<HealthSummaryTypeState> GetAsync();

    /// <summary>Create a new health summary type template.</summary>
    Task CreateAsync(
        string typeId,
        string name,
        string? description,
        string createdById,
        string createdByName);

    /// <summary>Update the template name and description.</summary>
    Task UpdateAsync(string name, string? description);

    /// <summary>Add or replace a component configuration in this template.</summary>
    Task AddOrUpdateComponentAsync(HealthSummaryComponentConfig component);

    /// <summary>Remove a component from this template by type.</summary>
    Task RemoveComponentAsync(HealthSummaryComponentType componentType);

    /// <summary>Activate or deactivate this summary type.</summary>
    Task SetActiveAsync(bool isActive);
}
