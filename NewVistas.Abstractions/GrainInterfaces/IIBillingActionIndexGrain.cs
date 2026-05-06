// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all Integrated Billing actions.
/// Provides fast lookup without activating individual action grains.
/// Grain key: "IB-ACTION-IDX:{patientId}"
/// </summary>
public interface IIBillingActionIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces an action entry in the index (upsert by BillingActionId).</summary>
    Task AddOrUpdateAsync(GrainStates.IBillingActionIndexEntry entry);

    /// <summary>Returns all billing action entries for this patient, newest first.</summary>
    Task<List<GrainStates.IBillingActionIndexEntry>> GetAllAsync();

    /// <summary>Returns all billing action entries with the given status.</summary>
    Task<List<GrainStates.IBillingActionIndexEntry>> GetByStatusAsync(GrainStates.IBillingActionStatus status);

    /// <summary>Returns all billing action entries whose service date falls within the given range.</summary>
    Task<List<GrainStates.IBillingActionIndexEntry>> GetByDateRangeAsync(DateTime from, DateTime to);
}
