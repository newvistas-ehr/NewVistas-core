// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Entry in the enrollment status reference index (VistA File #27.15 DG ENROLLMENT STATUS).
/// </summary>
[GenerateSerializer]
public record EnrollmentStatusEntry
{
    /// <summary>Short status code (e.g., "VERIFIED", "REJECTED").</summary>
    [Id(0)] public string StatusCode { get; init; } = string.Empty;

    /// <summary>Display name of the enrollment status.</summary>
    [Id(1)] public string StatusName { get; init; } = string.Empty;

    /// <summary>Description of when this status applies.</summary>
    [Id(2)] public string Description { get; init; } = string.Empty;

    /// <summary>Whether this status code is currently in use.</summary>
    [Id(3)] public bool IsActive { get; init; } = true;
}

/// <summary>
/// Singleton index of all VistA enrollment status codes (File #27.15).
/// Seeded with the 24 standard VA enrollment status codes.
/// </summary>
[GenerateSerializer]
public class EnrollmentStatusIndexState
{
    /// <summary>All enrollment status code entries.</summary>
    [Id(0)] public List<EnrollmentStatusEntry> Entries { get; set; } = new();
}
