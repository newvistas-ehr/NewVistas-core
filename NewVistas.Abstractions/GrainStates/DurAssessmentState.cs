// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Type of Drug Utilization Review check performed.
/// Derived from VistA PSOORED.m DUR checks and PSODRDUP.m duplicate detection.
/// </summary>
[GenerateSerializer]
public enum DurCheckType
{
    /// <summary>Exact same drug already on active medication list (PSODRDUP.m).</summary>
    DuplicateDrug = 0,

    /// <summary>Another drug in the same VA drug class is already active (PSODRDU2.m class check).</summary>
    DuplicateTherapy = 1,

    /// <summary>Drug-drug interaction detected via IDrugInteractionCheckerGrain (DRGINT.m).</summary>
    DrugInteraction = 2,

    /// <summary>Drug contradicts a documented allergy (PSOVER1.m ALLERGY check).</summary>
    DrugAllergyContraindication = 3,

    /// <summary>Prescribed dose exceeds known maximum daily dose.</summary>
    MaxDoseExceeded = 4,

    /// <summary>Days supply exceeds formulary or policy maximum (PSO CalcMaxRefills).</summary>
    DaysSupplyExceeded = 5,

    /// <summary>Refill requested before minimum interval since last fill.</summary>
    RefillTooSoon = 6,

    /// <summary>Dose may require age-based adjustment (pediatric or geriatric).</summary>
    AgeBasedDosing = 7,

    /// <summary>Dose may require renal function adjustment.</summary>
    RenalAdjustment = 8,

    /// <summary>Dose may require hepatic function adjustment.</summary>
    HepaticAdjustment = 9,

    /// <summary>Controlled substance DEA schedule enforcement (PSOORED.m DEA checks).</summary>
    ControlledSubstance = 10,

    /// <summary>Drug-gene (pharmacogenomic) contraindication from the patient's PGx profile (CPIC/FDA).</summary>
    Pharmacogenomic = 11
}

/// <summary>
/// Outcome of an individual DUR check.
/// </summary>
[GenerateSerializer]
public enum DurOutcome
{
    /// <summary>Check passed — no issue found.</summary>
    Pass = 0,

    /// <summary>Check failed — issue blocks dispensing until resolved.</summary>
    Fail = 1,

    /// <summary>Check produced a warning — informational, does not block.</summary>
    Warning = 2,

    /// <summary>Check failed but was overridden by a pharmacist with documented reason.</summary>
    Override = 3,

    /// <summary>Check was not applicable to this prescription.</summary>
    NotApplicable = 4,

    /// <summary>
    /// Check could not run because required reference data is unavailable
    /// (e.g., drug-interaction dataset not loaded). Blocks dispensing and is
    /// NOT pharmacist-overridable — resolution is an administrator loading
    /// the missing dataset.
    /// </summary>
    Unavailable = 5
}

/// <summary>
/// Overall status of a DUR assessment.
/// Maps to VistA PSO incomplete/pending DUR review status.
/// </summary>
[GenerateSerializer]
public enum DurAssessmentStatus
{
    /// <summary>Assessment is pending review — prescription held in DUR review queue.</summary>
    Pending = 0,

    /// <summary>All checks passed — prescription may proceed to fill.</summary>
    Passed = 1,

    /// <summary>One or more checks failed — prescription cannot be filled until resolved.</summary>
    Failed = 2,

    /// <summary>Failed checks were overridden by a pharmacist with documented reasons.</summary>
    OverriddenByPharmacist = 3,

    /// <summary>Results acknowledged by pharmacist — no further action required.</summary>
    Acknowledged = 4
}

/// <summary>
/// Result of a single DUR check within an assessment.
/// Each check type produces one result with outcome, severity, and clinical detail.
/// </summary>
[GenerateSerializer]
public class DurCheckResult
{
    /// <summary>Type of check performed.</summary>
    [Id(0)]
    public DurCheckType CheckType { get; set; }

    /// <summary>Outcome of this check.</summary>
    [Id(1)]
    public DurOutcome Outcome { get; set; }

    /// <summary>Clinical severity level (matches InteractionSeverity for drug interactions).</summary>
    [Id(2)]
    public string Severity { get; set; } = string.Empty;

    /// <summary>Human-readable message describing the finding.</summary>
    [Id(3)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Detailed clinical information (e.g., conflicting drug name, allergy details).</summary>
    [Id(4)]
    public string? Details { get; set; }

    /// <summary>Pharmacist who overrode this check (if Outcome == Override).</summary>
    [Id(5)]
    public string? OverriddenBy { get; set; }

    /// <summary>Documented clinical reason for override.</summary>
    [Id(6)]
    public string? OverrideReason { get; set; }

    /// <summary>UTC timestamp when this check was overridden.</summary>
    [Id(7)]
    public DateTime? OverrideDate { get; set; }

    /// <summary>ID of the conflicting entity (e.g., prescription ID, allergy ID).</summary>
    [Id(8)]
    public string? ConflictingEntityId { get; set; }

    /// <summary>Name of the conflicting entity (e.g., drug name, allergen name).</summary>
    [Id(9)]
    public string? ConflictingEntityName { get; set; }
}

/// <summary>
/// Full state of a Drug Utilization Review assessment.
/// VistA reference: PSOORED.m DUR checks, PSO incomplete status.
/// One assessment is created per prescription at time of entry/fill.
/// Key format: DUR:{guid}
/// </summary>
[GenerateSerializer]
public class DurAssessmentState
{
    /// <summary>Unique assessment identifier (grain primary key).</summary>
    [Id(0)]
    public string AssessmentId { get; set; } = string.Empty;

