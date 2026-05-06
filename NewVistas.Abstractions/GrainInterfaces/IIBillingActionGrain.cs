// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Integrated Billing Action grain — one grain per charge event (VistA File #350).
/// Tracks a single billable action: a pharmacy copay, outpatient visit copay,
/// inpatient per diem, or other billable service entry.
///
/// MUMPS routines: IBAUTL.m (utility/calculation), IBCPACT.m (copay actions),
///                 IBCPALR.m (copay alerts), IBCR*.m (charge removal).
/// Grain key: "IB-ACTION:{guid}"
/// </summary>
public interface IIBillingActionGrain : IGrainWithStringKey
{
    /// <summary>Returns the full billing action record.</summary>
    Task<GrainStates.IBillingActionState> GetAsync();

    /// <summary>
    /// Creates this billing action record with all required fields.
    /// Returns the new billing action ID (same as the grain key suffix).
    /// </summary>
    Task<string> CreateAsync(
        string patientId,
        string actionTypeCode,
        string actionTypeDescription,
        GrainStates.IBActionCategory actionCategory,
        decimal? chargeAmount,
        DateTime serviceDate,
        string enteredByUserId,
        string enteredByUserName,
        string? encounterId,
        string? diagnosisCode,
        string? procedureCode,
        string? locationId,
        string? orderId,
        string? prescriptionId,
        string? notes);

    /// <summary>
    /// Cancels this billing action with a remove reason (File #350.3).
    /// Sets status to Cancelled and records removal metadata.
    /// </summary>
    Task CancelAsync(
        string removeReasonCode,
        string removeReasonDescription,
        string removedByUserId);

    /// <summary>Updates the processing status of this action (File #350.21).</summary>
    Task UpdateStatusAsync(GrainStates.IBillingActionStatus newStatus);

    /// <summary>Marks this action as exempt from copay obligation, recording the exemption reason.</summary>
    Task SetExemptAsync(string exemptionReason);
}
