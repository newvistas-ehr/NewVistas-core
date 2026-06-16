// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// System-wide device inventory index for Home Telehealth.
/// Grain key: "HT-DEVICE-IDX" (singleton)
/// </summary>
public interface IHomeTelehealthDeviceIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a device to the inventory index.</summary>
    Task AddAsync(HtDeviceIndexEntry entry);

    /// <summary>
    /// Returns all devices, optionally filtered by type and/or status.
    /// </summary>
    Task<List<HtDeviceIndexEntry>> GetAsync(HtDeviceType? deviceType, HtDeviceStatus? status);

    /// <summary>Updates the status and assigned patient of a device in the index.</summary>
    Task UpdateStatusAsync(string deviceId, HtDeviceStatus status, string? assignedPatientId);
}
