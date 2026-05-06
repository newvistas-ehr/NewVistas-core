// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton Lab Surveillance Taxonomy index grain.
/// Key: "LAB-SURV-TAX-IDX"
/// </summary>
public interface ILabSurveillanceTaxonomyIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.LabSurveillanceTaxonomyIndexEntry>> GetAllAsync();
    Task<List<GrainStates.LabSurveillanceTaxonomyIndexEntry>> GetActiveAsync();
    Task UpsertAsync(GrainStates.LabSurveillanceTaxonomyIndexEntry entry);
}
