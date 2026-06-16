// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Health Factor Grain Interface based on VistA V HEALTH FACTORS file (#9000010.23)
/// </summary>
public interface IHealthFactorGrain : IGrainWithStringKey
{
    Task<GrainStates.HealthFactorState> GetHealthFactorAsync();

    Task RecordHealthFactorAsync(
        string patientId,
        string healthFactorName,
        string? healthFactorDefId,
        string? category,
        DateTime eventDateTime,
        string? levelSeverity,
        string? visitId,
        string? locationId,
        string? locationName,
        string? enteredById,
        string? enteredByName,
        string? comments);

    Task UpdateSeverityAsync(string severityLevel);
    Task SetCategoryAsync(string category, string? subcategory);
    Task SetValueAsync(string value, string? magnitude);
    Task LinkToVisitAsync(string visitId);
    Task AddCommentAsync(string comment);
    Task ResolveAsync(string resolvedByName);
    Task ReactivateAsync();
    Task AddHistoryEntryAsync(string value, string? severityLevel, string? comment, string? recordedByName);
    Task<List<GrainStates.HealthFactorHistoryEntry>> GetHistoryAsync();
}
