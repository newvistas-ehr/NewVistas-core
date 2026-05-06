// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton POS insurer index grain.
/// Key: "POS-INSURER-IDX"
/// </summary>
public interface IPharmacyPosInsurerIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.PosInsurerIndexEntry>> GetAllAsync();
    Task<List<GrainStates.PosInsurerIndexEntry>> GetActiveAsync();
    Task UpsertAsync(GrainStates.PosInsurerIndexEntry entry);
}
