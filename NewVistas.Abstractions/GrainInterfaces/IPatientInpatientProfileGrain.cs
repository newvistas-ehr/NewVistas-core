// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient inpatient medication profile index grain.
/// Grain key: "PSJ-PROFILE:{patientId}"
/// Provides fast active-order lookups for BCMA and ward pharmacist views.
/// </summary>
public interface IPatientInpatientProfileGrain : IGrainWithStringKey
{
    Task AddOrUpdateOrderAsync(InpatientOrderIndexEntry entry);
    Task RemoveOrderAsync(string orderId);
    Task<List<InpatientOrderIndexEntry>> GetAllOrdersAsync();
    Task<List<InpatientOrderIndexEntry>> GetActiveOrdersAsync();
    Task<List<InpatientOrderIndexEntry>> GetByTypeAsync(string orderType);
    Task<List<InpatientOrderIndexEntry>> GetPendingVerificationAsync();
    Task<int> GetTotalCountAsync();
    Task ClearAsync();
}
