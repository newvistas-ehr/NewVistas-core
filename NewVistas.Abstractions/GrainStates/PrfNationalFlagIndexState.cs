// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Entry representing one of the four VistA PRF national flag definitions (File #26.15).
/// </summary>
[GenerateSerializer]
public record PrfNationalFlagEntry
{
    /// <summary>Unique flag identifier.</summary>
    [Id(0)] public string FlagId { get; init; } = string.Empty;

    /// <summary>Official flag name (e.g., BEHAVIORAL, HIGH RISK FOR SUICIDE).</summary>
    [Id(1)] public string FlagName { get; init; } = string.Empty;

    /// <summary>Flag type code (NATIONAL).</summary>
    [Id(2)] public string FlagType { get; init; } = "NATIONAL";

    /// <summary>Description of what this flag signifies and when it should be assigned.</summary>
    [Id(3)] public string Description { get; init; } = string.Empty;

    /// <summary>Whether this flag definition is currently active.</summary>
    [Id(4)] public bool IsActive { get; init; } = true;
}

/// <summary>
/// Singleton index of the four VistA PRF national patient record flags (File #26.15).
/// Seeded with: BEHAVIORAL, HIGH RISK FOR SUICIDE, URGENT ADDRESS AS FEMALE, MISSING PATIENT.
/// </summary>
[GenerateSerializer]
public class PrfNationalFlagIndexState
{
    /// <summary>All national PRF flag definitions.</summary>
    [Id(0)] public List<PrfNationalFlagEntry> Entries { get; set; } = new();
}
