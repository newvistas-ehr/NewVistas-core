// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single Hospital-Acquired Infection (HAI) case record.
/// Key pattern: "HAI-CASE:{guid}"
/// </summary>
public interface IHAICaseGrain : IGrainWithStringKey
{
    Task<HAICaseState> GetCaseAsync();

    Task CreateCaseAsync(
        string caseId,
        string patientId,
        string patientName,
        DateTime? dateOfBirth,
        string locationId,
        string locationName,
        HAIType haiType,
        DateTime? infectionDate,
        string pathogen,
        string reportedById,
        string reportedByName,
        string? notes);

    Task UpdateStatusAsync(HAICaseStatus status, DateTime? confirmedDate);

    Task UpdateClinicalDataAsync(
        string cultureSource,
        DateTime? cultureDate,
        string gramStain,
        string cultureResult,
        string deviceType,
        int? deviceInDays,
        DateTime? surgeryDate,
        string surgeryProcedure);

    Task AddSusceptibilityResultAsync(AntibioticSusceptibilityResult result);

    Task LinkToOutbreakAsync(string outbreakId);

    Task UnlinkFromOutbreakAsync();
}
