// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single infection outbreak cluster.
/// Key pattern: "HAI-OUTBREAK:{guid}"
/// </summary>
public interface IOutbreakGrain : IGrainWithStringKey
{
    Task<OutbreakState> GetOutbreakAsync();

    Task CreateOutbreakAsync(
        string outbreakId,
        string name,
        string description,
        HAIType haiType,
        DateTime? startDate,
        string locationId,
        string locationName,
        string pathogen);

    Task UpdateStatusAsync(OutbreakStatus status, DateTime? controlDate, DateTime? closeDate);

    Task AddCaseAsync(string caseId);

    Task RemoveCaseAsync(string caseId);

    Task NotifyPublicHealthAsync(DateTime notificationDate);
}
