// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lab worklist task category for technologists.
/// </summary>
[GenerateSerializer]
public enum LabWorklistCategory
{
    PendingCollection = 0,
    PendingResult = 1,
    PendingVerification = 2,
    CriticalPending = 3,
    QcRequired = 4,
}

/// <summary>
/// A single item on the lab tech worklist.
/// </summary>
[GenerateSerializer]
public record LabWorklistItem
{
    [Id(0)] public string LabTestId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string TestName { get; init; } = string.Empty;
    [Id(3)] public string? TestCode { get; init; }
    [Id(4)] public string? Category { get; init; }
    [Id(5)] public string Status { get; init; } = string.Empty;
    [Id(6)] public LabWorklistCategory WorklistCategory { get; init; }
    [Id(7)] public DateTime OrderedDate { get; init; }
    [Id(8)] public DateTime? CollectionDateTime { get; init; }
    [Id(9)] public string? SpecimenType { get; init; }
    [Id(10)] public string? AccessionNumber { get; init; }
    [Id(11)] public bool IsCritical { get; init; }
    [Id(12)] public string? AbnormalFlag { get; init; }
}

/// <summary>
/// Lab tech worklist state — aggregated view of pending specimens, results, and verifications.
/// Grain key: "LAB-WORKLIST:{locationId}" (per-lab location)
/// </summary>
[GenerateSerializer]
public class LabWorklistState
{
    [Id(0)] public string LocationId { get; set; } = string.Empty;
    [Id(1)] public List<LabWorklistItem> Items { get; set; } = new();
    [Id(2)] public DateTime LastRefreshedDate { get; set; }
}