    /// <summary>Prescription this assessment was performed for.</summary>
    [Id(1)]
    public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Patient this prescription belongs to.</summary>
    [Id(2)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Drug name from the prescription (File #50 .01).</summary>
    [Id(3)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>Drug identifier (IEN or NDC).</summary>
    [Id(4)]
    public string? DrugId { get; set; }

    /// <summary>VA drug class code for duplicate therapy checking.</summary>
    [Id(5)]
    public string? DrugClass { get; set; }

    /// <summary>Prescribed dosage.</summary>
    [Id(6)]
    public string? Dosage { get; set; }

    /// <summary>Route of administration.</summary>
    [Id(7)]
    public string? Route { get; set; }

    /// <summary>Dosing schedule.</summary>
    [Id(8)]
    public string? Schedule { get; set; }

    /// <summary>Days supply for the prescription.</summary>
    [Id(9)]
    public int? DaysSupply { get; set; }

    /// <summary>Quantity to dispense.</summary>
    [Id(10)]
    public int? Quantity { get; set; }

    /// <summary>Overall assessment status.</summary>
    [Id(11)]
    public DurAssessmentStatus Status { get; set; } = DurAssessmentStatus.Pending;

    /// <summary>All DUR check results for this assessment.</summary>
    [Id(12)]
    public List<DurCheckResult> Checks { get; set; } = new();

    /// <summary>Pharmacist or system user who initiated the DUR.</summary>
    [Id(13)]
    public string? PerformedBy { get; set; }

    /// <summary>UTC timestamp when the DUR was performed.</summary>
    [Id(14)]
    public DateTime? PerformedDate { get; set; }

    /// <summary>Computed overall outcome based on check results.</summary>
    [Id(15)]
    public DurOutcome OverallOutcome { get; set; } = DurOutcome.Pass;

    /// <summary>Pharmacist who acknowledged the DUR results.</summary>
    [Id(16)]
    public string? AcknowledgedBy { get; set; }

    /// <summary>UTC timestamp when the DUR was acknowledged.</summary>
    [Id(17)]
    public DateTime? AcknowledgedDate { get; set; }

    /// <summary>Clinical notes or additional documentation.</summary>
    [Id(18)]
    public string? Notes { get; set; }

    /// <summary>UTC timestamp when this assessment was created.</summary>
    [Id(19)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last modification.</summary>
    [Id(20)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Number of checks that failed.</summary>
    [Id(21)]
    public int FailedCheckCount { get; set; }

    /// <summary>Number of checks that produced warnings.</summary>
    [Id(22)]
    public int WarningCheckCount { get; set; }

    /// <summary>Number of checks that were overridden.</summary>
    [Id(23)]
    public int OverriddenCheckCount { get; set; }
}

/// <summary>
/// Lightweight index entry for DUR assessment lists.
/// Stored in the per-patient DUR index grain for efficient querying.
/// </summary>
[GenerateSerializer]
public class DurAssessmentIndexEntry
{
    /// <summary>Assessment identifier.</summary>
    [Id(0)]
    public string AssessmentId { get; set; } = string.Empty;

    /// <summary>Prescription identifier.</summary>
    [Id(1)]
    public string PrescriptionId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(2)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Drug name.</summary>
    [Id(3)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>Current assessment status.</summary>
    [Id(4)]
    public DurAssessmentStatus Status { get; set; }

    /// <summary>Overall outcome of the assessment.</summary>
    [Id(5)]
    public DurOutcome OverallOutcome { get; set; }

    /// <summary>Number of failed checks.</summary>
    [Id(6)]
    public int FailedCheckCount { get; set; }

    /// <summary>Number of warning checks.</summary>
    [Id(7)]
    public int WarningCheckCount { get; set; }

    /// <summary>When the DUR was performed.</summary>
    [Id(8)]
    public DateTime? PerformedDate { get; set; }

    /// <summary>Pharmacist or user who performed the DUR.</summary>
    [Id(9)]
    public string? PerformedBy { get; set; }
}

/// <summary>
/// State for the per-patient DUR assessment index grain.
/// Key format: DUR-IDX:{patientId}
/// </summary>
[GenerateSerializer]
public class DurAssessmentIndexState
{
    /// <summary>All DUR assessment index entries for this patient.</summary>
    [Id(0)]
    public List<DurAssessmentIndexEntry> Entries { get; set; } = new();
}
