// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
    Task<List<IncompleteRecordEntry>> GetAllDeficienciesAsync();
    Task<List<IncompleteRecordEntry>> GetOpenDeficienciesAsync();
    Task<List<IncompleteRecordEntry>> GetDelinquentDeficienciesAsync();
    Task AddOrUpdateAsync(IncompleteRecordEntry entry);
    Task RemoveAsync(string deficiencyId);
}
