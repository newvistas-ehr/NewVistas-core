// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// System-wide (multi-institution) capacity view — a STATELESS fan-out over each
/// institution's BED-CAPACITY rollup via the INSTITUTION-INDEX. Grain key:
/// "SYSTEM-CAPACITY". No persisted aggregate: at ≤ dozens of institutions the
/// fan-out is a handful of in-cluster calls; if a deployment ever exceeds ~100
/// institutions, switch to pushing summaries into a cache grain instead.
/// </summary>
public interface ISystemCapacityGrain : IGrainWithStringKey
{
    /// <summary>Capacity across all active institutions, optionally one health system.</summary>
    Task<SystemCapacitySnapshot> GetSystemCapacityAsync(string? healthSystemId = null);

    /// <summary>
    /// Transfer-target search: active institutions that accept inbound transfers, have
    /// the required capability (when given), and have at least one placeable bed
    /// (of the requested type, when given).
    /// </summary>
    Task<List<InstitutionCapacitySummary>> FindPlacementTargetsAsync(BedType? requestedBedType, string? requiredCapability);
}
