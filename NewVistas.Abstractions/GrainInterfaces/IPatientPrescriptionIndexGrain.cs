// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient prescription index grain.
/// Grain key: "PSO-INDEX:{patientId}"
/// Provides fast list-all / filter queries without touching every IPharmacyGrain.
/// </summary>
public interface IPatientPrescriptionIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateEntryAsync(PrescriptionIndexEntry entry);
    Task RemoveEntryAsync(string prescriptionId);
    Task<List<PrescriptionIndexEntry>> GetAllAsync();
    Task<List<PrescriptionIndexEntry>> GetActiveAsync();
    Task<List<PrescriptionIndexEntry>> GetByStatusAsync(string status);
    Task<int> GetTotalCountAsync();
    Task ClearAsync();
}
