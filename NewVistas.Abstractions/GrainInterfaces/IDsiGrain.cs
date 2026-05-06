// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Decision Support Intervention Definition Grain.
/// §170.315(b)(11) — stores a CDS intervention with HTI-1 transparency attributes.
///
/// Grain Key: "DSI:{interventionId}"
/// </summary>
public interface IDsiInterventionGrain : IGrainWithStringKey
{
    Task SaveInterventionAsync(DsiInterventionState intervention);
    Task<DsiInterventionState> GetInterventionAsync();
    Task SetActiveAsync(bool isActive);
}

/// <summary>
/// DSI Intervention Index Grain — lists all registered interventions.
/// Grain Key: "DSI-INDEX"
/// </summary>
public interface IDsiInterventionIndexGrain : IGrainWithStringKey
{
    Task AddInterventionAsync(DsiInterventionSummary summary);
    Task RemoveInterventionAsync(string interventionId);
    Task<List<DsiInterventionSummary>> GetAllInterventionsAsync();
    Task<List<DsiInterventionSummary>> GetActiveInterventionsAsync();
}

/// <summary>
/// DSI Event Grain — records a single intervention firing and clinician response.
/// Grain Key: "DSI-EVENT:{eventId}"
/// </summary>
public interface IDsiEventGrain : IGrainWithStringKey
{
    /// <summary>Record an intervention firing for a patient.</summary>
    Task RecordFiringAsync(
        string interventionId,
        string interventionTitle,
        string interventionType,
        string patientId,
        string? userId,
        string recommendedAction,
        string severity,
        List<string> triggerEvidence,
        string sourceCitation);

    /// <summary>Record the clinician's response to the alert.</summary>
    Task RecordResponseAsync(string response, string? overrideReason);

    /// <summary>Get the full event state.</summary>
    Task<DsiEventState> GetEventAsync();
}

/// <summary>
/// DSI Event Index Grain — audit log of all intervention firings.
/// Grain Key: "DSI-EVENT-INDEX"
/// </summary>
public interface IDsiEventIndexGrain : IGrainWithStringKey
{
    Task AddEventAsync(DsiEventSummary summary);
    Task UpdateResponseAsync(string eventId, string response);
    Task<List<DsiEventSummary>> GetAllEventsAsync();
    Task<List<DsiEventSummary>> GetEventsByPatientAsync(string patientId);
    Task<List<DsiEventSummary>> GetEventsByInterventionAsync(string interventionId);
    Task<List<DsiEventSummary>> GetPendingEventsAsync();
}

/// <summary>
/// DSI Evaluation Grain — evaluates all active interventions against a patient's data.
/// Returns alerts that should be presented to the clinician.
///
/// Grain Key: "DSI-EVAL:{patientId}"
/// </summary>
public interface IDsiEvaluationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Evaluate all active interventions for a patient.
    /// Returns a list of triggered alerts with source attribution and HTI-1 transparency.
    /// </summary>
    Task<List<DsiEvaluationResult>> EvaluatePatientAsync();
}
