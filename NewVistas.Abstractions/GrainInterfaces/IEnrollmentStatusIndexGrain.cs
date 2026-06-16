// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton reference index of all VistA enrollment status codes (File #27.15).
/// Key: <c>"ENROLLMENT-STATUS-IDX"</c>
/// </summary>
public interface IEnrollmentStatusIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all enrollment status code entries.</summary>
    Task<List<EnrollmentStatusEntry>> GetAllAsync();

    /// <summary>
    /// Seeds the index with the 24 standard VistA enrollment status codes.
    /// Idempotent — no-op if entries already exist.
    /// </summary>
    Task SeedDefaultsAsync();
}
