// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Substance Abuse Treatment Episode Grain — RPMS CDMIS (File #9002170-9002174).
/// Key: "SA-EPISODE:{guid}"
///
/// Models a treatment episode: enrollment, modality, MAT, substances, discharge.
/// Additive feature grain per Site Flavor Architecture (Option 4).
/// </summary>
public interface ISATreatmentEpisodeGrain : IGrainWithStringKey
{
    Task<GrainStates.SATreatmentEpisodeState> GetAsync();

    Task CreateAsync(
        string patientId,
        GrainStates.SATreatmentModality modality,
        GrainStates.SubstanceType primarySubstance,
        List<GrainStates.SubstanceType>? secondarySubstances,
        DateTime intakeDate,
        DateTime? lastUseDate,
        DateTime? sobrietyDate,
        string? programName,
        List<string>? treatmentGoals,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        string? notes);

    Task AddMATEntryAsync(GrainStates.MATEntry entry);
    Task StopMATEntryAsync(string entryId, DateTime endDate);
    Task AddTreatmentGoalAsync(string goal);

    Task DischargeAsync(DateTime dischargeDate,
        GrainStates.SADischargeDisposition disposition, string? notes);

    Task ReopenAsync(string? notes);
    Task UpdateStatusAsync(GrainStates.SATreatmentStatus status);
}
