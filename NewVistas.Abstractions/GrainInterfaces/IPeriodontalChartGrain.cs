// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Optional feature grain for structured periodontal charting assessments.
/// Enabled per site via ISiteParametersGrain.Features containing "PERIODONTAL_CHARTING".
/// Follows the Site Flavor Architecture (Option 4 — Composition).
///
/// Maps to IHS RPMS Dental (DENT package) periodontal charting and VistA
/// Dental Record Manager (File #220). Captures 6-point probing depths,
/// recession, bleeding on probing, furcation, mobility, and plaque index
/// per tooth for a full-mouth periodontal assessment.
///
/// Keyed by chart ID (e.g., "PERIO:{guid}").
/// </summary>
public interface IPeriodontalChartGrain : IGrainWithStringKey
{
    Task<PeriodontalChartState> GetChartAsync();

    Task<PeriodontalChartState> CreateChartAsync(
        string patientId,
        string patientName,
        string providerId,
        string providerName,
        string? notes);

    Task RecordToothDataAsync(int toothNumber, PeriodontalToothData data);
    Task RecordMultipleTeethAsync(List<PeriodontalToothEntry> entries);
    Task MarkToothMissingAsync(int toothNumber, string reason);
    Task SetOverallAssessmentAsync(string classification, string? treatmentPlan, string assessedByName);
    Task FinalizeChartAsync(string finalizedByName);
    Task AddendChartAsync(string addendumNote, string addendedByName);
}

/// <summary>
/// System-level index for periodontal charts. Singleton "PERIO-IDX".
/// </summary>
public interface IPeriodontalChartIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(PeriodontalChartIndexEntry entry);
    Task<List<PeriodontalChartIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50);
    Task<List<PeriodontalChartIndexEntry>> GetByProviderAsync(string providerId, int maxResults = 50);
    Task<List<PeriodontalChartIndexEntry>> GetByClassificationAsync(string classification, int maxResults = 50);
    Task<List<PeriodontalChartIndexEntry>> SearchAsync(string? patientId, string? providerId, string? status, int maxResults = 50);
    Task<int> GetCountAsync();
}
