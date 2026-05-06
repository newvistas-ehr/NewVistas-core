// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Health Summary Index Grain — per-patient list of generated health summary reports.
/// </summary>
/// <remarks>Grain key: "HS-IDX:{patientId}"</remarks>
public interface IHealthSummaryIndexGrain : IGrainWithStringKey
{
    /// <summary>Get all generated summaries for the patient (newest first).</summary>
    Task<List<HealthSummaryIndexEntry>> GetAllAsync();

    /// <summary>Get generated summaries for a specific template type.</summary>
    Task<List<HealthSummaryIndexEntry>> GetByTypeAsync(string typeId);

    /// <summary>Add an entry when a new summary is generated.</summary>
    Task AddEntryAsync(HealthSummaryIndexEntry entry);
}
