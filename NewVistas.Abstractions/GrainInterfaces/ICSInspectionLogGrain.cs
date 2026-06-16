// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing the inspection history log for a controlled substance vault location.
/// VistA File #58.82 index — PSNINSP.m
/// Grain key: "CS-INSPECT-LOG:{locationId}"
/// </summary>
public interface ICSInspectionLogGrain : IGrainWithStringKey
{
    /// <summary>Returns all inspections for this location, newest first.</summary>
    Task<List<CSInspectionSummaryEntry>> GetAllInspectionsAsync();

    /// <summary>Returns inspections filtered by inspection type.</summary>
    Task<List<CSInspectionSummaryEntry>> GetInspectionsByTypeAsync(CSInspectionType type);

    /// <summary>Returns inspections filtered by overall result.</summary>
    Task<List<CSInspectionSummaryEntry>> GetInspectionsByResultAsync(CSInspectionResult result);

    /// <summary>Returns inspections that failed or identified discrepancies.</summary>
    Task<List<CSInspectionSummaryEntry>> GetFailedInspectionsAsync();

    /// <summary>Adds or updates an inspection summary entry in the log.</summary>
    Task UpsertInspectionAsync(CSInspectionSummaryEntry entry);

    /// <summary>Removes an inspection from the log by ID.</summary>
    Task RemoveInspectionAsync(string inspectionId);
}
