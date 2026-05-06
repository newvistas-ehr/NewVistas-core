// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient SA treatment episode index grain.
/// Key: "SA-EPISODE-IDX:{patientId}"
/// </summary>
public interface ISATreatmentEpisodeIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.SATreatmentEpisodeIndexEntry>> GetAllAsync();
    Task<List<GrainStates.SATreatmentEpisodeIndexEntry>> GetByStatusAsync(GrainStates.SATreatmentStatus status);
    Task<GrainStates.SATreatmentEpisodeIndexEntry?> GetActiveAsync();
    Task AddEntryAsync(GrainStates.SATreatmentEpisodeIndexEntry entry);
    Task UpdateEntryAsync(string episodeId, GrainStates.SATreatmentStatus status, DateTime? dischargeDate);
}
