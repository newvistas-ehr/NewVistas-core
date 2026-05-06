// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Mental Health Instrument grain based on VistA
/// MENTAL HEALTH RESULTS file and YS MH INSTRUMENT (#601.71).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for this instrument administration are persisted
/// atomically with the projection in a single <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public class MentalHealthState : EventSourcedStateBase
{
    [Id(0)]
    public string InstrumentId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Instrument Name (.01) � e.g., PHQ-9, GAD-7, AUDIT-C, PC-PTSD-5, Columbia Suicide Severity
    /// </summary>
    [Id(2)]
    public string InstrumentName { get; set; } = string.Empty;

    [Id(3)]
    public string? InstrumentDefId { get; set; }

    /// <summary>
    /// Administration Date
    /// </summary>
    [Id(4)]
    public DateTime AdministrationDateTime { get; set; }

    /// <summary>
    /// Total Score
    /// </summary>
    [Id(5)]
    public decimal? TotalScore { get; set; }

    /// <summary>
    /// Score Interpretation � NEGATIVE, LOW, MODERATE, MODERATELY SEVERE, SEVERE
    /// </summary>
    [Id(6)]
    public string? ScoreInterpretation { get; set; }

    /// <summary>
    /// Is Positive Screen
    /// </summary>
    [Id(7)]
    public bool? IsPositiveScreen { get; set; }

    /// <summary>
    /// Individual item responses stored as key/value
    /// </summary>
    [Id(8)]
    public Dictionary<string, string> Responses { get; set; } = new();

    /// <summary>
    /// Administered By
    /// </summary>
    [Id(9)]
    public string? AdministeredById { get; set; }

    [Id(10)]
    public string? AdministeredByName { get; set; }

    /// <summary>
    /// Ordering Provider
    /// </summary>
    [Id(11)]
    public string? OrderingProviderId { get; set; }

    [Id(12)]
    public string? OrderingProviderName { get; set; }

    [Id(13)]
    public string? LocationId { get; set; }

    [Id(14)]
    public string? LocationName { get; set; }

    [Id(15)]
    public string? VisitId { get; set; }

    /// <summary>
    /// Status � COMPLETED, IN PROGRESS, CANCELLED
    /// </summary>
    [Id(16)]
    public string Status { get; set; } = "COMPLETED";

    [Id(17)]
    public string? Comments { get; set; }

    [Id(18)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(19)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Scoring method used — AUTO or MANUAL
    /// </summary>
    [Id(20)]
    public string? ScoringMethodUsed { get; set; }

    /// <summary>
    /// Structured item-level responses (supplements the Dictionary-based Responses)
    /// </summary>
    [Id(21)]
    public List<MhItemResponse> ItemResponses { get; set; } = new();

    /// <summary>
    /// Risk Level — 0=None, 1=Low, 2=Moderate, 3=High, 4=Imminent
    /// </summary>
    [Id(22)]
    public int? RiskLevel { get; set; }

    /// <summary>
    /// Clinician's risk assessment notes
    /// </summary>
    [Id(23)]
    public string? RiskAssessmentNotes { get; set; }

    /// <summary>
    /// Flag indicating whether follow-up is required
    /// </summary>
    [Id(24)]
    public bool RequiresFollowUp { get; set; }

    /// <summary>
    /// Date by which follow-up is due
    /// </summary>
    [Id(25)]
    public DateTime? FollowUpDueDate { get; set; }

    /// <summary>
    /// Follow-up plan description
    /// </summary>
    [Id(26)]
    public string? FollowUpPlan { get; set; }

    /// <summary>
    /// Previous score for trending comparison
    /// </summary>
    [Id(27)]
    public decimal? PreviousScore { get; set; }

    /// <summary>
    /// Date of previous administration for trending
    /// </summary>
    [Id(28)]
    public DateTime? PreviousAdministrationDate { get; set; }

    /// <summary>
    /// Auto-generated clinical recommendation based on score interpretation
    /// </summary>
    [Id(29)]
    public string? ClinicalRecommendation { get; set; }

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// instrument on the grain does not retroactively mutate the historical
    /// snapshot stored on a clinical event payload.
    /// </summary>
    public MentalHealthState Clone() => new()
    {
        InstrumentId = InstrumentId,
        PatientId = PatientId,
        InstrumentName = InstrumentName,
        InstrumentDefId = InstrumentDefId,
        AdministrationDateTime = AdministrationDateTime,
        TotalScore = TotalScore,
        ScoreInterpretation = ScoreInterpretation,
        IsPositiveScreen = IsPositiveScreen,
        Responses = new Dictionary<string, string>(Responses),
        AdministeredById = AdministeredById,
        AdministeredByName = AdministeredByName,
        OrderingProviderId = OrderingProviderId,
        OrderingProviderName = OrderingProviderName,
        LocationId = LocationId,
        LocationName = LocationName,
        VisitId = VisitId,
        Status = Status,
        Comments = Comments,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        ScoringMethodUsed = ScoringMethodUsed,
        ItemResponses = ItemResponses.Select(r => new MhItemResponse
        {
            ItemNumber = r.ItemNumber,
            QuestionText = r.QuestionText,
            ResponseValue = r.ResponseValue,
            ResponseText = r.ResponseText
        }).ToList(),
        RiskLevel = RiskLevel,
        RiskAssessmentNotes = RiskAssessmentNotes,
        RequiresFollowUp = RequiresFollowUp,
        FollowUpDueDate = FollowUpDueDate,
        FollowUpPlan = FollowUpPlan,
        PreviousScore = PreviousScore,
        PreviousAdministrationDate = PreviousAdministrationDate,
        ClinicalRecommendation = ClinicalRecommendation
    };
}

/// <summary>
/// Structured item-level response for a mental health instrument question
/// </summary>
[GenerateSerializer]
public class MhItemResponse
{
    /// <summary>
    /// Item/question number within the instrument
    /// </summary>
    [Id(0)]
    public int ItemNumber { get; set; }

    /// <summary>
    /// Text of the question
    /// </summary>
    [Id(1)]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// Numeric response value (used for scoring)
    /// </summary>
    [Id(2)]
    public int ResponseValue { get; set; }

    /// <summary>
    /// Optional text representation of the response
    /// </summary>
    [Id(3)]
    public string? ResponseText { get; set; }
}
