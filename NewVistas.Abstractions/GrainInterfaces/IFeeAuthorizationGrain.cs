// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Fee basis authorization grain approving community care services from a specific vendor
/// up to a stated dollar limit (VistA File #162.6 FEE BASIS AUTHORIZATION).
/// Grain key: "FEE-AUTH:{authId}".
/// Managed by FBSVBR.m, FBAUTH.m MUMPS routines.
/// </summary>
public interface IFeeAuthorizationGrain : IGrainWithStringKey
{
    /// <summary>Returns the current authorization state.</summary>
    Task<FeeAuthorizationState> GetAsync();

    /// <summary>
    /// Creates a new fee basis authorization. Should only be called once per grain.
    /// </summary>
    Task CreateAsync(
        string patientId,
        string vendorId,
        string vendorName,
        FeeServiceType serviceType,
        DateTime authorizationDate,
        DateTime effectiveDate,
        DateTime? expirationDate,
        decimal authorizedAmount,
        string authorizedByUserId,
        string authorizedByUserName,
        string serviceDescription,
        int? maxVisits,
        string? diagnosisCode,
        string? authorizationNumber,
        string? notes);

    /// <summary>Suspends the authorization, preventing further invoices from being submitted.</summary>
    Task SuspendAsync(string reason, string userId);

    /// <summary>Cancels the authorization before services are fully completed.</summary>
    Task CancelAsync(string reason, string userId);

    /// <summary>
    /// Records payment of an approved invoice against this authorization.
    /// Updates SpentAmount, RemainingAmount, and VisitsUsed.
    /// Auto-transitions Status to Exhausted when RemainingAmount reaches zero.
    /// </summary>
    Task RecordInvoicePaymentAsync(string invoiceId, decimal paidAmount);
}
