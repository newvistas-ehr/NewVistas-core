// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Substance Abuse Treatment Visit Grain — RPMS CDMIS visit (File #9002172).
/// Key: "SA-VISIT:{guid}"
/// </summary>
public interface ISAVisitGrain : IGrainWithStringKey
{
    Task<GrainStates.SAVisitState> GetAsync();

    Task CreateAsync(
        string episodeId,
        string patientId,
        DateTime visitDate,
        GrainStates.SAVisitType visitType,
        int? durationMinutes,
        string? udsResult,
        List<string>? udsSubstancesDetected,
        int? daysSinceLastUse,
        int? cravingLevel,
        string? providerId, string? providerName,
        string? notes);
}
