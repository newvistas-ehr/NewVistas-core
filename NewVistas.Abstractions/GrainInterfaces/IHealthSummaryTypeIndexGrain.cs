// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Health Summary Type Index Grain — singleton index of all configured report templates.
/// VistA HEALTH SUMMARY TYPE file (#142).
/// </summary>
/// <remarks>Grain key: "HS-TYPE-IDX" (singleton)</remarks>
public interface IHealthSummaryTypeIndexGrain : IGrainWithStringKey
{
    /// <summary>Get all health summary type templates.</summary>
    Task<List<HealthSummaryTypeIndexEntry>> GetAllAsync();

    /// <summary>Get only active health summary types.</summary>
    Task<List<HealthSummaryTypeIndexEntry>> GetActiveAsync();

    /// <summary>Add or update a template entry in the index.</summary>
    Task UpsertEntryAsync(HealthSummaryTypeIndexEntry entry);

    /// <summary>Remove a template entry from the index.</summary>
    Task RemoveEntryAsync(string typeId);
}
