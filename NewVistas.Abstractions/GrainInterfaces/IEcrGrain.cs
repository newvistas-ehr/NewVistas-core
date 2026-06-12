// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Reportable Condition Trigger Grain — stores trigger definitions for eCR.
/// §170.315(f)(5) — Electronic Case Reporting.
///
/// Triggers define which clinical codes (ICD-10, LOINC, SNOMED) should
/// initiate an electronic case report to public health.
///
/// Based on RCTC (Reportable Condition Trigger Codes) maintained by CSTE/APHL.
///
/// Grain Key: "ECR-TRIGGER:{triggerId}"
/// </summary>
public interface IEcrTriggerGrain : IGrainWithStringKey
{
    /// <summary>Define or update a reportable condition trigger.</summary>
    Task SaveTriggerAsync(EcrTriggerState trigger);

    /// <summary>Get the full trigger definition.</summary>
    Task<EcrTriggerState> GetTriggerAsync();

    /// <summary>Activate/deactivate the trigger.</summary>
    Task SetActiveAsync(bool isActive);
}

/// <summary>
/// Reportable Condition Trigger Index Grain — lists all registered triggers.
///
/// Grain Key: "ECR-TRIGGER-INDEX"
/// </summary>
public interface IEcrTriggerIndexGrain : IGrainWithStringKey
{
    Task AddTriggerAsync(EcrTriggerSummary summary);
    Task RemoveTriggerAsync(string triggerId);
    Task<List<EcrTriggerSummary>> GetAllTriggersAsync();
    Task<List<EcrTriggerSummary>> GetActiveTriggersAsync();
}

/// <summary>
/// Electronic Case Report (eICR) Grain — manages the lifecycle of a single case report.
/// §170.315(f)(5) — Electronic Case Reporting.
///
/// Lifecycle: triggered → generated (eICR created) → submitted → acknowledged/reportable/not-reportable
///
/// Grain Key: "ECR-CASE:{caseId}"
/// </summary>
public interface IEcrCaseGrain : IGrainWithStringKey
{
    /// <summary>
    /// Create a new case report from a detected trigger match.
    /// Sets status to "triggered" and records clinical evidence.
    /// </summary>
    Task CreateCaseAsync(
        string patientId,
        string triggerId,
        string conditionName,
        string triggeringCode,
        string triggeringCodeSystem,
        string triggeringDescription,
        List<string> jurisdictions,
        List<string> clinicalEvidence,
        string? responsibleProvider,
        string? facilityName);

    /// <summary>
    /// Generate the eICR document (FHIR Bundle or CDA XML).
    /// Transitions status from "triggered" to "generated".
    /// </summary>
    Task GenerateEicrAsync();

    /// <summary>
    /// Mark the eICR as submitted to public health.
    /// Transitions status from "generated" to "submitted".
    /// </summary>
    Task MarkSubmittedAsync();

    /// <summary>
    /// Record the Reportability Response from public health decision support.
    /// Transitions status to the determination result.
    /// </summary>
    Task RecordReportabilityResponseAsync(
        string determination,
        string responseContent);

    /// <summary>Get the full case state.</summary>
    Task<EcrCaseState> GetCaseAsync();
}

/// <summary>
/// eCR Case Index Grain — lists all case reports for dashboard/monitoring.
///
/// Grain Key: "ECR-CASE-INDEX"
/// </summary>
public interface IEcrCaseIndexGrain : IGrainWithStringKey
{
    Task AddCaseAsync(EcrCaseSummary summary);
    Task UpdateCaseStatusAsync(string caseId, string status, string? determination);
    Task<List<EcrCaseSummary>> GetAllCasesAsync();
    Task<List<EcrCaseSummary>> GetCasesByStatusAsync(string status);
    Task<List<EcrCaseSummary>> GetCasesByPatientAsync(string patientId, int maxResults = 50);
    Task<List<EcrCaseSummary>> GetCasesByConditionAsync(string conditionName);
}

/// <summary>
/// eCR Screening Grain — evaluates a patient's clinical data against all active
/// reportable condition triggers. This is the "detection" engine.
///
/// Grain Key: "ECR-SCREEN:{patientId}"
/// </summary>
public interface IEcrScreeningGrain : IGrainWithStringKey
{
    /// <summary>
    /// Screen a patient's current clinical data against all active reportable condition triggers.
    /// Returns a list of matched triggers (conditions that should be reported).
    /// </summary>
    Task<List<EcrScreeningMatch>> ScreenPatientAsync();
}

/// <summary>
/// Result of screening a patient against a single reportable condition trigger.
/// </summary>
[GenerateSerializer]
public class EcrScreeningMatch
{
    [Id(0)]
    public string TriggerId { get; set; } = string.Empty;

    [Id(1)]
    public string ConditionName { get; set; } = string.Empty;

    [Id(2)]
    public string MatchedCode { get; set; } = string.Empty;

    [Id(3)]
    public string MatchedCodeSystem { get; set; } = string.Empty;

    [Id(4)]
    public string MatchedDescription { get; set; } = string.Empty;

    [Id(5)]
    public List<string> Jurisdictions { get; set; } = new();

    [Id(6)]
    public string ReportingTimeframe { get; set; } = string.Empty;

    [Id(7)]
    public List<string> ClinicalEvidence { get; set; } = new();
}
