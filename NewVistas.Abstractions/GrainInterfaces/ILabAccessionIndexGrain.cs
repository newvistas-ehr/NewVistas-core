// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient lab accession index.
/// Grain key: "LAB-ACCESSION-IDX:{patientId}"
/// </summary>
public interface ILabAccessionIndexGrain : IGrainWithStringKey
{
    Task<List<LabAccessionIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(LabAccessionIndexEntry entry);
    Task<List<LabAccessionIndexEntry>> GetPendingAsync();
}
