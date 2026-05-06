// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EdiClaimGrain : Grain, IEdiClaimGrain
{
    private readonly IPersistentState<EdiClaimState> _state;

    public EdiClaimGrain(
        [PersistentState("ediClaimState", "ediClaimStore")]
        IPersistentState<EdiClaimState> state)
    {
        _state = state;
    }

    public Task<EdiClaimState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task<string> PrepareAsync(
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
        string? notes)
    {
        string claimId = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;

        _state.State.ClaimId           = claimId;
        _state.State.PatientId         = patientId;
        _state.State.BillingActionId   = billingActionId;
        _state.State.ARAccountId       = arAccountId;
        _state.State.InsurancePlanId   = insurancePlanId;
        _state.State.PolicyId          = policyId;
        _state.State.PayerId           = payerId;
        _state.State.PayerName         = payerName;
        _state.State.ClaimType         = claimType;
        _state.State.Status            = EdiClaimStatus.Draft;
        _state.State.TotalBilledAmount = totalBilledAmount;
        _state.State.DiagnosisCodes    = diagnosisCodes;
        _state.State.ServiceLines      = serviceLines;
        _state.State.ServiceFromDate   = serviceFromDate;
        _state.State.ServiceToDate     = serviceToDate;
        _state.State.Notes             = notes;
        _state.State.CreatedDate       = now;
        _state.State.LastModifiedDate  = now;

        // Update the source IB billing action status to Billed
        if (!string.IsNullOrEmpty(billingActionId))
        {
            IIBillingActionGrain ibAction = GrainFactory.GetGrain<IIBillingActionGrain>($"IB-ACTION:{billingActionId}");
            await ibAction.UpdateStatusAsync(IBillingActionStatus.Billed);
        }

        await _state.WriteStateAsync();
        return claimId;
    }

    public async Task AddToTransmissionAsync(string transmissionId)
    {
        _state.State.TransmissionId   = transmissionId;
        _state.State.Status           = EdiClaimStatus.InTransmission;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkTransmittedAsync(DateTime submittedDate)
    {
        _state.State.Status           = EdiClaimStatus.Transmitted;
        _state.State.SubmittedDate    = submittedDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAcknowledgmentAsync(DateTime acknowledgedDate)
    {
        _state.State.Status           = EdiClaimStatus.Acknowledged;
        _state.State.AcknowledgedDate = acknowledgedDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordEraPaymentAsync(
        string eraId,
        decimal paidAmount,
        decimal? allowedAmount,
        decimal? adjustmentAmount,
        string? denialReasonCode,
        string? denialReasonDescription)
    {
        _state.State.EraId                  = eraId;
        _state.State.PaidAmount             = paidAmount;
        _state.State.AllowedAmount          = allowedAmount;
        _state.State.AdjustmentAmount       = adjustmentAmount;
        _state.State.DenialReasonCode       = denialReasonCode;
        _state.State.DenialReasonDescription = denialReasonDescription;
        _state.State.PaymentDate            = DateTime.UtcNow;

        // Determine status: Paid, Denied, or PartiallyPaid
        if (string.IsNullOrEmpty(denialReasonCode))
        {
            _state.State.Status = paidAmount > 0 ? EdiClaimStatus.Paid : EdiClaimStatus.Denied;
        }
        else
        {
            _state.State.Status = paidAmount > 0 ? EdiClaimStatus.PartiallyPaid : EdiClaimStatus.Denied;
        }

        // Post payment to the linked AR account
        if (!string.IsNullOrEmpty(_state.State.ARAccountId) && paidAmount > 0)
        {
            IARAccountGrain arAccount = GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{_state.State.ARAccountId}");
            await arAccount.PostPaymentAsync(
                paidAmount,
                "Insurance",
                "SYSTEM",
                "EDI-ERA",
                eraId,
                null,
                $"ERA payment for claim {_state.State.ClaimId}");
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
