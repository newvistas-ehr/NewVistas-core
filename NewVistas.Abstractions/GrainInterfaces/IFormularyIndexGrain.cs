// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Formulary drug list for a single benefit plan — enables coverage look-up at point
/// of prescribing and dispensing. Grain key: "PBM-FORMULARY:{planId}"
/// </summary>
public interface IFormularyIndexGrain : IGrainWithStringKey
{
    Task<List<FormularyEntry>> GetAllAsync();

    Task<List<FormularyEntry>> GetByTierAsync(int tier);

    Task<List<FormularyEntry>> GetRequiringPriorAuthAsync();

    /// <summary>Case-insensitive substring match on DrugName.</summary>
    Task<List<FormularyEntry>> SearchAsync(string term);

    Task<FormularyEntry?> GetDrugAsync(string drugId);

    Task AddOrUpdateEntryAsync(FormularyEntry entry);

    Task RemoveEntryAsync(string drugId);

    Task ClearAsync();
}
