// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton PCC Surveillance match index — dashboard for encounter matches.
/// Key: "PCC-SURV-MATCH-IDX"
/// </summary>
public interface IPccSurveillanceMatchIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.PccSurveillanceMatchIndexEntry>> GetAllAsync();
    Task<List<GrainStates.PccSurveillanceMatchIndexEntry>> GetByStatusAsync(
        GrainStates.PccSurveillanceMatchStatus status);
    Task<List<GrainStates.PccSurveillanceMatchIndexEntry>> GetByConditionAsync(string conditionName);
    Task AddEntryAsync(GrainStates.PccSurveillanceMatchIndexEntry entry);
    Task UpdateStatusAsync(string matchId, GrainStates.PccSurveillanceMatchStatus status);
}
