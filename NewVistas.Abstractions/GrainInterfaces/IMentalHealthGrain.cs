// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Mental Health Instrument Grain Interface based on VistA YS MH INSTRUMENT (#601.71)
/// </summary>
public interface IMentalHealthGrain : IGrainWithStringKey
{
    Task<GrainStates.MentalHealthState> GetInstrumentAsync();

    Task RecordInstrumentAsync(
        string patientId,
        string instrumentName,
        string? instrumentDefId,
        DateTime administrationDateTime,
        decimal? totalScore,
        string? scoreInterpretation,
        bool? isPositiveScreen,
        Dictionary<string, string>? responses,
        string? administeredById,
        string? administeredByName,
        string? orderingProviderId,
        string? orderingProviderName,
        string? locationId,
        string? locationName,
        string? visitId,
        string? comments);

    Task CancelAsync();

    /// <summary>
    /// Record a risk assessment with level and optional notes.
    /// Risk levels: 0=None, 1=Low, 2=Moderate, 3=High, 4=Imminent
    /// </summary>
    Task RecordRiskAssessmentAsync(int riskLevel, string? riskNotes);

    /// <summary>
    /// Set follow-up requirements for this instrument administration.
    /// </summary>
    Task SetFollowUpAsync(bool requiresFollowUp, DateTime? followUpDueDate, string? followUpPlan);

    /// <summary>
    /// Add an individual item response to the instrument.
    /// </summary>
    Task AddItemResponseAsync(int itemNumber, string questionText, int responseValue, string? responseText);

    /// <summary>
    /// Auto-score the instrument based on ItemResponses using the instrument library definitions.
    /// Sets TotalScore, ScoreInterpretation, IsPositiveScreen, ScoringMethodUsed, and ClinicalRecommendation.
    /// </summary>
    Task ScoreInstrumentAsync();

    /// <summary>
    /// Set the previous score and administration date for trending comparison.
    /// </summary>
    Task SetPreviousScoreAsync(decimal previousScore, DateTime previousDate);

    /// <summary>
    /// Calculate the change between current TotalScore and PreviousScore.
    /// Returns null if either score is not available.
    /// </summary>
    Task<decimal?> CalculateScoreChangeAsync();
}
