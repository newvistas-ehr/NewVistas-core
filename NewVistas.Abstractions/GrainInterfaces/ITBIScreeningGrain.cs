// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-screening TBI grain — key: "TBI-SCREEN:{guid}"
/// Represents one DVBIC-inspired 4-question TBI screening encounter.
/// </summary>
public interface ITBIScreeningGrain : IGrainWithStringKey
{
    Task<TBIScreeningState> GetScreeningAsync();

    Task CreateScreeningAsync(
        string patientId,
        string patientName,
        DateTime screeningDate,
        string screeningLocation,
        string screenedById,
        string screenedByName,
        string encounterType,
        List<TBIScreeningAnswer> answers,
        string? notes);

    Task FinalizeScreeningAsync(TBIScreeningResult result, bool triggeredFullEvaluation);

    Task RecordFullEvaluationAsync(
        DateTime fullEvalDate,
        string providerId,
        string providerName,
        TBISeverity confirmedSeverity);
}
