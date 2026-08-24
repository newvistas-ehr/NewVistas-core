// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Incomplete Record Grain — manages a single chart deficiency.
/// Key: "DGPT:{deficiencyId}"
/// Maps to VistA DGPT (Incomplete Records Tracking).
/// </summary>
public interface IIncompleteRecordGrain : IGrainWithStringKey
{
    Task<IncompleteRecordState> GetDeficiencyAsync();

    Task CreateDeficiencyAsync(
        string patientId, string patientName,
        string providerId, string providerName,
        string deficiencyType, string description,
        string? documentId, string? admissionId,
        DateTime? dischargeDate, int delinquentAfterDays);

    Task CompleteAsync(string completedBy);
    Task WaiveAsync(string reason, string authorizedBy);
    Task UpdateDaysOutstandingAsync();
}

/// <summary>
/// Incomplete Record Index Grain — per-provider deficiency list.
/// Key: "DGPT-INDEX:{providerId}"
/// </summary>
public interface IIncompleteRecordIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Seeds a small demo data set through this grain. Owned here rather than in a
    /// controller so Blazor, WPF and the REST API all invoke one implementation — an
    /// internal UI must never call the WebServer to populate its own screen.
    /// </summary>
    Task SeedDemoDataAsync();

    Task<List<IncompleteRecordEntry>> GetAllDeficienciesAsync();
    Task<List<IncompleteRecordEntry>> GetOpenDeficienciesAsync();
    Task<List<IncompleteRecordEntry>> GetDelinquentDeficienciesAsync();
    Task AddOrUpdateAsync(IncompleteRecordEntry entry);
    Task RemoveAsync(string deficiencyId);
}
