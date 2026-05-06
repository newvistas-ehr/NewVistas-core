// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton PCC Surveillance configuration index.
/// Key: "PCC-SURV-CONFIG-IDX"
/// </summary>
public interface IPccSurveillanceConfigIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.PccSurveillanceConfigIndexEntry>> GetAllAsync();
    Task<List<GrainStates.PccSurveillanceConfigIndexEntry>> GetActiveAsync();
    Task UpsertAsync(GrainStates.PccSurveillanceConfigIndexEntry entry);
}
