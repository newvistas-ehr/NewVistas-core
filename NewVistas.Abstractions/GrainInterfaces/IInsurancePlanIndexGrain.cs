// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton global index of all Group Insurance Plans (File #355.3).
/// Provides fast text search and type-filtered lookups across all plans
/// without activating individual plan grains.
/// Grain key: "IB-PLAN-INDEX"
/// </summary>
public interface IInsurancePlanIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or replaces a plan entry in the index (upsert by PlanId).</summary>
    Task AddOrUpdateAsync(GrainStates.InsurancePlanIndexEntry entry);

    /// <summary>
    /// Full-text search over plan names and insurance company names.
    /// Optionally filtered by plan type (MEDICARE, MEDICAID, TRICARE, COMMERCIAL, etc.)
    /// and active status.
    /// </summary>
    Task<List<GrainStates.InsurancePlanIndexEntry>> SearchAsync(
        string searchText,
        string? planType,
        bool activeOnly,
        int maxResults = 50);

    /// <summary>Returns all plan entries in the index.</summary>
    Task<List<GrainStates.InsurancePlanIndexEntry>> GetAllAsync();
}
