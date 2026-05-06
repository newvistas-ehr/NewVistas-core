// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Transfusion Index Grain — per-patient index of all transfusion records.
///
/// Grain key: "BB-TX-IDX:{patientId}"
/// </summary>
public interface ITransfusionIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(TransfusionIndexEntry entry);
    Task<List<TransfusionIndexEntry>> GetAllAsync();
}
