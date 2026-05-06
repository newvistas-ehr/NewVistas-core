// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for an individual electronic claim (X12 837P/837I).
/// Maps to VistA Files #361/362 (ELECTRONIC CLAIM).
/// MUMPS: IBCEF*.m, IBCED*.m
/// Grain key: "EDI-CLAIM:{guid}"
/// </summary>
public interface IEdiClaimGrain : IGrainWithStringKey
{
    /// <summary>Returns the current claim state.</summary>
    Task<EdiClaimState> GetAsync();

    /// <summary>
    /// Prepares a new EDI claim from an IB billing action. Sets Status = Draft.
    /// If <paramref name="billingActionId"/> is provided, updates the IB action status to Billed.
    /// </summary>
    Task<string> PrepareAsync(
        string patientId,
        string? billingActionId,
        string? arAccountId,
        string? insurancePlanId,
        string? policyId,
        string payerId,
        string payerName,
        EdiClaimType claimType,
        decimal totalBilledAmount,
        List<string> diagnosisCodes,
        List<EdiServiceLine> serviceLines,
        DateTime serviceFromDate,
        DateTime? serviceToDate,
        string? notes);

    /// <summary>
    /// Adds the claim to a transmission batch. Sets Status = InTransmission.
    /// </summary>
    Task AddToTransmissionAsync(string transmissionId);

    /// <summary>
    /// Marks the claim as transmitted to the payer. Sets Status = Transmitted and SubmittedDate.
    /// </summary>
    Task MarkTransmittedAsync(DateTime submittedDate);

    /// <summary>
    /// Records the payer's acknowledgment of the claim. Sets Status = Acknowledged.
    /// </summary>
    Task RecordAcknowledgmentAsync(DateTime acknowledgedDate);

    /// <summary>
    /// Records payment or denial from an ERA (835).
    /// Sets Status = Paid, PartiallyPaid, or Denied.
    /// If an AR account is linked and paidAmount &gt; 0, posts payment to the AR account.
    /// </summary>
    Task RecordEraPaymentAsync(
        string eraId,
        decimal paidAmount,
        decimal? allowedAmount,
        decimal? adjustmentAmount,
        string? denialReasonCode,
        string? denialReasonDescription);
}
