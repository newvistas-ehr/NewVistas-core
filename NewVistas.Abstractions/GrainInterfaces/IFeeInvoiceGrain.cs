// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Fee basis invoice grain for a vendor claim submitted against an authorization
/// (VistA File #162.1 FEE BASIS INVOICE).
/// Grain key: "FEE-INVOICE:{invoiceId}".
/// Managed by FBPAID.m, FBCLAIM.m MUMPS routines.
/// </summary>
public interface IFeeInvoiceGrain : IGrainWithStringKey
{
    /// <summary>Returns the current invoice state.</summary>
    Task<FeeInvoiceState> GetAsync();

    /// <summary>
    /// Submits a new invoice from a vendor. Sets Status = Received.
    /// Should only be called once per grain.
    /// </summary>
    Task SubmitAsync(
        string authorizationId,
        string patientId,
        string vendorId,
        string vendorName,
        string invoiceNumber,
        DateTime serviceDate,
        string serviceType,
        decimal billedAmount,
        string? diagnosisCode,
        List<string> procedureCodes,
        DateTime? serviceDateEnd,
        string? notes);

    /// <summary>
    /// Approves the invoice for payment with an approved amount.
    /// Sets Status = Approved.
    /// </summary>
    Task ApproveAsync(decimal approvedAmount, string reviewedByUserId, string reviewerName);

    /// <summary>
    /// Rejects the invoice with a reason.
    /// Sets Status = Rejected.
    /// </summary>
    Task RejectAsync(string reason, string reviewedByUserId, string reviewerName);

    /// <summary>
    /// Records disbursement of payment for an approved invoice.
    /// Sets Status = Paid, then calls IFeeAuthorizationGrain.RecordInvoicePaymentAsync
    /// to update the authorization's spent/remaining amounts.
    /// </summary>
    Task PayAsync(decimal paidAmount, string paymentMethod, string? checkNumber, DateTime paymentDate);
}
