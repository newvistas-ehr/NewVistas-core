// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient nursing care plan — VistA File #212 (NURSING PATIENT).
/// Manages the set of nursing diagnoses (NANDA-style), goals, interventions,
/// and outcome evaluations for a single patient.
/// Grain key: "NURS-CAREPLAN:{patientId}"
/// </summary>
public interface INursingCarePlanGrain : IGrainWithStringKey
{
    /// <summary>Returns the full care plan state.</summary>
    Task<NursingCarePlanState> GetAsync();

    /// <summary>
    /// Adds a nursing diagnosis (NANDA-style problem) to the care plan.
    /// Returns the new problem ID.
    /// </summary>
    Task<string> AddDiagnosisAsync(
        string nursingDiagnosis,
        string? relatedTo,
        string? evidencedBy,
        int? priority,
        string? createdById,
        string? createdByName);

    /// <summary>Adds a measurable goal to an existing nursing diagnosis.</summary>
    Task AddGoalAsync(string problemId, string goalText, DateTime? targetDate);

    /// <summary>Adds a nursing intervention (action) to an existing diagnosis.</summary>
    Task AddInterventionAsync(
        string problemId,
        string interventionText,
        string? frequency,
        string? createdById,
        string? createdByName);

    /// <summary>Records an outcome evaluation against a nursing diagnosis.</summary>
    Task RecordOutcomeEvaluationAsync(
        string problemId,
        NursingOutcomeRating rating,
        string evaluatedById,
        string evaluatedByName,
        string? notes);

    /// <summary>Updates the achievement status of a goal.</summary>
    Task UpdateGoalStatusAsync(string problemId, string goalId, NursingGoalStatus status);

    /// <summary>Resolves (closes) a nursing diagnosis.</summary>
    Task ResolveDiagnosisAsync(string problemId, string? resolutionNotes);

    /// <summary>Deactivates a nursing intervention (discontinued order).</summary>
    Task DeactivateInterventionAsync(string problemId, string interventionId);
}
