// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for an Electronic Remittance Advice (X12 835).
/// Maps to VistA File #364 (ELECTRONIC REMITTANCE ADVICE).
/// Grain key: "ERA:{guid}"
/// </summary>
public interface IEraGrain : IGrainWithStringKey
{
    /// <summary>Returns the current ERA state.</summary>
    Task<EraState> GetAsync();

    /// <summary>
    /// Records an incoming ERA from the payer. Sets Status = Received.
    /// </summary>
    Task RecordAsync(
        string payerId,
        string payerName,
        string? checkNumber,
        string? paymentMethod,
        DateTime paymentDate,
        decimal totalPaymentAmount,
        string? transactionSetId,
        List<EraClaimPayment> claimPayments,
        string? notes);

    /// <summary>
    /// Processes the ERA by posting each claim payment to the corresponding EDI claim grain,
    /// which in turn posts to the AR account. Sets Status = Posted on success, Error on failure.
    /// </summary>
    Task ProcessAsync();
}
