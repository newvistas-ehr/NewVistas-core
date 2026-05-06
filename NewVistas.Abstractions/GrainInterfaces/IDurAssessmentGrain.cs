// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single Drug Utilization Review (DUR) assessment.
///
/// VistA reference: PSOORED.m DUR checks, PSOVER1.m verification with DUR,
/// PSODRDUP.m duplicate drug detection, DRGINT.m drug interaction checking.
///
/// Key format: DUR:{guid}
/// Store: durAssessmentStore
///
/// Each assessment records the results of all DUR checks performed on a
/// prescription at time of entry, fill, or refill. Failed checks place the
/// prescription in a "PENDING DUR REVIEW" holding area until a pharmacist
/// overrides or the prescription is modified.
/// </summary>
public interface IDurAssessmentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Creates a new DUR assessment with the prescription details and check results.
    /// Called by the workflow grain after performing all DUR checks.
    /// </summary>
    Task CreateAsync(
        string prescriptionId,
        string patientId,
        string drugName,
        string? drugId,
        string? drugClass,
        string? dosage,
        string? route,
        string? schedule,
        int? daysSupply,
        int? quantity,
        string? performedBy,
        List<GrainStates.DurCheckResult> checks);

    /// <summary>Gets the full DUR assessment state.</summary>
    Task<GrainStates.DurAssessmentState> GetAsync();

    /// <summary>
    /// Overrides a specific failed check with a documented clinical reason.
    /// Only a pharmacist may override DUR checks. If all failed checks are
    /// overridden, the assessment status changes to OverriddenByPharmacist.
    /// Mirrors PSOORED.m pharmacist override with reason documentation.
    /// </summary>
    Task OverrideCheckAsync(
        GrainStates.DurCheckType checkType,
        string pharmacistId,
        string reason);

    /// <summary>
    /// Acknowledges the DUR assessment results.
    /// Used when all checks pass or after overrides are completed.
    /// Transitions status to Acknowledged.
    /// </summary>
    Task AcknowledgeAsync(string pharmacistId, string? notes);

    /// <summary>
    /// Adds additional clinical notes to the assessment.
    /// </summary>
    Task AddNotesAsync(string notes);
}
