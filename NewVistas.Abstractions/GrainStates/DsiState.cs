// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a Clinical Decision Support Intervention definition.
/// §170.315(b)(11) — Decision Support Interventions.
///
/// HTI-1 Final Rule requires transparency for both evidence-based and
/// predictive (AI/ML) decision support interventions.
///
/// Grain Key: "DSI:{interventionId}"
/// </summary>
[GenerateSerializer]
public class DsiInterventionState
{
    /// <summary>Unique intervention identifier.</summary>
    [Id(0)]
    public string InterventionId { get; set; } = string.Empty;

    /// <summary>Human-readable title (e.g., "Sepsis Early Warning Alert").</summary>
    [Id(1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description of what the intervention does.</summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Intervention type: "evidence-based", "predictive" (AI/ML).
    /// HTI-1 requires different transparency for each type.
    /// </summary>
    [Id(3)]
    public string InterventionType { get; set; } = "evidence-based";

    /// <summary>Clinical domain: "medication", "laboratory", "diagnostic", "preventive", "workflow".</summary>
    [Id(4)]
    public string ClinicalDomain { get; set; } = string.Empty;

    /// <summary>Whether this intervention is currently active.</summary>
    [Id(5)]
    public bool IsActive { get; set; } = true;

    // ─── Source Attribution (§170.315(b)(11)(i)) ─────────────────────────────

    /// <summary>Bibliographic citation or guideline reference (e.g., "AHA/ACC 2023 Hypertension Guideline").</summary>
    [Id(6)]
    public string SourceCitation { get; set; } = string.Empty;

    /// <summary>Developer/author of the intervention logic.</summary>
    [Id(7)]
    public string Developer { get; set; } = string.Empty;

    /// <summary>Funding source for the intervention development.</summary>
    [Id(8)]
    public string? FundingSource { get; set; }

    /// <summary>Date the intervention was last updated/revised.</summary>
    [Id(9)]
    public DateTime? LastRevisedDate { get; set; }

    // ─── Predictive DSI — HTI-1 Transparency (§170.315(b)(11)(iv-viii)) ─────

    /// <summary>For predictive DSI: description of the AI/ML model purpose and intended use.</summary>
    [Id(10)]
    public string? ModelPurpose { get; set; }

    /// <summary>For predictive DSI: training data description and characteristics.</summary>
    [Id(11)]
    public string? TrainingDataDescription { get; set; }

    /// <summary>For predictive DSI: validation/performance metrics (e.g., AUROC, sensitivity, specificity).</summary>
    [Id(12)]
    public string? PerformanceMetrics { get; set; }

    /// <summary>For predictive DSI: known limitations and potential biases.</summary>
    [Id(13)]
    public string? KnownLimitations { get; set; }

    /// <summary>For predictive DSI: fairness assessment across demographic groups.</summary>
    [Id(14)]
    public string? FairnessAssessment { get; set; }

    /// <summary>For predictive DSI: risk management practices.</summary>
    [Id(15)]
    public string? RiskManagement { get; set; }

    /// <summary>For predictive DSI: input data requirements.</summary>
    [Id(16)]
    public string? InputDataRequirements { get; set; }

    /// <summary>For predictive DSI: output description and interpretation guidance.</summary>
    [Id(17)]
    public string? OutputDescription { get; set; }

    // ─── Trigger Criteria ────────────────────────────────────────────────────

    /// <summary>
    /// Criteria that trigger this intervention. Evaluated against patient data.
    /// Uses the same CqmCriterion pattern for consistency.
    /// </summary>
    [Id(18)]
    public List<DsiTriggerCriterion> TriggerCriteria { get; set; } = new();

    /// <summary>Recommended action when the intervention fires.</summary>
    [Id(19)]
    public string RecommendedAction { get; set; } = string.Empty;

    /// <summary>Severity: "info", "warning", "critical".</summary>
    [Id(20)]
    public string Severity { get; set; } = "info";

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Trigger criterion for a DSI intervention.
/// </summary>
[GenerateSerializer]
public class DsiTriggerCriterion
{
    /// <summary>Data source: "Problem", "Lab", "Vital", "Medication", "Demographic", "Order".</summary>
    [Id(0)]
    public string DataSource { get; set; } = string.Empty;

    /// <summary>Code or value to match (e.g., ICD-10 code, LOINC code, vital type).</summary>
    [Id(1)]
    public string ValueSetOrCode { get; set; } = string.Empty;

    /// <summary>Comparison operator: "exists", "not-exists", "greater-than", "less-than", "between".</summary>
    [Id(2)]
    public string Operator { get; set; } = "exists";

    /// <summary>Comparison value.</summary>
    [Id(3)]
    public string? ComparisonValue { get; set; }

    /// <summary>Secondary comparison value (for "between").</summary>
    [Id(4)]
    public string? ComparisonValue2 { get; set; }

    /// <summary>Human-readable description.</summary>
    [Id(5)]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Index entry for listing DSI interventions.
/// </summary>
[GenerateSerializer]
public class DsiInterventionSummary
{
    [Id(0)]
    public string InterventionId { get; set; } = string.Empty;

    [Id(1)]
    public string Title { get; set; } = string.Empty;

    [Id(2)]
    public string InterventionType { get; set; } = string.Empty;

    [Id(3)]
    public string ClinicalDomain { get; set; } = string.Empty;

    [Id(4)]
    public bool IsActive { get; set; }

    [Id(5)]
    public string Severity { get; set; } = string.Empty;

    [Id(6)]
    public string Developer { get; set; } = string.Empty;
}

/// <summary>
/// Index state for listing all DSI interventions.
/// Grain Key: "DSI-INDEX"
/// </summary>
[GenerateSerializer]
public class DsiInterventionIndexState
{
    [Id(0)]
    public List<DsiInterventionSummary> Interventions { get; set; } = new();
}

/// <summary>
/// Record of a DSI intervention firing for a specific patient.
/// Tracks the intervention event and the clinician's response.
///
/// Grain Key: "DSI-EVENT:{eventId}"
/// </summary>
[GenerateSerializer]
public class DsiEventState
{
    /// <summary>Unique event identifier.</summary>
    [Id(0)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>Intervention that fired.</summary>
    [Id(1)]
    public string InterventionId { get; set; } = string.Empty;

    /// <summary>Intervention title (denormalized for display).</summary>
    [Id(2)]
    public string InterventionTitle { get; set; } = string.Empty;

    /// <summary>Intervention type ("evidence-based" or "predictive").</summary>
    [Id(3)]
    public string InterventionType { get; set; } = string.Empty;

    /// <summary>Patient for whom the intervention fired.</summary>
    [Id(4)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Clinician who received the alert.</summary>
    [Id(5)]
    public string? UserId { get; set; }

    /// <summary>Date/time the intervention fired.</summary>
    [Id(6)]
    public DateTime FiredDate { get; set; }

    /// <summary>The recommended action presented to the clinician.</summary>
    [Id(7)]
    public string RecommendedAction { get; set; } = string.Empty;

    /// <summary>Severity when fired.</summary>
    [Id(8)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>Clinical evidence that triggered the alert.</summary>
    [Id(9)]
    public List<string> TriggerEvidence { get; set; } = new();

    /// <summary>
    /// User response: "accepted", "overridden", "deferred", "not-applicable", "pending".
    /// </summary>
    [Id(10)]
    public string UserResponse { get; set; } = "pending";

    /// <summary>Reason for override (required when overridden).</summary>
    [Id(11)]
    public string? OverrideReason { get; set; }

    /// <summary>Date/time the user responded.</summary>
    [Id(12)]
    public DateTime? ResponseDate { get; set; }

    /// <summary>Source attribution displayed with the alert.</summary>
    [Id(13)]
    public string SourceCitation { get; set; } = string.Empty;

    [Id(14)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Index entry for DSI events.
/// </summary>
[GenerateSerializer]
public class DsiEventSummary
{
    [Id(0)]
    public string EventId { get; set; } = string.Empty;

    [Id(1)]
    public string InterventionId { get; set; } = string.Empty;

    [Id(2)]
    public string InterventionTitle { get; set; } = string.Empty;

    [Id(3)]
    public string PatientId { get; set; } = string.Empty;

    [Id(4)]
    public DateTime FiredDate { get; set; }

    [Id(5)]
    public string Severity { get; set; } = string.Empty;

    [Id(6)]
    public string UserResponse { get; set; } = string.Empty;
}

/// <summary>
/// Index state for DSI events (audit log).
/// Grain Key: "DSI-EVENT-INDEX"
/// </summary>
[GenerateSerializer]
public class DsiEventIndexState
{
    [Id(0)]
    public List<DsiEventSummary> Events { get; set; } = new();
}

/// <summary>
/// Result of evaluating a DSI intervention against a patient.
/// </summary>
[GenerateSerializer]
public class DsiEvaluationResult
{
    [Id(0)]
    public string InterventionId { get; set; } = string.Empty;

    [Id(1)]
    public string Title { get; set; } = string.Empty;

    [Id(2)]
    public string InterventionType { get; set; } = string.Empty;

    [Id(3)]
    public string Severity { get; set; } = string.Empty;

    [Id(4)]
    public string RecommendedAction { get; set; } = string.Empty;

    [Id(5)]
    public string SourceCitation { get; set; } = string.Empty;

    [Id(6)]
    public List<string> TriggerEvidence { get; set; } = new();

    /// <summary>For predictive DSI: model transparency info per HTI-1.</summary>
    [Id(7)]
    public DsiPredictiveTransparency? PredictiveTransparency { get; set; }
}

/// <summary>
/// HTI-1 transparency information for predictive DSI interventions.
/// Presented to clinicians alongside the recommendation.
/// </summary>
[GenerateSerializer]
public class DsiPredictiveTransparency
{
    [Id(0)]
    public string ModelPurpose { get; set; } = string.Empty;

    [Id(1)]
    public string Developer { get; set; } = string.Empty;

    [Id(2)]
    public string? PerformanceMetrics { get; set; }

    [Id(3)]
    public string? KnownLimitations { get; set; }

    [Id(4)]
    public string? FairnessAssessment { get; set; }

    [Id(5)]
    public string? InputDataRequirements { get; set; }

    [Id(6)]
    public string? OutputDescription { get; set; }
}
