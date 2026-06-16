// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Blind Rehabilitation Training Center Grain — a VA blind rehabilitation center.
///
/// Derived from VistA Blind Rehabilitation module:
///   File #782.1 — BLIND REHABILITATION CENTER
///
/// Grain key: "BR-CENTER:{centerId}"
/// </summary>
public interface IBRCenterGrain : IGrainWithStringKey
{
    /// <summary>Returns the full center record.</summary>
    Task<BRCenterState> GetAsync();

    /// <summary>
    /// Creates or updates the center record.
    /// Corresponds to VistA File #782.1 fields.
    /// </summary>
    Task SaveAsync(
        string centerId,
        string name,
        string facilityCode,
        string city,
        string state,
        BRCenterType centerType,
        int bedCapacity,
        bool acceptingPatients,
        List<BRTrainingArea> programsOffered,
        string? phoneNumber,
        string? contactName,
        string? notes);

    /// <summary>Sets whether the center is currently accepting new patients.</summary>
    Task SetAcceptingPatientsAsync(bool accepting);
}
