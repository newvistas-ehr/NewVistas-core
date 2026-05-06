// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeInvoiceGrain : Grain, IFeeInvoiceGrain
{
    private readonly IPersistentState<FeeInvoiceState> _state;

    public FeeInvoiceGrain(
        [PersistentState("feeInvoiceState", "feeInvoiceStore")]
        IPersistentState<FeeInvoiceState> state)
    {
        _state = state;
    }

    public Task<FeeInvoiceState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task SubmitAsync(
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
        string? notes)
    {
        _state.State.InvoiceId        = this.GetPrimaryKeyString();
        _state.State.AuthorizationId  = authorizationId;
        _state.State.PatientId        = patientId;
        _state.State.VendorId         = vendorId;
        _state.State.VendorName       = vendorName;
        _state.State.InvoiceNumber    = invoiceNumber;
        _state.State.ServiceDate      = serviceDate;
        _state.State.ServiceDateEnd   = serviceDateEnd;
        _state.State.ServiceType      = serviceType;
        _state.State.BilledAmount     = billedAmount;
        _state.State.DiagnosisCode    = diagnosisCode;
        _state.State.ProcedureCodes   = procedureCodes;
        _state.State.Notes            = notes;
        _state.State.Status           = FeeInvoiceStatus.Received;
        _state.State.CreatedDate      = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ApproveAsync(decimal approvedAmount, string reviewedByUserId, string reviewerName)
    {
        _state.State.ApprovedAmount     = approvedAmount;
        _state.State.ReviewedByUserId   = reviewedByUserId;
        _state.State.ReviewerName       = reviewerName;
        _state.State.ReviewedDate       = DateTime.UtcNow;
        _state.State.Status             = FeeInvoiceStatus.Approved;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RejectAsync(string reason, string reviewedByUserId, string reviewerName)
    {
        _state.State.RejectionReason    = reason;
        _state.State.ReviewedByUserId   = reviewedByUserId;
        _state.State.ReviewerName       = reviewerName;
        _state.State.ReviewedDate       = DateTime.UtcNow;
        _state.State.Status             = FeeInvoiceStatus.Rejected;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PayAsync(decimal paidAmount, string paymentMethod, string? checkNumber, DateTime paymentDate)
    {
        _state.State.PaidAmount       = paidAmount;
        _state.State.PaymentMethod    = paymentMethod;
        _state.State.CheckNumber      = checkNumber;
        _state.State.PaymentDate      = paymentDate;
        _state.State.Status           = FeeInvoiceStatus.Paid;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        // Update authorization spent/remaining amounts
        IFeeAuthorizationGrain auth = GrainFactory.GetGrain<IFeeAuthorizationGrain>(_state.State.AuthorizationId);
        await auth.RecordInvoicePaymentAsync(_state.State.InvoiceId, paidAmount);
    }
}
