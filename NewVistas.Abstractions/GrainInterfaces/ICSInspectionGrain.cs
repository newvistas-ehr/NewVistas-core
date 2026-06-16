// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single controlled substance vault inspection record.
/// VistA File #58.82 — PSNINSP.m, PSNCS.m
/// Grain key: "CS-INSPECTION:{guid}"
/// </summary>
public interface ICSInspectionGrain : IGrainWithStringKey
{
    /// <summary>Returns the full inspection state.</summary>
    Task<CSInspectionState> GetInspectionAsync();

    /// <summary>Creates a new vault inspection record.</summary>
    Task CreateInspectionAsync(
        string locationId,
        string locationName,
        CSInspectionType inspectionType,
        DateTime inspectionDateTime,
        string inspectorId,
        string inspectorName,
        string witnessId,
        string witnessName,
        string? secondWitnessId,
        string? secondWitnessName,
        string? notes);

    /// <summary>Adds a drug physical count entry to this inspection.</summary>
    Task AddDrugCountAsync(CSInspectionCount count);

    /// <summary>Finalizes the inspection with an overall result and discrepancy reporting details.</summary>
    Task FinalizeInspectionAsync(
        CSInspectionResult result,
        bool discrepanciesReported,
        string? reportedToId,
        string? reportedToName,
        string? investigationNotes);
}
